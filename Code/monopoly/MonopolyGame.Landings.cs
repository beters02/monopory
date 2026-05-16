using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	private void ResolveLanding( MonopolyPlayerState player )
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

		switch ( spaceDef.Type )
		{
			case SpaceType.Go:
				player.Money += 200;
				Log.Info( $"{player.PlayerName} collected $200." );
				break;

			case SpaceType.Tax:
				if ( PayBank( player, spaceDef.TaxAmount ) )
					Log.Info( $"{player.PlayerName} paid ${spaceDef.TaxAmount} tax." );
				break;

			case SpaceType.GoToJail:
				SendPlayerToJail( player );
				CompleteTurn();
				Phase = MonopolyGamePhase.WaitingToRoll;
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
				if ( Config?.VacationCash == true )
				{
					CompleteTurn();
					Phase = MonopolyGamePhase.WaitingToRoll;
				}
				break;
		}
	}

	private void ResolvePropertyLanding( MonopolyPlayerState player, SpaceDef def )
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
				Log.Info( $"{player.PlayerName} paid ${rent} rent to {owner.PlayerName}." );
			return;
		}
	}

	private void ResolveUnownedPropertyLanding( MonopolyPlayerState player, SpaceDef def )
	{
		switch ( Config?.LandedUnownedMode ?? UnownedLandingMode.SkipOrAuction )
		{
			case UnownedLandingMode.ForceAuction:
				StartAuction( def.Index );
				return;

			case UnownedLandingMode.ForceBuyIfPossible:
				if ( player.Money >= def.Price )
				{
					BuyUnownedPropertyForPlayer( player, def, CurrentPlayerIndex );
					return;
				}

				Log.Info( $"{player.PlayerName} could not afford {def.DisplayName}." );
				return;

			case UnownedLandingMode.SkipOrAuction:
			default:
				PendingPurchaseSpaceIndex = def.Index;
				Phase = MonopolyGamePhase.WaitingForBuyDecision;

				Log.Info( $"{player.PlayerName} can buy {def.DisplayName} for ${def.Price}." );
				return;
		}
	}

	private void ResolveFreeParkingLanding( MonopolyPlayerState player )
	{
		if ( Config?.VacationCash != true )
		{
			Log.Info( $"{player.PlayerName} landed on Free Parking." );
			return;
		}

		var payout = FreeParkingBank;
		FreeParkingBank = 0;

		if ( payout > 0 )
			player.Money += payout;

		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and skipped their extra roll." );
			return;
		}

		player.SkipsNextTurn = true;
		Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and will skip their next turn." );
	}
}
