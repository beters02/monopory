using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	[Rpc.Host]
	public void RequestCreateTrade( int receiverPlayerIndex, int senderMoney, int receiverMoney, string senderPropertyIndexes, string receiverPropertyIndexes, string senderCardIds = "", string receiverCardIds = "", bool isNegotiation = false )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var senderPlayerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( senderPlayerIndex < 0 )
			return;

		if ( MaxPendingSentTradesPerPlayer > 0 && CountPendingSentTrades( senderPlayerIndex ) >= MaxPendingSentTradesPerPlayer )
		{
			SendPopupToPlayer(
				senderPlayerIndex,
				"Trade limit reached",
				$"You can only have {MaxPendingSentTradesPerPlayer} sent trade request(s) pending.",
				PopupKind.Warning,
				true,
				4f
			);
			return;
		}

		var request = new TradeRequest
		{
			Id = NextTradeId++,
			SenderPlayerIndex = senderPlayerIndex,
			ReceiverPlayerIndex = receiverPlayerIndex,
			SenderMoney = Math.Max( senderMoney, 0 ),
			ReceiverMoney = Math.Max( receiverMoney, 0 ),
			SenderPropertyIndexes = ParseSpaceIndexList( senderPropertyIndexes ),
			ReceiverPropertyIndexes = ParseSpaceIndexList( receiverPropertyIndexes ),
			SenderCardIds = ParseTradableCardIdList( senderCardIds ),
			ReceiverCardIds = ParseTradableCardIdList( receiverCardIds )
		};

		if ( request.IsEmpty || !IsTradeValid( request ) )
			return;

		PendingTrades[request.Id] = request.Serialize();
		TradeViewers.Remove( request.Id );
		if ( isNegotiation )
			ShowTradeNegotiationReceivedNotification( request );
		else
			ShowTradeReceivedNotification( request );
		Log.Info( $"{Players[senderPlayerIndex].PlayerName} offered a trade to {Players[receiverPlayerIndex].PlayerName}." );
	}

	[Rpc.Host]
	public void RequestEditTrade( int tradeId, int senderMoney, int receiverMoney, string senderPropertyIndexes, string receiverPropertyIndexes, string senderCardIds = "", string receiverCardIds = "" )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		if ( !TryGetTrade( tradeId, out var existingTrade ) )
			return;

		var senderPlayerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( senderPlayerIndex != existingTrade.SenderPlayerIndex )
			return;

		var request = new TradeRequest
		{
			Id = tradeId,
			SenderPlayerIndex = existingTrade.SenderPlayerIndex,
			ReceiverPlayerIndex = existingTrade.ReceiverPlayerIndex,
			SenderMoney = Math.Max( senderMoney, 0 ),
			ReceiverMoney = Math.Max( receiverMoney, 0 ),
			SenderPropertyIndexes = ParseSpaceIndexList( senderPropertyIndexes ),
			ReceiverPropertyIndexes = ParseSpaceIndexList( receiverPropertyIndexes ),
			SenderCardIds = ParseTradableCardIdList( senderCardIds ),
			ReceiverCardIds = ParseTradableCardIdList( receiverCardIds )
		};

		if ( request.IsEmpty || !IsTradeValid( request ) )
			return;

		PendingTrades[tradeId] = request.Serialize();
		TradeViewers.Remove( tradeId );
		ShowTradeNegotiationReceivedNotification( request );
		Log.Info( $"{Players[request.SenderPlayerIndex].PlayerName} edited a trade to {Players[request.ReceiverPlayerIndex].PlayerName}." );
	}

	[Rpc.Host]
	public void RequestAcceptTrade( int tradeId )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		if ( !TryGetTrade( tradeId, out var trade ) )
			return;

		if ( GetPlayerIndexForCaller( Rpc.Caller ) != trade.ReceiverPlayerIndex )
			return;

		if ( !IsTradeValid( trade ) )
		{
			PendingTrades.Remove( tradeId );
			TradeViewers.Remove( tradeId );
			return;
		}

		var sender = Players[trade.SenderPlayerIndex];
		var receiver = Players[trade.ReceiverPlayerIndex];
		var ownedSetsBeforeTrade = CaptureOwnedSetKeys( trade.SenderPlayerIndex, trade.ReceiverPlayerIndex );

		sender.Money -= trade.SenderMoney;
		receiver.Money += trade.SenderMoney;

		receiver.Money -= trade.ReceiverMoney;
		sender.Money += trade.ReceiverMoney;

		foreach ( var spaceIndex in trade.SenderPropertyIndexes )
		{
			PropertyOwners[spaceIndex] = trade.ReceiverPlayerIndex;
			ReportPropertyAcquiredAchievements( trade.ReceiverPlayerIndex, Board?.GetSpaceDef( spaceIndex ) );
		}

		foreach ( var spaceIndex in trade.ReceiverPropertyIndexes )
		{
			PropertyOwners[spaceIndex] = trade.SenderPlayerIndex;
			ReportPropertyAcquiredAchievements( trade.SenderPlayerIndex, Board?.GetSpaceDef( spaceIndex ) );
		}

		foreach ( var cardId in trade.SenderCardIds )
			TransferTradableCard( trade.SenderPlayerIndex, trade.ReceiverPlayerIndex, cardId );

		foreach ( var cardId in trade.ReceiverCardIds )
			TransferTradableCard( trade.ReceiverPlayerIndex, trade.SenderPlayerIndex, cardId );

		TrySettlePendingForcedPaymentForPlayer( trade.SenderPlayerIndex );
		TrySettlePendingForcedPaymentForPlayer( trade.ReceiverPlayerIndex );

		PendingTrades.Remove( tradeId );
		TradeViewers.Remove( tradeId );
		RemoveInvalidTrades();
		ShowTradeAcceptedNotification( sender, receiver );
		ShowTradeAcceptedNotification( receiver, sender );
		ShowNewlyOwnedSetPopups( ownedSetsBeforeTrade, trade.SenderPlayerIndex, trade.ReceiverPlayerIndex );

		Log.Info( $"{receiver.PlayerName} accepted a trade from {sender.PlayerName}." );
		TryAutosaveStablePoint( "Trade accepted" );
	}

	[Rpc.Host]
	public void RequestDenyTrade( int tradeId, bool suppressNotification = false )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		if ( !TryGetTrade( tradeId, out var trade ) )
			return;

		var callerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( callerIndex != trade.ReceiverPlayerIndex && callerIndex != trade.SenderPlayerIndex )
			return;

		if ( !suppressNotification )
			ShowTradeDeniedNotification( trade, callerIndex );
		PendingTrades.Remove( tradeId );
		TradeViewers.Remove( tradeId );
		TryAutosaveStablePoint( "Trade removed" );
	}

	[Rpc.Host]
	public void RequestSetTradeViewing( int tradeId, bool isViewing )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		if ( !TryGetTrade( tradeId, out var trade ) )
		{
			TradeViewers.Remove( tradeId );
			return;
		}

		var viewerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !trade.InvolvesPlayer( viewerIndex ) )
			return;

		var viewers = GetTradeViewerIndexes( tradeId ).ToHashSet();
		if ( isViewing )
			viewers.Add( viewerIndex );
		else
			viewers.Remove( viewerIndex );

		if ( viewers.Count == 0 )
			TradeViewers.Remove( tradeId );
		else
			TradeViewers[tradeId] = string.Join( ",", viewers.OrderBy( index => index ) );
	}

	public List<TradeRequest> GetTrades()
	{
		return PendingTrades
			.Select( entry => TradeRequest.TryDeserialize( entry.Key, entry.Value, out var trade ) ? trade : null )
			.Where( trade => trade is not null )
			.OrderBy( trade => trade.Id )
			.ToList();
	}

	public bool TryGetTrade( int tradeId, out TradeRequest trade )
	{
		trade = null;

		if ( !PendingTrades.TryGetValue( tradeId, out var value ) )
			return false;

		return TradeRequest.TryDeserialize( tradeId, value, out trade );
	}

	public bool IsPlayerViewingTrade( int tradeId, int playerIndex )
	{
		return GetTradeViewerIndexes( tradeId ).Contains( playerIndex );
	}

	public bool IsTradeValid( TradeRequest trade )
	{
		if ( trade is null )
			return false;

		var sender = Players.ElementAtOrDefault( trade.SenderPlayerIndex );
		var receiver = Players.ElementAtOrDefault( trade.ReceiverPlayerIndex );

		if ( sender is null || receiver is null || !sender.IsAssigned || !receiver.IsAssigned || sender.IsBankrupt || receiver.IsBankrupt )
			return false;

		if ( trade.SenderPlayerIndex == trade.ReceiverPlayerIndex )
			return false;

		if ( sender.Money < trade.SenderMoney || receiver.Money < trade.ReceiverMoney )
			return false;

		foreach ( var spaceIndex in trade.SenderPropertyIndexes )
		{
			if ( GetOwnerIndexForSpace( spaceIndex ) != trade.SenderPlayerIndex )
				return false;
		}

		foreach ( var spaceIndex in trade.ReceiverPropertyIndexes )
		{
			if ( GetOwnerIndexForSpace( spaceIndex ) != trade.ReceiverPlayerIndex )
				return false;
		}

		foreach ( var cardId in trade.SenderCardIds )
		{
			if ( !PlayerOwnsTradableCard( trade.SenderPlayerIndex, cardId ) )
				return false;
		}

		foreach ( var cardId in trade.ReceiverCardIds )
		{
			if ( !PlayerOwnsTradableCard( trade.ReceiverPlayerIndex, cardId ) )
				return false;
		}

		return true;
	}

	private void RemoveInvalidTrades()
	{
		foreach ( var trade in GetTrades() )
		{
			if ( !IsTradeValid( trade ) )
			{
				PendingTrades.Remove( trade.Id );
				TradeViewers.Remove( trade.Id );
			}
		}
	}

	private int CountPendingSentTrades( int playerIndex )
	{
		return GetTrades().Count( trade => trade.SenderPlayerIndex == playerIndex );
	}

	private List<int> GetTradeViewerIndexes( int tradeId )
	{
		if ( !TradeViewers.TryGetValue( tradeId, out var value ) || string.IsNullOrWhiteSpace( value ) )
			return new();

		return value.Split( ',', StringSplitOptions.RemoveEmptyEntries )
			.Select( part => int.TryParse( part, out var index ) ? index : -1 )
			.Where( index => index >= 0 )
			.Distinct()
			.OrderBy( index => index )
			.ToList();
	}

	private static List<int> ParseSpaceIndexList( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return new();

		return value.Split( ',', StringSplitOptions.RemoveEmptyEntries )
			.Select( part => int.TryParse( part, out var index ) ? index : -1 )
			.Where( index => index >= 0 )
			.Distinct()
			.OrderBy( index => index )
			.ToList();
	}

	private static List<string> ParseTradableCardIdList( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return new();

		return value.Split( ',', StringSplitOptions.RemoveEmptyEntries )
			.Select( part => part.Trim() )
			.Where( cardId => cardId is TradableCardIds.ChanceGetOutOfJailFree or TradableCardIds.CommunityChestGetOutOfJailFree )
			.Distinct()
			.OrderBy( cardId => cardId )
			.ToList();
	}
}
