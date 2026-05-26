using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{
	private const int JailFineAmount = 50;
	private const int JailTurnCount = 3;

	private void UpdateTurnTimer()
	{
		var limit = Config?.TurnTimeLimitSeconds ?? 180;
		if ( limit <= 0 )
		{
			CurrentTurnEndsAt = 0f;
			return;
		}

		var player = CurrentPlayer;
		if ( player is null || !player.IsAssigned || player.IsBankrupt )
		{
			AdvanceTurn();
			return;
		}

		if ( Phase == GamePhase.ResolvingSpace )
			return;

		if ( CurrentTurnEndsAt <= 0f )
			StartTurnTimer();

		if ( CurrentTurnEndsAt > 0f && Time.Now >= CurrentTurnEndsAt )
			SkipCurrentTurnForTimeout();
	}

	private void StartTurnTimer()
	{
		var limit = Config?.TurnTimeLimitSeconds ?? 180;
		CurrentTurnEndsAt = limit <= 0 ? 0f : Time.Now + limit;
	}

	private void SkipCurrentTurnForTimeout()
	{
		var skippedPlayerIndex = CurrentPlayerIndex;
		var skippedPlayer = CurrentPlayer;

		PendingPurchaseSpaceIndex = -1;

		if ( Phase == GamePhase.Auctioning )
			ClearAuction();

		CurrentTurnGetsExtraRoll = false;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;
		CurrentTurnEndsAt = 0f;

		if ( skippedPlayer is null || !skippedPlayer.IsAssigned || skippedPlayer.IsBankrupt )
		{
			AdvanceTurn();
			return;
		}

		if ( HasPendingForcedPaymentForPlayer( skippedPlayerIndex ) )
			BankruptPlayer( skippedPlayerIndex, Players.ElementAtOrDefault( PendingForcedPaymentReceiverIndex ) );

		Log.Info( $"{skippedPlayer.PlayerName}'s turn timed out and was skipped." );
		SendPopupToAll( "Turn skipped", $"{skippedPlayer.PlayerName}'s turn timed out.", PopupKind.Warning, true, 4f );
		ClearSelectedSpaceForPlayer( skippedPlayer );

		AdvanceTurn();
	}

	[Button( "Roll Dice" )]
	public async Task RollDiceAsync(int amount = -1, float throwStrength = 0.5f)
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( CurrentPlayer is null )
			return;

		if ( Phase != GamePhase.WaitingToRoll )
			return;

		if ( CurrentPlayer is null || !CurrentPlayer.IsAssigned || CurrentPlayer.IsBankrupt )
		{
			AdvanceTurn();
			return;
		}

		if ( CurrentPlayer.IsInJail )
		{
			await TryRollForJailReleaseAsync( amount, throwStrength );
			return;
		}

		var executionKind = amount >= 0
			? RollExecutionKind.ForcedAmount
			: RollExecutionKind.Physical;
		await RollCurrentPlayerAsync( amount, false, executionKind, throwStrength );
	}

	public async Task PayToLeaveJailAsync()
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( CurrentPlayer is null || !CurrentPlayer.IsAssigned || CurrentPlayer.IsBankrupt )
			return;

		if ( Phase != GamePhase.WaitingToRoll || !CurrentPlayer.IsInJail )
			return;

		if ( !PayBank( CurrentPlayer, JailFineAmount ) )
		{
			SendPopupToPlayer( CurrentPlayer, "Jail fine", $"Raise ${JailFineAmount} to leave Jail.", PopupKind.Warning );
			return;
		}

		ReleasePlayerFromJail( CurrentPlayer );
		SendPopupToAll( "Jail fine paid", $"{CurrentPlayer.PlayerName} paid ${JailFineAmount} to leave Jail.", PopupKind.Info, true, 4f );
		await RollCurrentPlayerAsync( -1, true, RollExecutionKind.JailRelease );
	}

	public async Task TryRollForJailReleaseAsync( int amount = -1, float throwStrength = 0.5f )
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( CurrentPlayer is null || !CurrentPlayer.IsAssigned || CurrentPlayer.IsBankrupt )
			return;

		if ( Phase != GamePhase.WaitingToRoll || !CurrentPlayer.IsInJail )
			return;

		BeginResolvedAction();
		BeginPendingRoll( CurrentPlayerIndex, RollExecutionKind.JailRelease, true, true );

		int total;
		bool rolledDoubles = false;

		if ( amount != -1 )
		{
			total = amount;
			LastDieA = 0;
			LastDieB = 0;
		}
		else
		{
			(LastDieA, LastDieB) = await RollPhysicalDiceAsync( throwStrength );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}

		PendingRollTotal = total;
		await CompletePendingJailRollAsync( total, rolledDoubles );
	}

	private async Task CompletePendingJailRollAsync( int total, bool rolledDoubles )
	{
		if ( rolledDoubles )
		{
			ReleasePlayerFromJail( CurrentPlayer );
			Log.Info( $"{CurrentPlayer.PlayerName} rolled doubles to leave Jail." );
			ClearPendingRollState();
			await MoveCurrentPlayerAfterRoll( total, RollExecutionKind.JailRelease );
			return;
		}

		CurrentPlayer.JailTurnsRemaining = Math.Max( CurrentPlayer.JailTurnsRemaining - 1, 0 );
		Log.Info( $"{CurrentPlayer.PlayerName} failed to roll doubles and has {CurrentPlayer.JailTurnsRemaining} Jail turns remaining." );

		if ( CurrentPlayer.JailTurnsRemaining <= 0 )
		{
			if ( Config?.ForceJailFineAfterFailedDoubles != true )
			{
				Log.Info( $"{CurrentPlayer.PlayerName} stayed in Jail after their third failed doubles attempt." );
				ClearPendingRollState();
				Phase = GamePhase.TurnEnded;
				return;
			}

			if ( !PayBank( CurrentPlayer, JailFineAmount ) )
			{
				ClearPendingRollState();
				Phase = GamePhase.TurnEnded;
				SendPopupToPlayer( CurrentPlayer, "Jail fine", $"Raise ${JailFineAmount} to leave Jail.", PopupKind.Warning );
				return;
			}

			ReleasePlayerFromJail( CurrentPlayer );
			SendPopupToAll( "Jail fine paid", $"{CurrentPlayer.PlayerName} paid ${JailFineAmount} after three failed Jail rolls.", PopupKind.Info, true, 4f );
			ClearPendingRollState();
			await MoveCurrentPlayerAfterRoll( total, RollExecutionKind.JailRelease );
			return;
		}

		ClearPendingRollState();
		Phase = GamePhase.TurnEnded;
	}

	private async Task RollCurrentPlayerAsync( int amount, bool suppressDoublesExtraTurn, RollExecutionKind executionKind, float throwStrength = 0.5f )
	{
		BeginResolvedAction();
		BeginPendingRoll( CurrentPlayerIndex, executionKind, suppressDoublesExtraTurn, false );

		int total;
		bool rolledDoubles = false;

		if ( amount != -1 )
		{
			total = amount;
			LastDieA = 0;
			LastDieB = 0;
		}
		else
		{
			(LastDieA, LastDieB) = await RollPhysicalDiceAsync( throwStrength );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}

		PendingRollTotal = total;
		await CompletePendingNormalRollAsync( total, rolledDoubles );
	}

	private async Task CompletePendingNormalRollAsync( int total, bool rolledDoubles )
	{
		if ( PendingRollPlayerIndex >= 0 )
			CurrentPlayerIndex = PendingRollPlayerIndex;

		var suppressDoublesExtraTurn = PendingRollSuppressDoublesExtraTurn;
		var executionKind = PendingRollExecutionKind >= 0
			? (RollExecutionKind)PendingRollExecutionKind
			: RollExecutionKind.Physical;

		if ( !suppressDoublesExtraTurn && Config?.DoublesGoesAgain == true && ApplyDoublesRule( rolledDoubles ) )
		{
			ClearPendingRollState();
			return;
		}

		if ( CurrentPlayer is null || CurrentPlayer.IsInJail )
		{
			ClearPendingRollState();
			return;
		}

		ClearPendingRollState();
		await MoveCurrentPlayerAfterRoll( total, executionKind );
	}

	private void BeginPendingRoll( int playerIndex, RollExecutionKind executionKind, bool suppressDoublesExtraTurn, bool isJailAttempt )
	{
		PendingRollPlayerIndex = playerIndex;
		PendingRollExecutionKind = (int)executionKind;
		PendingRollTotal = 0;
		PendingRollSuppressDoublesExtraTurn = suppressDoublesExtraTurn;
		PendingRollIsJailAttempt = isJailAttempt;
		PendingRollStartedAt = Time.Now;
	}

	private void ClearPendingRollState()
	{
		PendingRollPlayerIndex = -1;
		PendingRollExecutionKind = -1;
		PendingRollTotal = 0;
		PendingRollSuppressDoublesExtraTurn = false;
		PendingRollIsJailAttempt = false;
		PendingRollStartedAt = 0f;
		IsResolvingPhysicalDice = false;
		PhysicalDiceStartedAt = 0f;
	}

	private async Task RecoverPendingRollAsync()
	{
		if ( PendingRollPlayerIndex < 0 )
			return;

		CurrentPlayerIndex = PendingRollPlayerIndex;
		var total = PendingRollTotal;
		bool rolledDoubles;

		if ( total > 0 )
		{
			rolledDoubles = LastDieA >= 1 && LastDieB >= 1 && LastDieA == LastDieB;
		}
		else
		{
			var (dieA, dieB) = await WaitForPhysicalDiceResultAsync();
			LastDieA = dieA;
			LastDieB = dieB;
			total = LastDieA + LastDieB;
			PendingRollTotal = total;
			rolledDoubles = LastDieA == LastDieB;
		}

		Log.Warning( $"Recovered pending roll as {LastDieA} + {LastDieB} = {total}." );

		if ( PendingRollIsJailAttempt )
			await CompletePendingJailRollAsync( total, rolledDoubles );
		else
			await CompletePendingNormalRollAsync( total, rolledDoubles );
	}

	private bool ApplyDoublesRule( bool rolledDoubles )
	{
		if ( CurrentPlayer is null )
			return false;

		if ( CurrentTurnDoublesPlayerIndex != CurrentPlayerIndex )
		{
			CurrentTurnDoublesPlayerIndex = CurrentPlayerIndex;
			CurrentTurnConsecutiveDoubles = 0;
			CurrentPlayer.ConsecutiveDoubles = 0;
		}

		if ( rolledDoubles )
		{
			CurrentTurnConsecutiveDoubles++;
			CurrentPlayer.ConsecutiveDoubles = CurrentTurnConsecutiveDoubles;
			if ( CurrentTurnConsecutiveDoubles >= 3 )
			{
				SendPopupToAll(
					"Three doubles",
					$"{CurrentPlayer.PlayerName} rolled doubles three times in a row and was sent to Jail.",
					PopupKind.Danger,
					true,
					5f
				);
				SendPlayerToJail( CurrentPlayer );
				CurrentTurnConsecutiveDoubles = 0;
				CurrentTurnDoublesPlayerIndex = -1;
				CurrentPlayer.ConsecutiveDoubles = 0;
				Log.Info( $"{CurrentPlayer.PlayerName} rolled three doubles in a row and went to Jail." );
				AdvanceTurnImmediately();
				return true;
			}

			CurrentTurnGetsExtraRoll = true;
			SendPopupToAll(
				"Doubles rolled",
				$"{CurrentPlayer.PlayerName} rolled doubles and gets another turn.",
				PopupKind.Success,
				true,
				4f
			);
			return false;
		}

		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = CurrentPlayerIndex;
		CurrentPlayer.ConsecutiveDoubles = 0;
		return false;
	}

	private async Task MoveCurrentPlayerAfterRoll( int total, RollExecutionKind executionKind )
	{
		//var SpaceIndex = (CurrentPlayer.SpaceIndex + total) % 40;
		//CurrentPlayer.SpaceIndex = SpaceIndex;
		await MovePlayerSteps( CurrentPlayer, total );

		Log.Info( $"Player {CurrentPlayerIndex + 1} rolled {LastDieA} + {LastDieB} = {total}" );

		// Roll origin affects dice generation and jail handling, but normal post-landing
		// turn flow should be determined by the resolved gameplay state itself.
		_ = executionKind;
	}

	private async Task MovePlayerSteps(PlayerState player, int steps)
	{
		if ( player is null )
			return;

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return;

		BeginActiveMovement( playerIndex, Math.Max( steps, 0 ) );
		await ContinueActiveMovementAsync();
	}

	private void BeginActiveMovement( int playerIndex, int steps )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null )
			return;

		ActiveMovementPlayerIndex = playerIndex;
		ActiveMovementRemainingSteps = steps;
		ActiveMovementGoPassCount = 0;
		ActiveMovementTargetSpaceIndex = NormalizeSpaceIndex( player.SpaceIndex + steps );
		ActiveMovementLastProgressAt = Time.Now;
		PendingLandingPlayerIndex = -1;
		PendingLandingSpaceIndex = -1;
		PendingLandingGoPassCount = 0;
		PendingLandingResolved = false;
	}

	private async Task ContinueActiveMovementAsync()
	{
		if ( isContinuingRecoveredMovement )
			return;

		if ( ActiveMovementPlayerIndex < 0 )
			return;

		var player = Players.ElementAtOrDefault( ActiveMovementPlayerIndex );
		if ( player is null )
			return;

		isContinuingRecoveredMovement = true;
		SetPlayerTokenWalking( player, true );

		try
		{
			while ( ActiveMovementRemainingSteps > 0 )
			{
				player.SpaceIndex = NormalizeSpaceIndex( player.SpaceIndex + 1 );

				ActiveMovementRemainingSteps = Math.Max( ActiveMovementRemainingSteps - 1, 0 );
				if ( player.SpaceIndex == 0 )
					ActiveMovementGoPassCount++;

				ActiveMovementLastProgressAt = Time.Now;
				await Task.DelaySeconds( 0.4f );
			}

			BeginPendingLanding( ActiveMovementPlayerIndex, player.SpaceIndex, ActiveMovementGoPassCount );
			ResolvePendingLandingOnce();
			FinalizeResolvedActionAfterLanding();
			UpdateHostRecoveryStateFlag();
		}
		finally
		{
			SetPlayerTokenWalking( player, false );
			isContinuingRecoveredMovement = false;
		}
	}

	private void BeginPendingLanding( int playerIndex, int spaceIndex, int goPassCount )
	{
		PendingLandingPlayerIndex = playerIndex;
		PendingLandingSpaceIndex = NormalizeSpaceIndex( spaceIndex );
		PendingLandingGoPassCount = Math.Max( goPassCount, 0 );
		PendingLandingResolved = false;
	}

	private void ResolvePendingLandingOnce()
	{
		if ( PendingLandingResolved || PendingLandingPlayerIndex < 0 )
			return;

		var player = Players.ElementAtOrDefault( PendingLandingPlayerIndex );
		if ( player is null )
		{
			ClearMovementRecoveryState();
			return;
		}

		player.SpaceIndex = PendingLandingSpaceIndex;
		PendingLandingResolved = true;
		ResolveLanding( player, PendingLandingGoPassCount );
	}

	private void FinalizeResolvedActionAfterLanding()
	{
		ClearMovementRecoveryState();

		if ( Phase != GamePhase.ResolvingSpace )
			return;

		if ( CurrentPlayer is not null && CurrentPlayer.IsBankrupt )
		{
			AdvanceTurnImmediately();
			return;
		}

		if ( resolvedActionOutcome == ResolvedActionOutcome.AdvanceImmediately )
		{
			AdvanceTurnImmediately();
			return;
		}

		Phase = GamePhase.TurnEnded;
	}

	private void ClearMovementRecoveryState()
	{
		ActiveMovementPlayerIndex = -1;
		ActiveMovementRemainingSteps = 0;
		ActiveMovementGoPassCount = 0;
		ActiveMovementTargetSpaceIndex = -1;
		ActiveMovementLastProgressAt = 0f;
		PendingLandingPlayerIndex = -1;
		PendingLandingSpaceIndex = -1;
		PendingLandingGoPassCount = 0;
		PendingLandingResolved = false;
	}

	private void SendPlayerToJail( PlayerState player )
	{
		player.SpaceIndex = 10;
		player.IsInJail = true;
		player.JailTurnsRemaining = JailTurnCount;
		player.ConsecutiveDoubles = 0;
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;

		Log.Info( $"{player.PlayerName} was sent to Jail." );
	}

	private void ReleasePlayerFromJail( PlayerState player )
	{
		if ( player is null )
			return;

		player.IsInJail = false;
		player.JailTurnsRemaining = 0;
		player.ConsecutiveDoubles = 0;
		CurrentTurnGetsExtraRoll = false;
	}

	private void BeginResolvedAction()
	{
		Phase = GamePhase.ResolvingSpace;
		CurrentTurnGetsExtraRoll = false;
		resolvedActionOutcome = ResolvedActionOutcome.StayInTurnEnded;
		ClearMovementRecoveryState();
		ClearPendingRollState();
	}

	private void MarkResolvedActionToAdvanceImmediately()
	{
		resolvedActionOutcome = ResolvedActionOutcome.AdvanceImmediately;
	}

	private void AdvanceTurnImmediately()
	{
		resolvedActionOutcome = ResolvedActionOutcome.StayInTurnEnded;
		CompleteTurn();
	}

	private void CompleteTurn()
	{
		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			StartTurnTimer();
			return;
		}

		ClearSelectedSpaceForPlayer( CurrentPlayer );
		if ( CurrentPlayer is not null )
			CurrentPlayer.ConsecutiveDoubles = 0;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;
		AdvanceTurn();
	}

	[Button( "End Turn" )]
	public void EndTurn()
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != GamePhase.TurnEnded )
			return;

		if ( HasPendingForcedPayment )
			return;

		CompleteTurn();
		Phase = GamePhase.WaitingToRoll;
		StartTurnTimer();
	}

	private void AdvanceTurn()
	{
		CheckForGameOver();
		if ( MatchState == MatchLifecycleState.GameOver )
			return;

		if ( Players.Count == 0 )
			return;

		for ( int i = 0; i < Players.Count; i++ )
		{
			CurrentPlayerIndex = NormalizePlayerIndex(CurrentPlayerIndex + 1);
			//CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;

			var player = Players[CurrentPlayerIndex];
			if ( !player.IsAssigned || player.IsBankrupt )
				continue;

			if ( player.SkipsNextTurn )
			{
				player.SkipsNextTurn = false;
				Log.Info( $"{player.PlayerName} skipped their turn." );
				continue;
			}

			if ( player.IsAssigned && !player.IsBankrupt )
			{
				BeginTurnForCurrentPlayer();
				Phase = GamePhase.WaitingToRoll;
				StartTurnTimer();
				return;
			}
		}

		for ( int i = 0; i < Players.Count; i++ )
		{
			//CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
			CurrentPlayerIndex = NormalizePlayerIndex(CurrentPlayerIndex + 1);

			if ( Players[CurrentPlayerIndex].IsAssigned && !Players[CurrentPlayerIndex].IsBankrupt )
			{
				BeginTurnForCurrentPlayer();
				Phase = GamePhase.WaitingToRoll;
				StartTurnTimer();
				return;
			}
		}

		CurrentTurnEndsAt = 0f;
	}

	private void BeginTurnForCurrentPlayer()
	{
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = CurrentPlayerIndex;

		var player = CurrentPlayer;
		if ( player is not null )
			player.ConsecutiveDoubles = 0;
	}
}
