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
	public bool IsCorner { get; init; }
}

public static class BoardVisualLayout
{
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
		var meshOffsetZ = boardThickness + spaceThickness * 0.5f;

		return new BoardSpaceLayout
		{
			Position = position,
			Rotation = Rotation.Identity,
			VisualSize = visualSize,
			ColliderSize = colliderSize,
			IsCorner = layout.IsCornerIndex( index )
		};
	}

	public static float GetBoardBaseSize( Board board )
	{
		return board.GetBoardWorldSize();
	}
}
