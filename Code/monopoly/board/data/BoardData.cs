using System;
using Sandbox;

public static class BoardData
{
	private static readonly RailroadDef defaultRailroadDefs = new()
	{
		Price = 200,
		OneOwnedRent = 25,
		TwoOwnedRent = 50,
		ThreeOwnedRent = 100,
		FourOwnedRent = 200
	};

	private static readonly UtilityDef defaultUtilityDefs = new()
	{
		Price = 150,
		OneOwnedMultiplier = 4,
		BothOwnedMultiplier = 10
	};

	public static List<SpaceDef> CreateSpaceDefs()
	{
		return new()
		{
			new()
			{
				Index = 0,
				Key = "go",
				DisplayName = "GO",
				Type = SpaceType.Go
			},
			new()
			{
				Index = 1,
				Key = "property_brown_0",
				DisplayName = "de_nuke", // Harvey Milk Blvd
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				OneHouseRent = 10,
				TwoHouseRent = 30,
				ThreeHouseRent = 90,
				FourHouseRent = 160,
				HotelRent = 250,
				ColorGroup = ColorGroup.Brown
			},
			new()
			{
				Index = 2,
				Key = "chest_0",
				DisplayName = "Community Chest",
				Type = SpaceType.CommunityChest,
			},
			new()
			{
				Index = 3,
				Key = "property_brown_1", // Chiraq
				DisplayName = "de_miraq",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 4,
				OneHouseRent = 20,
				TwoHouseRent = 60,
				ThreeHouseRent = 180,
				FourHouseRent = 320,
				HotelRent = 450,
				ColorGroup = ColorGroup.Brown
			},
			new()
			{
				Index = 4,
				Key = "tax_income",
				DisplayName = "Plug\nTax",
				Type = SpaceType.Tax,
				TaxAmount = 200
			},
			new()
			{
				Index = 5,
				Key = "railroad_0",
				DisplayName = "Season Railroad",
				Type = SpaceType.Railroad,
				Price = 200,
				BaseRent = 100
			},
			new()
			{
				Index = 6,
				Key = "property_light_blue_0",
				DisplayName = "CSGO\nWild\n.com", // CSGO Blackjack
				Type = SpaceType.Property,
				Price = 100,
				BaseRent = 6,
				OneHouseRent = 30,
				TwoHouseRent = 90,
				ThreeHouseRent = 270,
				FourHouseRent = 400,
				HotelRent = 550,
				ColorGroup = ColorGroup.LightBlue
			},
			new()
			{
				Index = 7,
				Key = "chance_0",
				DisplayName = "Chance",
				Type = SpaceType.Chance
			},
			new()
			{
				Index = 8,
				Key = "property_light_blue_1",
				DisplayName = "CSGO\nRoll\n.com",
				Type = SpaceType.Property,
				Price = 100,
				BaseRent = 6,
				OneHouseRent = 30,
				TwoHouseRent = 90,
				ThreeHouseRent = 270,
				FourHouseRent = 400,
				HotelRent = 550,
				ColorGroup = ColorGroup.LightBlue
			},
			new()
			{
				Index = 9,
				Key = "property_light_blue_2",
				DisplayName = "CSGO\nBlackjack\n.com", // wild
				Type = SpaceType.Property,
				Price = 120,
				BaseRent = 8,
				OneHouseRent = 40,
				TwoHouseRent = 100,
				ThreeHouseRent = 300,
				FourHouseRent = 450,
				HotelRent = 600,
				ColorGroup = ColorGroup.LightBlue
			},
			new()
			{
				Index = 10,
				Key = "jail",
				DisplayName = "Jail",
				Type = SpaceType.Jail
			},
			new()
			{
				Index = 11,
				Key = "property_pink_0",
				DisplayName = "The Liqo Sto",
				Type = SpaceType.Property,
				Price = 140,
				BaseRent = 10,
				OneHouseRent = 50,
				TwoHouseRent = 150,
				ThreeHouseRent = 450,
				FourHouseRent = 625,
				HotelRent = 750,
				ColorGroup = ColorGroup.Pink
			},
			new()
			{
				Index = 12,
				Key = "utility_0",
				DisplayName = "Kickapoo Casino",
				Type = SpaceType.Utility,
				Price = 60,
				BaseRent = 2,
			},
			new()
			{
				Index = 13,
				Key = "property_pink_1",
				DisplayName = "Section 80",
				Type = SpaceType.Property,
				Price = 140,
				BaseRent = 10,
				OneHouseRent = 50,
				TwoHouseRent = 150,
				ThreeHouseRent = 450,
				FourHouseRent = 625,
				HotelRent = 750,
				ColorGroup = ColorGroup.Pink
			},
			new()
			{
				Index = 14,
				Key = "property_pink_2",
				DisplayName = "Yodie-Land",
				Type = SpaceType.Property,
				Price = 160,
				BaseRent = 12,
				OneHouseRent = 60,
				TwoHouseRent = 180,
				ThreeHouseRent = 500,
				FourHouseRent = 700,
				HotelRent = 900,
				ColorGroup = ColorGroup.Pink
			},
			new()
			{
				Index = 15,
				Key = "railroad_1",
				DisplayName = "Train Railroad",
				Type = SpaceType.Railroad,
				Price = 200,
				BaseRent = 2
			},
			new()
			{
				Index = 16,
				Key = "property_orange_0",
				DisplayName = "Brycen's Goon Cave",
				Type = SpaceType.Property,
				Price = 180,
				BaseRent = 14,
				OneHouseRent = 70,
				TwoHouseRent = 200,
				ThreeHouseRent = 550,
				FourHouseRent = 750,
				HotelRent = 950,
				ColorGroup = ColorGroup.Orange
			},
			new()
			{
				Index = 17,
				Key = "chest_1",
				DisplayName = "Community Chest",
				Type = SpaceType.CommunityChest
			},
			new()
			{
				Index = 18,
				Key = "property_orange_1",
				DisplayName = "Fitz' FN FREEHAND",
				Type = SpaceType.Property,
				Price = 180,
				BaseRent = 14,
				OneHouseRent = 70,
				TwoHouseRent = 200,
				ThreeHouseRent = 550,
				FourHouseRent = 750,
				HotelRent = 950,
				ColorGroup = ColorGroup.Orange
			},
			new()
			{
				Index = 19,
				Key = "property_orange_2",
				DisplayName = "Bryce's Skunky Dungeon", 
				Type = SpaceType.Property,
				Price = 200,
				BaseRent = 16,
				OneHouseRent = 80,
				TwoHouseRent = 220,
				ThreeHouseRent = 600,
				FourHouseRent = 800,
				HotelRent = 1000,
				ColorGroup = ColorGroup.Orange
			},
			new()
			{
				Index = 20,
				Key = "free_parking",
				DisplayName = "Free Parking",
				Type = SpaceType.FreeParking
			},
			new()
			{
				Index = 21,
				Key = "property_red_0",
				DisplayName = "Landon's Room",
				Type = SpaceType.Property,
				Price = 220,
				BaseRent = 18,
				OneHouseRent = 90,
				TwoHouseRent = 250,
				ThreeHouseRent = 700,
				FourHouseRent = 875,
				HotelRent = 1050,
				ColorGroup = ColorGroup.Red
			},
			new()
			{
				Index = 22,
				Key = "chance_1",
				DisplayName = "Chance",
				Type = SpaceType.Chance
			},
			new()
			{
				Index = 23,
				Key = "property_red_1",
				DisplayName = "Caden's Cockhouse",
				Type = SpaceType.Property,
				Price = 220,
				BaseRent = 18,
				OneHouseRent = 90,
				TwoHouseRent = 250,
				ThreeHouseRent = 700,
				FourHouseRent = 875,
				HotelRent = 1050,
				ColorGroup = ColorGroup.Red
			},
			new()
			{
				Index = 24,
				Key = "property_red_2",
				DisplayName = "Luke's Law",
				Type = SpaceType.Property,
				Price = 240,
				BaseRent = 20,
				OneHouseRent = 100,
				TwoHouseRent = 300,
				ThreeHouseRent = 750,
				FourHouseRent = 925,
				HotelRent = 1100,
				ColorGroup = ColorGroup.Red
			},
			new()
			{
				Index = 25,
				Key = "railroad_2",
				DisplayName = "Cobblestone Railroad",
				Type = SpaceType.Railroad,
				Price = 200,
				BaseRent = 2
			},
			new()
			{ // Quinn's
				Index = 26,
				Key = "property_yellow_0",
				DisplayName = "LeBron's Room",
				Type = SpaceType.Property,
				Price = 260,
				BaseRent = 22,
				OneHouseRent = 110,
				TwoHouseRent = 330,
				ThreeHouseRent = 800,
				FourHouseRent = 975,
				HotelRent = 1150,
				ColorGroup = ColorGroup.Yellow
			},
			new()
			{
				Index = 27,
				Key = "property_yellow_1",
				DisplayName = "Ethan's Dirty Den",
				Type = SpaceType.Property,
				Price = 260,
				BaseRent = 22,
				OneHouseRent = 110,
				TwoHouseRent = 330,
				ThreeHouseRent = 800,
				FourHouseRent = 975,
				HotelRent = 1150,
				ColorGroup = ColorGroup.Yellow
			},
			new()
			{
				Index = 28,
				Key = "utility_1",
				DisplayName = "Riverwind Casino",
				Type = SpaceType.Utility,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 29,
				Key = "property_yellow_2",
				DisplayName = "The Co-Op", // Shrine Auditorium
				Type = SpaceType.Property,
				Price = 280,
				BaseRent = 24,
				OneHouseRent = 120,
				TwoHouseRent = 360,
				ThreeHouseRent = 850,
				FourHouseRent = 1025,
				HotelRent = 1200,
				ColorGroup = ColorGroup.Yellow
			},
			new()
			{
				Index = 30,
				Key = "go_to_jail",
				DisplayName = "Go to Jail",
				Type = SpaceType.GoToJail
			},
			new()
			{
				Index = 31,
				Key = "property_green_0",
				DisplayName = "Chance",
				Type = SpaceType.Property,
				Price = 300,
				BaseRent = 26,
				OneHouseRent = 130,
				TwoHouseRent = 390,
				ThreeHouseRent = 900,
				FourHouseRent = 1105,
				HotelRent = 1275,
				ColorGroup = ColorGroup.Green
			},
			new()
			{ // Troops
				Index = 32,
				Key = "property_green_1",
				DisplayName = "House\nMade Of\nVelos",
				Type = SpaceType.Property,
				Price = 300,
				BaseRent = 26,
				OneHouseRent = 130,
				TwoHouseRent = 390,
				ThreeHouseRent = 900,
				FourHouseRent = 1105,
				HotelRent = 1275,
				ColorGroup = ColorGroup.Green
			},
			new()
			{
				Index = 33,
				Key = "chest_2",
				DisplayName = "Community Chest",
				Type = SpaceType.CommunityChest
			},
			new()
			{
				Index = 34,
				Key = "property_green_2",
				DisplayName = "Blayze's Giant Balls",
				Type = SpaceType.Property,
				Price = 320,
				BaseRent = 28,
				OneHouseRent = 150,
				TwoHouseRent = 450,
				ThreeHouseRent = 1000,
				FourHouseRent = 1200,
				HotelRent = 1400,
				ColorGroup = ColorGroup.Green
			},
			new()
			{
				Index = 35,
				Key = "railroad_3",
				DisplayName = "Cache Railroad",
				Type = SpaceType.Railroad,
				Price = 200,
				BaseRent = 2
			},
			new()
			{
				Index = 36,
				Key = "chance_2",
				DisplayName = "Chance",
				Type = SpaceType.Chance
			},
			new()
			{
				Index = 37,
				Key = "property_dark_blue_0",
				DisplayName = "The Mos Eisley Cantina",
				Type = SpaceType.Property,
				Price = 350,
				BaseRent = 35,
				OneHouseRent = 175,
				TwoHouseRent = 500,
				ThreeHouseRent = 1100,
				FourHouseRent = 1300,
				HotelRent = 1500,
				ColorGroup = ColorGroup.DarkBlue
			},
			new()
			{
				Index = 38,
				Key = "tax_luxury",
				DisplayName = "Bag Tax",
				Type = SpaceType.Tax,
				TaxAmount = 200
			},
			new()
			{
				Index = 39,
				Key = "property_dark_blue_1",
				DisplayName = "The Grand Casino",
				Type = SpaceType.Property,
				Price = 400,
				BaseRent = 50,
				OneHouseRent = 200,
				TwoHouseRent = 600,
				ThreeHouseRent = 1400,
				FourHouseRent = 1700,
				HotelRent = 2000,
				ColorGroup = ColorGroup.DarkBlue
			},
		};
	}

	public static RailroadDef CreateRailroadDefs()
	{
		return new()
		{
			Price = defaultRailroadDefs.Price,
			OneOwnedRent = defaultRailroadDefs.OneOwnedRent,
			TwoOwnedRent = defaultRailroadDefs.TwoOwnedRent,
			ThreeOwnedRent = defaultRailroadDefs.ThreeOwnedRent,
			FourOwnedRent = defaultRailroadDefs.FourOwnedRent
		};
	}

	public static UtilityDef CreateUtilityDefs()
	{
		return new()
		{
			Price = defaultUtilityDefs.Price,
			OneOwnedMultiplier = defaultUtilityDefs.OneOwnedMultiplier,
			BothOwnedMultiplier = defaultUtilityDefs.BothOwnedMultiplier
		};
	}

}
