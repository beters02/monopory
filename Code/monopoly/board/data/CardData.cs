using System;
using Sandbox;

public static class CardData
{

	public static bool gambleDebugEnabled = false;

	public static List<CardDef> CreateChanceCards()
	{
		return new()
		{
			new() { Key = "chance_advance_go", Deck = CardDeck.Chance, Title = "Advance to {space_0}", Description = "Collect $200.", Action = CardAction.MoveToSpace, TargetSpaceIndex = 0 },
			new() { Key = "chance_advance_illinois", Deck = CardDeck.Chance, Title = "Advance to {space_26}", Description = "Move to {space_26}. Collect $200 if you pass {space_0}.", Action = CardAction.MoveToSpace, TargetSpaceIndex = 26 },
			new() { Key = "chance_advance_st_charles", Deck = CardDeck.Chance, Title = "Advance to {space_13}", Description = "Move to {space_13}. Collect $200 if you pass {space_0}.", Action = CardAction.MoveToSpace, TargetSpaceIndex = 13 },
			new() { Key = "chance_advance_boardwalk", Deck = CardDeck.Chance, Title = "Advance to {space_39}", Description = "Move to {space_39}.", Action = CardAction.MoveToSpace, TargetSpaceIndex = 39 },
			new() { Key = "chance_advance_railroad", Deck = CardDeck.Chance, Title = "Take a ride", Description = "Move to {space_5}. Collect $200 if you pass {space_0}.", Action = CardAction.MoveToSpace, TargetSpaceIndex = 5 },
			new() { Key = "chance_advance_nearest_railroad", Deck = CardDeck.Chance, Title = "Advance to nearest Railroad", Description = "Move to the nearest Railroad. Collect $200 if you pass {space_0}.", Action = CardAction.MoveToNearestRailroad, Weight = 2 },
			new() { Key = "chance_advance_nearest_utility", Deck = CardDeck.Chance, Title = "Advance to nearest Utility", Description = "Move to the nearest Utility. Collect $200 if you pass {space_0}.", Action = CardAction.MoveToNearestUtility },
			new() { Key = "chance_bank_dividend", Deck = CardDeck.Chance, Title = "Bank dividend", Description = "Collect $50.", Action = CardAction.CollectFromBank, Amount = 50 },
			new() { Key = "chance_back_three", Deck = CardDeck.Chance, Title = "Go back 3 spaces", Description = "Move back 3 spaces.", Action = CardAction.MoveRelative, RelativeSpaces = -3, CollectGo = false },
			new() { Key = "chance_go_jail", Deck = CardDeck.Chance, Title = "Go to {space_10}", Description = "Go directly to {space_10}. Do not collect $200.", Action = CardAction.GoToJail },
			new() { Key = "chance_repairs", Deck = CardDeck.Chance, Title = "Property repairs", Description = "Pay $25 per house and $100 per hotel.", Action = CardAction.PayPerImprovement, HouseAmount = 25, HotelAmount = 100 },
			new() { Key = "chance_poor_tax", Deck = CardDeck.Chance, Title = "Speeding fine", Description = "Pay $15.", Action = CardAction.PayBank, Amount = 15 },
			new() { Key = "chance_chairman", Deck = CardDeck.Chance, Title = "Elected chairman", Description = "Pay each player $50.", Action = CardAction.PayEachPlayer, Amount = 50 },
			new() { Key = "chance_building_loan", Deck = CardDeck.Chance, Title = "Building loan matures", Description = "Collect $150.", Action = CardAction.CollectFromBank, Amount = 150 },
			new() { Key = "chance_get_out_jail", Deck = CardDeck.Chance, Title = "Get Out of {space_10} Free", Description = "Keep this card until you need to leave {space_10}.", Action = CardAction.GetOutOfJailFree },
			new() { Key = "chance_gamble_coin_flip", Deck = CardDeck.Chance, Title = "Gamble Card", Description = "You're forced to play in a coinflip for a random amount.", Action = CardAction.Gamble, Amount = 150, Weight = 2 }
		};
	}

	public static List<CardDef> CreateCommunityChestCards()
	{
		return new()
		{
			new() { Key = "chest_advance_go", Deck = CardDeck.CommunityChest, Title = "Advance to {space_0}", Description = "Collect $200.", Action = CardAction.MoveToSpace, TargetSpaceIndex = 0 },
			new() { Key = "chest_bank_error", Deck = CardDeck.CommunityChest, Title = "Bank error in your favor", Description = "Collect $200.", Action = CardAction.CollectFromBank, Amount = 200 },
			new() { Key = "chest_doctor_fee", Deck = CardDeck.CommunityChest, Title = "Doctor's fee", Description = "Pay $50.", Action = CardAction.PayBank, Amount = 50 },
			new() { Key = "chest_stock_sale", Deck = CardDeck.CommunityChest, Title = "Stock sale", Description = "Collect $50.", Action = CardAction.CollectFromBank, Amount = 50 },
			new() { Key = "chest_get_out_jail", Deck = CardDeck.CommunityChest, Title = "Get Out of {space_10} Free", Description = "Keep this card until you need to leave {space_10}.", Action = CardAction.GetOutOfJailFree },
			new() { Key = "chest_go_jail", Deck = CardDeck.CommunityChest, Title = "Go to {space_10}", Description = "Go directly to {space_10}. Do not collect $200.", Action = CardAction.GoToJail },
			new() { Key = "chest_holiday_fund", Deck = CardDeck.CommunityChest, Title = "Holiday fund matures", Description = "Collect $100.", Action = CardAction.CollectFromBank, Amount = 100 },
			new() { Key = "chest_tax_refund", Deck = CardDeck.CommunityChest, Title = "Tax refund", Description = "Collect $20.", Action = CardAction.CollectFromBank, Amount = 20 },
			new() { Key = "chest_birthday", Deck = CardDeck.CommunityChest, Title = "Birthday pool", Description = "Collect $10 from each player.", Action = CardAction.CollectFromEachPlayer, Amount = 10 },
			new() { Key = "chest_life_insurance", Deck = CardDeck.CommunityChest, Title = "Life insurance matures", Description = "Collect $100.", Action = CardAction.CollectFromBank, Amount = 100 },
			new() { Key = "chest_hospital", Deck = CardDeck.CommunityChest, Title = "Hospital fees", Description = "Pay $100.", Action = CardAction.PayBank, Amount = 100 },
			new() { Key = "chest_school", Deck = CardDeck.CommunityChest, Title = "School fees", Description = "Pay $50.", Action = CardAction.PayBank, Amount = 50 },
			new() { Key = "chest_consultancy", Deck = CardDeck.CommunityChest, Title = "Consultancy fee", Description = "Collect $25.", Action = CardAction.CollectFromBank, Amount = 25 },
			new() { Key = "chest_street_repairs", Deck = CardDeck.CommunityChest, Title = "Street repairs", Description = "Pay $40 per house and $115 per hotel.", Action = CardAction.PayPerImprovement, HouseAmount = 40, HotelAmount = 115 },
			new() { Key = "chest_beauty_contest", Deck = CardDeck.CommunityChest, Title = "Beauty contest", Description = "Collect $10.", Action = CardAction.CollectFromBank, Amount = 10 },
			new() { Key = "chest_inherit", Deck = CardDeck.CommunityChest, Title = "Inheritance", Description = "Collect $100.", Action = CardAction.CollectFromBank, Amount = 100 },
			new() { Key = "chance_gamble_coin_flip", Deck = CardDeck.CommunityChest, Title = "Gamble Card", Description = "You're forced to play in a coinflip for a random amount.", Action = CardAction.Gamble, Amount = 150, Weight = 2 }
		};
	}

	// 
	public static List<CardDef> GambleDebugChanceCards()
	{
		return new()
		{
			new() { Key = "chance_gamble_coin_flip", Deck = CardDeck.Chance, Title = "Gamble Card", Description = "You're forced to play in a coinflip for a random amount.", Action = CardAction.Gamble, Amount = 150 }
		};
	}

	public static List<CardDef> GambleDebugChestCards()
	{
		return new()
		{
			new() { Key = "chance_gamble_coin_flip", Deck = CardDeck.CommunityChest, Title = "Gamble Card", Description = "You're forced to play in a coinflip for a random amount.", Action = CardAction.Gamble, Amount = 150 }
		};
	}
}
