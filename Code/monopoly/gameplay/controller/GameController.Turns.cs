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
	public async Task RollDiceAsync(int amount = -1)
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

		Phase = GamePhase.ResolvingSpace;
		CurrentTurnGetsExtraRoll = false;
		currentRollDrewCard = false;

		// move player...

		int total;
		bool rolledDoubles = false;

		if (amount != -1)
			total = amount;
		else
		{
			LastDieA = Game.Random.Int( 1, 6 );
			LastDieB = Game.Random.Int( 1, 6 );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}

		if ( Config?.DoublesGoesAgain == true )
		{
			if ( rolledDoubles )
			{
				CurrentPlayer.ConsecutiveDoubles++;
				if ( CurrentPlayer.ConsecutiveDoubles >= 3 )
				{
					SendPlayerToJail( CurrentPlayer );
					CurrentPlayer.ConsecutiveDoubles = 0;
					Log.Info( $"{CurrentPlayer.PlayerName} rolled three doubles in a row and went to Jail." );
					CompleteTurn();
					Phase = GamePhase.WaitingToRoll;
					return;
				}

				CurrentTurnGetsExtraRoll = true;
			}
			else
			{
				CurrentPlayer.ConsecutiveDoubles = 0;
			}
		}

		//var SpaceIndex = (CurrentPlayer.SpaceIndex + total) % 40;
		//CurrentPlayer.SpaceIndex = SpaceIndex;
		await MovePlayerSteps( CurrentPlayer, total );

		Log.Info( $"Player {CurrentPlayerIndex + 1} rolled {LastDieA} + {LastDieB} = {total}" );

		if ( Phase == GamePhase.ResolvingSpace )
		{
			if ( CurrentPlayer is not null && CurrentPlayer.IsBankrupt )
			{
				CompleteTurn();
				Phase = GamePhase.WaitingToRoll;
			}
			else if ( amount >= 0 && !currentRollDrewCard && !HasPendingForcedPayment )
			{
				CompleteTurn();
				Phase = GamePhase.WaitingToRoll;
			}
			else
			{
				Phase = GamePhase.TurnEnded;
			}
		}
	}

	private async Task MovePlayerSteps(PlayerState player, int steps)
	{
		SetPlayerTokenWalking( player, true );

		try
		{
			for ( int i = 0; i < steps; i++ )
			{
				player.SpaceIndex = NormalizeSpaceIndex(player.SpaceIndex + 1);

				await Task.DelaySeconds( 0.4f );

				if ( player.SpaceIndex == 0 )
					player.Money += 200;
			}

			ResolveLanding( player );
		}
		finally
		{
			SetPlayerTokenWalking( player, false );
		}
	}

	private void SendPlayerToJail( PlayerState player )
	{
		player.SpaceIndex = 10;
		player.IsInJail = true;
		player.ConsecutiveDoubles = 0;
		CurrentTurnGetsExtraRoll = false;

		Log.Info( $"{player.PlayerName} was sent to Jail." );
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
		CurrentPlayer.ConsecutiveDoubles = 0;
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
				Phase = GamePhase.WaitingToRoll;
				StartTurnTimer();
				return;
			}
		}

		CurrentTurnEndsAt = 0f;
	}
}
