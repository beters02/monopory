using System;
using System.Collections.Generic;
using System.Linq;

public static class BoardSpaceNameConfig
{
	public static string CreateDefaultSnapshot()
	{
		return CreateDefaultSnapshot( BoardCatalog.DefaultBoardId );
	}

	public static string CreateDefaultSnapshot( string boardId )
	{
		return SerializeModifiers( new() );
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
		if ( modifiersObject is null )
			return "";

		return string.Join( ";", modifiersObject
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

	public static string RemapSnapshot( string snapshot, string fromBoardId, string toBoardId )
	{
		if ( string.Equals( fromBoardId, toBoardId, StringComparison.OrdinalIgnoreCase ) )
			return snapshot ?? "";

		var fromSpaces = GetBoardSpaces( fromBoardId );
		var toSpaces = GetBoardSpaces( toBoardId );
		var resolvedNames = DeserializeNames( snapshot, fromBoardId );
		var customNamesByKey = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var space in fromSpaces )
		{
			if ( space is null || string.IsNullOrWhiteSpace( space.Key ) )
				continue;

			if ( space.Index < 0 || space.Index >= resolvedNames.Count )
				continue;

			var resolvedName = resolvedNames[space.Index] ?? "";
			if ( string.Equals( resolvedName, space.DisplayName ?? "", StringComparison.Ordinal ) )
				continue;

			customNamesByKey[space.Key] = resolvedName;
		}

		var remappedNames = toSpaces
			.Select( space => customNamesByKey.TryGetValue( space.Key ?? "", out var customName )
				? customName
				: space.DisplayName ?? "" )
			.ToList();

		return SerializeNames( remappedNames, toBoardId );
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
		var modifiers = new BoardSpaceNameModifiersObject();

		foreach ( var space in spaces
			.Where( space => space is not null && !string.IsNullOrWhiteSpace( space.Key ) )
			.Where( space => HasNameForSpace( namesList, space ) )
			.Where( space => !onlyOverrides || !string.Equals( namesList[space.Index] ?? "", space.DisplayName ?? "", StringComparison.Ordinal ) ) )
		{
			modifiers.Add( new( space.Key, namesList[space.Index] ?? "" ) );
		}

		return modifiers;
	}

	private static bool HasNameForSpace( List<string> names, SpaceDef space )
	{
		return space.Index >= 0 && space.Index < names.Count;
	}

}

public sealed class BoardSpaceNameModifiersObject : List<BoardSpaceNameModifier>
{
}

public sealed class BoardSpaceNameModifier
{
	public string Id { get; }
	public string Name { get; }

	public BoardSpaceNameModifier( string id, string name )
	{
		Id = id;
		Name = name;
	}
}
