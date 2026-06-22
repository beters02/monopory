using System.Threading.Tasks;
using System;
using Sandbox;
using Sandbox.UI;
using Microsoft.VisualBasic;

public class CardActionResult : GameResultKind
{
	protected CardActionResult( string msg ) : base( msg ) {}
}

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
			$"{player.PlayerName} drew {deck}: title=\"{ResolveCardText( card.Title )}\", text=\"{ResolveCardText( card.Description )}\", " +
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
				ResolveCardText( card.Title ),
				ResolveCardText( card.Description ),
				PopupKind.Info,
				true,
				6f
			);
		}
	}

	private string GetCardDisplayText( CardDef card )
	{
		if ( card is null )
			return "";

		var title = ResolveCardText( card.Title );
		var description = ResolveCardText( card.Description );

		if ( string.IsNullOrWhiteSpace( title ) )
			return description ?? "";

		if ( string.IsNullOrWhiteSpace( description ) )
			return title;

		return $"{title}. {description}";
	}

	private string ResolveCardText( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return text ?? "";

		return text
			.Replace( "{pass_go_money}", GetPassGoMoney().ToString() )
			.Replace( "{land_on_go_money}", GetLandOnGoMoney().ToString() )
			.Replace( "{go_landing_money}", GetGoLandingMoney().ToString() );
	}

	private int GetPassGoMoney()
	{
		return Math.Max( Config?.PassGoMoney ?? 200, 0 );
	}

	private int GetLandOnGoMoney()
	{
		return Math.Max( Config?.LandOnGoMoney ?? 200, 0 );
	}

	private int GetGoLandingMoney()
	{
		return GetPassGoMoney() + GetLandOnGoMoney();
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

	public string BuildDeckListText( CardDeck deck )
	{
		var drawPile = deck == CardDeck.Chance
			? chanceDrawPile
			: communityChestDrawPile;

		if ( drawPile.Count == 0 )
			ReshuffleCardDrawPile( deck );

		if ( drawPile.Count == 0 )
			return $"{GetDeckDisplayName( deck )} deck is empty.";

		var lines = drawPile
			.Select( ( card, index ) => $"{index + 1}. {ResolveCardText( card.Title )}: {ResolveCardText( card.Description )}" )
			.ToList();

		return $"{GetDeckDisplayName( deck )} deck ({lines.Count} cards):\n{string.Join( "\n", lines )}";
	}

	public bool TryParseCardDeck( string value, out CardDeck deck )
	{
		deck = CardDeck.Chance;

		if ( string.IsNullOrWhiteSpace( value ) )
			return false;

		var normalized = value.Trim().Replace( "_", "" ).Replace( "-", "" ).Replace( " ", "" ).ToLowerInvariant();
		switch ( normalized )
		{
			case "chance":
				deck = CardDeck.Chance;
				return true;
			case "chest":
			case "community":
			case "communitychest":
				deck = CardDeck.CommunityChest;
				return true;
			default:
				return false;
		}
	}

	private static string GetDeckDisplayName( CardDeck deck )
	{
		return deck == CardDeck.Chance ? "Chance" : "Community Chest";
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

			case CardAction.SwapPlayerPosition:
				HandleSwapPlayerPositionsCard( player );
				return "Swapping player positions";
				
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


	// TODO: polish. need to account for other player potentially having skipped turn etc
	private void HandleSwapPlayerPositionsCard( PlayerState player )
	{
		if ( player is null || Board is null )
			return;

		var allowedPlayers = Players
			.Where( loopPlayer => loopPlayer is not null && loopPlayer != player && loopPlayer.IsAssigned && !loopPlayer.IsBankrupt )
			.ToList();
		if ( allowedPlayers.Count == 0 )
			return;

		var randomInt = Game.Random.Int(allowedPlayers.Count - 1);
		if ( randomInt < 0 )
			return;
		
		PlayerState tradingPlayer = allowedPlayers[randomInt];
		if ( tradingPlayer is null || tradingPlayer == player )
			return;

		SwapPlayerPositions( player, tradingPlayer );
	}

	private void SwapPlayerPositions( PlayerState player, PlayerState swapPlayer )
	{
		if ( player is null || swapPlayer is null || Board is null )
			return;

		var playerStartSpaceIndex = player.SpaceIndex;
		var swapPlayerStartSpaceIndex = swapPlayer.SpaceIndex;

		if ( swapPlayer.IsInJail )
		{
			// release player automatically cancels extra turn for current player which is great
			ReleasePlayerFromJail( swapPlayer );
			SendPlayerToJail( player );
			MovePlayerToCardDestination( swapPlayer, playerStartSpaceIndex, true, false );
			return;
		}

		var playerGoPassCount = GetGoPassCountForAbsoluteMove( playerStartSpaceIndex, swapPlayerStartSpaceIndex, true );
		var swapPlayerGoPassCount = GetGoPassCountForAbsoluteMove( swapPlayerStartSpaceIndex, playerStartSpaceIndex, true );

		player.SpaceIndex = NormalizeSpaceIndex( swapPlayerStartSpaceIndex );
		swapPlayer.SpaceIndex = NormalizeSpaceIndex( playerStartSpaceIndex );
		SnapPlayerTokenToSpace( player );
		SnapPlayerTokenToSpace( swapPlayer );

		ApplyGoMovementPayout( swapPlayer, swapPlayerGoPassCount, swapPlayer.SpaceIndex == (Board?.GoSpaceIndex ?? 0) );
		ResolveLanding( player, playerGoPassCount );
	}
	
	private void MovePlayerToCardDestination( PlayerState player, int targetSpaceIndex, bool collectGo, bool resolveDestination )
	{
		if ( player is null || Board is null || targetSpaceIndex < 0 )
			return;

		var startSpaceIndex = player.SpaceIndex;
		targetSpaceIndex = NormalizeSpaceIndex( targetSpaceIndex );
		var goPassCount = GetGoPassCountForAbsoluteMove( startSpaceIndex, targetSpaceIndex, collectGo );

		player.SpaceIndex = targetSpaceIndex;
		SnapPlayerTokenToSpace( player );

		if ( resolveDestination )
		{
			ResolveLanding( player, goPassCount );
			return;
		}

		ApplyGoMovementPayout( player, goPassCount, targetSpaceIndex == (Board?.GoSpaceIndex ?? 0) );
	}

	private void MovePlayerByCardOffset( PlayerState player, int relativeSpaces, bool collectGo, bool resolveDestination )
	{
		if ( player is null )
			return;

		var startSpaceIndex = player.SpaceIndex;
		var targetSpaceIndex = NormalizeSpaceIndex( player.SpaceIndex + relativeSpaces );
		var goPassCount = GetGoPassCountForRelativeMove( startSpaceIndex, relativeSpaces, collectGo );

		player.SpaceIndex = targetSpaceIndex;
		SnapPlayerTokenToSpace( player );

		if ( resolveDestination )
		{
			ResolveLanding( player, goPassCount );
			return;
		}

		ApplyGoMovementPayout( player, goPassCount, targetSpaceIndex == (Board?.GoSpaceIndex ?? 0) );
	}

	private int GetGoPassCountForAbsoluteMove( int startSpaceIndex, int targetSpaceIndex, bool collectGo )
	{
		if ( !collectGo )
			return 0;

		startSpaceIndex = NormalizeSpaceIndex( startSpaceIndex );
		targetSpaceIndex = NormalizeSpaceIndex( targetSpaceIndex );
		var spaceCount = Math.Max( Board?.SpaceCount ?? BoardCatalog.GetDefaultSpaceCount(), 1 );

		var forwardDistance = targetSpaceIndex >= startSpaceIndex
			? targetSpaceIndex - startSpaceIndex
			: spaceCount - startSpaceIndex + targetSpaceIndex;

		if ( forwardDistance <= 0 )
			return 0;

		return (startSpaceIndex + forwardDistance) / spaceCount;
	}

	private int GetGoPassCountForRelativeMove( int startSpaceIndex, int relativeSpaces, bool collectGo )
	{
		if ( !collectGo || relativeSpaces <= 0 )
			return 0;

		startSpaceIndex = NormalizeSpaceIndex( startSpaceIndex );
		var spaceCount = Math.Max( Board?.SpaceCount ?? BoardCatalog.GetDefaultSpaceCount(), 1 );
		return (startSpaceIndex + relativeSpaces) / spaceCount;
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
