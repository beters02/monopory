using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private void UpdateAuction()
	{
		if ( Phase != MonopolyGamePhase.Auctioning )
			return;

		if ( AuctionSpaceIndex < 0 || Time.Now < AuctionEndsAt )
			return;

		FinishAuction();
	}

	[Button( "Auction Pending Property" )]
	public void AuctionPendingProperty()
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		StartAuction( PendingPurchaseSpaceIndex );
	}

	private void StartAuction( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var def = Board?.GetSpaceDef( spaceIndex );
		if ( def is null || !IsPurchasableSpace( def ) || GetOwnerIndexForSpace( spaceIndex ) >= 0 )
		{
			PendingPurchaseSpaceIndex = -1;
			Phase = MonopolyGamePhase.TurnEnded;
			return;
		}

		PendingPurchaseSpaceIndex = -1;
		AuctionSpaceIndex = spaceIndex;
		AuctionCurrentBid = 0;
		AuctionHighBidderIndex = -1;
		AuctionEndsAt = Time.Now + 15f;
		Phase = MonopolyGamePhase.Auctioning;

		SendPopupToAll( "Auction started", $"{def.DisplayName} is up for auction.", PopupKind.Info, true, 4f );
		Log.Info( $"Auction started for {def.DisplayName}." );
	}

	private void FinishAuction()
	{
		if ( !Networking.IsHost )
			return;

		var def = Board?.GetSpaceDef( AuctionSpaceIndex );

		if ( def is not null && AuctionHighBidderIndex >= 0 )
		{
			var winner = Players.ElementAtOrDefault( AuctionHighBidderIndex );
			if ( winner is not null && winner.IsAssigned && winner.Money >= AuctionCurrentBid && PayBank( winner, AuctionCurrentBid ) )
			{
				PropertyOwners[def.Index] = AuctionHighBidderIndex;
				SendPopupToAll( "Auction won", $"{winner.PlayerName} won {def.DisplayName} for ${AuctionCurrentBid}.", PopupKind.Success, true, 5f );
				Log.Info( $"{winner.PlayerName} won {def.DisplayName} for ${AuctionCurrentBid}." );
			}
		}
		else if ( def is not null )
		{
			SendPopupToAll( "Auction ended", $"{def.DisplayName} received no bids.", PopupKind.Warning, true, 5f );
			Log.Info( $"Auction for {def.DisplayName} ended with no bids." );
		}

		ClearAuction();
		Phase = MonopolyGamePhase.TurnEnded;
	}

	private void ClearAuction()
	{
		AuctionSpaceIndex = -1;
		AuctionCurrentBid = 0;
		AuctionHighBidderIndex = -1;
		AuctionEndsAt = 0f;
	}

	public void PlaceAuctionBid( int bidderIndex, int bidAmount )
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != MonopolyGamePhase.Auctioning )
			return;

		var bidder = Players.ElementAtOrDefault( bidderIndex );
		var def = Board?.GetSpaceDef( AuctionSpaceIndex );
		if ( bidder is null || !bidder.IsAssigned || bidder.IsBankrupt || def is null )
			return;

		if ( bidAmount <= AuctionCurrentBid || bidAmount > bidder.Money )
			return;

		AuctionCurrentBid = bidAmount;
		AuctionHighBidderIndex = bidderIndex;
		AuctionEndsAt = MathF.Max( AuctionEndsAt, Time.Now + 7f );

		Log.Info( $"{bidder.PlayerName} bid ${AuctionCurrentBid} on {def.DisplayName}." );
	}
}
