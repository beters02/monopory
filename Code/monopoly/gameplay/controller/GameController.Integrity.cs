using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public sealed partial class GameController : Component
{
	private const int IntegritySeedBytes = 32;
	private string privateDiceSeed = "";
	private int activeMoveHistoryTurnNumber;
	private string activeMoveHistoryMoneySignature = "";

	public bool IsVerifiedMatch => !CheatsEnabledEver &&
		!AdminCommandUsedEver &&
		!MatchConfigChangedAfterStart &&
		DoesDiceHistoryVerify();

	public void InitializeMatchIntegrity()
	{
		CheatsEnabledEver = false;
		AdminCommandUsedEver = false;
		MatchConfigChangedAfterStart = false;
		RevealedSeed = "";
		NextDiceRollIndex = 0;
		NextAdminHistoryId = 1;
		NextMoveHistoryTurnNumber = 1;
		privateDiceSeed = CreateSeed();
		DiceCommitmentHash = ComputeSha256Hex( privateDiceSeed );
		DiceHistory.Clear();
		AdminHistory.Clear();
		MoveHistory.Clear();
		activeMoveHistoryTurnNumber = 0;
		activeMoveHistoryMoneySignature = "";
	}

	public void RestoreMatchIntegritySeed( string seed )
	{
		privateDiceSeed = seed ?? "";
		if ( string.IsNullOrWhiteSpace( DiceCommitmentHash ) && !string.IsNullOrWhiteSpace( privateDiceSeed ) )
			DiceCommitmentHash = ComputeSha256Hex( privateDiceSeed );
	}

	public string CapturePrivateDiceSeed() => privateDiceSeed ?? "";

	public void RevealMatchSeed()
	{
		if ( string.IsNullOrWhiteSpace( RevealedSeed ) && !string.IsNullOrWhiteSpace( privateDiceSeed ) )
			RevealedSeed = privateDiceSeed;
	}

	public void MarkCheatsEnabledEver()
	{
		if ( !HasStarted )
			return;

		CheatsEnabledEver = true;
		RecordMoveHistoryEvent( "Integrity", "Cheats enabled", "sv_cheats was enabled." );
	}

	public void RecordAdminCommand( Connection caller, string commandName, string target = "match" )
	{
		if ( !HasStarted || string.IsNullOrWhiteSpace( commandName ) )
			return;

		var adminName = caller?.Name ?? Connection.Local?.Name ?? "Host";
		var entry = new AdminHistoryEntry
		{
			Id = NextAdminHistoryId++,
			TurnNumber = Math.Max( activeMoveHistoryTurnNumber, 0 ),
			AdminName = adminName,
			Command = commandName,
			Target = string.IsNullOrWhiteSpace( target ) ? "match" : target,
			Message = $"[ADMIN] {adminName} used {commandName} on {(string.IsNullOrWhiteSpace( target ) ? "match" : target)}"
		};

		AdminCommandUsedEver = true;
		AdminHistory[entry.Id] = MatchIntegrityJson.Serialize( entry );
		RecordMoveHistoryEvent( "Admin", "Admin command", entry.Message );
		SendTableChatMessage( "Admin", entry.Message );
	}

	public (int DieA, int DieB) RollVerifiedDice( int playerIndex, bool isJailAttempt, bool isForced, string context )
	{
		if ( string.IsNullOrWhiteSpace( privateDiceSeed ) )
			InitializeMatchIntegrity();

		var rollIndex = NextDiceRollIndex;
		var dieA = GetVerifiedDie( privateDiceSeed, rollIndex, 0 );
		var dieB = GetVerifiedDie( privateDiceSeed, rollIndex, 1 );
		RecordDiceResult( playerIndex, dieA, dieB, isJailAttempt, isForced, context );
		return (dieA, dieB);
	}

	public void RecordDiceResult( int playerIndex, int dieA, int dieB, bool isJailAttempt, bool isForced, string context )
	{
		var rollIndex = NextDiceRollIndex++;
		var player = Players.ElementAtOrDefault( playerIndex );
		var entry = new DiceHistoryEntry
		{
			RollIndex = rollIndex,
			TurnNumber = Math.Max( activeMoveHistoryTurnNumber, 0 ),
			PlayerIndex = playerIndex,
			PlayerName = player?.PlayerName ?? $"Player {playerIndex + 1}",
			DieA = dieA,
			DieB = dieB,
			Total = dieA + dieB,
			IsJailAttempt = isJailAttempt,
			IsForced = isForced,
			Context = context ?? ""
		};

		DiceHistory[rollIndex] = MatchIntegrityJson.Serialize( entry );
		RecordMoveHistoryEvent( "Dice", "Dice rolled", $"{entry.PlayerName} rolled {dieA} + {dieB} = {entry.Total}." );
	}

	public bool DoesDiceHistoryVerify()
	{
		var seed = !string.IsNullOrWhiteSpace( RevealedSeed ) ? RevealedSeed : privateDiceSeed;
		if ( string.IsNullOrWhiteSpace( seed ) || string.IsNullOrWhiteSpace( DiceCommitmentHash ) )
			return false;

		if ( !string.Equals( ComputeSha256Hex( seed ), DiceCommitmentHash, StringComparison.OrdinalIgnoreCase ) )
			return false;

		foreach ( var stored in DiceHistory.OrderBy( entry => entry.Key ) )
		{
			var entry = MatchIntegrityJson.Deserialize<DiceHistoryEntry>( stored.Value );
			if ( entry.RollIndex != stored.Key )
				return false;

			if ( entry.DieA != GetVerifiedDie( seed, entry.RollIndex, 0 ) ||
				entry.DieB != GetVerifiedDie( seed, entry.RollIndex, 1 ) )
				return false;
		}

		return true;
	}

	private static int GetVerifiedDie( string seed, int rollIndex, int dieIndex )
	{
		var bytes = SHA256.HashData( Encoding.UTF8.GetBytes( $"{seed}:{rollIndex}:{dieIndex}" ) );
		return bytes[0] % 6 + 1;
	}

	private static string CreateSeed()
	{
		return Convert.ToHexString( RandomNumberGenerator.GetBytes( IntegritySeedBytes ) ).ToLowerInvariant();
	}

	private static string ComputeSha256Hex( string value )
	{
		return Convert.ToHexString( SHA256.HashData( Encoding.UTF8.GetBytes( value ?? "" ) ) ).ToLowerInvariant();
	}

	public void BeginMoveHistoryTurn()
	{
		if ( !Networking.IsHost || MatchState != MatchLifecycleState.InGame || CurrentPlayer is null )
			return;

		FinalizeMoveHistoryTurn();
		var turn = new MoveHistoryTurn
		{
			TurnNumber = NextMoveHistoryTurnNumber++,
			PlayerIndex = CurrentPlayerIndex,
			PlayerName = CurrentPlayer.PlayerName ?? $"Player {CurrentPlayerIndex + 1}",
			StartPlayers = CaptureMoveHistoryPlayers()
		};

		activeMoveHistoryTurnNumber = turn.TurnNumber;
		activeMoveHistoryMoneySignature = BuildMoveHistoryMoneySignature();
		MoveHistory[turn.TurnNumber] = MatchIntegrityJson.Serialize( turn );
		RecordMoveHistoryEvent( "Turn", "Turn started", $"{turn.PlayerName}'s turn started." );
	}

	public void FinalizeMoveHistoryTurn()
	{
		if ( activeMoveHistoryTurnNumber <= 0 || !MoveHistory.TryGetValue( activeMoveHistoryTurnNumber, out var value ) )
			return;

		var turn = MatchIntegrityJson.Deserialize<MoveHistoryTurn>( value );
		if ( turn.IsFinalized )
			return;

		DetectMoveHistoryMoneyChanges( "Turn ended" );
		turn = MatchIntegrityJson.Deserialize<MoveHistoryTurn>( MoveHistory[activeMoveHistoryTurnNumber] );
		turn.EndPlayers = CaptureMoveHistoryPlayers();
		turn.IsFinalized = true;
		MoveHistory[turn.TurnNumber] = MatchIntegrityJson.Serialize( turn );
		activeMoveHistoryTurnNumber = 0;
		activeMoveHistoryMoneySignature = "";
	}

	public void RecordMoveHistoryEvent( string kind, string title, string message )
	{
		if ( activeMoveHistoryTurnNumber <= 0 || !MoveHistory.TryGetValue( activeMoveHistoryTurnNumber, out var value ) )
			return;

		var turn = MatchIntegrityJson.Deserialize<MoveHistoryTurn>( value );
		var eventId = turn.Events.Count + 1;
		turn.Events.Add( new MoveHistoryEvent
		{
			Id = eventId,
			Kind = kind ?? "",
			Title = title ?? "",
			Message = message ?? ""
		} );
		MoveHistory[turn.TurnNumber] = MatchIntegrityJson.Serialize( turn );
		DetectMoveHistoryMoneyChanges( string.IsNullOrWhiteSpace( message ) ? title : message, eventId );
	}

	private void DetectMoveHistoryMoneyChanges( string reason, int eventId = 0 )
	{
		if ( activeMoveHistoryTurnNumber <= 0 || !MoveHistory.TryGetValue( activeMoveHistoryTurnNumber, out var value ) )
			return;

		var signature = BuildMoveHistoryMoneySignature();
		if ( string.Equals( signature, activeMoveHistoryMoneySignature, StringComparison.Ordinal ) )
			return;

		var turn = MatchIntegrityJson.Deserialize<MoveHistoryTurn>( value );
		foreach ( var snapshot in CaptureMoveHistoryPlayers() )
		{
			var before = turn.StartPlayers.FirstOrDefault( player => player.PlayerIndex == snapshot.PlayerIndex );
			var lastDelta = turn.MoneyDeltas.LastOrDefault( delta => delta.PlayerIndex == snapshot.PlayerIndex );
			var beforeMoney = lastDelta is not null ? lastDelta.AfterMoney : before?.Money ?? snapshot.Money;
			if ( beforeMoney == snapshot.Money )
				continue;

			turn.MoneyDeltas.Add( new MoveHistoryMoneyDelta
			{
				PlayerIndex = snapshot.PlayerIndex,
				PlayerName = snapshot.PlayerName,
				BeforeMoney = beforeMoney,
				AfterMoney = snapshot.Money,
				Delta = snapshot.Money - beforeMoney,
				Reason = reason ?? "",
				EventId = eventId
			} );
		}

		activeMoveHistoryMoneySignature = signature;
		MoveHistory[turn.TurnNumber] = MatchIntegrityJson.Serialize( turn );
	}

	private List<MoveHistoryPlayerSnapshot> CaptureMoveHistoryPlayers()
	{
		return Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null && entry.player.IsAssigned )
			.Select( entry => new MoveHistoryPlayerSnapshot
			{
				PlayerIndex = entry.index,
				PlayerName = entry.player.PlayerName ?? $"Player {entry.index + 1}",
				Money = entry.player.Money,
				SpaceIndex = entry.player.SpaceIndex,
				IsInJail = entry.player.IsInJail,
				IsBankrupt = entry.player.IsBankrupt,
				PropertyCount = GetOwnedPropertyIndexes( entry.index ).Count,
				IsTurnHolder = entry.index == CurrentPlayerIndex
			} )
			.ToList();
	}

	private string BuildMoveHistoryMoneySignature()
	{
		return string.Join( "|", Players
			.Select( ( player, index ) => player is null || !player.IsAssigned ? "" : $"{index}:{player.Money}" ) );
	}
}
