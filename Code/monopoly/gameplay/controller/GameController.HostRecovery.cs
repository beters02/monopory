using Sandbox;

public sealed partial class GameController
{
	private const float ResolvingSpaceRecoveryDelay = 2.5f;
	private const float RecoveryRetryDelay = 1.0f;

	public bool IsEffectiveHostCaller( Connection caller )
	{
		if ( !Networking.IsHost )
			return false;

		if ( caller is null || caller == Connection.Local )
			return true;

		return PreferredHostOwnerId != 0 && caller.SteamId == PreferredHostOwnerId;
	}

	private void EnsurePreferredHostOwnerId()
	{
		if ( PreferredHostOwnerId != 0 )
			return;

		PreferredHostOwnerId = Connection.Local?.SteamId ?? Connection.Host?.SteamId ?? 0L;
		PreferredHostDisconnected = false;
	}

	void Component.INetworkListener.OnConnected( Connection connection )
	{
		HandleHostConnectionChanged( connection, true, "connected" );
	}

	void Component.INetworkListener.OnActive( Connection connection )
	{
		HandleHostConnectionChanged( connection, true, "active" );
	}

	void Component.INetworkListener.OnDisconnected( Connection connection )
	{
		HandleHostConnectionChanged( connection, false, "disconnected" );
	}

	void Component.INetworkListener.OnBecameHost( Connection previousHost )
	{
		EnsurePreferredHostOwnerId();
		if ( previousHost is not null && previousHost.SteamId == PreferredHostOwnerId )
			PreferredHostDisconnected = true;

		if ( IsResolvingPhysicalDice )
		{
			Log.Warning( "Previous host left while physical dice were resolving. Cancelling dice wait and recovering current space." );
			IsResolvingPhysicalDice = false;
			PhysicalDiceStartedAt = 0f;
		}

		Log.Warning( $"Became network host after {previousHost?.DisplayName ?? "previous host"} left. Recovering game state." );
		SendPopupToAll( "Host changed", "Recovering game state after the host left.", PopupKind.Warning, true, 4f );
		RecoverGameplayState( true );
	}

	private void HandleHostConnectionChanged( Connection connection, bool connected, string eventName )
	{
		if ( connection is null )
			return;

		if ( PreferredHostOwnerId == 0 && Networking.IsHost )
			PreferredHostOwnerId = connection.SteamId;

		if ( connection.SteamId != PreferredHostOwnerId )
			return;

		PreferredHostDisconnected = !connected;
		Log.Info( $"Preferred host {connection.DisplayName} {eventName}." );
	}

	private void UpdateHostRecoveryWatchdog()
	{
		EnsurePreferredHostOwnerId();
		RecoverDisconnectedCurrentPlayerDecision();

		if ( Phase != GamePhase.ResolvingSpace )
			return;

		if ( IsResolvingPhysicalDice && PhysicalDiceStartedAt > 0f && Time.Now - PhysicalDiceStartedAt > DiceSettleTimeout + ResolvingSpaceRecoveryDelay )
		{
			Log.Warning( "Recovering stale physical dice resolution." );
			IsResolvingPhysicalDice = false;
			PhysicalDiceStartedAt = 0f;
		}

		if ( IsResolvingPhysicalDice )
			return;

		var staleMovement = ActiveMovementPlayerIndex >= 0 &&
			(ActiveMovementLastProgressAt <= 0f || Time.Now - ActiveMovementLastProgressAt >= ResolvingSpaceRecoveryDelay);
		var missingMovement = ActiveMovementPlayerIndex < 0 && PendingLandingPlayerIndex < 0;

		if ( !staleMovement && !missingMovement )
			return;

		if ( Time.Now - lastRecoveryAttemptAt < RecoveryRetryDelay )
			return;

		lastRecoveryAttemptAt = Time.Now;
		RecoverGameplayState( false );
	}

	private void RecoverGameplayState( bool force )
	{
		if ( !Networking.IsHost || MatchState != MatchLifecycleState.InGame )
			return;

		if ( Phase != GamePhase.ResolvingSpace && !force )
			return;

		if ( PendingLandingPlayerIndex >= 0 )
		{
			Log.Warning( "Recovering pending landing after host interruption." );
			ResolvePendingLandingOnce();
			FinalizeResolvedActionAfterLanding();
			return;
		}

		if ( ActiveMovementPlayerIndex >= 0 )
		{
			Log.Warning( "Recovering active movement after host interruption." );
			_ = ContinueActiveMovementAsync();
			return;
		}

		if ( Phase == GamePhase.ResolvingSpace )
		{
			Log.Warning( "Recovering stuck ResolvingSpace without movement data." );
			BeginPendingLanding( CurrentPlayerIndex, CurrentPlayer?.SpaceIndex ?? -1, 0 );
			ResolvePendingLandingOnce();
			FinalizeResolvedActionAfterLanding();
		}
	}

	private void RecoverDisconnectedCurrentPlayerDecision()
	{
		if ( MatchState != MatchLifecycleState.InGame )
			return;

		var player = CurrentPlayer;
		if ( player is null || !player.IsDisconnected || player.AbandonEndsAt > Time.Now )
			return;

		if ( Phase == GamePhase.WaitingForBuyDecision && PendingPurchaseSpaceIndex >= 0 )
		{
			Log.Info( $"{player.PlayerName} abandoned a pending property decision. Starting auction." );
			AuctionPendingProperty();
			return;
		}

		if ( Phase == GamePhase.TurnEnded )
		{
			Log.Info( $"{player.PlayerName} abandoned at turn end. Advancing turn." );
			CompleteTurn();
			Phase = GamePhase.WaitingToRoll;
			StartTurnTimer();
		}
	}
}
