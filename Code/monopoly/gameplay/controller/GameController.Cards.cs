using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private void ResolveCardLanding( PlayerState player, CardDeck deck )
	{
		var card = DrawCard( deck );
		if ( player is null || card is null )
			return;

		string cardDisplayText = GetCardDisplayText( card );
		ShowCardForPlayerWhoLanded( player, cardDisplayText );
		SendPopupToAll( card.Title, card.Description, PopupKind.Info, true, 6f );
		currentRollDrewCard = true;
		Log.Info( $"{player.PlayerName} drew {deck}: {card.Title}." );
		ApplyCard( player, card );
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
		var cards = deck == CardDeck.Chance
			? Board?.ChanceCards
			: Board?.CommunityChestCards;

		if ( cards is null || cards.Count == 0 )
			return null;

		return cards[Game.Random.Int( 0, cards.Count - 1 )];
	}

	private void ApplyCard( PlayerState player, CardDef card )
	{
		if ( player is null || card is null )
			return;

		switch ( card.Action )
		{
			case CardAction.CollectFromBank:
				player.Money += Math.Max( card.Amount, 0 );
				TrySettlePendingForcedPaymentForPlayer( GetPlayerIndex( player ) );
				Log.Info( $"{player.PlayerName} collected ${card.Amount} from {card.Title}." );
				break;

			case CardAction.PayBank:
				if ( PayBank( player, card.Amount ) )
					Log.Info( $"{player.PlayerName} paid ${card.Amount} from {card.Title}." );
				break;

			case CardAction.MoveToSpace:
				MovePlayerToCardDestination( player, card.TargetSpaceIndex, card.CollectGo, card.ResolveDestination );
				break;

			case CardAction.MoveRelative:
				MovePlayerByCardOffset( player, card.RelativeSpaces, card.CollectGo, card.ResolveDestination );
				break;

			case CardAction.GoToJail:
				SendPlayerToJail( player );
				CompleteTurn();
				Phase = GamePhase.WaitingToRoll;
				break;

			case CardAction.CollectFromEachPlayer:
				CollectFromEachPlayerForCard( player, card.Amount );
				break;

			case CardAction.PayEachPlayer:
				PayEachPlayerForCard( player, card.Amount );
				break;

			case CardAction.PayPerImprovement:
				PayPerImprovementForCard( player, card.HouseAmount, card.HotelAmount );
				break;
		}
	}

	private void MovePlayerToCardDestination( PlayerState player, int targetSpaceIndex, bool collectGo, bool resolveDestination )
	{
		if ( player is null || Board is null || targetSpaceIndex < 0 )
			return;

		targetSpaceIndex = NormalizeSpaceIndex( targetSpaceIndex );
		var passedGo = collectGo && targetSpaceIndex != 0 && targetSpaceIndex < player.SpaceIndex;

		if ( passedGo )
			player.Money += 200;

		player.SpaceIndex = targetSpaceIndex;

		if ( resolveDestination )
			ResolveLanding( player );
	}

	private void MovePlayerByCardOffset( PlayerState player, int relativeSpaces, bool collectGo, bool resolveDestination )
	{
		if ( player is null )
			return;

		var targetSpaceIndex = NormalizeSpaceIndex( player.SpaceIndex + relativeSpaces );
		var passedGo = collectGo && relativeSpaces > 0 && targetSpaceIndex < player.SpaceIndex;

		if ( passedGo )
			player.Money += 200;

		player.SpaceIndex = targetSpaceIndex;

		if ( resolveDestination )
			ResolveLanding( player );
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

		if ( PayBank( player, amount ) )
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
			BankruptPlayer( playerIndex, null );
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

		foreach ( var payerIndex in GetAssignedPlayerIndexes().Where( index => index != receiverIndex ) )
		{
			var payer = Players.ElementAtOrDefault( payerIndex );
			if ( payer is null || payer.Money < amount )
				continue;

			payer.Money -= amount;
			player.Money += amount;
		}
	}
}
