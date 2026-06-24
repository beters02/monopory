using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;
using Sandbox.Network;

public sealed partial class GameController : Component
{

	public bool TryBankruptPlayerForCheat( PlayerState player, out string message )
	{
		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 || player is null || !player.IsAssigned )
		{
			message = "Player is not active.";
			return false;
		}

		if ( player.IsBankrupt )
		{
			message = $"{player.PlayerName} is already bankrupt.";
			return false;
		}

		BankruptPlayer( playerIndex, null, true );
		message = $"Bankrupted {player.PlayerName}.";
		return true;
	}

	public int GetPlayerLiquidAssetTotal( int playerIndex )
	{
		if ( playerIndex < 0 )
			return 0;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || player.IsBankrupt )
			return 0;

		var total = Math.Max( player.Money, 0 );
		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			total += GetImprovementCount( spaceIndex ) * GetImprovementSellValue( spaceIndex );

			if ( !IsMortgaged( spaceIndex ) )
				total += GetMortgageValue( spaceIndex );
		}

		return total;
	}

	private void BankruptPlayer( int playerIndex, PlayerState creditor, bool advanceTurnIfCurrent = false, int debtAmount = 0 )
	{
		if ( playerIndex < 0 )
			return;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || player.IsBankrupt )
			return;

		var creditorIndex = creditor is null ? -1 : GetPlayerIndex( creditor );

		player.IsBankrupt = true;
		player.IsInJail = false;
		player.JailTurnsRemaining = 0;
		ReturnHeldTradableCardsToDeck( player );
		player.ConsecutiveDoubles = 0;
		player.SkipsNextTurn = false;
		player.IsReturningFromVacationCashBreak = false;

		if ( PendingForcedPaymentPlayerIndex == playerIndex )
			ClearPendingForcedPayment();

		ResolveBankruptedPlayerAssets( playerIndex, player, creditor, creditorIndex, debtAmount );

		foreach ( var trade in GetTrades() )
		{
			if ( trade.SenderPlayerIndex == playerIndex || trade.ReceiverPlayerIndex == playerIndex )
			{
				PendingTrades.Remove( trade.Id );
				TradeViewers.Remove( trade.Id );
				TradeEditors.Remove( trade.Id );
			}
		}

		if ( AuctionHighBidderIndex == playerIndex )
		{
			AuctionHighBidderIndex = -1;
			AuctionCurrentBid = 0;
		}

		var creditorText = creditor is null ? "" : $" while owing {creditor.PlayerName}";
		Log.Info( $"{player.PlayerName} went bankrupt{creditorText}." );
		SendGlobalPopupToAll( "Bankrupt", $"{player.PlayerName} is bankrupt.", PopupKind.Danger, true, 6f );
		ReportAchievementEvent( player, AchievementEventTypes.WentBankrupt );
		if ( creditor is not null )
			ReportAchievementEvent( creditor, AchievementEventTypes.BankruptedOpponent );

		if ( advanceTurnIfCurrent &&
			MatchState == MatchLifecycleState.InGame &&
			CurrentPlayerIndex == playerIndex )
		{
			PendingPurchaseSpaceIndex = -1;
			CurrentTurnGetsExtraRoll = false;
			if ( Phase == GamePhase.Auctioning )
				ClearAuction();
			Phase = GamePhase.TurnEnded;
			AdvanceTurn();
		}

		CheckForGameOver();
		TryAutosaveStablePoint( "Bankruptcy" );
	}

	private void ResolveBankruptedPlayerAssets( int playerIndex, PlayerState player, PlayerState creditor, int creditorIndex, int debtAmount )
	{
		if ( player is null )
			return;

		if ( creditor is not null && creditorIndex >= 0 && !creditor.IsBankrupt )
		{
			switch ( Config?.PlayerBankruptedPlayerMode ?? PlayerBankruptedPlayerMode.GivePropertiesToBankrupter )
			{
				case PlayerBankruptedPlayerMode.MakePropertiesUnowned:
					MakeBankruptedPlayerPropertiesUnowned( playerIndex );
					PayBankruptedPlayerDebtFromBank( player, creditor, debtAmount );
					return;

				case PlayerBankruptedPlayerMode.GivePropertiesToBankrupter:
				default:
					GiveBankruptedPlayerAssetsToCreditor( playerIndex, player, creditor, creditorIndex );
					return;
			}
		}

		MakeBankruptedPlayerPropertiesUnowned( playerIndex );
		player.Money = 0;
	}

	private void GiveBankruptedPlayerAssetsToCreditor( int playerIndex, PlayerState player, PlayerState creditor, int creditorIndex )
	{
		var ownedProperties = GetOwnedPropertyIndexes( playerIndex );
		var ownedSetsBeforeTransfer = CaptureOwnedSetKeys( creditorIndex );
		var soldImprovementValue = 0;

		foreach ( var spaceIndex in ownedProperties )
		{
			soldImprovementValue += GetImprovementCount( spaceIndex ) * GetImprovementSellValue( spaceIndex );
			PropertyImprovements.Remove( spaceIndex );
			PropertyOwners[spaceIndex] = creditorIndex;
			ReportPropertyAcquiredAchievements( creditorIndex, Board?.GetSpaceDef( spaceIndex ) );
		}

		player.Money += soldImprovementValue;

		var transferredMoney = Math.Max( player.Money, 0 );
		if ( transferredMoney > 0 )
		{
			creditor.Money += transferredMoney;
			ShowMoneyReceivedPopup( creditor, transferredMoney, player.PlayerName );
		}

		player.Money = 0;
		ShowNewlyOwnedSetPopups( ownedSetsBeforeTransfer, creditorIndex );

		if ( ownedProperties.Count > 0 )
			SendTableChatMessage( "Bankruptcy transfer", $"{creditor.PlayerName} received {ownedProperties.Count} properties from {player.PlayerName}." );
	}

	private void MakeBankruptedPlayerPropertiesUnowned( int playerIndex )
	{
		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			PropertyOwners.Remove( spaceIndex );
			PropertyImprovements.Remove( spaceIndex );
			MortgagedProperties.Remove( spaceIndex );
		}
	}

	private void PayBankruptedPlayerDebtFromBank( PlayerState player, PlayerState creditor, int debtAmount )
	{
		var payment = debtAmount > 0 ? debtAmount : Math.Max( player?.Money ?? 0, 0 );
		if ( payment > 0 && creditor is not null )
		{
			creditor.Money += payment;
			ShowMoneyReceivedPopup( creditor, payment, "the bank" );
			SendTableChatMessage( "Bankruptcy payment", $"{creditor.PlayerName} received the full ${payment} payment after {player.PlayerName} went bankrupt." );
		}

		if ( player is not null )
			player.Money = 0;
	}

	public bool TryBankruptAndAbandonPlayer( PlayerState player )
	{
		if ( !Networking.IsHost )
			return false;

		if ( player is null || !player.IsAssigned || player.IsBankrupt )
			return false;

		FinalizeAbandonedPlayer( player );
		return true;
	}

	public bool TryDeclareBankruptcy( PlayerState player )
	{
		if ( !Networking.IsHost )
			return false;

		if ( player is null || !player.IsAssigned || player.IsBankrupt )
			return false;

		var playerIndex = Players.IndexOf( player );
		if ( playerIndex < 0 )
			return false;

		var creditor = GetPendingForcedPaymentCreditorForPlayer( playerIndex );
		var debtAmount = HasPendingForcedPaymentForPlayer( playerIndex ) ? PendingForcedPaymentAmount : 0;
		player.IsDisconnected = false;
		player.AbandonEndsAt = 0f;
		BankruptPlayer( playerIndex, creditor, true, debtAmount );
		return true;
	}

	private PlayerState GetPendingForcedPaymentCreditorForPlayer( int playerIndex )
	{
		if ( !HasPendingForcedPaymentForPlayer( playerIndex ) )
			return null;

		if ( PendingForcedPaymentToBank || PendingForcedPaymentToEachPlayer )
			return null;

		return Players.ElementAtOrDefault( PendingForcedPaymentReceiverIndex );
	}

	private void CheckForGameOver()
	{
		if ( MatchState != MatchLifecycleState.InGame )
			return;

		if ( StartingPlayerCount <= 1 )
			return;

		var remaining = Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null && entry.player.IsAssigned && !entry.player.IsBankrupt )
			.ToList();

		if ( remaining.Count > 1 )
			return;

		WinnerPlayerIndex = remaining.Count == 1 ? remaining[0].index : -1;
		CurrentTurnEndsAt = 0f;
		ClearAuction();
		ClearPendingForcedPayment();
		PendingPurchaseSpaceIndex = -1;
		Phase = GamePhase.TurnEnded;
		FinalizeMoveHistoryTurn();
		RevealMatchSeed();
		MatchState = MatchLifecycleState.GameOver;
		if ( Networking.IsHost && Connection.All.Count <= 1 )
			NetworkSession.ClearRejoinWindow();

		var winnerName = Winner?.PlayerName ?? "No one";
		ReportMatchCompletedAchievements();
		ReportWinnerAchievements();
		PlayPlayerTokenCheering( Winner );
		SendGlobalPopupToAll( "Game over", $"{winnerName} won the game.", PopupKind.Success, true, 8f );
		Log.Info( $"Game over. Winner: {winnerName}." );
		TryAutosaveStablePoint( "Game over" );
	}
}
