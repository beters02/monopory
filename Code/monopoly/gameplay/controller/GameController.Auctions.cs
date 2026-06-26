using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private void UpdateAuction()
	{
		if ( Phase != GamePhase.Auctioning )
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

		if ( Phase != GamePhase.WaitingForBuyDecision )
			return;

		MarkTurnActionAccepted();
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
			SetPostActionPhase();
			return;
		}

		PendingPurchaseSpaceIndex = -1;
		AuctionSpaceIndex = spaceIndex;
		AuctionCurrentBid = 0;
		AuctionHighBidderIndex = -1;
		AuctionEndsAt = Time.Now + 15f;
		PauseTurnTimerForAuction();
		Phase = GamePhase.Auctioning;

		PlayGlobalSound( AuctionStartSound );
		SendGlobalPopupToAll( "Auction started", $"{def.DisplayName} is up for auction.", PopupKind.Info, true, 4f );
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
			if ( winner is not null && winner.IsAssigned && winner.Money >= AuctionCurrentBid && PayBank( winner, AuctionCurrentBid, false ) )
			{
				var ownedSetsBeforePurchase = CaptureOwnedSetKeys( AuctionHighBidderIndex );
				PropertyOwners[def.Index] = AuctionHighBidderIndex;
				ReportPropertyAcquiredAchievements( AuctionHighBidderIndex, def );
				SendGlobalPopupToAll( "Auction won", $"{winner.PlayerName} won {def.DisplayName} for ${AuctionCurrentBid}.", PopupKind.Success, true, 5f );
				SendTableChatMessage( "Auction won", $"{winner.PlayerName} won {def.DisplayName} for ${AuctionCurrentBid}." );
				ShowNewlyOwnedSetPopups( ownedSetsBeforePurchase, AuctionHighBidderIndex );
				Log.Info( $"{winner.PlayerName} won {def.DisplayName} for ${AuctionCurrentBid}." );
			}
		}
		else if ( def is not null )
		{
			SendTableChatMessage( "Auction ended", $"{def.DisplayName} received no bids." );
			Log.Info( $"Auction for {def.DisplayName} ended with no bids." );
		}

		ClearAuction();
		SetPostActionPhase();
		ResumeTurnTimerAfterAuction();
		TryAutosaveStablePoint( "Auction ended" );
	}

	private void ClearAuction()
	{
		AuctionSpaceIndex = -1;
		AuctionCurrentBid = 0;
		AuctionHighBidderIndex = -1;
		AuctionEndsAt = 0f;
	}

	private void PauseTurnTimerForAuction()
	{
		auctionPausedTurnRemainingSeconds = CurrentTurnEndsAt <= 0f ? 0f : Math.Max( 0f, CurrentTurnEndsAt - Time.Now );
		CurrentTurnEndsAt = 0f;
	}

	private void ResumeTurnTimerAfterAuction()
	{
		if ( CurrentTurnEndsAt <= 0f && auctionPausedTurnRemainingSeconds > 0f && MatchState == MatchLifecycleState.InGame )
			CurrentTurnEndsAt = Time.Now + auctionPausedTurnRemainingSeconds;

		auctionPausedTurnRemainingSeconds = 0f;
	}

	public void PlaceAuctionBid( int bidderIndex, int bidAmount )
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != GamePhase.Auctioning )
			return;

		var bidder = Players.ElementAtOrDefault( bidderIndex );
		var def = Board?.GetSpaceDef( AuctionSpaceIndex );
		if ( bidder is null || !bidder.IsAssigned || bidder.IsBankrupt || def is null )
			return;

		if ( bidderIndex == AuctionHighBidderIndex )
			return;

		if ( bidAmount <= AuctionCurrentBid || bidAmount > bidder.Money )
			return;

		MarkTurnActionAccepted( bidder );
		AuctionCurrentBid = bidAmount;
		AuctionHighBidderIndex = bidderIndex;
		AuctionEndsAt = MathF.Max( AuctionEndsAt, Time.Now + 7f );

		PlayGlobalSound( AuctionBidSound );
		Log.Info( $"{bidder.PlayerName} bid ${AuctionCurrentBid} on {def.DisplayName}." );
	}
}
