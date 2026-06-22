using System;
using System.Collections.Generic;
using System.Linq;

public static class BoardVisualText
{
	private const int DefaultMaxLineLength = 10;
	private const int MaxLabelLines = 3;

	public static string GetSpaceLabel( SpaceDef def, BoardSpaceLayout layout )
	{
		if ( def is null )
			return "";

		var source = def.DisplayName ?? def.Key ?? "";
		if ( string.IsNullOrWhiteSpace( source ) )
			return "";

		var maxLineLength = GetMaxLineLength( layout );
		return FormatWrappedLabel( source, maxLineLength );
	}

	public static int CountLabelLines( string labelText )
	{
		if ( string.IsNullOrWhiteSpace( labelText ) )
			return 0;

		return labelText.Split( '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ).Length;
	}

	public static float GetLabelScale( BoardSpaceLayout layout, int lineCount )
	{
		var baseScale = layout.IsCorner ? 0.078f : 0.065f;

		if ( lineCount >= 3 )
			return baseScale * 0.82f;

		if ( lineCount >= 2 )
			return baseScale * 0.9f;

		return baseScale;
	}

	public static int GetMaxLineLength( BoardSpaceLayout layout )
	{
		var longEdge = layout.SideIndex is 0 or 2
			? layout.VisualSize.y
			: layout.VisualSize.x;

		if ( layout.IsCorner )
			return Math.Clamp( (int)(longEdge * 1.05f), 8, 14 );

		return Math.Clamp( (int)(longEdge * 0.9f), 6, DefaultMaxLineLength );
	}

	public static string FormatWrappedLabel( string displayName, int maxLineLength )
	{
		var lines = GetDisplayNameLines( displayName )
			.SelectMany( line => WrapLine( line, maxLineLength ) )
			.Where( line => !string.IsNullOrWhiteSpace( line ) )
			.Take( MaxLabelLines )
			.ToArray();

		return string.Join( "\n", lines );
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

	private static IEnumerable<string> GetDisplayNameLines( string displayName )
	{
		return (displayName ?? "")
			.Replace( '_', ' ' )
			.Split( '\n' )
			.Select( line => line.Trim() )
			.Where( line => !string.IsNullOrWhiteSpace( line ) );
	}

	private static IEnumerable<string> WrapLine( string line, int maxLineLength )
	{
		var words = line.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		var currentLine = "";

		foreach ( var word in words )
		{
			foreach ( var chunk in SplitOversizedWord( word, maxLineLength ) )
			{
				if ( currentLine.Length == 0 )
				{
					currentLine = chunk;
					continue;
				}

				if ( currentLine.Length + 1 + chunk.Length > maxLineLength )
				{
					yield return currentLine;
					currentLine = chunk;
					continue;
				}

				currentLine += $" {chunk}";
			}
		}

		if ( currentLine.Length > 0 )
			yield return currentLine;
	}

	private static IEnumerable<string> SplitOversizedWord( string word, int maxLineLength )
	{
		if ( word.Length <= maxLineLength )
		{
			yield return word;
			yield break;
		}

		for ( var i = 0; i < word.Length; i += maxLineLength )
		{
			var length = Math.Min( maxLineLength, word.Length - i );
			yield return word.Substring( i, length );
		}
	}
}
