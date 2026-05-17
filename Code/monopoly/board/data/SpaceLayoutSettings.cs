using System;
using Sandbox;

public static class SpaceLayoutSettings
{
	public static readonly float DefaultScale = 0.009f;
	public static readonly float WorldPanelLabelZOffset = 0.75f;
	public static readonly float WorldPanelLabelPixelsPerWorldUnit = 64f;

	public static readonly Vector2 WorldPanelSizePixels = new(180,300);
	public static readonly float WorldPanelLabelRenderScale = 0.53f;

	// 2
	// pos -0.7099998,0.490000159,0.749999762
	// rot 0.49999997,-0.49999997,0.49999997,0.49999997

	// 3
	// pos 2.20000005,0,0.749999762
	// rot 0,-0.707106769,0,0.707106769

	// 4
	// pos -1.39999998,-0.880000353,0.749999762
	// rot -0.49999997,-0.49999997,-0.49999997,0.49999997

	//  pos
	//  rot

	public static Vector3[] WorldLabelPositions =
	{
		new (-1.39999998f, 0, 0.749999762f),
		new (-0.7099998f, 0.490000159f, 0.749999762f),
		new (2.20000005f, 0, 0.749999762f),
		new (-1.39999998f, -0.880000353f, 0.749999762f)
	};

	public static readonly List<Rotation> WorldLabelRotations =
	[
		new (0.707106769f, 3.09086197E-08f, 0.707106769f, -3.09086197E-08f),
		new (0.49999997f, -0.49999997f, 0.49999997f, 0.49999997f),
		new (0, -0.707106769f, 0, 0.707106769f),
		new (-0.49999997f, -0.49999997f, -0.49999997f, 0.49999997f)
	];

	public static readonly List<Rotation> WorldLabelRotationsProcedural =
	[
		WorldLabelRotations[0],
		WorldLabelRotations[3],
		WorldLabelRotations[2],
		WorldLabelRotations[1]
	];

	public static readonly Vector3 WorldLabelLocalPosition = new (-1.39999998f, 0, 0.749999762f);
	public static readonly Rotation WorldLabelLocalRotation = new (0.707106769f, 3.09086197E-08f, 0.707106769f, -3.09086197E-08f);

	public static readonly Vector3 FirstQuadrantLocalPosition = new( 2.79999804f, 0, 0.5f );
	public static readonly Rotation FirstQuadrantLocalRotation = Rotation.FromPitch( 90f );

	public static readonly Vector3 SecondQuadrantLocalPosition = new(0f, -2.79999995f, 0.5f);
	public static readonly Rotation SecondQuadrantLocalRotation = new(0.5f,0.49999994f,-0.5f,0.49999994f);

	public static readonly Vector3 ThirdQuadrantLocalPosition = new(-2.79999995f, 0, 0.5f);
	public static readonly Rotation ThirdQuadrantLocalRotation = new(0.707106769f, -3.09086197E-08f, -0.707106769f, -3.09086197E-08f);

	public static readonly Vector3 FourthQuadrantLocalPosition = new(0, 4.39999056f, 0.5f);
	public static readonly Rotation FourthQuadrantLocalRotation = new(-0.49999997f, 0.49999997f, 0.49999997f, 0.49999997f);

	public static readonly Vector3 HitboxSize = Vector3.One * new Vector3(7f, 12f, 4f);
	public static readonly Vector3 CornerHitboxSize = Vector3.One * new Vector3(13f, 13f, 4f);
	
}
