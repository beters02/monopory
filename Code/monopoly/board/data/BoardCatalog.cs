using System;

public static class BoardCatalog
{
	public const string DefaultBoardId = "default";
	public const string ExampleBoardId = "example";
	public const string NamedBoardId = "named";

	private static readonly List<Func<BoardDefinition>> boardFactories = new()
	{
		CreateDefaultBoard,
		CreateNamedBoard,
		//CreateExampleBoard
	};

	public static IReadOnlyList<BoardDefinition> GetAll()
	{
		return boardFactories
			.Select( factory => factory() )
			.Where( board => board is not null )
			.ToList();
	}

	public static BoardDefinition GetDefault()
	{
		return GetById( DefaultBoardId );
	}

	public static BoardDefinition GetById( string boardId )
	{
		var normalizedId = string.IsNullOrWhiteSpace( boardId )
			? DefaultBoardId
			: boardId.Trim();

		foreach ( var factory in boardFactories )
		{
			var board = factory();
			if ( string.Equals( board?.Id, normalizedId, StringComparison.OrdinalIgnoreCase ) )
				return board;
		}

		return CreateDefaultBoard();
	}

	public static int GetDefaultSpaceCount()
	{
		return GetDefault().SpaceCount;
	}

	private static BoardDefinition CreateDefaultBoard()
	{
		var spaces = BoardData.CreateSpaceDefs();

		return new BoardDefinition
		{
			Id = DefaultBoardId,
			DisplayName = "Default Board",
			Spaces = spaces,
			ChanceCards = CardData.CreateChanceCards(),
			CommunityChestCards = CardData.CreateCommunityChestCards(),
			RailroadData = BoardData.CreateRailroadDefs(),
			UtilityData = BoardData.CreateUtilityDefs(),
			Layout = BoardLayoutDefinition.Classic( spaces.Count ),
			Theme = new BoardThemeDefinition
			{
				ColorGroupColors = new()
				{
					[ColorGroup.Brown] = "#8b3a00",
					[ColorGroup.LightBlue] = "#7fa1d0",
					[ColorGroup.Pink] = "#ed1978",
					[ColorGroup.Orange] = "#ff7f0e",
					[ColorGroup.Red] = "#f22520",
					[ColorGroup.Yellow] = "#ffe21a",
					[ColorGroup.Green] = "#07a64f",
					[ColorGroup.DarkBlue] = "#06469a"
				}
			}
		};
	}

	private static BoardDefinition CreateExampleBoard()
	{
		var board = CreateDefaultBoard();
		board.Id = ExampleBoardId;
		board.DisplayName = "Example Board";

		SetSpaceName( board.Spaces, 0, "Launch Pad" );
		SetSpaceName( board.Spaces, 1, "Old Town" );
		SetSpaceName( board.Spaces, 3, "Canal Street" );
		SetSpaceName( board.Spaces, 5, "Metro Line" );
		SetSpaceName( board.Spaces, 10, "Timeout" );
		SetSpaceName( board.Spaces, 20, "Public Park" );
		SetSpaceName( board.Spaces, 30, "Go To Timeout" );
		SetSpaceName( board.Spaces, 39, "Skyline Tower" );

		board.Spaces =
		[
			.. board.Spaces,
			new()
			{
				Index = 40,
				Key = "property_purple_1",
				DisplayName = "de_nuke", // Harvey Milk Blvd
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				OneHouseRent = 10,
				TwoHouseRent = 30,
				ThreeHouseRent = 90,
				FourHouseRent = 160,
				HotelRent = 250,
				ColorGroup = ColorGroup.Pink
			}
,
		];

		board.Theme = new BoardThemeDefinition
		{
			BoardEdgeColor = "#20313f",
			CenterColor = "#f0ead2",
			SpaceColor = "#fffaf0",
			BorderColor = "#1d242c",
			TextColor = "#17202a",
			ChestIconColor = "#b7791f",
			GoColor = "#2f855a",
			FreeParkingColor = "#c05621",
			GoToJailColor = "#9b2c2c",
			ColorGroupColors = new()
			{
				[ColorGroup.Brown] = "#8d5524",
				[ColorGroup.LightBlue] = "#63b3ed",
				[ColorGroup.Pink] = "#d53f8c",
				[ColorGroup.Orange] = "#ed8936",
				[ColorGroup.Red] = "#e53e3e",
				[ColorGroup.Yellow] = "#ecc94b",
				[ColorGroup.Green] = "#38a169",
				[ColorGroup.DarkBlue] = "#2b6cb0"
			}
		};

		return board;
	}

	private static BoardDefinition CreateNamedBoard()
	{
		var board = CreateDefaultBoard();
		board.Id = NamedBoardId;
		board.DisplayName = "Named Board";

		SetSpaceName( board.Spaces, 0, "Landing" );
		SetSpaceName( board.Spaces, 1, "Studio Apartment" );
		SetSpaceName( board.Spaces, 2, "Community Chest" );
		SetSpaceName( board.Spaces, 3, "Laundry Lofts" );
		SetSpaceName( board.Spaces, 4, "Plug Tax" );
		SetSpaceName( board.Spaces, 5, "Transit Hub" );
		SetSpaceName( board.Spaces, 6, "College Commons" );
		SetSpaceName( board.Spaces, 7, "Chance" );
		SetSpaceName( board.Spaces, 8, "de_Miraq" );
		SetSpaceName( board.Spaces, 9, "de_Nuke" );
		SetSpaceName( board.Spaces, 10, "Visiting Eviction Court" );
		SetSpaceName( board.Spaces, 11, "Downtown District" );
		SetSpaceName( board.Spaces, 12, "Power Grid" );
		SetSpaceName( board.Spaces, 13, "Market Square" );
		SetSpaceName( board.Spaces, 14, "Canals District" );
		SetSpaceName( board.Spaces, 15, "Metro Line" );
		SetSpaceName( board.Spaces, 16, "Riverside Villas" );
		SetSpaceName( board.Spaces, 17, "Community Chest" );
		SetSpaceName( board.Spaces, 18, "Harbor 17" );
		SetSpaceName( board.Spaces, 19, "Skyline Towers" );
		SetSpaceName( board.Spaces, 20, "Free Parking" );
		SetSpaceName( board.Spaces, 21, "Black Mesa Business Park" );
		SetSpaceName( board.Spaces, 22, "Chance" );
		SetSpaceName( board.Spaces, 23, "Lambda Square" );
		SetSpaceName( board.Spaces, 24, "Ravenholm Heights" );
		SetSpaceName( board.Spaces, 25, "Express Line" );
		SetSpaceName( board.Spaces, 26, "Kleiner Commons" );
		SetSpaceName( board.Spaces, 27, "White Forest Estates" );
		SetSpaceName( board.Spaces, 28, "Internet Provider" );
		SetSpaceName( board.Spaces, 29, "Vertigo Towers" );
		SetSpaceName( board.Spaces, 30, "Evicted!" );
		SetSpaceName( board.Spaces, 31, "Construct Court" );
		SetSpaceName( board.Spaces, 32, "City 17 Condos" );
		SetSpaceName( board.Spaces, 33, "Community Chest" );
		SetSpaceName( board.Spaces, 34, "Nova Prospekt Villas" );
		SetSpaceName( board.Spaces, 35, "Rapid Transit" );
		SetSpaceName( board.Spaces, 36, "Chance" );
		SetSpaceName( board.Spaces, 37, "Facepunch Plaza" );
		SetSpaceName( board.Spaces, 38, "HOA Fine" );
		SetSpaceName( board.Spaces, 39, "Billionaire Boulevard" );

		return board;
	}

	private static void SetSpaceName( List<SpaceDef> spaces, int index, string displayName )
	{
		var space = spaces?.FirstOrDefault( space => space.Index == index );
		if ( space is not null )
			space.DisplayName = displayName;
	}
}
