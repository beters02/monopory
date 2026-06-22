using System;
using System.Collections.Generic;
using System.Linq;

public static class BoardVisualText
{
	private const int DefaultMaxLineLength = 10;
	private const int MaxLabelLines = 3;
	private const float CharWidthFactor = 0.55f;
	private const float LongEdgePadding = 0.72f;
	private const float ScaleFitPadding = 0.92f;
	private const float NarrowEdgePadding = 0.68f;

	public readonly struct SpaceLabelLayout
	{
		public IReadOnlyList<string> Lines { get; init; }
		public float Scale { get; init; }
		public int FontSize { get; init; }
		public float LineSpacing { get; init; }
	}

	public static SpaceLabelLayout BuildSpaceLabelLayout( SpaceDef def, BoardSpaceLayout layout )
	{
		if ( def is null )
			return default;

		var source = def.DisplayName ?? def.Key ?? "";
		if ( string.IsNullOrWhiteSpace( source ) )
			return default;

		var fontSize = layout.IsCorner ? 64 : 50;
		var longEdge = GetLongEdge( layout ) * LongEdgePadding;
		var narrowEdge = GetNarrowEdge( layout ) * NarrowEdgePadding;
		var probeScale = layout.IsCorner ? 0.046f : 0.036f;
		var lines = WrapLabelLines( source, layout, fontSize, probeScale, longEdge, narrowEdge );
		if ( lines.Count == 0 )
			return default;

		var scale = ComputeLabelScale( lines, fontSize, longEdge, narrowEdge );
		var lineSpacing = lines.Count <= 1
			? scale * fontSize * CharWidthFactor * 1.32f
			: MathF.Max( narrowEdge * 0.84f / lines.Count, scale * fontSize * CharWidthFactor * 1.35f );

		return new SpaceLabelLayout
		{
			Lines = lines,
			Scale = scale,
			FontSize = fontSize,
			LineSpacing = lineSpacing
		};
	}

	public static string GetSpaceLabel( SpaceDef def, BoardSpaceLayout layout )
	{
		var lines = GetSpaceLabelLines( def, layout );
		return lines.Count == 0 ? "" : string.Join( "\n", lines );
	}

	public static IReadOnlyList<string> GetSpaceLabelLines( SpaceDef def, BoardSpaceLayout layout )
	{
		if ( def is null )
			return Array.Empty<string>();

		var source = def.DisplayName ?? def.Key ?? "";
		if ( string.IsNullOrWhiteSpace( source ) )
			return Array.Empty<string>();

		var maxLineLength = GetMaxLineLength( layout, layout.IsCorner ? 64 : 50, layout.IsCorner ? 0.046f : 0.036f );
		return FormatWrappedLabelLines( source, maxLineLength );
	}

	public static int CountLabelLines( string labelText )
	{
		if ( string.IsNullOrWhiteSpace( labelText ) )
			return 0;

		return labelText.Split( '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ).Length;
	}

	public static float GetLabelScale( BoardSpaceLayout layout, int lineCount, int longestLineLength = 0 )
	{
		var fontSize = layout.IsCorner ? 64 : 50;
		var longEdge = GetLongEdge( layout ) * LongEdgePadding;
		var narrowEdge = GetNarrowEdge( layout ) * NarrowEdgePadding;
		var lines = Enumerable.Repeat( new string( 'M', Math.Max( 1, longestLineLength ) ), Math.Max( 1, lineCount ) ).ToArray();
		return ComputeLabelScale( lines, fontSize, longEdge, narrowEdge );
	}

	public static float GetLineSpacing( BoardSpaceLayout layout, float labelScale, int fontSize )
	{
		return labelScale * fontSize * CharWidthFactor * 1.32f;
	}

	public static int GetMaxLineLength( BoardSpaceLayout layout, int fontSize, float probeScale )
	{
		var longEdge = GetLongEdge( layout ) * LongEdgePadding;
		var chars = (int)(longEdge / (fontSize * probeScale * CharWidthFactor));

		if ( layout.IsCorner )
			return Math.Clamp( chars, 6, 11 );

		return Math.Clamp( chars, 4, DefaultMaxLineLength );
	}

	public static int GetMaxLineLength( BoardSpaceLayout layout )
	{
		return GetMaxLineLength( layout, layout.IsCorner ? 64 : 50, layout.IsCorner ? 0.046f : 0.036f );
	}

	private static float GetLongEdge( BoardSpaceLayout layout )
	{
		if ( layout.IsCorner )
			return MathF.Max( layout.VisualSize.x, layout.VisualSize.y );

		// Bottom/top: long along X. Left/right: long along Y.
		return layout.SideIndex is 0 or 2 ? layout.VisualSize.x : layout.VisualSize.y;
	}

	private static float GetNarrowEdge( BoardSpaceLayout layout )
	{
		if ( layout.IsCorner )
			return MathF.Min( layout.VisualSize.x, layout.VisualSize.y );

		return layout.SideIndex is 0 or 2 ? layout.VisualSize.y : layout.VisualSize.x;
	}

	private static IReadOnlyList<string> WrapLabelLines(
		string source,
		BoardSpaceLayout layout,
		int fontSize,
		float probeScale,
		float longEdge,
		float narrowEdge )
	{
		var maxChars = GetMaxLineLength( layout, fontSize, probeScale );
		var lines = FormatWrappedLabelLines( source, maxChars );

		while ( maxChars > 4 )
		{
			var scale = ComputeLabelScale( lines, fontSize, longEdge, narrowEdge );
			var spacing = lines.Count <= 1 ? 0f : narrowEdge * 0.84f / lines.Count;
			var stackUsed = spacing * Math.Max( 0, lines.Count - 1 ) + scale * fontSize * CharWidthFactor;

			if ( lines.Count > 1 && stackUsed <= narrowEdge && scale >= 0.024f )
				break;

			if ( lines.Count == 1 && scale >= 0.032f )
				break;

			maxChars--;
			lines = FormatWrappedLabelLines( source, maxChars );
		}

		return lines;
	}

	private static float ComputeLabelScale(
		IReadOnlyList<string> lines,
		int fontSize,
		float longEdge,
		float narrowEdge )
	{
		var longestLine = lines.Max( line => line.Length );
		var scaleByLength = longEdge / (longestLine * fontSize * CharWidthFactor);
		var scaleByStack = narrowEdge / (lines.Count * fontSize * CharWidthFactor * 1.35f);
		var scale = MathF.Min( scaleByLength, scaleByStack );

		foreach ( var line in lines )
		{
			var lineScale = longEdge / (line.Length * fontSize * CharWidthFactor);
			scale = MathF.Min( scale, lineScale );
		}

		return Math.Clamp( scale * ScaleFitPadding, 0.022f, 0.048f );
	}

	public static string FormatWrappedLabel( string displayName, int maxLineLength )
	{
		return string.Join( "\n", FormatWrappedLabelLines( displayName, maxLineLength ) );
	}

	public static IReadOnlyList<string> FormatWrappedLabelLines( string displayName, int maxLineLength )
	{
		return GetDisplayNameLines( displayName )
			.SelectMany( line => WrapLine( line, maxLineLength ) )
			.Where( line => !string.IsNullOrWhiteSpace( line ) )
			.Take( MaxLabelLines )
			.ToArray();
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

	public static bool ShouldShowDetailIcon( SpaceDef def, bool labelsEnabled = false )
	{
		if ( def is null )
			return false;

		if ( labelsEnabled && def.Type is SpaceType.Chance or SpaceType.CommunityChest )
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
