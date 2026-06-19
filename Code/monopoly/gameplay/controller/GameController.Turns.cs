using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

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

		if ( Phase == GamePhase.WaitingToRoll || Phase == GamePhase.TurnEnded )
		{
			if ( CurrentTurnReminderSoundsPlayed < MaxTurnReminders && Time.Now - LastTimeCurrentTurnReminderPlayed >= SecondsBetweenTurnReminders )
			{
				CurrentTurnReminderSoundsPlayed++;
				LastTimeCurrentTurnReminderPlayed = Time.Now;
				PlayTurnSound( CurrentPlayer );
			}
		}

		if ( Phase is GamePhase.ResolvingDiceRoll or GamePhase.ResolvingSpace )
			return;

		if ( IsGambleScreenActive )
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
			BankruptPlayer( skippedPlayerIndex, Players.ElementAtOrDefault( PendingForcedPaymentReceiverIndex ), debtAmount: PendingForcedPaymentAmount );

		skippedPlayer.TurnTimeoutCount = Math.Max( skippedPlayer.TurnTimeoutCount + 1, 1 );
		ClearSelectedSpaceForPlayer( skippedPlayer );

		if ( skippedPlayer.TurnTimeoutCount >= 2 )
		{
			Log.Info( $"{skippedPlayer.PlayerName}'s turn timed out twice and they were removed for AFK." );
			SendTableChatMessage( "Player removed", $"{skippedPlayer.PlayerName} timed out twice and was removed for AFK." );
			RemovePlayerForAfkTimeout( skippedPlayer );
			AdvanceTurn();
			return;
		}

		Log.Info( $"{skippedPlayer.PlayerName}'s turn timed out and was skipped. Timeout strike {skippedPlayer.TurnTimeoutCount}/2." );
		SendTableChatMessage( "Turn skipped", $"{skippedPlayer.PlayerName}'s turn timed out. One more timeout will remove them." );

		AdvanceTurn();
	}

	[Button( "Roll Dice" )]
	public async Task RollDiceAsync(int amount = -1, float throwStrength = 0.5f)
	{

		if ( CurrentPlayer == LocalPlayer )
			ClearSelectedSpaceForPlayer( CurrentPlayer );

		await RollDiceAsync( amount, null, throwStrength );
	}

	public async Task RollTwoDiceAsync( int dieA, int dieB )
	{
		if ( !IsValidDieValue( dieA ) || !IsValidDieValue( dieB ) )
			return;

		await RollDiceAsync( dieA + dieB, (dieA, dieB), 0.5f );
	}

	private async Task RollDiceAsync( int amount, (int DieA, int DieB)? specifiedDice, float throwStrength )
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
			await TryRollForJailReleaseAsync( amount, throwStrength, specifiedDice );
			return;
		}

		var executionKind = amount >= 0 || specifiedDice.HasValue
			? RollExecutionKind.ForcedAmount
			: RollExecutionKind.Physical;
		if ( executionKind == RollExecutionKind.ForcedAmount )
			RecordAdminCommand( null, specifiedDice.HasValue ? "roll_two_dice" : "roll_dice", CurrentPlayer.PlayerName );
		await RollCurrentPlayerAsync( amount, false, executionKind, throwStrength, specifiedDice );
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

		if ( !PayBank( CurrentPlayer, JailFineAmount, true, BankPaymentSource.JailFine ) )
		{
			SendPopupToPlayer( CurrentPlayer, "Jail fine", $"Raise ${JailFineAmount} to leave Jail.", PopupKind.Warning );
			return;
		}

		ReleasePlayerFromJail( CurrentPlayer );
		SendTableChatMessage( "Jail fine paid", $"{CurrentPlayer.PlayerName} paid ${JailFineAmount} to leave Jail." );
		await RollCurrentPlayerAsync( -1, ShouldSuppressDoublesExtraTurnForJailRelease(), RollExecutionKind.JailRelease );
	}

	public async Task UseGetOutOfJailFreeCardAsync()
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( CurrentPlayer is null || !CurrentPlayer.IsAssigned || CurrentPlayer.IsBankrupt )
			return;

		if ( Phase != GamePhase.WaitingToRoll || !CurrentPlayer.IsInJail )
			return;

		if ( IsForcedJailFineDue( CurrentPlayer ) )
		{
			SendPopupToPlayer( CurrentPlayer, "Jail fine", $"Pay the ${JailFineAmount} fine to leave Jail.", PopupKind.Warning );
			return;
		}

		if ( !TryUseGetOutOfJailFreeCard( CurrentPlayer ) )
		{
			SendPopupToPlayer( CurrentPlayer, "Get Out of Jail Free", "You do not have a Get Out of Jail Free card.", PopupKind.Warning );
			return;
		}

		await RollCurrentPlayerAsync( -1, ShouldSuppressDoublesExtraTurnForJailRelease(), RollExecutionKind.JailRelease );
	}

	public async Task TryRollForJailReleaseAsync( int amount = -1, float throwStrength = 0.5f, (int DieA, int DieB)? specifiedDice = null )
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( CurrentPlayer is null || !CurrentPlayer.IsAssigned || CurrentPlayer.IsBankrupt )
			return;

		if ( Phase != GamePhase.WaitingToRoll || !CurrentPlayer.IsInJail )
			return;

		if ( IsForcedJailFineDue( CurrentPlayer ) )
		{
			SendPopupToPlayer( CurrentPlayer, "Jail fine", $"Pay the ${JailFineAmount} fine to leave Jail.", PopupKind.Warning );
			return;
		}

		if ( amount >= 0 || specifiedDice.HasValue )
			RecordAdminCommand( null, specifiedDice.HasValue ? "roll_two_dice" : "roll_dice", CurrentPlayer.PlayerName );

		BeginResolvedAction();
		BeginPendingRoll( CurrentPlayerIndex, RollExecutionKind.JailRelease, ShouldSuppressDoublesExtraTurnForJailRelease(), true );

		int total;
		bool rolledDoubles = false;

		if ( specifiedDice.HasValue )
		{
			LastDieA = specifiedDice.Value.DieA;
			LastDieB = specifiedDice.Value.DieB;
			RecordDiceResult( CurrentPlayerIndex, LastDieA, LastDieB, true, true, "Forced jail roll" );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}
		else if ( amount != -1 )
		{
			LastDieA = Math.Clamp( amount - 1, 1, 6 );
			LastDieB = Math.Clamp( amount - LastDieA, 1, 6 );
			RecordDiceResult( CurrentPlayerIndex, LastDieA, LastDieB, true, true, "Forced jail roll" );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}
		else
		{
			var rollIndex = NextDiceRollIndex;
			(LastDieA, LastDieB) = RollVerifiedDice( CurrentPlayerIndex, true, false, "Jail roll" );
			(LastDieA, LastDieB) = await RollPhysicalDiceAsync(
				throwStrength, LastDieA, LastDieB, rollIndex );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}

		PendingRollTotal = total;
		await CompletePendingJailRollAsync( total, rolledDoubles );
	}

	private async Task CompletePendingJailRollAsync( int total, bool rolledDoubles )
	{
		RecordPendingDiceRollForStats();
		ReportDiceRollAchievements( CurrentPlayer, rolledDoubles );
		ApplySnakeEyesBonus( CurrentPlayer, rolledDoubles );

		if ( rolledDoubles )
		{
			ReleasePlayerFromJail( CurrentPlayer );
			Log.Info( $"{CurrentPlayer.PlayerName} rolled doubles to leave Jail." );
			if ( Config?.DoublesGoesAgain == true && Config?.DoublesGoAgainOutOfJail == true && ApplyDoublesRule( true ) )
			{
				ClearPendingRollState();
				return;
			}

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
				AdvanceTurnImmediately();
				return;
			}

			if ( !PayBank( CurrentPlayer, JailFineAmount, true, BankPaymentSource.JailFine ) )
			{
				ClearPendingRollState();
				if ( CurrentPlayer is null || CurrentPlayer.IsBankrupt )
				{
					AdvanceTurnImmediately();
					return;
				}

				Phase = GamePhase.WaitingToRoll;
				CurrentTurnGetsExtraRoll = false;
				StartTurnTimer();
				SendPopupToPlayer( CurrentPlayer, "Jail fine", $"Raise ${JailFineAmount} to leave Jail.", PopupKind.Warning );
				return;
			}

			ReleasePlayerFromJail( CurrentPlayer );
			SendTableChatMessage( "Jail fine paid", $"{CurrentPlayer.PlayerName} paid ${JailFineAmount} after three failed Jail rolls." );
			ClearPendingRollState();
			await MoveCurrentPlayerAfterRoll( total, RollExecutionKind.JailRelease );
			return;
		}

		ClearPendingRollState();
		AdvanceTurnImmediately();
	}

	private async Task RollCurrentPlayerAsync( int amount, bool suppressDoublesExtraTurn, RollExecutionKind executionKind, float throwStrength = 0.5f, (int DieA, int DieB)? specifiedDice = null )
	{
		BeginResolvedAction();
		BeginPendingRoll( CurrentPlayerIndex, executionKind, suppressDoublesExtraTurn, false );

		int total;
		bool rolledDoubles = false;

		if ( specifiedDice.HasValue )
		{
			LastDieA = specifiedDice.Value.DieA;
			LastDieB = specifiedDice.Value.DieB;
			RecordDiceResult( CurrentPlayerIndex, LastDieA, LastDieB, false, true, "Forced roll" );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}
		else if ( amount != -1 )
		{
			LastDieA = Math.Clamp( amount - 1, 1, 6 );
			LastDieB = Math.Clamp( amount - LastDieA, 1, 6 );
			RecordDiceResult( CurrentPlayerIndex, LastDieA, LastDieB, false, true, "Forced roll" );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}
		else
		{
			var rollIndex = NextDiceRollIndex;
			(LastDieA, LastDieB) = RollVerifiedDice( CurrentPlayerIndex, false, false, "Normal roll" );
			(LastDieA, LastDieB) = await RollPhysicalDiceAsync(
				throwStrength, LastDieA, LastDieB, rollIndex );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}

		PendingRollTotal = total;
		await CompletePendingNormalRollAsync( total, rolledDoubles );
	}

	private static bool IsValidDieValue( int value )
	{
		return value is >= 1 and <= 6;
	}

	private void ApplySnakeEyesBonus( PlayerState player, bool rolledDoubles )
	{
		if ( player is null || player.IsBankrupt )
			return;

		if ( LastDieA != 1 || LastDieB != 1 )
			return;

		if ( IsThirdConsecutiveDoublesRoll( rolledDoubles ) &&
			Config?.DoublesGoesAgain == true &&
			Config?.SnakeEyesBonusWhenRolledDoublesThreeInARow != true )
			return;

		var bonus = Math.Max( Config?.SnakeEyesBonusMoney ?? 0, 0 );
		if ( bonus <= 0 )
			return;

		player.Money += bonus;
		TrySettlePendingForcedPaymentForPlayer( GetPlayerIndex( player ) );
		ShowMoneyReceivedPopup( player, bonus, "snake eyes" );
		Log.Info( $"{player.PlayerName} collected ${bonus} for rolling snake eyes." );
	}

	private bool IsThirdConsecutiveDoublesRoll( bool rolledDoubles )
	{
		if ( !rolledDoubles )
			return false;

		var consecutiveBeforeRoll = CurrentTurnDoublesPlayerIndex == CurrentPlayerIndex
			? CurrentTurnConsecutiveDoubles
			: 0;

		return consecutiveBeforeRoll >= 2;
	}

	private async Task CompletePendingNormalRollAsync( int total, bool rolledDoubles )
	{
		if ( PendingRollPlayerIndex >= 0 )
			CurrentPlayerIndex = PendingRollPlayerIndex;

		var executionKind = PendingRollExecutionKind >= 0
			? (RollExecutionKind)PendingRollExecutionKind
			: RollExecutionKind.Physical;
		var player = CurrentPlayer;
		var suppressDoublesExtraTurn = PendingRollSuppressDoublesExtraTurn || ShouldSuppressDoublesExtraTurnForVacationCashBreak( player, executionKind );

		RecordPendingDiceRollForStats();
		ApplySnakeEyesBonus( CurrentPlayer, rolledDoubles );
		ReportDiceRollAchievements( CurrentPlayer, rolledDoubles );

		if ( !suppressDoublesExtraTurn && Config?.DoublesGoesAgain == true && ApplyDoublesRule( rolledDoubles ) )
		{
			ClearVacationCashBreakFlag( player );
			ClearPendingRollState();
			return;
		}

		if ( CurrentPlayer is null || CurrentPlayer.IsInJail )
		{
			ClearVacationCashBreakFlag( player );
			ClearPendingRollState();
			return;
		}

		ClearVacationCashBreakFlag( player );
		ClearPendingRollState();
		await MoveCurrentPlayerAfterRoll( total, executionKind );
	}

	private bool ShouldSuppressDoublesExtraTurnForJailRelease()
	{
		return Config?.DoublesGoAgainOutOfJail != true;
	}

	private bool ShouldSuppressDoublesExtraTurnForVacationCashBreak( PlayerState player, RollExecutionKind executionKind )
	{
		return executionKind != RollExecutionKind.JailRelease &&
			player?.IsReturningFromVacationCashBreak == true &&
			Config?.VacationCash == true &&
			Config?.DoublesGoAgainOutOfVacationCashBreak != true;
	}

	private static void ClearVacationCashBreakFlag( PlayerState player )
	{
		if ( player is not null )
			player.IsReturningFromVacationCashBreak = false;
	}

	private void BeginPendingRoll( int playerIndex, RollExecutionKind executionKind, bool suppressDoublesExtraTurn, bool isJailAttempt )
	{
		PendingRollPlayerIndex = playerIndex;
		PendingRollExecutionKind = (int)executionKind;
		PendingRollTotal = 0;
		PendingRollStatsRecorded = false;
		PendingRollSuppressDoublesExtraTurn = suppressDoublesExtraTurn;
		PendingRollIsJailAttempt = isJailAttempt;
		PendingRollStartedAt = Time.Now;
	}

	private void ClearPendingRollState()
	{
		PendingRollPlayerIndex = -1;
		PendingRollExecutionKind = -1;
		PendingRollTotal = 0;
		PendingRollStatsRecorded = false;
		PendingRollSuppressDoublesExtraTurn = false;
		PendingRollIsJailAttempt = false;
		PendingRollStartedAt = 0f;
		IsResolvingPhysicalDice = false;
		PhysicalDiceStartedAt = 0f;
	}

	private async Task RecoverPendingRollAsync()
	{
		await RecoverPendingRollAsync( true );
	}

	private async Task RecoverPendingRollAsync( bool waitForPhysicalDiceResult )
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
			var (dieA, dieB) = waitForPhysicalDiceResult
				? await WaitForPhysicalDiceResultAsync()
				: GetFallbackDiceResult();
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
				SendPopupToPlayer(
					CurrentPlayer,
					"Three doubles",
					"You rolled doubles three times in a row and were sent to Jail.",
					PopupKind.Danger,
					true,
					5f
				);
				SendTableChatMessage( "Three doubles", $"{CurrentPlayer.PlayerName} rolled doubles three times in a row and was sent to Jail." );
				SendPlayerToJail( CurrentPlayer );
				CurrentTurnConsecutiveDoubles = 0;
				CurrentTurnDoublesPlayerIndex = -1;
				CurrentPlayer.ConsecutiveDoubles = 0;
				Log.Info( $"{CurrentPlayer.PlayerName} rolled three doubles in a row and went to Jail." );
				AdvanceTurnImmediately();
				return true;
			}

			CurrentTurnGetsExtraRoll = true;
			SendPopupToPlayer(
				CurrentPlayer,
				"Doubles rolled",
				"You rolled doubles and get another turn.",
				PopupKind.Success,
				true,
				4f
			);
			SendTableChatMessage( "Doubles rolled", $"{CurrentPlayer.PlayerName} rolled doubles and gets another turn." );
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

		Phase = GamePhase.ResolvingSpace;
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
				PlayPlayerTokenStepForPlayer( ActiveMovementPlayerIndex, Board.GetSpacePosition(player.SpaceIndex) );

				ActiveMovementRemainingSteps = Math.Max( ActiveMovementRemainingSteps - 1, 0 );
				if ( player.SpaceIndex == (Board?.GoSpaceIndex ?? 0) )
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

	public bool CanFinishActiveMovement( PlayerState player )
	{
		if ( player is null || MatchState != MatchLifecycleState.InGame )
			return false;

		if ( Phase != GamePhase.ResolvingSpace || ActiveMovementRemainingSteps <= 0 )
			return false;

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 || ActiveMovementPlayerIndex != playerIndex )
			return false;

		var unlockMinutes = Math.Max( Config?.InstantMoveButtonUnlockMinutes ?? 0, 0 );
		var elapsedSeconds = GameStartedAt <= 0f ? 0f : Math.Max( Time.Now - GameStartedAt, 0f );
		return elapsedSeconds >= unlockMinutes * 60f;
	}

	public void FinishActiveMovement( PlayerState player )
	{
		if ( !Networking.IsHost || !CanFinishActiveMovement( player ) )
			return;

		var skippedPasses = CountGoPassesDuringMovement( player.SpaceIndex, ActiveMovementRemainingSteps );
		player.SpaceIndex = ActiveMovementTargetSpaceIndex >= 0
			? NormalizeSpaceIndex( ActiveMovementTargetSpaceIndex )
			: NormalizeSpaceIndex( player.SpaceIndex + ActiveMovementRemainingSteps );
		ActiveMovementGoPassCount += skippedPasses;
		ActiveMovementRemainingSteps = 0;
		ActiveMovementLastProgressAt = Time.Now;
		PlayPlayerTokenStepForPlayer( ActiveMovementPlayerIndex, Board.GetSpacePosition( player.SpaceIndex ) );
	}

	private int CountGoPassesDuringMovement( int startSpaceIndex, int steps )
	{
		var passCount = 0;
		var spaceIndex = NormalizeSpaceIndex( startSpaceIndex );

		for ( var i = 0; i < steps; i++ )
		{
			spaceIndex = NormalizeSpaceIndex( spaceIndex + 1 );
			if ( spaceIndex == (Board?.GoSpaceIndex ?? 0) )
				passCount++;
		}

		return passCount;
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

		SetPostActionPhase();
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

	public void SendPlayerToJail( PlayerState player )
	{
		if ( player is null )
			return;

		player.SpaceIndex = Board?.JailSpaceIndex ?? 10;
		player.IsInJail = true;
		player.JailTurnsRemaining = JailTurnCount;
		player.ConsecutiveDoubles = 0;
		SnapPlayerTokenToSpace( player );
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;

		Log.Info( $"{player.PlayerName} was sent to Jail." );
		ReportAchievementEvent( player, AchievementEventTypes.SentToJail );
	}

	public bool IsForcedJailFineDue( PlayerState player )
	{
		return player is not null &&
			player.IsInJail &&
			player.JailTurnsRemaining <= 0 &&
			Config?.ForceJailFineAfterFailedDoubles == true;
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

	private bool TryUseGetOutOfJailFreeCard( PlayerState player )
	{
		if ( player is null )
			return false;

		if ( player.ChanceGetOutOfJailFreeCards > 0 )
		{
			player.ChanceGetOutOfJailFreeCards--;
			ReturnGetOutOfJailFreeCardToDeck( CardDeck.Chance );
		}
		else if ( player.CommunityChestGetOutOfJailFreeCards > 0 )
		{
			player.CommunityChestGetOutOfJailFreeCards--;
			ReturnGetOutOfJailFreeCardToDeck( CardDeck.CommunityChest );
		}
		else
		{
			return false;
		}

		ReleasePlayerFromJail( player );
		SendPopupToPlayer( player, "Get Out of Jail Free", "You used a Get Out of Jail Free card.", PopupKind.Info, true, 4f );
		SendTableChatMessage( "Get Out of Jail Free", $"{player.PlayerName} used a Get Out of Jail Free card." );
		return true;
	}

	private void ReturnGetOutOfJailFreeCardToDeck( CardDeck deck )
	{
		var card = (deck == CardDeck.Chance ? Board?.ChanceCards : Board?.CommunityChestCards)?
			.FirstOrDefault( card => card?.Action == CardAction.GetOutOfJailFree );

		if ( card is null )
			return;

		var drawPile = deck == CardDeck.Chance
			? chanceDrawPile
			: communityChestDrawPile;

		drawPile.Add( card );
	}

	private void BeginResolvedAction()
	{
		Phase = GamePhase.ResolvingDiceRoll;
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
		FinalizeMoveHistoryTurn();

		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			PlayTurnSound( CurrentPlayer );
			StartTurnTimer();
			TryAutosaveStablePoint( "Extra roll" );
			return;
		}

		ClearSelectedSpaceForPlayer( CurrentPlayer );
		if ( CurrentPlayer is not null )
		{
			CurrentPlayer.ConsecutiveDoubles = 0;
			CurrentPlayer.TurnTimeoutCount = 0;
		}
		CurrentTurnConsecutiveDoubles = 0;
		CurrentTurnDoublesPlayerIndex = -1;
		AdvanceTurn();
	}

	private void SetPostActionPhase()
	{
		if ( HasPendingForcedPayment )
		{
			Phase = GamePhase.TurnEnded;
			StartTurnTimer();
			return;
		}

		if ( CurrentTurnGetsExtraRoll && !HasPendingForcedPayment )
		{
			//ClearSelectedSpaceForPlayer( CurrentPlayer );
			PlayTurnSound( CurrentPlayer );
			Phase = GamePhase.WaitingToRoll;
			StartTurnTimer();
			return;
		}

		Phase = GamePhase.TurnEnded;
	}

	private void PlayTurnSound( PlayerState player )
	{
		PlaySoundToConnection( GetConnectionForPlayer( player ), GameAssets.Sounds.PianoBingBingBing, TurnSoundDelay );
	}

	public bool TryPlayTurnReminder( PlayerState player )
	{
		if ( !Networking.IsHost || player is null || !player.IsAssigned || player.IsBankrupt )
			return false;

		PlayTurnSound( player );
		return true;
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
		TryAutosaveStablePoint( "Turn ended" );
	}

	private void AdvanceTurn()
	{
		CheckForGameOver();
		if ( MatchState == MatchLifecycleState.GameOver )
			return;

		if ( Players.Count == 0 )
			return;

		CurrentTurnReminderSoundsPlayed = 0;
		LastTimeCurrentTurnReminderPlayed = Time.Now;

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
				BeginMoveHistoryTurn();
				Phase = GamePhase.WaitingToRoll;
				StartTurnTimer();
				TryAutosaveStablePoint( "Turn advanced" );
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
				BeginMoveHistoryTurn();
				Phase = GamePhase.WaitingToRoll;
				StartTurnTimer();
				TryAutosaveStablePoint( "Turn advanced" );
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
		{
			player.ConsecutiveDoubles = 0;
			PlayTurnSound(player);
		}
			
	}
}
