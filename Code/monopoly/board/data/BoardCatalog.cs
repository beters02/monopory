using System;

public static class BoardCatalog
{
	public const string DefaultBoardId = "default";

	private static readonly List<Func<BoardDefinition>> boardFactories = new()
	{
		CreateDefaultBoard
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
}
