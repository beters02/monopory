using System;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

public sealed partial class GameController
{
	private const float NonCardGambleRevealSeconds = 2f;
	private const float NonCardGambleHoldSeconds = 3.5f;

	public bool CanLocalPlayerStartNonCardGamble
	{
		get
		{
			var player = Players.ElementAtOrDefault( LocalPlayerIndex );

			if ( Config is null )
				return false;

			if ( LocalPlayerIndex == CurrentPlayerIndex )
				if ( (bool) !Config?.CanTurnPlayerGambleNonCard )
					return false;

			return Config?.GambleGamesEnabledNonCard == true &&
				MatchState == MatchLifecycleState.InGame &&
				player is not null &&
				player.IsAssigned &&
				!player.IsBankrupt &&
				!GetGambleSessions().Any( session => session.PlayerIndex == LocalPlayerIndex );
		}
	}

	public IReadOnlyList<GambleSession> GetGambleSessions()
	{
		return GambleSessions.Values
			.Select( GambleSessionSerializer.Deserialize )
			.Where( session => session is not null )
			.ToList();
	}

	public GambleSession GetGambleSessionAtStation( int stationId )
	{
		return GambleSessions.TryGetValue( stationId, out var value )
			? GambleSessionSerializer.Deserialize( value )
			: null;
	}

	public GambleSession GetLocalGambleSession()
	{
		return GetGambleSessions()
			.FirstOrDefault( session => session.IsCardGame || session.PlayerIndex == LocalPlayerIndex );
	}

	public bool IsGambleStationAvailable( int stationId )
	{
		return FindGambleStation( stationId ) is not null && GetGambleSessionAtStation( stationId ) is null;
	}

	public int GetFirstAvailableGambleStationId()
	{
		return Scene.GetAllComponents<GambleStation>()
			.OrderBy( station => station.StationId )
			.Where( station => IsGambleStationAvailable( station.StationId ) )
			.Select( station => station.StationId )
			.FirstOrDefault( -1 );
	}

	public PlayerToken GetLocalPlayerTokenForGambling()
	{
		return Scene.GetAllComponents<PlayerToken>()
			.FirstOrDefault( token => token?.IsLocalPlayerToken == true );
	}

	public bool TryGetLocalGambleCameraTarget( out GambleStation station )
	{
		station = null;
		var session = GetLocalGambleSession();
		if ( session is null || session.Presentation != GamblePresentationMode.ThreeDimensional )
			return false;

		station = FindGambleStation( session.StationId );
		return station is not null;
	}

	[Rpc.Host]
	public void RequestStartNonCardCoinFlip( int stationId, int side, int wager )
	{
		var player = GetPlayerForCaller( Rpc.Caller );
		var playerIndex = GetPlayerIndex( player );
		if ( !CanStartNonCardGamble( player, playerIndex, stationId, out var message ) )
		{
			SendPopupToPlayer( player, "Coin Flip", message, PopupKind.Warning );
			return;
		}

		var chosenSide = Enum.IsDefined( typeof( CoinFlipSide ), side )
			? (CoinFlipSide)side
			: CoinFlipSide.Heads;
		var spendsMoney = Config?.CanGambleMonopolyMoney == true;
		if ( spendsMoney && (wager < 1 || wager > player.Money) )
		{
			SendPopupToPlayer( player, "Invalid wager", "Choose a wager from $1 up to your available cash.", PopupKind.Warning );
			return;
		}

		_ = PlayNonCardCoinFlipAsync( player, playerIndex, stationId, chosenSide, spendsMoney ? wager : 0 );
	}

	private bool CanStartNonCardGamble( PlayerState player, int playerIndex, int stationId, out string message )
	{
		message = "";
		if ( Config?.GambleGamesEnabledNonCard != true )
			message = "Non-card gamble games are disabled for this match.";
		else if ( MatchState != MatchLifecycleState.InGame )
			message = "Gamble games are only available during an active match.";
		else if ( player is null || playerIndex < 0 || player.IsBankrupt )
			message = "No eligible player was found.";
		else if ( playerIndex == CurrentPlayerIndex && Config?.CanTurnPlayerGambleNonCard != true )
			message = "Finish your turn before playing a side game.";
		else if ( GetGambleSessions().Any( session => session.PlayerIndex == playerIndex ) )
			message = "You already have an active gamble game.";
		else if ( !IsGambleStationAvailable( stationId ) )
			message = "That gamble station is currently busy.";

		return string.IsNullOrEmpty( message );
	}

	private async Task PlayNonCardCoinFlipAsync(
		PlayerState player,
		int playerIndex,
		int stationId,
		CoinFlipSide chosenSide,
		int wager )
	{
		if ( wager > 0 )
			player.Money -= wager;

		var outcome = Game.Random.Int( 0, 1 ) == 0 ? CoinFlipSide.Heads : CoinFlipSide.Tails;
		var won = chosenSide == outcome;
		var now = Time.Now;
		var session = new GambleSession
		{
			Id = NextGambleSessionId++,
			StationId = stationId,
			PlayerIndex = playerIndex,
			GameType = GambleType.CoinFlip,
			Presentation = FindGambleStation( stationId )?.Presentation ?? GamblePresentationMode.ThreeDimensional,
			ChosenSide = chosenSide,
			OutcomeSide = outcome,
			Wager = wager,
			StartedAt = now,
			RevealAt = now + NonCardGambleRevealSeconds,
			EndsAt = now + NonCardGambleRevealSeconds + NonCardGambleHoldSeconds,
			Won = won,
			Title = "Coin Flip",
			Description = wager > 0
				? $"{player.PlayerName} chose {chosenSide} for ${wager}."
				: $"{player.PlayerName} chose {chosenSide} in free-play mode."
		};
		SetGambleSession( session );

		await Task.DelaySeconds( NonCardGambleRevealSeconds );
		var current = GetGambleSessionAtStation( stationId );
		if ( current?.Id != session.Id || current.IsResolved )
			return;

		if ( wager > 0 && won )
			player.Money += wager * 2;

		current.IsResolved = true;
		current.ResultMessage = won
			? wager > 0
				? $"{outcome}! {player.PlayerName} won ${wager}."
				: $"{outcome}! {player.PlayerName} won."
			: wager > 0
				? $"{outcome}. {player.PlayerName} lost ${wager}."
				: $"{outcome}. Better luck next flip.";
		SetGambleSession( current );

		await Task.DelaySeconds( NonCardGambleHoldSeconds );
		RemoveGambleSession( stationId, session.Id );
	}

	private void UpdateGambleSessions()
	{
		foreach ( var session in GetGambleSessions().Where( session => !session.IsCardGame ).ToList() )
		{
			if ( !session.IsResolved && Time.Now >= session.RevealAt )
			{
				var player = Players.ElementAtOrDefault( session.PlayerIndex );
				if ( player is not null && session.Wager > 0 && session.Won )
					player.Money += session.Wager * 2;

				session.IsResolved = true;
				session.ResultMessage = session.Won
					? session.Wager > 0
						? $"{session.OutcomeSide}! {player?.PlayerName ?? "Player"} won ${session.Wager}."
						: $"{session.OutcomeSide}! {player?.PlayerName ?? "Player"} won."
					: session.Wager > 0
						? $"{session.OutcomeSide}. {player?.PlayerName ?? "Player"} lost ${session.Wager}."
						: $"{session.OutcomeSide}. Better luck next flip.";
				SetGambleSession( session );
			}

			if ( Time.Now >= session.EndsAt )
				RemoveGambleSession( session.StationId, session.Id );
		}
	}

	private void SetGambleSession( GambleSession session )
	{
		if ( session is null )
			return;

		GambleSessions[session.StationId] = GambleSessionSerializer.Serialize( session );
	}

	private void RemoveGambleSession( int stationId, int expectedSessionId = -1 )
	{
		var current = GetGambleSessionAtStation( stationId );
		if ( current is null || (expectedSessionId >= 0 && current.Id != expectedSessionId) )
			return;

		if ( !current.IsCardGame && current.IsResolved )
			RecordGambleHistory( current );

		GambleSessions.Remove( stationId );
	}

	private GambleStation FindGambleStation( int stationId )
	{
		return Scene.GetAllComponents<GambleStation>()
			.FirstOrDefault( station => station.StationId == stationId );
	}

	private void EnsureGambleStations()
	{
		if ( Scene.GetAllComponents<GambleStation>().Any() )
			return;

		var stationObject = new GameObject( true, "GambleStation_Default" );
		stationObject.SetParent( GameObject );
		var station = stationObject.Components.Create<GambleStation>();
		station.StationId = 0;

		var spaces = Board?.Spaces;
		stationObject.WorldPosition = spaces is not null && spaces.Count > 0
			? spaces.Select( space => space.TokenPosition ).Aggregate( Vector3.Zero, (sum, value) => sum + value ) / spaces.Count + Vector3.Up * 12f
			: GameObject.WorldPosition + Vector3.Up * 12f;
	}

	private void EnsureGambleSessionStations()
	{
		var sessions = GetGambleSessions();
		foreach ( var session in sessions )
		{
			if ( FindGambleStation( session.StationId ) is not null )
				continue;

			var stationObject = new GameObject( true, $"GambleStation_Session_{session.StationId}" );
			stationObject.SetParent( GameObject );
			var station = stationObject.Components.Create<GambleStation>();
			station.StationId = session.StationId;
			station.IsRuntimeSessionStation = true;
			var anchor = Scene.GetAllComponents<GambleStation>()
				.FirstOrDefault( candidate => !candidate.IsRuntimeSessionStation );
			stationObject.WorldPosition = (anchor?.GameObject.WorldPosition ?? GameObject.WorldPosition)
				+ Vector3.Right * (18f * (session.StationId + 1));
		}

		foreach ( var station in Scene.GetAllComponents<GambleStation>()
			.Where( station => station.IsRuntimeSessionStation ).ToList() )
		{
			if ( sessions.All( session => session.StationId != station.StationId ) )
				station.GameObject.Destroy();
		}
	}

	private GambleSession BeginCardGambleSession( int playerIndex, string title, string description, int wager )
	{
		EnsureGambleStations();
		var stationId = GetFirstAvailableGambleStationId();
		if ( stationId < 0 )
			stationId = Scene.GetAllComponents<GambleStation>().Select( station => station.StationId ).DefaultIfEmpty( -1 ).Max() + 1;

		var now = Time.Now;
		var won = Game.Random.Int( 0, 1 ) == 1;
		var session = new GambleSession
		{
			Id = NextGambleSessionId++,
			StationId = stationId,
			PlayerIndex = playerIndex,
			GameType = GambleType.CoinFlip,
			Presentation = GamblePresentationMode.ThreeDimensional,
			IsCardGame = true,
			ChosenSide = CoinFlipSide.Heads,
			OutcomeSide = won ? CoinFlipSide.Heads : CoinFlipSide.Tails,
			Wager = wager,
			StartedAt = now,
			RevealAt = now + GambleRevealDelaySeconds,
			EndsAt = now + GambleRevealDelaySeconds + GambleResultHoldSeconds,
			Won = won,
			Title = title,
			Description = description
		};
		SetGambleSession( session );
		return session;
	}

	private GambleSession GetActiveCardGambleSession()
	{
		return GetGambleSessions().FirstOrDefault( session => session.IsCardGame );
	}

	private void ResolveActiveCardGambleSession( string resultMessage )
	{
		var session = GetActiveCardGambleSession();
		if ( session is null )
			return;

		session.IsResolved = true;
		session.ResultMessage = resultMessage ?? "";
		SetGambleSession( session );
	}

	private void ClearActiveCardGambleSession()
	{
		var session = GetActiveCardGambleSession();
		if ( session is not null )
			RemoveGambleSession( session.StationId, session.Id );
	}
}
