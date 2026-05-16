using System;
using Sandbox;

public static class MonopolyCardData
{
	public static List<MonopolyCardDef> CreateChanceCards()
	{
		return new()
		{
			new() { Key = "chance_advance_go", Deck = MonopolyCardDeck.Chance, Title = "Advance to GO", Description = "Collect $200.", Action = MonopolyCardAction.MoveToSpace, TargetSpaceIndex = 0 },
			new() { Key = "chance_advance_illinois", Deck = MonopolyCardDeck.Chance, Title = "Advance to The Co-Op", Description = "Move to The Co-Op. Collect $200 if you pass GO.", Action = MonopolyCardAction.MoveToSpace, TargetSpaceIndex = 26 },
			new() { Key = "chance_advance_st_charles", Deck = MonopolyCardDeck.Chance, Title = "Advance to Fitz' FN FREEHAND", Description = "Move to Fitz' FN FREEHAND. Collect $200 if you pass GO.", Action = MonopolyCardAction.MoveToSpace, TargetSpaceIndex = 13 },
			new() { Key = "chance_advance_boardwalk", Deck = MonopolyCardDeck.Chance, Title = "Advance to The Grand Casino", Description = "Move to The Grand Casino.", Action = MonopolyCardAction.MoveToSpace, TargetSpaceIndex = 39 },
			new() { Key = "chance_advance_railroad", Deck = MonopolyCardDeck.Chance, Title = "Take a ride", Description = "Move to Season Railroad. Collect $200 if you pass GO.", Action = MonopolyCardAction.MoveToSpace, TargetSpaceIndex = 5 },
			new() { Key = "chance_bank_dividend", Deck = MonopolyCardDeck.Chance, Title = "Bank dividend", Description = "Collect $50.", Action = MonopolyCardAction.CollectFromBank, Amount = 50 },
			new() { Key = "chance_back_three", Deck = MonopolyCardDeck.Chance, Title = "Go back 3 spaces", Description = "Move back 3 spaces.", Action = MonopolyCardAction.MoveRelative, RelativeSpaces = -3, CollectGo = false },
			new() { Key = "chance_go_jail", Deck = MonopolyCardDeck.Chance, Title = "Go to Jail", Description = "Go directly to Jail. Do not collect $200.", Action = MonopolyCardAction.GoToJail },
			new() { Key = "chance_repairs", Deck = MonopolyCardDeck.Chance, Title = "Property repairs", Description = "Pay $25 per house and $100 per hotel.", Action = MonopolyCardAction.PayPerImprovement, HouseAmount = 25, HotelAmount = 100 },
			new() { Key = "chance_poor_tax", Deck = MonopolyCardDeck.Chance, Title = "Speeding fine", Description = "Pay $15.", Action = MonopolyCardAction.PayBank, Amount = 15 },
			new() { Key = "chance_chairman", Deck = MonopolyCardDeck.Chance, Title = "Elected chairman", Description = "Pay each player $50.", Action = MonopolyCardAction.PayEachPlayer, Amount = 50 },
			new() { Key = "chance_building_loan", Deck = MonopolyCardDeck.Chance, Title = "Building loan matures", Description = "Collect $150.", Action = MonopolyCardAction.CollectFromBank, Amount = 150 }
		};
	}

	public static List<MonopolyCardDef> CreateCommunityChestCards()
	{
		return new()
		{
			new() { Key = "chest_advance_go", Deck = MonopolyCardDeck.CommunityChest, Title = "Advance to GO", Description = "Collect $200.", Action = MonopolyCardAction.MoveToSpace, TargetSpaceIndex = 0 },
			new() { Key = "chest_bank_error", Deck = MonopolyCardDeck.CommunityChest, Title = "Bank error in your favor", Description = "Collect $200.", Action = MonopolyCardAction.CollectFromBank, Amount = 200 },
			new() { Key = "chest_doctor_fee", Deck = MonopolyCardDeck.CommunityChest, Title = "Doctor's fee", Description = "Pay $50.", Action = MonopolyCardAction.PayBank, Amount = 50 },
			new() { Key = "chest_stock_sale", Deck = MonopolyCardDeck.CommunityChest, Title = "Stock sale", Description = "Collect $50.", Action = MonopolyCardAction.CollectFromBank, Amount = 50 },
			new() { Key = "chest_go_jail", Deck = MonopolyCardDeck.CommunityChest, Title = "Go to Jail", Description = "Go directly to Jail. Do not collect $200.", Action = MonopolyCardAction.GoToJail },
			new() { Key = "chest_holiday_fund", Deck = MonopolyCardDeck.CommunityChest, Title = "Holiday fund matures", Description = "Collect $100.", Action = MonopolyCardAction.CollectFromBank, Amount = 100 },
			new() { Key = "chest_tax_refund", Deck = MonopolyCardDeck.CommunityChest, Title = "Tax refund", Description = "Collect $20.", Action = MonopolyCardAction.CollectFromBank, Amount = 20 },
			new() { Key = "chest_birthday", Deck = MonopolyCardDeck.CommunityChest, Title = "Birthday pool", Description = "Collect $50.", Action = MonopolyCardAction.CollectFromBank, Amount = 50 },
			new() { Key = "chest_life_insurance", Deck = MonopolyCardDeck.CommunityChest, Title = "Life insurance matures", Description = "Collect $100.", Action = MonopolyCardAction.CollectFromBank, Amount = 100 },
			new() { Key = "chest_hospital", Deck = MonopolyCardDeck.CommunityChest, Title = "Hospital fees", Description = "Pay $100.", Action = MonopolyCardAction.PayBank, Amount = 100 },
			new() { Key = "chest_school", Deck = MonopolyCardDeck.CommunityChest, Title = "School fees", Description = "Pay $50.", Action = MonopolyCardAction.PayBank, Amount = 50 },
			new() { Key = "chest_consultancy", Deck = MonopolyCardDeck.CommunityChest, Title = "Consultancy fee", Description = "Collect $25.", Action = MonopolyCardAction.CollectFromBank, Amount = 25 },
			new() { Key = "chest_street_repairs", Deck = MonopolyCardDeck.CommunityChest, Title = "Street repairs", Description = "Pay $40 per house and $115 per hotel.", Action = MonopolyCardAction.PayPerImprovement, HouseAmount = 40, HotelAmount = 115 },
			new() { Key = "chest_beauty_contest", Deck = MonopolyCardDeck.CommunityChest, Title = "Beauty contest", Description = "Collect $10.", Action = MonopolyCardAction.CollectFromBank, Amount = 10 },
			new() { Key = "chest_inherit", Deck = MonopolyCardDeck.CommunityChest, Title = "Inheritance", Description = "Collect $100.", Action = MonopolyCardAction.CollectFromBank, Amount = 100 }
		};
	}
}
