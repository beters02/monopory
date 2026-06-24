using System;
using Sandbox;

public sealed class DiceComponent : Component, Component.ICollisionListener
{
	[RequireComponent] public Rigidbody Body { get; set; }

	[Property] public int UpFaceValue { get; set; } = 1;
	[Property] public int DownFaceValue { get; set; } = 6;
	[Property] public int ForwardFaceValue { get; set; } = 2;
	[Property] public int BackFaceValue { get; set; } = 5;
	[Property] public int RightFaceValue { get; set; } = 3;
	[Property] public int LeftFaceValue { get; set; } = 4;
	[Property] public float SettledLinearSpeed { get; set; } = 2f;
	[Property] public float SettledAngularSpeed { get; set; } = 4f;
	[Property] public float SettledStillSeconds { get; set; } = 0.35f;
	[Property, Group( "Audio" )] public bool EnableCollisionAudio { get; set; } = true;
	public GameSound CollisionSound = GameAssets.Sounds.DiceImpact;
	[Property, Group( "Audio" )] public float CollisionSoundMinSpeed { get; set; } = 5f;
	[Property, Group("Audio")] public float CollisionSoundVolumeMinSpeed { get; set; } = 20f;
	[Property, Group("Audio")] public float CollisionSoundVolumeMaxSpeed { get; set; } = 130f;
	[Property, Group( "Audio" )] public float CollisionSoundCooldown { get; set; } = 0.12f;

	private Vector3 startingPosition;
	private Rotation startingRotation;
	private float stillTime;
	private float nextCollisionSoundTime;

	public bool IsNetworkOwner => GameObject?.Network.IsOwner == true;

	protected override void OnStart()
	{
		startingPosition = GameObject.WorldPosition;
		startingRotation = GameObject.WorldRotation;
	}

	public void Throw( Vector3 position, Rotation rotation, Vector3 velocity, Vector3 angularVelocity )
	{
		if ( Body is null )
			Body = Components.Get<Rigidbody>();

		GameObject.WorldPosition = position;
		GameObject.WorldRotation = rotation;
		stillTime = 0f;
		nextCollisionSoundTime = 0f;

		if ( Body is null )
			return;

		Body.MotionEnabled = true;
		Body.Velocity = velocity;
		Body.AngularVelocity = angularVelocity;
	}

	void Component.ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( !EnableCollisionAudio || !CollisionSound.IsAssigned )
			return;

		if ( Time.Now < nextCollisionSoundTime )
			return;

		if ( Body is null )
			Body = Components.Get<Rigidbody>();

		var impactSpeed = Body?.Velocity.Length ?? 0f;
		if ( impactSpeed < CollisionSoundMinSpeed )
			return;
		
		var mult = Math.Clamp(impactSpeed / CollisionSoundVolumeMaxSpeed, 0.2f, 1f);
		SoundHandle handle = CollisionSound.PlayWithHandle();
		handle.Volume *= mult;
		nextCollisionSoundTime = Time.Now + CollisionSoundCooldown;
	}

	void Component.ICollisionListener.OnCollisionUpdate( Collision collision )
	{
	}

	void Component.ICollisionListener.OnCollisionStop( CollisionStop collision )
	{
	}

	public void ResetToStart()
	{
		Throw( startingPosition, startingRotation, Vector3.Zero, Vector3.Zero );
	}

	public bool IsSettled()
	{
		if ( Body is null )
			Body = Components.Get<Rigidbody>();

		if ( Body is null )
			return true;

		var isSlow =
			Body.Velocity.Length <= SettledLinearSpeed &&
			Body.AngularVelocity.Length <= SettledAngularSpeed;

		stillTime = isSlow ? stillTime + Time.Delta : 0f;
		return stillTime >= SettledStillSeconds;
	}

	public int GetTopFaceValue()
	{
		var rotation = GameObject.WorldRotation;
		var bestValue = UpFaceValue;
		var bestDot = float.MinValue;

		CheckFace( rotation.Up, UpFaceValue, ref bestValue, ref bestDot );
		CheckFace( -rotation.Up, DownFaceValue, ref bestValue, ref bestDot );
		CheckFace( rotation.Forward, ForwardFaceValue, ref bestValue, ref bestDot );
		CheckFace( -rotation.Forward, BackFaceValue, ref bestValue, ref bestDot );
		CheckFace( rotation.Right, RightFaceValue, ref bestValue, ref bestDot );
		CheckFace( -rotation.Right, LeftFaceValue, ref bestValue, ref bestDot );

		return Math.Clamp( bestValue, 1, 6 );
	}

	private static void CheckFace( Vector3 worldDirection, int value, ref int bestValue, ref float bestDot )
	{
		var dot = Vector3.Dot( worldDirection.Normal, Vector3.Up );
		if ( dot <= bestDot )
			return;

		bestDot = dot;
		bestValue = value;
	}
}
