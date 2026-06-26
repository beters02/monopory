using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private bool IsHostCaller( Connection caller )
	{
		return IsEffectiveHostCaller( caller );
	}

	private bool CanAcceptGameplayInput()
	{
		return MatchState == MatchLifecycleState.InGame && !IsGambleScreenActive;
	}

	private void MarkTurnActionAccepted( PlayerState player = null )
	{
		player ??= CurrentPlayer;
		if ( player is not null )
			player.TurnTimeoutCount = 0;
	}

	public bool TryStartGame( bool requireReady = true )
	{
		if ( !Networking.IsHost )
			return false;

		EnsurePreferredHostOwnerId();
		SyncLobbyConnections();

		if ( requireReady && !CanStartGame )
			return false;

		if ( !requireReady && GetLobbyPlayers().Count < MinPlayers )
			return false;

		BeginServerLoading( "Starting game", "The host is preparing players, tokens, and the first turn." );
		try
		{
			var activePlayers = GetLobbyPlayers();
			CurrentGameIdentifier = GameSaveService.CreateGameIdentifier( activePlayers.Select( player => player.PlayerName ).ToList() );
			loadedSourceSaveId = "";
			currentManualSaveId = "";
			hasLoadedRestorePoint = false;
			CaptureMatchConfigDefaultSnapshot();
			ResetGameState( false );
			InitializeMatchIntegrity();
			StartingPlayerCount = activePlayers.Count;

			foreach ( var player in Players )
			{
				if ( player is not null && player.IsAssigned && !activePlayers.Contains( player ) )
					ClearPlayerSlot( player );
			}

			foreach ( var player in activePlayers )
			{
				ResetPlayerForGame( player );
				player.ColorSlot = activePlayers.IndexOf( player );
				player.IsReady = false;
			}

			SpawnTokensForPlayers( activePlayers );

			var firstPlayerIndex = Players.FindIndex( player => player is not null && player.IsAssigned );
			CurrentPlayerIndex = Math.Max( firstPlayerIndex, 0 );
			MatchState = MatchLifecycleState.Starting;
			GameStartedAt = Time.Now;
			MatchState = MatchLifecycleState.InGame;
			BeginTurnForCurrentPlayer();
			BeginMoveHistoryTurn();
			StartTurnTimer();

			ReportMatchStartedAchievements();
			SendGlobalPopupToAll( "Game started", "The first turn is live.", PopupKind.Success, true, 4f );
			TryAutosaveStablePoint( "Game started" );
			return true;
		}
		finally
		{
			ClearServerLoading();
		}
	}

	public bool TrySetReady( PlayerState player, bool isReady )
	{
		if ( !Networking.IsHost || MatchState != MatchLifecycleState.Lobby )
			return false;

		if ( player is null || !player.IsAssigned )
			return false;

		player.IsReady = isReady;
		return true;
	}

	public bool TryForceReadyUp( out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can force ready up.";
			return false;
		}

		if ( MatchState != MatchLifecycleState.Lobby )
		{
			message = "Players can only be forced ready in the lobby.";
			return false;
		}

		var players = GetLobbyPlayers();
		if ( players.Count == 0 )
		{
			message = "No lobby players to ready up.";
			return false;
		}

		foreach ( var player in players )
			player.IsReady = true;

		message = $"Forced {players.Count} player{(players.Count == 1 ? "" : "s")} ready.";
		SendTableChatMessage( "Ready up", message );
		return true;
	}

	public bool TryPauseGame()
	{
		if ( !Networking.IsHost || MatchState != MatchLifecycleState.InGame )
			return false;

		pausedTurnRemainingSeconds = CurrentTurnEndsAt <= 0f ? 0f : Math.Max( 0f, CurrentTurnEndsAt - Time.Now );
		pausedAuctionRemainingSeconds = AuctionEndsAt <= 0f ? 0f : Math.Max( 0f, AuctionEndsAt - Time.Now );
		CurrentTurnEndsAt = 0f;
		AuctionEndsAt = 0f;
		MatchState = MatchLifecycleState.Paused;
		SendGlobalPopupToAll( "Paused", "The host paused the game.", PopupKind.Info, true, 4f );
		return true;
	}

	public bool TryResumeGame()
	{
		if ( !Networking.IsHost || MatchState != MatchLifecycleState.Paused )
			return false;

		if ( pausedTurnRemainingSeconds > 0f )
			CurrentTurnEndsAt = Time.Now + pausedTurnRemainingSeconds;

		if ( pausedAuctionRemainingSeconds > 0f )
			AuctionEndsAt = Time.Now + pausedAuctionRemainingSeconds;

		pausedTurnRemainingSeconds = 0f;
		pausedAuctionRemainingSeconds = 0f;
		MatchState = MatchLifecycleState.InGame;
		SendGlobalPopupToAll( "Resumed", "Back to the board.", PopupKind.Success, true, 3f );
		return true;
	}

	public bool TryReturnToLobby()
	{
		if ( !Networking.IsHost )
			return false;

		TryAutosaveStablePoint( "Returned to lobby" );
		ClearSpawnedTokens();
		ResetGameState( false );
		SyncLobbyConnections();
		MatchState = MatchLifecycleState.Lobby;
		SceneFlow.LoadLobby( Scene );
		return true;
	}

	public bool TryForceEndGameWin( PlayerState winner, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can force a game win.";
			return false;
		}

		if ( winner is null || !winner.IsAssigned )
		{
			message = "Winner is not an active player.";
			return false;
		}

		var winnerIndex = Players.IndexOf( winner );
		if ( winnerIndex < 0 )
		{
			message = "Winner is not part of this game.";
			return false;
		}

		CurrentTurnEndsAt = 0f;
		ClearAuction();
		ClearPendingForcedPayment();
		PendingPurchaseSpaceIndex = -1;
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;
		Phase = GamePhase.TurnEnded;
		WinnerPlayerIndex = winnerIndex;
		FinalizeMoveHistoryTurn();
		RevealMatchSeed();
		MatchState = MatchLifecycleState.GameOver;
		if ( Networking.IsHost && Connection.All.Count <= 1 )
			NetworkSession.ClearRejoinWindow();

		ReportMatchCompletedAchievements();
		ReportWinnerAchievements();
		PlayPlayerTokenCheering( winner );
		SendGlobalPopupToAll( "Game over", $"{winner.PlayerName} won the game.", PopupKind.Success, true, 8f );
		Log.Info( $"Game force-ended. Winner: {winner.PlayerName}." );

		message = $"Forced game over. Winner: {winner.PlayerName}.";
		return true;
	}

	private bool tempGameEnded = false;
	private float timePassedSinceGameEnded = 0f;

	internal void ForceEndGameFromException( Exception ex )
	{
		if ( !Networking.IsHost )
			return;

		if (tempGameEnded)
		{
			timePassedSinceGameEnded += Time.Delta;
			if (timePassedSinceGameEnded >= 3)
			{
				TryReturnToLobby();
			}
			return;
		}
		
		Log.Error( ex );
		tempGameEnded = true;
		
		CurrentTurnEndsAt = 0f;
		ClearAuction();
		ClearPendingForcedPayment();
		PendingPurchaseSpaceIndex = -1;
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;

		Phase = GamePhase.TurnEnded;
		FinalizeMoveHistoryTurn();
		RevealMatchSeed();
		MatchState = MatchLifecycleState.GameOver;
		if ( Networking.IsHost && Connection.All.Count <= 1 )
			NetworkSession.ClearRejoinWindow();

		SendGlobalPopupToAll(
			"Game ended",
			"The game hit a fatal rules error and was ended by the host. Returning everyone to lobby in 3 seconds.",
			PopupKind.Danger,
			true,
			8f
		);
	}
}
