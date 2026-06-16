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
		return SerializeNames( GetBoardSpaces( boardId ).Select( space => space.DisplayName ) );
	}

	public static string SerializeNames( IEnumerable<string> names )
	{
		if ( names is null )
			return "";

		return string.Join( ";", names.Select( ( name, index ) => $"{index}:{Uri.EscapeDataString( name ?? "" )}" ) );
	}

	public static List<string> DeserializeNames( string snapshot )
	{
		return DeserializeNames( snapshot, BoardCatalog.DefaultBoardId );
	}

	public static List<string> DeserializeNames( string snapshot, string boardId )
	{
		var defaults = GetBoardSpaces( boardId )
			.Select( space => space.DisplayName ?? "" )
			.ToList();

		if ( string.IsNullOrWhiteSpace( snapshot ) )
			return defaults;

		foreach ( var pair in snapshot.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			var separatorIndex = pair.IndexOf( ':' );
			if ( separatorIndex <= 0 || separatorIndex >= pair.Length - 1 )
				continue;

			if ( !int.TryParse( pair[..separatorIndex], out var index ) )
				continue;

			if ( index < 0 || index >= defaults.Count )
				continue;

			defaults[index] = Uri.UnescapeDataString( pair[(separatorIndex + 1)..] );
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
}
