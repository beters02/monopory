using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private PlayerState GetPlayerForConnection( Connection connection )
	{
		if ( connection is null )
			return null;

		return Players.FirstOrDefault( x => x.OwnerId == connection.SteamId );
	}

	private PlayerState GetPlayerForCaller( Connection caller )
	{
		return GetPlayerForConnection( caller ) ?? (Networking.IsHost && caller is null ? LocalPlayer : null);
	}

	private Connection GetConnectionForPlayer( PlayerState player )
	{
		return Connection.All.FirstOrDefault( c => c.SteamId == player.OwnerId );
	}

	public List<PlayerState> GetLobbyPlayers()
	{
		return Players
			.Where( player => player is not null && player.IsAssigned )
			.Take( MaxPlayers )
			.ToList();
	}

	private List<PlayerState> GetAssignedPlayers()
	{
		return Players
			.Where( player => player is not null && player.IsAssigned )
			.ToList();
	}

	private void SyncLobbyConnections()
	{
		if ( !Networking.IsHost )
			return;

		EnsurePlayerSlots();
		SeedBootstrappedPlayers();

		var availableSlotCount = Players.Count( player => player is not null );
		var registrationLimit = Math.Min( MaxPlayers, availableSlotCount );
		var allowNewRegistrations = MatchState == MatchLifecycleState.Lobby || MatchState == MatchLifecycleState.GameOver;

		foreach ( var player in Players.Where( player => player is not null && player.IsAssigned ).ToList() )
		{
			var hasConnection = Connection.All.Any( connection => connection.SteamId == player.OwnerId );
			if ( hasConnection )
			{
				if ( player.IsDisconnected )
					Log.Info( $"{player.PlayerName} reconnected." );

				player.IsDisconnected = false;
				player.AbandonEndsAt = 0f;
				player.TurnTimeoutCount = 0;
				continue;
			}

			if ( IsExpectedBootstrappedPlayer( player.OwnerId ) )
				continue;

			HandleDisconnectedPlayerSlot( player );
		}

		foreach ( var connection in Connection.All )
		{
			if ( GetPlayerForConnection( connection ) is not null )
				continue;

			if ( !allowNewRegistrations )
				continue;

			if ( GetAssignedPlayers().Count >= registrationLimit )
				continue;

			RegisterPlayer( connection );
		}
	}

	private void SeedBootstrappedPlayers()
	{
		var bootstrap = MatchBootstrap.Current;
		if ( bootstrap?.AutoStartGame != true || bootstrap.StartingPlayers.Count == 0 )
			return;

		foreach ( var startingPlayer in bootstrap.StartingPlayers )
		{
			if ( startingPlayer is null || startingPlayer.OwnerId == 0 )
				continue;

			if ( Players.Any( player => player is not null && player.OwnerId == startingPlayer.OwnerId ) )
				continue;

			var emptySlot = Players.FirstOrDefault( player => player is not null && !player.IsAssigned );
			if ( emptySlot is null )
				return;

			var startingSteamId = (long)startingPlayer.SteamId;
			emptySlot.OwnerId = startingPlayer.OwnerId;
			emptySlot.SteamId = startingSteamId != 0 ? startingSteamId : startingPlayer.OwnerId;
			emptySlot.PlayerName = string.IsNullOrWhiteSpace( startingPlayer.Name ) ? "Player" : startingPlayer.Name;
			emptySlot.IsReady = false;
			ResetPlayerForGame( emptySlot );
			emptySlot.SelectedPieceId = PieceCatalog.GetByIdOrDefault( startingPlayer.SelectedPieceId ).Id;
			emptySlot.SelectedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( startingPlayer.SelectedDiceSkinId ).Id;
		}
	}

	private bool IsExpectedBootstrappedPlayer( long ownerId )
	{
		var bootstrap = MatchBootstrap.Current;
		return bootstrap?.AutoStartGame == true &&
			bootstrap.StartingPlayers.Any( player => player is not null && player.OwnerId == ownerId );
	}

	private void EnsurePlayerSlots()
	{
		RefreshScenePlayerSlots();

		for ( var i = 0; i < Players.Count; i++ )
			EnsurePlayerStateObject( i );

		if ( Players.Count < MaxPlayers )
			Log.Warning( $"GameController has {Players.Count} scene PlayerState slot(s), but config allows {MaxPlayers} player(s)." );
	}

	private void RefreshScenePlayerSlots()
	{
		var scenePlayers = Scene.GetAllComponents<PlayerState>()
			.Where( player => player?.GameObject is not null )
			.Where( player => player.GameObject.Name.StartsWith( "PlayerState_", StringComparison.OrdinalIgnoreCase ) )
			.OrderBy( player => GetPlayerSlotSortValue( player.GameObject.Name ) )
			.ToList();

		if ( scenePlayers.Count == 0 )
		{
			Log.Warning( "No scene PlayerState slots were found. Add PlayerState_01..PlayerState_24 objects under the GameController object." );
			return;
		}

		if ( AreSamePlayerSlots( scenePlayers ) )
			return;

		Players = scenePlayers;
	}

	private void RefreshReplicatedPlayerSlots()
	{
		if ( Networking.IsHost )
			return;

		RefreshScenePlayerSlots();
	}

	private bool AreSamePlayerSlots( List<PlayerState> replicatedPlayers )
	{
		if ( Players is null || Players.Count != replicatedPlayers.Count )
			return false;

		for ( var i = 0; i < replicatedPlayers.Count; i++ )
		{
			if ( Players[i] != replicatedPlayers[i] )
				return false;
		}

		return true;
	}

	private static int GetPlayerSlotSortValue( string objectName )
	{
		var match = Regex.Match( objectName ?? "", @"PlayerState_(\d+)", RegexOptions.IgnoreCase );
		if ( match.Success && int.TryParse( match.Groups[1].Value, out var slotNumber ) )
			return slotNumber;

		return int.MaxValue;
	}

	private void EnsurePlayerStateObject( int playerIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var slotNumber = playerIndex + 1;

		if ( player is null || player.GameObject is null || !player.GameObject.IsValid() )
		{
			Log.Warning( $"Missing scene PlayerState slot {slotNumber}." );
			return;
		}

		player.GameObject.Name = $"PlayerState_{slotNumber:00}";
		player.GameObject.SetParent( GameObject );
		player.GameObject.NetworkMode = NetworkMode.Object;
		player.GameObject.Network.SetOrphanedMode( NetworkOrphaned.Host );
	}

	private void ClearPlayerSlot( PlayerState player )
	{
		if ( player is null )
			return;

		player.OwnerId = 0;
		player.SteamId = 0;
		player.PlayerName = "Player";
		player.IsReady = false;
		player.IsDisconnected = false;
		player.AbandonEndsAt = 0f;
		player.TurnTimeoutCount = 0;
		ResetPlayerForGame( player );
		player.SelectedPieceId = PieceCatalog.DefaultPieceId;
		player.SelectedDiceSkinId = DiceSkinCatalog.DefaultDiceSkinId;
	}

	private void ResetPlayerForGame( PlayerState player )
	{
		if ( player is null )
			return;

		player.Money = Math.Max( Config?.StartingMoney ?? 1500, 0 );
		player.SpaceIndex = 0;
		player.IsInJail = false;
		player.JailTurnsRemaining = 0;
		player.ChanceGetOutOfJailFreeCards = 0;
		player.CommunityChestGetOutOfJailFreeCards = 0;
		player.ConsecutiveDoubles = 0;
		player.SkipsNextTurn = false;
		player.IsReturningFromVacationCashBreak = false;
		player.IsBankrupt = false;
		player.IsDisconnected = false;
		player.AbandonEndsAt = 0f;
		player.TurnTimeoutCount = 0;
		player.ColorSlot = -1;
	}

	private void ResetGameState( bool resetPlayers )
	{
		CurrentPlayerIndex = 0;
		LastDieA = 0;
		LastDieB = 0;
		IsResolvingPhysicalDice = false;
		PhysicalDiceStartedAt = 0f;
		ClearPendingRollState();
		Phase = GamePhase.WaitingToRoll;
		PendingPurchaseSpaceIndex = -1;
		ClearAuction();
		NextTradeId = 1;
		ResetVacationCashBankToMinimum();
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;
		CurrentTurnEndsAt = 0f;
		LastTimeCurrentTurnReminderPlayed = 0;
		CurrentTurnReminderSoundsPlayed = 0;
		IsRecoveringHostState = false;
		WinnerPlayerIndex = -1;
		GameStartedAt = 0f;
		StartingPlayerCount = 0;
		pausedTurnRemainingSeconds = 0f;
		pausedAuctionRemainingSeconds = 0f;
		auctionPausedTurnRemainingSeconds = 0f;
		LocalSelectedDrawnCardText = "";
		ClearMovementRecoveryState();
		ClearPendingForcedPayment();
		ClearGambleScreen();
		ResetCardDrawPiles();
		PropertyOwners.Clear();
		PropertyImprovements.Clear();
		MortgagedProperties.Clear();
		PendingTrades.Clear();
		TradeViewers.Clear();
		TradeEditors.Clear();
		TokenPhysicsStates.Clear();
		StatsLogDiceFaceCounts.Clear();
		DiceHistory.Clear();
		AdminHistory.Clear();
		MoveHistory.Clear();
		CheatsEnabledEver = false;
		AdminCommandUsedEver = false;
		MatchConfigChangedAfterStart = false;
		DiceCommitmentHash = "";
		RevealedSeed = "";
		NextDiceRollIndex = 0;
		NextAdminHistoryId = 1;
		NextMoveHistoryTurnNumber = 1;
		RestoreMatchIntegritySeed( "" );

		foreach ( var player in Players )
		{
			if ( resetPlayers )
				ClearPlayerSlot( player );
			else
				ResetPlayerForGame( player );
		}
	}

	private void RegisterPlayer( Connection connection )
	{
		if ( connection is null )
			return;

		if ( GetPlayerForConnection( connection ) is not null )
			return;

		var emptySlot = Players.FirstOrDefault( x => !x.IsAssigned );

		if ( emptySlot is null )
		{
			Log.Warning( $"No available player slot for {GetConnectionPlayerName( connection )}" );
			return;
		}

		emptySlot.SteamId = connection.SteamId;
		emptySlot.OwnerId = connection.SteamId;
		emptySlot.PlayerName = GetConnectionPlayerName( connection );
		emptySlot.IsReady = false;
		ResetPlayerForGame( emptySlot );
		emptySlot.SelectedPieceId = PieceCatalog.DefaultPieceId;
		emptySlot.SelectedDiceSkinId = DiceSkinCatalog.DefaultDiceSkinId;

		if ( PreferredHostOwnerId == 0 )
			PreferredHostOwnerId = connection.SteamId;

		Log.Info( $"Assigned {emptySlot.PlayerName} to player slot {Players.IndexOf( emptySlot )}" );
	}

	private static string GetConnectionPlayerName( Connection connection, string fallback = "Player" )
	{
		var name = connection?.Name ?? "";
		return string.IsNullOrWhiteSpace( name ) ? fallback : name;
	}

	private void HandleDisconnectedPlayerSlot( PlayerState player )
	{
		if ( player is null || !player.IsAssigned )
			return;

		var abandonTimeoutSeconds = Math.Max( Config?.AbandonTimeoutSeconds ?? 180, 1 );
		if ( !player.IsDisconnected )
		{
			player.IsDisconnected = true;
			player.AbandonEndsAt = Time.Now + abandonTimeoutSeconds;
			RemoveTradesForPlayer( Players.IndexOf( player ) );
			Log.Info( $"{player.PlayerName} disconnected. Abandon timeout: {abandonTimeoutSeconds}s." );
			return;
		}

		if ( player.AbandonEndsAt > Time.Now )
			return;

		FinalizeAbandonedPlayer( player );
	}

	private void FinalizeAbandonedPlayer( PlayerState player )
	{
		var playerIndex = Players.IndexOf( player );
		if ( playerIndex < 0 || player is null || !player.IsAssigned )
			return;

		RemoveTradesForPlayer( playerIndex );
		BankruptPlayer( playerIndex, null );
		TokenPhysicsStates.Remove( playerIndex );
		ClearPlayerSlot( player );

		if ( CurrentPlayerIndex == playerIndex && MatchState == MatchLifecycleState.InGame )
		{
			if ( Phase == GamePhase.WaitingForBuyDecision && PendingPurchaseSpaceIndex >= 0 )
				AuctionPendingProperty();
			else if ( Phase == GamePhase.TurnEnded )
				CompleteTurn();
			else
				AdvanceTurn();
		}

		TryAutosaveStablePoint( "Player abandoned" );
	}

	private void RemoveTradesForPlayer( int playerIndex )
	{
		foreach ( var trade in GetTrades() )
		{
			if ( trade.SenderPlayerIndex == playerIndex || trade.ReceiverPlayerIndex == playerIndex )
			{
				PendingTrades.Remove( trade.Id );
				TradeViewers.Remove( trade.Id );
				TradeEditors.Remove( trade.Id );
			}
		}
	}
}
