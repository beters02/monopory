using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

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

		SendPopupToAll( "Game started", "The first turn is live.", PopupKind.Success, true, 4f );
		return true;
	}

	public bool TrySetReady( PlayerState player, bool isReady )
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
		SendPopupToAll( "Paused", "The host paused the game.", PopupKind.Info, true, 4f );
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
		SendPopupToAll( "Resumed", "Back to the board.", PopupKind.Success, true, 3f );
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
			PopupKind.Danger,
			true,
			8f
		);
	}
}
