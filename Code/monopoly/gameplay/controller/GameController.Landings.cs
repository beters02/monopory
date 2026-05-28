using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{
	private enum UnownedLandingAction
	{
		ForceBuy,
		ForceAuction,
		PendingDecision
	}

	private void ResolveLanding( PlayerState player, int goPassCount = 0 )
	{
		if ( player is null || Board is null )
		{
			landingLogger.Error($"FAILED FOR PLAYER/BOARD NULL: IsPlayerNull: {player == null} . IsBoardNull: {Board == null}");
			return;
		}

		landingLogger.Info($"RESOLVING PLAYER LANDING FOR PLAYER {player.PlayerName}");
		landingLogger.Info($"GETTING BOARD SPACE DEF FOR INDEX: {player.SpaceIndex}");
		var spaceDef = Board.GetSpaceDef( player.SpaceIndex );
		landingLogger.Info($"SAVED BOARD SPACE DEF");

		if ( spaceDef is null )
		{
			landingLogger.Error($"FAILED FOR SPACE DEF NULL: SpaceIndex: {player.SpaceIndex}");
			ForceEndGameFromException(new InvalidOperationException($"No board definition for space index {player.SpaceIndex}."));
			return;
		}

		Log.Info( $"{player.PlayerName} landed on {spaceDef.DisplayName}" );

		//if (spaceDef.Type != SpaceType.Go && spaceDef.Type )
		ShowCardForPlayerWhoLanded(player);
		ApplyGoMovementPayout( player, goPassCount, spaceDef.Type == SpaceType.Go );

		switch ( spaceDef.Type )
		{
			case SpaceType.Go:
				break;

			case SpaceType.Tax:
				if ( PayBank( player, spaceDef.TaxAmount, true, BankPaymentSource.TaxSpace ) )
					Log.Info( $"{player.PlayerName} paid ${spaceDef.TaxAmount} tax." );
				break;

			case SpaceType.GoToJail:
				SendPlayerToJail( player );
				MarkResolvedActionToAdvanceImmediately();
				break;

			case SpaceType.Property:
			case SpaceType.Railroad:
			case SpaceType.Utility:
				ResolvePropertyLanding( player, spaceDef );
				break;

			case SpaceType.Chance:
				ResolveCardLanding( player, CardDeck.Chance );
				break;

			case SpaceType.CommunityChest:
				ResolveCardLanding( player, CardDeck.CommunityChest );
				break;

			case SpaceType.Jail:
				Log.Info( $"{player.PlayerName} is just visiting Jail." );
				break;

			case SpaceType.FreeParking:
				ResolveFreeParkingLanding( player );
				break;
		}
	}

	private void ResolvePropertyLanding( PlayerState player, SpaceDef def )
	{
		if (!PropertyOwners.ContainsKey(def.Index))
		{
			ResolveUnownedPropertyLanding( player, def );
			return;
		}

		if ( PropertyOwners.TryGetValue( def.Index, out var ownerIndex ) )
		{
			var owner = Players.ElementAtOrDefault( ownerIndex );

			if ( owner is null || owner == player )
				return;

			if ( Config?.DontCollectRentWhileInPrison == true && owner.IsInJail )
			{
				Log.Info( $"{owner.PlayerName} is in Jail and cannot collect rent from {player.PlayerName}." );
				return;
			}

			var rent = GetRentForSpace( def.Index );
			if ( PayPlayer( player, owner, rent ) )
			{
				PlayRentCutscene( GetPlayerIndex( player ), ownerIndex, rent );
				Log.Info( $"{player.PlayerName} paid ${rent} rent to {owner.PlayerName}." );
			}
			return;
		}
	}

	private void ResolveUnownedPropertyLanding( PlayerState player, SpaceDef def )
	{
		switch ( GetUnownedLandingAction( player, def ) )
		{
			case UnownedLandingAction.ForceBuy:
				BuyUnownedPropertyForPlayer( player, def, CurrentPlayerIndex );
				return;

			case UnownedLandingAction.ForceAuction:
				StartAuction( def.Index );
				return;

			case UnownedLandingAction.PendingDecision:
			default:
				PendingPurchaseSpaceIndex = def.Index;
				Phase = GamePhase.WaitingForBuyDecision;

				Log.Info( GetPendingPropertyDecisionPrompt( def, player ) );
				return;
		}
	}

	private UnownedLandingAction GetUnownedLandingAction( PlayerState player, SpaceDef def )
	{
		var canAfford = player?.Money >= (def?.Price ?? int.MaxValue);
		if ( canAfford )
		{
			return (Config?.LandedUnownedCanAffordMode ?? UnownedAffordableLandingMode.Decision) == UnownedAffordableLandingMode.ForceBuy
				? UnownedLandingAction.ForceBuy
				: UnownedLandingAction.PendingDecision;
		}

		var canSkip = Config?.CanSkipUnowned == true;
		return (Config?.LandedUnownedCantAffordMode == UnownedUnaffordableLandingMode.Decision && canSkip)
			? UnownedLandingAction.PendingDecision
			: UnownedLandingAction.ForceAuction;
	}

	private void ResolveFreeParkingLanding( PlayerState player )
	{
		if ( Config?.VacationCash != true )
		{
			Log.Info( $"{player.PlayerName} landed on Free Parking." );
			return;
		}

		var payout = FreeParkingBank;
		FreeParkingBank = 0;

		if ( payout > 0 )
		{
			player.Money += payout;
			ShowMoneyReceivedPopup( player, payout, "Free Parking" );
		}

		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and skipped their extra roll." );
			return;
		}

		player.SkipsNextTurn = true;
		Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and will skip their next turn." );
	}

	private void ApplyGoMovementPayout( PlayerState player, int goPassCount, bool landedOnGo )
	{
		if ( player is null )
			return;

		goPassCount = Math.Max( goPassCount, 0 );

		var passGoMoney = Math.Max( Config?.PassGoMoney ?? 200, 0 );
		var landingAdditionalMoney = landedOnGo ? Math.Max( Config?.LandOnGoMoney ?? 200, 0 ) : 0;
		var amount = (passGoMoney * goPassCount) + landingAdditionalMoney;

		if ( amount <= 0 )
			return;

		player.Money += amount;
		ShowMoneyReceivedPopup( player, amount, "the bank" );
		Log.Info( $"{player.PlayerName} collected ${amount} for GO movement (passes: {goPassCount}, landed on GO: {landedOnGo})." );
	}
}
