using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

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

	private void BankruptPlayer( int playerIndex, PlayerState creditor )
	{
		if ( playerIndex < 0 )
			return;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || player.IsBankrupt )
			return;

		player.IsBankrupt = true;
		player.Money = 0;
		player.IsInJail = false;
		player.ConsecutiveDoubles = 0;
		player.SkipsNextTurn = false;

		if ( PendingForcedPaymentPlayerIndex == playerIndex )
			ClearPendingForcedPayment();

		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			PropertyOwners.Remove( spaceIndex );
			PropertyImprovements.Remove( spaceIndex );
			MortgagedProperties.Remove( spaceIndex );
		}

		foreach ( var trade in GetTrades() )
		{
			if ( trade.SenderPlayerIndex == playerIndex || trade.ReceiverPlayerIndex == playerIndex )
				PendingTrades.Remove( trade.Id );
		}

		if ( AuctionHighBidderIndex == playerIndex )
		{
			AuctionHighBidderIndex = -1;
			AuctionCurrentBid = 0;
		}

		var creditorText = creditor is null ? "" : $" while owing {creditor.PlayerName}";
		Log.Info( $"{player.PlayerName} went bankrupt{creditorText}." );
		CheckForGameOver();
	}

	private void CheckForGameOver()
	{
		if ( MatchState != MonopolyMatchState.InGame )
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
		Phase = MonopolyGamePhase.TurnEnded;
		MatchState = MonopolyMatchState.GameOver;

		var winnerName = Winner?.PlayerName ?? "No one";
		SendPopupToAll( "Game over", $"{winnerName} won the game.", PopupKind.Success, true, 8f );
		Log.Info( $"Monopoly game over. Winner: {winnerName}." );
	}
}
