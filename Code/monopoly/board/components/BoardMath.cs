using Sandbox.ui;

public class BoardMath
{

    public static Rect GetSpaceRect( int spaceIndex, BoardPanel boardPanel )
    {
        (float left, float top, float width, float height) = GetSpaceRectFloats( spaceIndex, boardPanel );
        return new(left, top, width, height);
    }

    private static (float Left, float Top, float Width, float Height) GetSpaceRectFloats( int spaceIndex, BoardPanel boardPanel )
	{

        float CornerSize = boardPanel.CornerSize;
        float TrackDepth = boardPanel.TrackDepth;
		var layout = boardPanel.ActiveLayout ?? BoardLayoutDefinition.Classic();
		var spaceCount = boardPanel.SpaceCount;
		var cornerOrder = layout.GetCornerOrder( spaceIndex );

		if ( cornerOrder == 0 )
			return (100f - CornerSize, 100f - CornerSize, CornerSize, CornerSize);

		if ( cornerOrder == 1 )
			return (0f, 100f - CornerSize, CornerSize, CornerSize);

		if ( cornerOrder == 2 )
			return (0f, 0f, CornerSize, CornerSize);

		if ( cornerOrder == 3 )
			return (100f - CornerSize, 0f, CornerSize, CornerSize);

		var sideIndex = layout.GetSideIndex( spaceIndex, spaceCount );
		var firstIndex = layout.GetFirstRegularIndexForSide( sideIndex, spaceCount );
		var lastIndex = layout.GetLastRegularIndexForSide( sideIndex, spaceCount );

		if ( sideIndex == 0 )
		{
			var offset = GetSideOffsetBefore( firstIndex, lastIndex, spaceIndex, boardPanel );
			var length = GetNormalizedSpaceLength( firstIndex, lastIndex, spaceIndex, boardPanel );
			return (100f - CornerSize - offset - length, 100f - TrackDepth, length, TrackDepth);
		}

		if ( sideIndex == 1 )
		{
			var offset = GetSideOffsetBefore( firstIndex, lastIndex, spaceIndex, boardPanel );
			var length = GetNormalizedSpaceLength( firstIndex, lastIndex, spaceIndex, boardPanel );
			return (0f, 100f - CornerSize - offset - length, TrackDepth, length);
		}

		if ( sideIndex == 2 )
		{
			var offset = GetSideOffsetBefore( firstIndex, lastIndex, spaceIndex, boardPanel );
			var length = GetNormalizedSpaceLength( firstIndex, lastIndex, spaceIndex, boardPanel );
			return (CornerSize + offset, 0f, length, TrackDepth);
		}

		var sideOffset = GetSideOffsetBefore( firstIndex, lastIndex, spaceIndex, boardPanel );
		var sideLength = GetNormalizedSpaceLength( firstIndex, lastIndex, spaceIndex, boardPanel );
		return (100f - TrackDepth, CornerSize + sideOffset, TrackDepth, sideLength);
	}

    private static float GetNormalizedSpaceLength( int firstIndex, int lastIndex, int spaceIndex, BoardPanel boardPanel )
	{
		var totalWeight = boardPanel.GetSideWeightTotal( firstIndex, lastIndex );
		return boardPanel.SideRegularLength * (boardPanel.GetSpaceLengthWeight( spaceIndex ) / totalWeight);
	}

    private static float GetSideOffsetBefore( int firstIndex, int lastIndex, int spaceIndex, BoardPanel boardPanel )
	{
		var offset = 0f;

		for ( var index = firstIndex; index < spaceIndex && index <= lastIndex; index++ )
			offset += GetNormalizedSpaceLength( firstIndex, lastIndex, index, boardPanel );

		return offset;
	}

    public static Vector3 RectPercentToLocal(
        Rect spaceRect,
        float boardWorldSize,
        float z = 0f )
    {
        var centerXPercent = spaceRect.Left + spaceRect.Width * 0.5f;
        var centerYPercent = spaceRect.Top + spaceRect.Height * 0.5f;

        var half = boardWorldSize * 0.5f;

        var localX = (50f - centerYPercent) / 50f * half;
        var localY = (50f - centerXPercent) / 50f * half;

        return new Vector3( localX, localY, z );
    }

    public static Vector3 RectPercentToColliderSize(
        Rect spaceRect,
        float boardWorldSize,
        float zSize = 4f )
    {
        var worldWidth = boardWorldSize * (spaceRect.Width / 100f);
        var worldHeight = boardWorldSize * (spaceRect.Height / 100f);

        // Because panel horizontal maps to local Y, and panel vertical maps to local X.
        return new Vector3( worldHeight, worldWidth, zSize );
    }
}
