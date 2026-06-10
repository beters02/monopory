using System.Threading.Tasks;
using System;
using Sandbox;
using Sandbox.UI;

public sealed partial class GameController : Component
{
	private readonly List<CardDef> chanceDrawPile = new();
	private readonly List<CardDef> communityChestDrawPile = new();

	private string ResolveCardLanding( PlayerState player, CardDeck deck )
	{
		var card = DrawCard( deck );
		if ( player is null || card is null )
			return card is null ? $"No {deck} card drawn" : "Card landing missing player";

		string cardDisplayText = GetCardDisplayText( card );
		ShowCardForPlayerWhoLanded( player, cardDisplayText );
		SendCardDrawPopupToOtherPlayers( player, card );
		var cardResult = ApplyCard( player, card );
		Log.Info(
			$"{player.PlayerName} drew {deck}: title=\"{card.Title}\", text=\"{card.Description}\", " +
			$"action={card.Action}, result={cardResult}" );
		return $"Drew {deck}: {card.Title}; {cardResult}";
	}

	private void SendCardDrawPopupToOtherPlayers( PlayerState drawingPlayer, CardDef card )
	{
		var drawingPlayerIndex = GetPlayerIndex( drawingPlayer );
		if ( drawingPlayerIndex < 0 || card is null )
			return;

		foreach ( var playerIndex in GetAssignedPlayerIndexes().Where( index => index != drawingPlayerIndex ) )
		{
			SendPopupToPlayer(
				playerIndex,
				card.Title,
				card.Description,
				PopupKind.Info,
				true,
				6f
			);
		}
	}

	private static string GetCardDisplayText( CardDef card )
	{
		if ( card is null )
			return "";

		if ( string.IsNullOrWhiteSpace( card.Title ) )
			return card.Description ?? "";

		if ( string.IsNullOrWhiteSpace( card.Description ) )
			return card.Title;

		return $"{card.Title}. {card.Description}";
	}

	private CardDef DrawCard( CardDeck deck )
	{
		var drawPile = deck == CardDeck.Chance
			? chanceDrawPile
			: communityChestDrawPile;

		if ( drawPile.Count == 0 )
			ReshuffleCardDrawPile( deck );

		if ( drawPile.Count == 0 )
			return null;

		var card = drawPile[0];
		drawPile.RemoveAt( 0 );
		return card;
	}

	private void ResetCardDrawPiles()
	{
		chanceDrawPile.Clear();
		communityChestDrawPile.Clear();
	}

	private void ReshuffleCardDrawPile( CardDeck deck )
	{
		var cards = deck == CardDeck.Chance
			? Board?.ChanceCards
			: Board?.CommunityChestCards;

		if ( cards is null || cards.Count == 0 )
			return;

		var drawPile = deck == CardDeck.Chance
			? chanceDrawPile
			: communityChestDrawPile;

		drawPile.Clear();
		foreach ( var card in cards )
		{
			if ( card is null )
				continue;

			var weight = Math.Max( card.Weight, 1 );
			if ( IsHeldOutOfDeck( card ) )
				weight = Math.Max( weight - 1, 0 );

			for ( var i = 0; i < weight; i++ )
				drawPile.Add( card );
		}

		for ( var i = drawPile.Count - 1; i > 0; i-- )
		{
			var swapIndex = Game.Random.Int( 0, i );
			(drawPile[i], drawPile[swapIndex]) = (drawPile[swapIndex], drawPile[i]);
		}
	}

	private bool IsHeldOutOfDeck( CardDef card )
	{
		if ( card?.Action != CardAction.GetOutOfJailFree )
			return false;

		return card.Deck == CardDeck.Chance
			? Players.Any( player => player?.ChanceGetOutOfJailFreeCards > 0 )
			: Players.Any( player => player?.CommunityChestGetOutOfJailFreeCards > 0 );
	}

	public List<string> GetOwnedTradableCardIds( int playerIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null )
			return new();

		var cardIds = new List<string>();

		if ( player.ChanceGetOutOfJailFreeCards > 0 )
			cardIds.Add( TradableCardIds.ChanceGetOutOfJailFree );

		if ( player.CommunityChestGetOutOfJailFreeCards > 0 )
			cardIds.Add( TradableCardIds.CommunityChestGetOutOfJailFree );

		return cardIds;
	}

	public bool PlayerOwnsTradableCard( int playerIndex, string cardId )
	{
		return GetOwnedTradableCardIds( playerIndex ).Contains( cardId );
	}

	public string GetTradableCardName( string cardId )
	{
		return cardId switch
		{
			TradableCardIds.ChanceGetOutOfJailFree => "Get Out of Jail Free (Chance)",
			TradableCardIds.CommunityChestGetOutOfJailFree => "Get Out of Jail Free (Chest)",
			_ => "Card"
		};
	}

	private bool TransferTradableCard( int fromPlayerIndex, int toPlayerIndex, string cardId )
	{
		var from = Players.ElementAtOrDefault( fromPlayerIndex );
		var to = Players.ElementAtOrDefault( toPlayerIndex );
		if ( from is null || to is null )
			return false;

		switch ( cardId )
		{
			case TradableCardIds.ChanceGetOutOfJailFree when from.ChanceGetOutOfJailFreeCards > 0:
				from.ChanceGetOutOfJailFreeCards--;
				to.ChanceGetOutOfJailFreeCards++;
				return true;

			case TradableCardIds.CommunityChestGetOutOfJailFree when from.CommunityChestGetOutOfJailFreeCards > 0:
				from.CommunityChestGetOutOfJailFreeCards--;
				to.CommunityChestGetOutOfJailFreeCards++;
				return true;

			default:
				return false;
		}
	}

	private void ReturnHeldTradableCardsToDeck( PlayerState player )
	{
		if ( player is null )
			return;

		while ( player.ChanceGetOutOfJailFreeCards > 0 )
		{
			player.ChanceGetOutOfJailFreeCards--;
			ReturnGetOutOfJailFreeCardToDeck( CardDeck.Chance );
		}

		while ( player.CommunityChestGetOutOfJailFreeCards > 0 )
		{
			player.CommunityChestGetOutOfJailFreeCards--;
			ReturnGetOutOfJailFreeCardToDeck( CardDeck.CommunityChest );
		}
	}

	private string ApplyCard( PlayerState player, CardDef card )
	{
		if ( player is null || card is null )
			return "Card not applied";

		switch ( card.Action )
		{
			case CardAction.CollectFromBank:
				var collectedAmount = Math.Max( card.Amount, 0 );
				player.Money += collectedAmount;
				ShowMoneyReceivedPopup( player, collectedAmount, string.IsNullOrWhiteSpace( card.Title ) ? "the bank" : card.Title );
				TrySettlePendingForcedPaymentForPlayer( GetPlayerIndex( player ) );
				Log.Info( $"{player.PlayerName} collected ${card.Amount} from {card.Title}." );
				return $"Collected ${collectedAmount} from bank";

			case CardAction.PayBank:
				if ( PayBank( player, card.Amount, true, BankPaymentSource.ChanceOrCommunityChest ) )
				{
					Log.Info( $"{player.PlayerName} paid ${card.Amount} from {card.Title}." );
					return $"Paid ${card.Amount} to bank";
				}
				return $"Bank payment unresolved for ${card.Amount}";

			case CardAction.MoveToSpace:
				MovePlayerToCardDestination( player, card.TargetSpaceIndex, card.CollectGo, card.ResolveDestination );
				return $"Moved to space {NormalizeSpaceIndex( card.TargetSpaceIndex )}";

			case CardAction.MoveToNearestRailroad:
				var railroadIndex = GetNextSpaceIndexOfType( player.SpaceIndex, SpaceType.Railroad );
				MovePlayerToCardDestination( player, railroadIndex, card.CollectGo, card.ResolveDestination );
				return $"Moved to nearest railroad at space {railroadIndex}";

			case CardAction.MoveToNearestUtility:
				var utilityIndex = GetNextSpaceIndexOfType( player.SpaceIndex, SpaceType.Utility );
				MovePlayerToCardDestination( player, utilityIndex, card.CollectGo, card.ResolveDestination );
				return $"Moved to nearest utility at space {utilityIndex}";

			case CardAction.MoveRelative:
				MovePlayerByCardOffset( player, card.RelativeSpaces, card.CollectGo, card.ResolveDestination );
				return $"Moved {card.RelativeSpaces} spaces";

			case CardAction.GoToJail:
				SendPlayerToJail( player );
				MarkResolvedActionToAdvanceImmediately();
				return "Sent to Jail";

			case CardAction.GetOutOfJailFree:
				if ( card.Deck == CardDeck.Chance )
					player.ChanceGetOutOfJailFreeCards++;
				else
					player.CommunityChestGetOutOfJailFreeCards++;

				Log.Info( $"{player.PlayerName} kept a Get Out of Jail Free card." );
				return $"Kept Get Out of Jail Free card from {card.Deck}";

			case CardAction.CollectFromEachPlayer:
				CollectFromEachPlayerForCard( player, card.Amount );
				return $"Collected up to ${Math.Max( card.Amount, 0 )} from each player";

			case CardAction.PayEachPlayer:
				PayEachPlayerForCard( player, card.Amount );
				return $"Paid or owes ${Math.Max( card.Amount, 0 )} to each player";

			case CardAction.PayPerImprovement:
				PayPerImprovementForCard( player, card.HouseAmount, card.HotelAmount );
				return $"Paid repairs at ${Math.Max( card.HouseAmount, 0 )}/house and ${Math.Max( card.HotelAmount, 0 )}/hotel";

			case CardAction.Gamble:
				_ = PlayGambleCardAsync( player, card );
				return "Started gamble card";
		}

		return $"Unhandled card action {card.Action}";
	}

	private int GetNextSpaceIndexOfType( int startSpaceIndex, SpaceType type )
	{
		if ( Board?.SpaceDefs is null || Board.SpaceDefs.Count == 0 )
			return -1;

		var spaceCount = Board.SpaceDefs.Count;
		startSpaceIndex = NormalizeSpaceIndex( startSpaceIndex );

		for ( var offset = 1; offset <= spaceCount; offset++ )
		{
			var index = NormalizeSpaceIndex( startSpaceIndex + offset );
			var def = Board.SpaceDefs.ElementAtOrDefault( index );
			if ( def?.Type == type )
				return index;
		}

		return -1;
	}

	private void MovePlayerToCardDestination( PlayerState player, int targetSpaceIndex, bool collectGo, bool resolveDestination )
	{
		if ( player is null || Board is null || targetSpaceIndex < 0 )
			return;

		var startSpaceIndex = player.SpaceIndex;
		targetSpaceIndex = NormalizeSpaceIndex( targetSpaceIndex );
		var goPassCount = GetGoPassCountForAbsoluteMove( startSpaceIndex, targetSpaceIndex, collectGo );

		player.SpaceIndex = targetSpaceIndex;

		if ( resolveDestination )
		{
			ResolveLanding( player, goPassCount );
			return;
		}

		ApplyGoMovementPayout( player, goPassCount, targetSpaceIndex == 0 );
	}

	private void MovePlayerByCardOffset( PlayerState player, int relativeSpaces, bool collectGo, bool resolveDestination )
	{
		if ( player is null )
			return;

		var startSpaceIndex = player.SpaceIndex;
		var targetSpaceIndex = NormalizeSpaceIndex( player.SpaceIndex + relativeSpaces );
		var goPassCount = GetGoPassCountForRelativeMove( startSpaceIndex, relativeSpaces, collectGo );

		player.SpaceIndex = targetSpaceIndex;

		if ( resolveDestination )
		{
			ResolveLanding( player, goPassCount );
			return;
		}

		ApplyGoMovementPayout( player, goPassCount, targetSpaceIndex == 0 );
	}

	private static int GetGoPassCountForAbsoluteMove( int startSpaceIndex, int targetSpaceIndex, bool collectGo )
	{
		if ( !collectGo )
			return 0;

		startSpaceIndex = NormalizeSpaceIndex( startSpaceIndex );
		targetSpaceIndex = NormalizeSpaceIndex( targetSpaceIndex );

		var forwardDistance = targetSpaceIndex >= startSpaceIndex
			? targetSpaceIndex - startSpaceIndex
			: 40 - startSpaceIndex + targetSpaceIndex;

		if ( forwardDistance <= 0 )
			return 0;

		return (startSpaceIndex + forwardDistance) / 40;
	}

	private static int GetGoPassCountForRelativeMove( int startSpaceIndex, int relativeSpaces, bool collectGo )
	{
		if ( !collectGo || relativeSpaces <= 0 )
			return 0;

		startSpaceIndex = NormalizeSpaceIndex( startSpaceIndex );
		return (startSpaceIndex + relativeSpaces) / 40;
	}

	private void PayPerImprovementForCard( PlayerState player, int houseAmount, int hotelAmount )
	{
		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return;

		var houses = 0;
		var hotels = 0;
		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			var count = GetImprovementCount( spaceIndex );
			if ( count >= 5 )
				hotels++;
			else
				houses += count;
		}

		var amount = houses * Math.Max( houseAmount, 0 ) + hotels * Math.Max( hotelAmount, 0 );
		if ( amount <= 0 )
		{
			Log.Info( $"{player.PlayerName} had no repair fees." );
			return;
		}

		if ( PayBank( player, amount, true, BankPaymentSource.ChanceOrCommunityChest ) )
			Log.Info( $"{player.PlayerName} paid ${amount} for repairs." );
	}

	private void PayEachPlayerForCard( PlayerState player, int amountPerPlayer )
	{
		var playerIndex = GetPlayerIndex( player );
		var receivers = GetAssignedPlayerIndexes()
			.Where( index => index != playerIndex )
			.ToList();

		var amount = Math.Max( amountPerPlayer, 0 );
		var total = amount * receivers.Count;
		if ( playerIndex < 0 || amount <= 0 || total <= 0 )
			return;

		if ( player.Money >= total )
		{
			CompleteForcedPaymentToEachPlayer( playerIndex, amount );
			return;
		}

		if ( GetPlayerLiquidAssetTotal( playerIndex ) < total )
		{
			BankruptPlayer( playerIndex, null, true );
			return;
		}

		BeginPendingForcedPaymentToEachPlayer( playerIndex, amount );
	}

	private void CollectFromEachPlayerForCard( PlayerState player, int amountPerPlayer )
	{
		var receiverIndex = GetPlayerIndex( player );
		var amount = Math.Max( amountPerPlayer, 0 );
		if ( receiverIndex < 0 || amount <= 0 )
			return;

		var totalCollected = 0;
		foreach ( var payerIndex in GetAssignedPlayerIndexes().Where( index => index != receiverIndex ) )
		{
			var payer = Players.ElementAtOrDefault( payerIndex );
			if ( payer is null || payer.Money < amount )
				continue;

			payer.Money -= amount;
			player.Money += amount;
			totalCollected += amount;
		}

		ShowMoneyReceivedPopup( player, totalCollected, "other players" );
	}

}
