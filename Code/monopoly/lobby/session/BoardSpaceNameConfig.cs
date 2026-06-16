using System;
using System.Collections.Generic;
using System.Linq;

public static class BoardSpaceNameConfig
{

	// EXAMPLE !!
	public static IReadOnlyList<MatchSettingsPreset> BuildBoardSpaceNamePredefinedPresets()
	{
		return new[]
		{
			new MatchSettingsPreset { Id = "board_default", Name = "Default Board", Snapshot = CreateDefaultSnapshot( BoardCatalog.DefaultBoardId ), IsPredefined = true },
			new MatchSettingsPreset { Id = "board_classic", Name = "Classic Names", Snapshot = CreateClassicSnapshot(), IsPredefined = true }
		};
	}
	//

	private static readonly BoardSpaceNameModifiersObject ClassicBoardSpaceNameModifiers = new()
	{
		Modifiers = new()
		{
			new() { Id = "property_brown_0", Name = "Mediterranean Avenue" },
			new() { Id = "property_brown_1", Name = "Baltic Avenue" },
			new() { Id = "tax_income", Name = "Income Tax" },
			new() { Id = "railroad_0", Name = "Reading Railroad" },
			new() { Id = "property_light_blue_0", Name = "Oriental Avenue" },
			new() { Id = "property_light_blue_1", Name = "Vermont Avenue" },
			new() { Id = "property_light_blue_2", Name = "Connecticut Avenue" },
			new() { Id = "property_pink_0", Name = "St. Charles Place" },
			new() { Id = "utility_0", Name = "Electric Company" },
			new() { Id = "property_pink_1", Name = "States Avenue" },
			new() { Id = "property_pink_2", Name = "Virginia Avenue" },
			new() { Id = "railroad_1", Name = "Pennsylvania Railroad" },
			new() { Id = "property_orange_0", Name = "St. James Place" },
			new() { Id = "property_orange_1", Name = "Tennessee Avenue" },
			new() { Id = "property_orange_2", Name = "New York Avenue" },
			new() { Id = "property_red_0", Name = "Kentucky Avenue" },
			new() { Id = "property_red_1", Name = "Indiana Avenue" },
			new() { Id = "property_red_2", Name = "Illinois Avenue" },
			new() { Id = "railroad_2", Name = "B&O Railroad" },
			new() { Id = "property_yellow_0", Name = "Atlantic Avenue" },
			new() { Id = "property_yellow_1", Name = "Ventnor Avenue" },
			new() { Id = "utility_1", Name = "Water Works" },
			new() { Id = "property_yellow_2", Name = "Marvin Gardens" },
			new() { Id = "property_green_0", Name = "Pacific Avenue" },
			new() { Id = "property_green_1", Name = "North Carolina Avenue" },
			new() { Id = "property_green_2", Name = "Pennsylvania Avenue" },
			new() { Id = "railroad_3", Name = "Short Line" },
			new() { Id = "property_dark_blue_0", Name = "Park Place" },
			new() { Id = "tax_luxury", Name = "Luxury Tax" },
			new() { Id = "property_dark_blue_1", Name = "Boardwalk" }
		}
	};

	public static string CreateDefaultSnapshot()
	{
		return CreateDefaultSnapshot( BoardCatalog.DefaultBoardId );
	}

	public static string CreateDefaultSnapshot( string boardId )
	{
		return SerializeModifiers( new() );
	}

	public static string CreateClassicSnapshot()
	{
		return SerializeModifiers( ClassicBoardSpaceNameModifiers );
	}

	public static string SerializeNames( IEnumerable<string> names )
	{
		if ( names is null )
			return "";

		return string.Join( ";", names.Select( ( name, index ) => $"{index}:{Uri.EscapeDataString( name ?? "" )}" ) );
	}

	public static string SerializeNames( IEnumerable<string> names, string boardId )
	{
		return SerializeModifiers( CreateModifiers( names, boardId, true ) );
	}

	public static string SerializeModifiers( BoardSpaceNameModifiersObject modifiersObject )
	{
		if ( modifiersObject?.Modifiers is null )
			return "";

		return string.Join( ";", modifiersObject.Modifiers
			.Where( modifier => modifier is not null && !string.IsNullOrWhiteSpace( modifier.Id ) )
			.Select( modifier => $"{Uri.EscapeDataString( modifier.Id.Trim() )}:{Uri.EscapeDataString( modifier.Name ?? "" )}" ) );
	}

	public static List<string> DeserializeNames( string snapshot )
	{
		return DeserializeNames( snapshot, BoardCatalog.DefaultBoardId );
	}

	public static List<string> DeserializeNames( string snapshot, string boardId )
	{
		var spaces = GetBoardSpaces( boardId );
		var defaults = spaces
			.Select( space => space.DisplayName ?? "" )
			.ToList();

		if ( string.IsNullOrWhiteSpace( snapshot ) )
			return defaults;

		foreach ( var pair in snapshot.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			var separatorIndex = pair.IndexOf( ':' );
			if ( separatorIndex <= 0 || separatorIndex >= pair.Length - 1 )
				continue;

			var id = Uri.UnescapeDataString( pair[..separatorIndex] );
			var name = Uri.UnescapeDataString( pair[(separatorIndex + 1)..] );

			if ( int.TryParse( id, out var index ) )
			{
				if ( index >= 0 && index < defaults.Count )
					defaults[index] = name;

				continue;
			}

			var space = spaces.FirstOrDefault( space => string.Equals( space.Key, id, StringComparison.OrdinalIgnoreCase ) );
			if ( space is null || space.Index < 0 || space.Index >= defaults.Count )
				continue;

			defaults[space.Index] = name;
		}

		return defaults;
	}

	public static void ApplySnapshot( List<SpaceDef> spaces, string snapshot )
	{
		ApplySnapshot( spaces, snapshot, BoardCatalog.DefaultBoardId );
	}

	public static void ApplySnapshot( List<SpaceDef> spaces, string snapshot, string boardId )
	{
		if ( spaces is null || spaces.Count == 0 )
			return;

		var names = DeserializeNames( snapshot, boardId );
		foreach ( var space in spaces )
		{
			if ( space is null || space.Index < 0 || space.Index >= names.Count )
				continue;

			var name = names[space.Index]?.Trim();
			if ( string.IsNullOrWhiteSpace( name ) )
				continue;

			space.DisplayName = name;
		}
	}

	private static List<SpaceDef> GetBoardSpaces( string boardId )
	{
		return BoardCatalog.GetById( boardId )
			.Spaces
			.OrderBy( space => space.Index )
			.ToList();
	}

	private static BoardSpaceNameModifiersObject CreateModifiers( IEnumerable<string> names, string boardId, bool onlyOverrides )
	{
		var spaces = GetBoardSpaces( boardId );
		var namesList = names?.ToList() ?? new();
		return new()
		{
			Modifiers = spaces
				.Where( space => space is not null && !string.IsNullOrWhiteSpace( space.Key ) )
				.Where( space => HasNameForSpace( namesList, space ) )
				.Where( space => !onlyOverrides || !string.Equals( namesList[space.Index] ?? "", space.DisplayName ?? "", StringComparison.Ordinal ) )
				.Select( space => new BoardSpaceNameModifier
				{
					Id = space.Key,
					Name = namesList[space.Index] ?? ""
				} )
				.ToList()
		};
	}

	private static bool HasNameForSpace( List<string> names, SpaceDef space )
	{
		return space.Index >= 0 && space.Index < names.Count;
	}
}

public sealed class BoardSpaceNameModifiersObject
{
	public List<BoardSpaceNameModifier> Modifiers { get; set; } = new();
}

public sealed class BoardSpaceNameModifier
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
}
