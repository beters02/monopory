using System;

public static class BoardCatalog
{
	public const string DefaultBoardId = "default";
	public const string ExampleBoardId = "example";
	public const string LegacyNamedBoardId = "named";

	private static readonly List<Func<BoardDefinition>> boardFactories = new()
	{
		CreateDefaultBoard,
		//CreateExampleBoard
	};

	private static IReadOnlyList<MatchSettingsPreset> namePresets;

	public static IReadOnlyList<BoardDefinition> GetAll()
	{
		return GetLayoutBoards();
	}

	public static IReadOnlyList<BoardDefinition> GetLayoutBoards()
	{
		return boardFactories
			.Select( factory => factory() )
			.Where( board => board is not null )
			.ToList();
	}

	public static IReadOnlyList<MatchSettingsPreset> GetNamePresets()
	{
		return namePresets ??= BuildNamePresets();
	}

	public static MatchSettingsPreset GetNamePresetById( string presetId )
	{
		if ( string.IsNullOrWhiteSpace( presetId ) )
			return null;

		return GetNamePresets()
			.FirstOrDefault( preset => string.Equals( preset?.Id, presetId.Trim(), StringComparison.OrdinalIgnoreCase ) );
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

		if ( string.Equals( normalizedId, LegacyNamedBoardId, StringComparison.OrdinalIgnoreCase ) )
			normalizedId = DefaultBoardId;

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

	private static IReadOnlyList<MatchSettingsPreset> BuildNamePresets()
	{
		return TypeLibrary.GetTypesWithAttribute<BoardSpaceNamePresetAttribute>()
			.OrderBy( entry => entry.Attribute.Order )
			.ThenBy( entry => entry.Attribute.Name )
			.Select( entry => entry.Attribute.Build( entry.Type ) )
			.Where( preset => preset is not null )
			.ToList();
	}

	private static BoardDefinition CreateDefaultBoard()
	{
		var spaces = BoardData.CreateSpaceDefs();

		return new BoardDefinition
		{
			Id = DefaultBoardId,
			DisplayName = "Default Layout",
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
		board.DisplayName = "Example Layout";

		board.Spaces =
		[
			.. board.Spaces,
			new()
			{
				Index = 40,
				Key = "property_purple_1",
				DisplayName = "de_nuke",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				OneHouseRent = 10,
				TwoHouseRent = 30,
				ThreeHouseRent = 90,
				FourHouseRent = 160,
				HotelRent = 250,
				ColorGroup = ColorGroup.Pink
			},
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
}
