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

	public static Rotation GetLabelRotation( int sideIndex )
	{
		return sideIndex switch
		{
			0 => Rotation.Identity,
			1 => Rotation.FromYaw( -90f ),
			2 => Rotation.FromYaw( 180f ),
			_ => Rotation.FromYaw( 90f )
		};
	}

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
