using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;
using Sandbox.UI;

public sealed partial class GameController : Component
{

	public class GambleResult
	{
		public bool CouldAfford;
		public bool Won;
		public String Message;
		public int AmountSpent;
		public int AmountWon;
		public PopupKind PopupKind;
	}

	private void ResolveCardLanding( PlayerState player, CardDeck deck )
	{
		var card = DrawCard( deck );
		if ( player is null || card is null )
			return;

		string cardDisplayText = GetCardDisplayText( card );
		ShowCardForPlayerWhoLanded( player, cardDisplayText );
		SendGlobalPopupToAll( card.Title, card.Description, PopupKind.Info, true, 6f );
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
				var collectedAmount = Math.Max( card.Amount, 0 );
				player.Money += collectedAmount;
				ShowMoneyReceivedPopup( player, collectedAmount, string.IsNullOrWhiteSpace( card.Title ) ? "the bank" : card.Title );
				TrySettlePendingForcedPaymentForPlayer( GetPlayerIndex( player ) );
				Log.Info( $"{player.PlayerName} collected ${card.Amount} from {card.Title}." );
				break;

			case CardAction.PayBank:
				if ( PayBank( player, card.Amount, true, BankPaymentSource.ChanceOrCommunityChest ) )
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
				MarkResolvedActionToAdvanceImmediately();
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

			case CardAction.Gamble:
				PlayerGamble( player );
				break;
		}
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

	private void PlayerGamble( PlayerState player )
	{

		// get random gamble type
		GambleResult result = PlayerGambleCoinFlip( player );

		TryChangeMoneyForPlayer( player, result.AmountSpent * -1, out string _ );
		TryChangeMoneyForPlayer( player, result.AmountWon * 2, out string _ );

		SendPopupToPlayer(
				player,
				"Gamble Card",
				result.Message,
				result.PopupKind
			);
	}
	
	private GambleResult PlayerGambleCoinFlip( PlayerState player )
	{

		GambleResult result = new();

		// bet amount
		int betAmnt = Game.Random.Int(50, 100);

		if (player.Money < betAmnt)
		{
			result.Message = "You were forced to make a gambling bet, but you could not afford it.";
			result.PopupKind = PopupKind.Danger;
			result.AmountSpent = 0;
			result.AmountWon = 0;
			result.CouldAfford = false;
			result.Won = false;
			return result;
		}

		// did win
		int randomInt = Game.Random.Int(0, 1);

		if ( randomInt == 0 )
		{
			result.Message = $"You lost the ${betAmnt} bet!";
			result.PopupKind = PopupKind.Danger;
			result.AmountSpent = betAmnt;
			result.AmountWon = 0;
			result.CouldAfford = true;
			result.Won = false;
			return result;
		}

		result.Message = $"You won the ${betAmnt} bet!";
		result.PopupKind = PopupKind.Success;
		result.AmountSpent = betAmnt;
		result.AmountWon = betAmnt;
		result.CouldAfford = true;
		result.Won = true;
		return result;
	}
}
