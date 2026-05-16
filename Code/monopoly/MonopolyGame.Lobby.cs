using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	private MonopolyPlayerState GetPlayerForConnection( Connection connection )
	{
		if ( connection is null )
			return null;

		return Players.FirstOrDefault( x => x.OwnerId == connection.SteamId );
	}

	private Connection GetConnectionForPlayer( MonopolyPlayerState player )
	{
		return Connection.All.FirstOrDefault( c => c.SteamId == player.OwnerId );
	}

	public List<MonopolyPlayerState> GetLobbyPlayers()
	{
		return Players
			.Where( player => player is not null && player.IsAssigned )
			.Take( MaxPlayers )
			.ToList();
	}

	private List<MonopolyPlayerState> GetAssignedPlayers()
	{
		return Players
			.Where( player => player is not null && player.IsAssigned )
			.ToList();
	}

	private void SyncLobbyConnections()
	{
		if ( !Networking.IsHost )
			return;

		if ( HasStarted && MatchState != MonopolyMatchState.GameOver )
			return;

		EnsurePlayerSlots();

		var availableSlotCount = Players.Count( player => player is not null );
		var registrationLimit = Math.Min( MaxPlayers, availableSlotCount );

		foreach ( var player in Players.Where( player => player is not null && player.IsAssigned ).ToList() )
		{
			if ( Connection.All.Any( connection => connection.SteamId == player.OwnerId ) )
				continue;

			ClearPlayerSlot( player );
		}

		foreach ( var connection in Connection.All )
		{
			if ( GetPlayerForConnection( connection ) is not null )
				continue;

			if ( GetAssignedPlayers().Count >= registrationLimit )
				continue;

			RegisterPlayer( connection );
		}
	}

	private void EnsurePlayerSlots()
	{
		var targetSlotCount = Math.Max( MaxPlayers, 1 );
		while ( Players.Count < targetSlotCount )
		{
			var slotNumber = Players.Count + 1;
			var player = CreatePlayerStateObject( slotNumber );
			player.PlayerName = $"Player {slotNumber}";
			Players.Add( player );
		}

		for ( var i = 0; i < Players.Count; i++ )
			EnsurePlayerStateObject( i );
	}

	private MonopolyPlayerState CreatePlayerStateObject( int slotNumber )
	{
		var playerObject = new GameObject( true, $"PlayerState_{slotNumber:00}" );
		playerObject.SetParent( GameObject );
		return playerObject.Components.Create<MonopolyPlayerState>();
	}

	private void EnsurePlayerStateObject( int playerIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var slotNumber = playerIndex + 1;

		if ( player is null || player.GameObject is null || !player.GameObject.IsValid() )
		{
			Players[playerIndex] = CreatePlayerStateObject( slotNumber );
			Players[playerIndex].PlayerName = $"Player {slotNumber}";
			return;
		}

		player.GameObject.Name = $"PlayerState_{slotNumber:00}";
		player.GameObject.SetParent( GameObject );
	}

	private void ClearPlayerSlot( MonopolyPlayerState player )
	{
		if ( player is null )
			return;

		player.OwnerId = 0;
		player.PlayerName = "Player";
		player.IsReady = false;
		ResetPlayerForGame( player );
	}

	private void ResetPlayerForGame( MonopolyPlayerState player )
	{
		if ( player is null )
			return;

		player.Money = Math.Max( Config?.StartingMoney ?? 1500, 0 );
		player.SpaceIndex = 0;
		player.IsInJail = false;
		player.ConsecutiveDoubles = 0;
		player.SkipsNextTurn = false;
		player.IsBankrupt = false;
	}

	private void ResetGameState( bool resetPlayers )
	{
		CurrentPlayerIndex = 0;
		LastDieA = 0;
		LastDieB = 0;
		Phase = MonopolyGamePhase.WaitingToRoll;
		PendingPurchaseSpaceIndex = -1;
		ClearAuction();
		NextTradeId = 1;
		FreeParkingBank = 0;
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnEndsAt = 0f;
		WinnerPlayerIndex = -1;
		GameStartedAt = 0f;
		StartingPlayerCount = 0;
		pausedTurnRemainingSeconds = 0f;
		pausedAuctionRemainingSeconds = 0f;
		LocalSelectedDrawnCardText = "";
		ClearPendingForcedPayment();
		PropertyOwners.Clear();
		PropertyImprovements.Clear();
		MortgagedProperties.Clear();
		PendingTrades.Clear();

		foreach ( var player in Players )
		{
			if ( resetPlayers )
				ClearPlayerSlot( player );
			else
				ResetPlayerForGame( player );
		}
	}

	private bool IsHostCaller( Connection caller )
	{
		if ( Networking.IsHost && (caller is null || caller == Connection.Local) )
			return true;

		return false;
	}

	private bool CanAcceptGameplayInput()
	{
		return MatchState == MonopolyMatchState.InGame;
	}

	public bool TryStartGame( bool requireReady = true )
	{
		if ( !Networking.IsHost )
			return false;

		SyncLobbyConnections();

		if ( requireReady && !CanStartGame )
			return false;

		if ( !requireReady && GetLobbyPlayers().Count < MinPlayers )
			return false;

		var activePlayers = GetLobbyPlayers();
		ResetGameState( false );
		StartingPlayerCount = activePlayers.Count;

		foreach ( var player in Players )
		{
			if ( player is not null && player.IsAssigned && !activePlayers.Contains( player ) )
				ClearPlayerSlot( player );
		}

		foreach ( var player in activePlayers )
		{
			ResetPlayerForGame( player );
			player.IsReady = false;
		}

		SpawnTokensForPlayers( activePlayers );

		var firstPlayerIndex = Players.FindIndex( player => player is not null && player.IsAssigned );
		CurrentPlayerIndex = Math.Max( firstPlayerIndex, 0 );
		MatchState = MonopolyMatchState.Starting;
		GameStartedAt = Time.Now;
		MatchState = MonopolyMatchState.InGame;
		StartTurnTimer();

		SendPopupToAll( "Game started", "The first turn is live.", MonopolyPopupKind.Success, true, 4f );
		return true;
	}

	private void SpawnTokensForPlayers( IReadOnlyList<MonopolyPlayerState> activePlayers )
	{
		ClearSpawnedTokens();
		ClearExistingTokenObjects();

		if ( TokenPrefab is null )
		{
			Log.Warning( "MonopolyGame has no TokenPrefab assigned, so player tokens were not spawned." );
			return;
		}

		if ( Board is null )
			Board = Scene.GetAllComponents<MonopolyBoard>().FirstOrDefault();

		for ( var i = 0; i < activePlayers.Count; i++ )
		{
			var player = activePlayers[i];
			if ( player is null || !player.IsAssigned )
				continue;

			var tokenObject = TokenPrefab.Clone();
			tokenObject.Name = $"Token_{i + 1:00}";
			tokenObject.SetParent( GameObject );

			var token = tokenObject.Components.Get<MonopolyToken>() ?? tokenObject.Components.Create<MonopolyToken>();
			token.Board = Board;
			token.PlayerState = player;

			if ( Board is not null )
				tokenObject.WorldPosition = Board.GetSpacePosition( player.SpaceIndex ) + Vector3.Up * token.HeightOffset;

			spawnedTokenObjects.Add( tokenObject );
		}
	}

	private void ClearExistingTokenObjects()
	{
		foreach ( var token in Scene.GetAllComponents<MonopolyToken>().ToList() )
		{
			if ( token?.GameObject is null || token.GameObject == TokenPrefab )
				continue;

			token.GameObject.Destroy();
		}
	}

	private void ClearSpawnedTokens()
	{
		for ( var i = spawnedTokenObjects.Count - 1; i >= 0; i-- )
		{
			if ( spawnedTokenObjects[i] is not null )
				spawnedTokenObjects[i].Destroy();
		}

		spawnedTokenObjects.Clear();
	}

	public bool TrySetReady( MonopolyPlayerState player, bool isReady )
	{
		if ( !Networking.IsHost || MatchState != MonopolyMatchState.Lobby )
			return false;

		if ( player is null || !player.IsAssigned )
			return false;

		player.IsReady = isReady;
		return true;
	}

	public bool TryPauseGame()
	{
		if ( !Networking.IsHost || MatchState != MonopolyMatchState.InGame )
			return false;

		pausedTurnRemainingSeconds = CurrentTurnEndsAt <= 0f ? 0f : Math.Max( 0f, CurrentTurnEndsAt - Time.Now );
		pausedAuctionRemainingSeconds = AuctionEndsAt <= 0f ? 0f : Math.Max( 0f, AuctionEndsAt - Time.Now );
		CurrentTurnEndsAt = 0f;
		AuctionEndsAt = 0f;
		MatchState = MonopolyMatchState.Paused;
		SendPopupToAll( "Paused", "The host paused the game.", MonopolyPopupKind.Info, true, 4f );
		return true;
	}

	public bool TryResumeGame()
	{
		if ( !Networking.IsHost || MatchState != MonopolyMatchState.Paused )
			return false;

		if ( pausedTurnRemainingSeconds > 0f )
			CurrentTurnEndsAt = Time.Now + pausedTurnRemainingSeconds;

		if ( pausedAuctionRemainingSeconds > 0f )
			AuctionEndsAt = Time.Now + pausedAuctionRemainingSeconds;

		pausedTurnRemainingSeconds = 0f;
		pausedAuctionRemainingSeconds = 0f;
		MatchState = MonopolyMatchState.InGame;
		SendPopupToAll( "Resumed", "Back to the board.", MonopolyPopupKind.Success, true, 3f );
		return true;
	}

	public bool TryReturnToLobby()
	{
		if ( !Networking.IsHost )
			return false;

		ClearSpawnedTokens();
		ResetGameState( false );
		SyncLobbyConnections();
		MatchState = MonopolyMatchState.Lobby;
		return true;
	}

	internal void ForceEndGameFromException( Exception ex )
	{
		if ( !Networking.IsHost )
			return;

		Log.Error( ex );

		CurrentTurnEndsAt = 0f;
		ClearAuction();
		ClearPendingForcedPayment();
		PendingPurchaseSpaceIndex = -1;
		CurrentTurnGetsExtraRoll = false;

		Phase = MonopolyGamePhase.TurnEnded;
		MatchState = MonopolyMatchState.GameOver;

		SendPopupToAll(
			"Game ended",
			"The game hit a fatal rules error and was ended by the host.",
			MonopolyPopupKind.Danger,
			true,
			8f
		);
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
			Log.Warning( $"No available Monopoly player slot for {connection.DisplayName}" );
			return;
		}

		emptySlot.OwnerId = connection.SteamId;
		emptySlot.PlayerName = connection.DisplayName;
		emptySlot.IsReady = false;
		ResetPlayerForGame( emptySlot );

		Log.Info( $"Assigned {connection.DisplayName} to Monopoly player slot {Players.IndexOf( emptySlot )}" );
	}
}
