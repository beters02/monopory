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
		var startingMoney = player?.Money ?? 0;
		var startingSpaceIndex = player?.SpaceIndex ?? -1;
		var startingPhase = Phase;
		SpaceDef spaceDef = null;
		var result = "Not resolved";

		try
		{
			if ( player is null || Board is null )
			{
				result = $"Failed: player or board missing (player null: {player is null}, board null: {Board is null})";
				landingLogger.Error($"FAILED FOR PLAYER/BOARD NULL: IsPlayerNull: {player == null} . IsBoardNull: {Board == null}");
				return;
			}

			landingLogger.Info($"RESOLVING PLAYER LANDING FOR PLAYER {player.PlayerName}");
			landingLogger.Info($"GETTING BOARD SPACE DEF FOR INDEX: {player.SpaceIndex}");
			spaceDef = Board.GetSpaceDef( player.SpaceIndex );
			landingLogger.Info($"SAVED BOARD SPACE DEF");

			if ( spaceDef is null )
			{
				result = $"Failed: no board definition for space index {player.SpaceIndex}";
				landingLogger.Error($"FAILED FOR SPACE DEF NULL: SpaceIndex: {player.SpaceIndex}");
				ForceEndGameFromException(new InvalidOperationException($"No board definition for space index {player.SpaceIndex}."));
				return;
			}

			Log.Info( $"{player.PlayerName} landed on {spaceDef.DisplayName}" );
			RecordPropertyLandingForStats( player, spaceDef );
			Board.VisualStateController?.NotifyPlayerLanded( player );

			//if (spaceDef.Type != SpaceType.Go && spaceDef.Type )
			ShowCardForPlayerWhoLanded(player);
			ApplyGoMovementPayout( player, goPassCount, spaceDef.Type == SpaceType.Go );

			switch ( spaceDef.Type )
			{
				case SpaceType.Go:
					result = "Resolved GO";
					break;

				case SpaceType.Tax:

					if ( spaceDef.Key == "tax_income")
						PlayGlobalSound(GameAssets.Sounds.Pluh);

					if ( PayBank( player, spaceDef.TaxAmount, true, BankPaymentSource.TaxSpace ) )
					{
						result = $"Paid ${spaceDef.TaxAmount} tax";
						Log.Info( $"{player.PlayerName} paid ${spaceDef.TaxAmount} tax." );
					}
					else
					{
						result = $"Tax payment unresolved for ${spaceDef.TaxAmount}";
					}
					break;

				case SpaceType.GoToJail:
					SendPlayerToJail( player );
					MarkResolvedActionToAdvanceImmediately();
					result = "Sent to Jail";
					break;

				case SpaceType.Property:
				case SpaceType.Railroad:
				case SpaceType.Utility:
					result = ResolvePropertyLanding( player, spaceDef );
					break;

				case SpaceType.Chance:
					result = ResolveCardLanding( player, CardDeck.Chance );
					break;

				case SpaceType.CommunityChest:
					result = ResolveCardLanding( player, CardDeck.CommunityChest );
					break;

				case SpaceType.Jail:
					result = "Just visiting Jail";
					Log.Info( $"{player.PlayerName} is just visiting Jail." );
					break;

				case SpaceType.FreeParking:
					result = ResolveFreeParkingLanding( player );
					break;
			}
		}
		finally
		{
			LogResolveLandingResult( player, spaceDef, result, startingSpaceIndex, startingMoney, startingPhase, goPassCount );
		}
	}

	private string ResolvePropertyLanding( PlayerState player, SpaceDef def )
	{
		if (!PropertyOwners.ContainsKey(def.Index))
		{
			return ResolveUnownedPropertyLanding( player, def );
		}

		if ( PropertyOwners.TryGetValue( def.Index, out var ownerIndex ) )
		{
			var owner = Players.ElementAtOrDefault( ownerIndex );

			if ( owner is null || owner == player )
				return owner is null ? "Property owner missing" : "Landed on own property";

			if ( Config?.DontCollectRentWhileInPrison == true && owner.IsInJail )
			{
				Log.Info( $"{owner.PlayerName} is in Jail and cannot collect rent from {player.PlayerName}." );
				return $"{owner.PlayerName} is in Jail; rent skipped";
			}

			var rent = GetRentForSpace( def.Index );
			if ( PayPlayer( player, owner, rent ) )
			{
				RecordPropertyRentEarnedForStats( ownerIndex, def.Index, rent );
				Log.Info( $"{player.PlayerName} paid ${rent} rent to {owner.PlayerName}." );
				return $"Paid ${rent} rent to {owner.PlayerName}";
			}

			return $"Rent payment unresolved for ${rent} to {owner.PlayerName}";
		}

		return "Property ownership lookup failed";
	}

	private string ResolveUnownedPropertyLanding( PlayerState player, SpaceDef def )
	{
		switch ( GetUnownedLandingAction( player, def ) )
		{
			case UnownedLandingAction.ForceBuy:
				BuyUnownedPropertyForPlayer( player, def, CurrentPlayerIndex );
				return $"Force bought {def.DisplayName}";

			case UnownedLandingAction.ForceAuction:
				StartAuction( def.Index );
				return $"Started auction for {def.DisplayName}";

			case UnownedLandingAction.PendingDecision:
			default:
				PendingPurchaseSpaceIndex = def.Index;
				Phase = GamePhase.WaitingForBuyDecision;

				Log.Info( GetPendingPropertyDecisionPrompt( def, player ) );
				return $"Waiting for buy decision on {def.DisplayName}";
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

	private string ResolveFreeParkingLanding( PlayerState player )
	{
		if ( Config?.VacationCash != true )
		{
			Log.Info( $"{player.PlayerName} landed on Free Parking." );
			return "Landed on Free Parking";
		}

		EnsureVacationCashMinimum();
		var payout = FreeParkingBank;
		ResetVacationCashBankToMinimum();

		if ( payout > 0 )
		{
			player.Money += payout;
			ShowMoneyReceivedPopup( player, payout, "Free Parking" );
		}

		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			player.IsReturningFromVacationCashBreak = true;
			Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and skipped their extra roll." );
			return $"Collected ${payout} from Free Parking; extra roll skipped";
		}

		player.SkipsNextTurn = true;
		player.IsReturningFromVacationCashBreak = true;
		Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and will skip their next turn." );
		return $"Collected ${payout} from Free Parking; next turn skipped";
	}

	private void LogResolveLandingResult(
		PlayerState player,
		SpaceDef spaceDef,
		string result,
		int startingSpaceIndex,
		int startingMoney,
		GamePhase startingPhase,
		int goPassCount )
	{
		var playerName = player?.PlayerName ?? "null";
		var endingSpaceIndex = player?.SpaceIndex ?? -1;
		var endingMoney = player?.Money ?? 0;
		var spaceName = spaceDef?.DisplayName ?? "unknown";
		var spaceType = spaceDef?.Type.ToString() ?? "unknown";

		Log.Info(
			$"ResolveLanding result: player={playerName}, space={spaceName} ({spaceType}), " +
			$"result={result}, spaceIndex={startingSpaceIndex}->{endingSpaceIndex}, " +
			$"money=${startingMoney}->${endingMoney}, goPassCount={goPassCount}, " +
			$"phase={startingPhase}->{Phase}, pendingPurchase={PendingPurchaseSpaceIndex}, " +
			$"auctionSpace={AuctionSpaceIndex}, bankrupt={player?.IsBankrupt == true}" );
	}

	private void ApplyGoMovementPayout( PlayerState player, int goPassCount, bool landedOnGo )
	{
		if ( player is null )
			return;

		goPassCount = Math.Max( goPassCount, 0 );

		var passGoMoney = GetPassGoMoney();
		var landingAdditionalMoney = landedOnGo ? GetLandOnGoMoney() : 0;
		var amount = (passGoMoney * goPassCount) + landingAdditionalMoney;

		if ( amount <= 0 )
			return;

		player.Money += amount;
		ShowMoneyReceivedPopup( player, amount, "the bank" );
		Log.Info( $"{player.PlayerName} collected ${amount} for GO movement (passes: {goPassCount}, landed on GO: {landedOnGo})." );
	}

	private void RecordPropertyLandingForStats( PlayerState player, SpaceDef def )
	{
		if ( player is null || def is null || !IsPurchasableSpace( def ) )
			return;

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return;

		IncrementPropertyStat( PropertyLandingCounts, BuildPropertyStatKey( playerIndex, def.Index ), 1 );
	}

	private void RecordPropertyRentEarnedForStats( int ownerIndex, int spaceIndex, int amount )
	{
		if ( ownerIndex < 0 || spaceIndex < 0 || amount <= 0 )
			return;

		IncrementPropertyStat( PropertyRentEarned, BuildPropertyStatKey( ownerIndex, spaceIndex ), amount );
	}

	private static int BuildPropertyStatKey( int playerIndex, int spaceIndex )
	{
		return (playerIndex * 1000) + spaceIndex;
	}

	private static void IncrementPropertyStat( NetDictionary<int, int> stats, int key, int amount )
	{
		stats[key] = stats.TryGetValue( key, out var current ) ? current + amount : amount;
	}
}
