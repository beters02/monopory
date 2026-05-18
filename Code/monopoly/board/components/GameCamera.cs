using System;
using Sandbox;

public enum BoardCameraMode
{
	Default,
	Board,
	FreeCam
}

public sealed class GameCamera : Component
{
	private static GameCamera instance;
	public static GameCamera Instance => instance;

	[Property] public GameObject Target { get; set; }

	[Property] public float Distance { get; set; } = 900f;
	[Property] public float FreecamModeStartDistance = 400f;
	[Property] public float DefaultModeDistance { get; set; } = 200f;
	[Property] public float Pitch { get; set; } = 60f;
	[Property] public float DefaultModePitch { get; set; } = 75f;
	[Property] public float Fov { get; set; } = 35f;
	[Property] public float FollowLerpSpeed { get; set; } = 12f;
	[Property] public float RotationLerpSpeed { get; set; } = 12f;
	[Property] public float FreeCamMoveSpeed { get; set; } = 120f;
	[Property] public float FreeCamZoomSpeed { get; set; } = 80f;
	[Property] public float FreeCamMinDistance { get; set; } = 250f;
	[Property] public float FreeCamMaxDistance { get; set; } = 1200f;
	[Property] public float FreeCamBoundsPadding { get; set; } = 48f;

	public BoardCameraMode Mode { get; private set; } = BoardCameraMode.Default;

	private Vector3 freeCamFocus;
	private float freeCamDistance;
	private bool hasFreeCamFocus;

	protected override void OnStart()
	{
		instance = this;

		CameraComponent camera = Components.Get<CameraComponent>();

		if ( camera != null )
		{
			camera.FieldOfView = Fov;
		}

		freeCamDistance = Distance;
	}

	protected override void OnUpdate()
	{
		if ( Mode == BoardCameraMode.FreeCam )
		{
			UpdateFreeCam();
			return;
		}

		var center = GetModeFocus();
		var yaw = Mode == BoardCameraMode.Default
			? GetCurrentTokenYaw()
			: 0f;

		ApplyView( center, Mode == BoardCameraMode.Default ? DefaultModeDistance : Distance, yaw, true );
	}

	public void SetMode( BoardCameraMode mode )
	{
		if ( Mode == mode )
			return;

		Mode = mode;

		if ( mode == BoardCameraMode.FreeCam )
		{
			freeCamFocus = GetModeFocus();
			freeCamDistance = FreecamModeStartDistance;
			hasFreeCamFocus = true;
		}
	}

	private void UpdateFreeCam()
	{
		if ( !hasFreeCamFocus )
		{
			freeCamFocus = GetModeFocus();
			freeCamDistance = Distance;
			hasFreeCamFocus = true;
		}

		var move = GetFreeCamMoveInput();
		if ( move.Length > 0f )
		{
			var speed = FreeCamMoveSpeed * Time.Delta;
			freeCamFocus += move.Normal * speed;
			freeCamFocus = ClampFocusToBoard( freeCamFocus );
		}

		var wheel = Input.MouseWheel.y;
		if ( Math.Abs( wheel ) > 0f )
		{
			freeCamDistance = MathX.Clamp(
				freeCamDistance - wheel * FreeCamZoomSpeed,
				FreeCamMinDistance,
				FreeCamMaxDistance
			);
		}

		ApplyView( freeCamFocus, freeCamDistance, 0f, true );
	}

	private Vector3 GetFreeCamMoveInput()
	{
		var move = Vector3.Zero;

		if ( Input.Down( "Forward" ) )
			move += Vector3.Forward;

		if ( Input.Down( "Backward" ) )
			move += Vector3.Backward;

		if ( Input.Down( "Left" ) )
			move += Vector3.Left;

		if ( Input.Down( "Right" ) )
			move += Vector3.Right;

		return move;
	}

	private Vector3 GetModeFocus()
	{
		if ( Mode == BoardCameraMode.Default )
		{
			var player = GameController.Instance?.CurrentPlayer;
			var tokenPosition = GetTokenPosition( player );
			if ( tokenPosition.HasValue )
				return tokenPosition.Value;

			if ( player is not null )
				return Board.Instance?.GetSpacePosition( player.SpaceIndex ) ?? Vector3.Zero;
		}

		if ( Target is not null )
			return Target.WorldPosition;

		return GetBoardCenter();
	}

	private Vector3? GetTokenPosition( PlayerState player )
	{
		if ( player is null )
			return null;

		foreach ( var token in Scene.GetAllComponents<PlayerToken>() )
		{
			if ( token?.PlayerState != player )
				continue;

			return token.GameObject.WorldPosition;
		}

		return null;
	}

	private Vector3 GetBoardCenter()
	{
		var board = Board.Instance;
		if ( board?.Spaces is null || board.Spaces.Count == 0 )
			return Vector3.Zero;

		var min = new Vector3( float.MaxValue, float.MaxValue, float.MaxValue );
		var max = new Vector3( float.MinValue, float.MinValue, float.MinValue );

		foreach ( var space in board.Spaces )
		{
			if ( space is null )
				continue;

			var position = space.TokenPosition;
			min = Vector3.Min( min, position );
			max = Vector3.Max( max, position );
		}

		return (min + max) * 0.5f;
	}

	private Vector3 ClampFocusToBoard( Vector3 focus )
	{
		var board = Board.Instance;
		if ( board?.Spaces is null || board.Spaces.Count == 0 )
			return focus;

		var min = new Vector3( float.MaxValue, float.MaxValue, focus.z );
		var max = new Vector3( float.MinValue, float.MinValue, focus.z );

		foreach ( var space in board.Spaces )
		{
			if ( space is null )
				continue;

			var position = space.TokenPosition;
			min.x = Math.Min( min.x, position.x );
			min.y = Math.Min( min.y, position.y );
			max.x = Math.Max( max.x, position.x );
			max.y = Math.Max( max.y, position.y );
		}

		min.x -= FreeCamBoundsPadding;
		min.y -= FreeCamBoundsPadding;
		max.x += FreeCamBoundsPadding;
		max.y += FreeCamBoundsPadding;

		focus.x = MathX.Clamp( focus.x, min.x, max.x );
		focus.y = MathX.Clamp( focus.y, min.y, max.y );
		return focus;
	}

	private float GetCurrentTokenYaw()
	{
		var player = GameController.Instance?.CurrentPlayer;
		if ( player is null )
			return 0f;

		return Board.GetSpaceQuadrantIncludeCorners( player.SpaceIndex ) switch
		{
			1 => 0f,
			2 => -90f,
			3 => 180f,
			_ => 90f
		};
	}

	private void ApplyView( Vector3 center, float distance, float yaw, bool smooth )
	{
		var rotation = Rotation.From( Mode == BoardCameraMode.Default ? DefaultModePitch : Pitch, yaw, 0f );
		var offset = -rotation.Forward * distance;
		var targetPosition = center + offset;

		GameObject.WorldPosition = smooth
			? GameObject.WorldPosition.LerpTo( targetPosition, Time.Delta * FollowLerpSpeed )
			: targetPosition;
		GameObject.WorldRotation = smooth
			? GameObject.WorldRotation.LerpTo( rotation, Time.Delta * RotationLerpSpeed )
			: rotation;
	}
}
