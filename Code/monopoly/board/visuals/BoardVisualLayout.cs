using Sandbox;
using System;
using Sandbox.ui;

/// <summary>
/// Maps board space indices to generated mesh/collider layout data.
/// Uses the same BoardPanel percent rects as the procedural UI board.
/// </summary>
public readonly struct BoardSpaceLayout
{
	public Vector3 Position { get; init; }
	public Rotation Rotation { get; init; }
	public Vector3 VisualSize { get; init; }
	public Vector3 ColliderSize { get; init; }
	public Vector3 ColliderCenter { get; init; }
	public bool IsCorner { get; init; }
	public int SideIndex { get; init; }
}

public static class BoardVisualLayout
{
	public static int GetSideIndex( int spaceIndex, Board board )
	{
		var layout = board.Layout ?? BoardLayoutDefinition.Classic();
		return layout.GetSideIndex( spaceIndex, board.SpaceCount );
	}

	public static Rotation GetLabelRotationForQuadrant( int quadrant )
	{
		return quadrant switch
		{
			1 => SpaceLayoutSettings.FirstQuadrantLocalRotation,
			2 => SpaceLayoutSettings.SecondQuadrantLocalRotation,
			3 => SpaceLayoutSettings.ThirdQuadrantLocalRotation,
			_ => SpaceLayoutSettings.FourthQuadrantLocalRotation
		};
	}

	public static Rotation GetLabelRotation( int sideIndex )
	{
		return GetLabelRotationForQuadrant( sideIndex + 1 );
	}

	public static Vector3 GetLabelLineTileOffset( Rotation labelRotation, int quadrant, float lineOffset )
	{
		if ( MathF.Abs( lineOffset ) < 0.0001f )
			return Vector3.Zero;

		// lineOffset is already in the vector magnitude — do not re-normalize (that
		// collapses +offset and -offset to the same XY after zeroing Z).
		var stack = labelRotation * Vector3.Up * lineOffset;
		stack.z = 0f;

		if ( stack.Length < 0.01f )
		{
			stack = labelRotation * Vector3.Right * lineOffset;
			stack.z = 0f;
		}

		if ( stack.Length < 0.01f )
			return GetQuadrantFallbackStackOffset( quadrant, lineOffset );

		return stack;
	}

	private static Vector3 GetQuadrantFallbackStackOffset( int quadrant, float lineOffset )
	{
		return quadrant switch
		{
			1 => new Vector3( lineOffset, 0f, 0f ),
			2 => new Vector3( 0f, lineOffset, 0f ),
			3 => new Vector3( 0f, -lineOffset, 0f ),
			_ => new Vector3( 0f, lineOffset, 0f )
		};
	}

	private const float ColorBarThicknessFraction = 0.18f;
	private const float IconInnerBiasFraction = 0.12f;

	public static Vector3 GetOuterDirection( int sideIndex )
	{
		return sideIndex switch
		{
			0 => Vector3.Left,
			1 => Vector3.Up,
			2 => Vector3.Right,
			_ => Vector3.Down
		};
	}

	public static float GetColorBarDepth( int sideIndex, Vector3 tileSize )
	{
		return sideIndex is 0 or 2
			? tileSize.x * ColorBarThicknessFraction
			: tileSize.y * ColorBarThicknessFraction;
	}

	public static Vector3 GetContentCenter( int sideIndex, Vector3 tileSize )
	{
		var barDepth = GetColorBarDepth( sideIndex, tileSize );

		return sideIndex switch
		{
			0 => new Vector3( -barDepth * 0.5f, 0f, 0f ),
			1 => new Vector3( 0f, barDepth * 0.5f, 0f ),
			2 => new Vector3( barDepth * 0.5f, 0f, 0f ),
			_ => new Vector3( 0f, -barDepth * 0.5f, 0f )
		};
	}

	public static float GetContentSpan( int sideIndex, Vector3 tileSize )
	{
		return sideIndex is 0 or 2 ? tileSize.x : tileSize.y;
	}

	public static Vector3 GetDetailIconPosition( int sideIndex, Vector3 tileSize, float surfaceZ )
	{
		var z = surfaceZ + 0.12f;
		var center = GetContentCenter( sideIndex, tileSize );
		var inner = -GetOuterDirection( sideIndex );
		var span = GetContentSpan( sideIndex, tileSize ) - GetColorBarDepth( sideIndex, tileSize );
		return (center + inner * span * IconInnerBiasFraction) with { z = z };
	}

	public static Vector3 GetQuadrantLabelReference( int quadrant )
	{
		return quadrant switch
		{
			1 => SpaceLayoutSettings.FirstQuadrantLocalPosition,
			2 => SpaceLayoutSettings.SecondQuadrantLocalPosition,
			3 => SpaceLayoutSettings.ThirdQuadrantLocalPosition,
			_ => SpaceLayoutSettings.FourthQuadrantLocalPosition
		};
	}

	/// <summary>
	/// Tile XY anchor for labels. <paramref name="labelHeight"/> is distance in the tile
	/// plane along the legacy quadrant direction — never applied to Z.
	/// </summary>
	public static Vector3 GetLabelAnchor(
		int quadrant,
		float surfaceZ,
		float labelHeight )
	{
		var reference = GetQuadrantLabelReference( quadrant );
		var planar = new Vector3( reference.x, reference.y, 0f );
		var magnitude = planar.Length;

		if ( magnitude < 0.0001f || labelHeight <= 0f )
			return new Vector3( 0f, 0f, surfaceZ );

		var scaled = planar / magnitude * labelHeight;
		return new Vector3( scaled.x, scaled.y, surfaceZ );
	}

	public static Vector3 GetLabelPosition(
		int quadrant,
		float surfaceZ,
		float labelHeight = 2.8f )
	{
		return GetLabelAnchor( quadrant, surfaceZ, labelHeight );
	}

	public static void GetOwnershipTabLayout(
		int sideIndex,
		Vector3 tileSize,
		float surfaceZ,
		out Vector3 size,
		out Vector3 center )
	{
		const float tabThickness = 0.14f;
		const float tabSpan = 0.58f;
		const float tabDepth = 1.85f;
		var z = surfaceZ + tabThickness * 0.55f;

		switch ( sideIndex )
		{
			case 0:
				size = new Vector3( tabDepth, tileSize.y * tabSpan, tabThickness );
				center = new Vector3( (tileSize.x - tabDepth) * 0.5f - 0.25f, 0f, z );
				return;
			case 1:
				size = new Vector3( tileSize.x * tabSpan, tabDepth, tabThickness );
				center = new Vector3( 0f, (AccentDepthFallback( tileSize.y ) - tileSize.y) * 0.5f + tabDepth * 0.2f, z );
				return;
			case 2:
				size = new Vector3( tabDepth, tileSize.y * tabSpan, tabThickness );
				center = new Vector3( (tabDepth - tileSize.x) * 0.5f + 0.25f, 0f, z );
				return;
			default:
				size = new Vector3( tileSize.x * tabSpan, tabDepth, tabThickness );
				center = new Vector3( 0f, (tileSize.y - tabDepth) * 0.5f - 0.25f, z );
				return;
		}
	}

	private static float AccentDepthFallback( float tileSpan ) => MathF.Min( 2.4f, tileSpan * 0.35f );

	public static Vector3 GetOwnershipMarkerCenter( int sideIndex, Vector3 tileSize, float surfaceZ )
	{
		var insetX = tileSize.x * 0.34f;
		var insetY = tileSize.y * 0.34f;
		var z = surfaceZ + 0.18f;

		return sideIndex switch
		{
			0 => new Vector3( insetX, 0f, z ),
			1 => new Vector3( 0f, -insetY, z ),
			2 => new Vector3( -insetX, 0f, z ),
			_ => new Vector3( 0f, insetY, z )
		};
	}

	public static BoardSpaceLayout GetSpaceLayout( int index, Board board, float spaceThickness, float boardThickness, float spaceGap )
	{
		var panel = board.ProceduralBoardPanel;
		if ( panel is null )
			return default;

		var layout = board.Layout ?? BoardLayoutDefinition.Classic();
		var rect = BoardMath.GetSpaceRect( index, panel );
		var worldSize = board.GetBoardWorldSize();

		var colliderSize = BoardMath.RectPercentToColliderSize( rect, worldSize );
		var gapWorld = worldSize * (spaceGap / 100f);
		var visualSize = new Vector3(
			MathF.Max( 0.5f, colliderSize.x - gapWorld ),
			MathF.Max( 0.5f, colliderSize.y - gapWorld ),
			spaceThickness
		);

		var position = BoardMath.RectPercentToLocal( rect, worldSize, board.ProceduralSpaceZOffset );
		var tileCenterZ = boardThickness + spaceThickness * 0.5f;
		var sideIndex = layout.GetSideIndex( index, board.SpaceCount );

		return new BoardSpaceLayout
		{
			Position = position,
			Rotation = Rotation.Identity,
			VisualSize = visualSize,
			ColliderSize = new Vector3(
				colliderSize.x,
				colliderSize.y,
				boardThickness + spaceThickness + 4f
			),
			ColliderCenter = new Vector3( 0f, 0f, tileCenterZ ),
			IsCorner = layout.IsCornerIndex( index ),
			SideIndex = sideIndex
		};
	}

	public static float GetBoardBaseSize( Board board )
	{
		return board.GetBoardWorldSize();
	}
}
