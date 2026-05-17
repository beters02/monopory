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

		if ( spaceIndex == 0 )
			return (100f - CornerSize, 100f - CornerSize, CornerSize, CornerSize);

		if ( spaceIndex > 0 && spaceIndex < 10 )
		{
			var offset = GetSideOffsetBefore( 1, 9, spaceIndex, boardPanel );
			var length = GetNormalizedSpaceLength( 1, 9, spaceIndex, boardPanel );
			return (100f - CornerSize - offset - length, 100f - TrackDepth, length, TrackDepth);
		}

		if ( spaceIndex == 10 )
			return (0f, 100f - CornerSize, CornerSize, CornerSize);

		if ( spaceIndex > 10 && spaceIndex < 20 )
		{
			var offset = GetSideOffsetBefore( 11, 19, spaceIndex, boardPanel );
			var length = GetNormalizedSpaceLength( 11, 19, spaceIndex, boardPanel );
			return (0f, 100f - CornerSize - offset - length, TrackDepth, length);
		}

		if ( spaceIndex == 20 )
			return (0f, 0f, CornerSize, CornerSize);

		if ( spaceIndex > 20 && spaceIndex < 30 )
		{
			var offset = GetSideOffsetBefore( 21, 29, spaceIndex, boardPanel );
			var length = GetNormalizedSpaceLength( 21, 29, spaceIndex, boardPanel );
			return (CornerSize + offset, 0f, length, TrackDepth);
		}

		if ( spaceIndex == 30 )
			return (100f - CornerSize, 0f, CornerSize, CornerSize);

		{
			var offset = GetSideOffsetBefore( 31, 39, spaceIndex, boardPanel );
			var length = GetNormalizedSpaceLength( 31, 39, spaceIndex, boardPanel );
			return (100f - TrackDepth, CornerSize + offset, TrackDepth, length);
		}
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