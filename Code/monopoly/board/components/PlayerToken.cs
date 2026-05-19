using System;
using System.Numerics;
using Sandbox;

public sealed class PlayerToken : Component
{

	public Board Board {get; set;}
	public PlayerState PlayerState;

	[Property] public float HeightOffset { get; set; } = 4f;
	[Property] public float MoveSpeed { get; set; } = 15f;
	[Property] public float RotationSpeed {get; set;} = 10f;
	[Property] public string WalkingParameterName { get; set; } = "Walking";
	[Property] public string XDirectionParameterName { get; set; } = "XDirection";

	private SkinnedModelRenderer renderer;
	private bool walkingAnim;
	private bool turningAnim;
	private bool hasAppliedWalkingAnim;
	private bool requestedWalking;
	private Vector3 lastXDirection = new(0, 1, 0);

	protected override void OnStart()
	{
		renderer = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
	}

	protected override void OnUpdate()
	{
		if ( Board is null || PlayerState is null )
			return;

		var target = Board.GetSpacePosition( PlayerState.SpaceIndex ) + Vector3.Up * HeightOffset;
		var targetRot = GetRotation( PlayerState.SpaceIndex );
		var isMoving = !GameObject.WorldPosition.AlmostEqual( target, 0.1f );

        if (isMoving)
        {
            GameObject.WorldPosition = GameObject.WorldPosition.LerpTo(
                target,
                Time.Delta * MoveSpeed
            );
        }

		if (GameObject.LocalRotation != targetRot)
		{
			GameObject.LocalRotation = GameObject.LocalRotation.LerpTo(
				targetRot,
				Time.Delta * RotationSpeed
			);
		}

		ApplyWalkingAnim( requestedWalking || isMoving );
		//ApplyDirection(targetRot.Forward);
	}

	private static Rotation GetRotation( int index )
	{
		int quad = Board.GetSpaceQuadrantIncludeCorners( index );
		return quad switch
		{
			1 => Rotation.From(0, 90, 0),
			2 => Rotation.From(0, 0, 0),
			3 => Rotation.From(1, -90, 1),
			_ => Rotation.From(0, 180, 0)
		};
	}

	public void SetWalkingAnim( bool walking )
	{
		requestedWalking = walking;
		ApplyWalkingAnim( walking );
	}

	private void ApplyWalkingAnim( bool walking )
	{
		if ( walkingAnim == walking && hasAppliedWalkingAnim )
			return;

		walkingAnim = walking;

		if ( renderer is null )
			renderer = GameObject.GetComponentInChildren<SkinnedModelRenderer>();

		if ( renderer is null || string.IsNullOrWhiteSpace( WalkingParameterName ) )
			return;

		renderer.Set( WalkingParameterName, walking );
		hasAppliedWalkingAnim = true;
	}

	private void ApplyDirection( Vector3 dir )
	{
		if (renderer is null)
			return;

		if (turningAnim)
		{
			turningAnim = false;
			renderer.Set( XDirectionParameterName, false );
			return;
		}

		Log.Info(dir);

		if ( dir != lastXDirection )
		{
			lastXDirection = dir;
			turningAnim = true;
			renderer.Set( XDirectionParameterName, true );
		}
	}

}
