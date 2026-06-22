using System;
using System.Linq;

public static class BoardVisualText
{
	public static string GetSpaceLabel( SpaceDef def )
	{
		if ( def is null )
			return "";

		if ( def.IsCorner || IsCornerType( def.Type ) )
			return GetCornerHeadline( def );

		return (def.DisplayName ?? def.Key ?? "").Replace( "\n", " " ).Trim();
	}

	public static string GetSpaceDetailIcon( SpaceDef def )
	{
		if ( def is null )
			return "";

		return def.Type switch
		{
			SpaceType.Railroad => "RR",
			SpaceType.Utility => "UTIL",
			SpaceType.Chance => "?",
			SpaceType.CommunityChest => "CHEST",
			SpaceType.Tax => "$",
			SpaceType.Go => "GO",
			SpaceType.Jail => "JAIL",
			SpaceType.FreeParking => "P",
			SpaceType.GoToJail => "JAIL",
			_ => ""
		};
	}

	public static bool ShouldShowDetailIcon( SpaceDef def )
	{
		if ( def is null )
			return false;

		return def.Type != SpaceType.Property && !string.IsNullOrWhiteSpace( GetSpaceDetailIcon( def ) );
	}

	private static bool IsCornerType( SpaceType type ) =>
		type is SpaceType.Go or SpaceType.Jail or SpaceType.FreeParking or SpaceType.GoToJail;

	private static string GetCornerHeadline( SpaceDef def )
	{
		var lines = SplitLines( def.DisplayName );
		if ( lines.Length > 0 && !string.IsNullOrWhiteSpace( lines[0] ) )
			return lines[0];

		return GetSpaceDetailIcon( def );
	}

	private static string[] SplitLines( string text ) =>
		(text ?? "").Split( '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
}
