using System;
using Sandbox;

public enum BoardCameraMode
{
	Default,
	Board,
	FreeCam,
	Token
}

public sealed class GameCamera : Component
{
	private static GameCamera instance;
	public static GameCamera Instance => instance;

	[Property] public GameObject Target { get; set; }
	[Property] public float Distance { get; set; } = 900f;
	[Property] public float FreecamModeStartDistance = 400f;
	[Property] public float DefaultModeDistance { get; set; } = 168f;
	[Property] public float Pitch { get; set; } = 60f;
	[Property] public float DefaultModePitch { get; set; } = 43f;
	[Property] public float Fov { get; set; } = 58f;
	[Property] public float FollowLerpSpeed { get; set; } = 3f;
	[Property] public float RotationLerpSpeed { get; set; } = 2.5f;
	[Property] public float FreeCamMoveSpeed { get; set; } = 120f;
	[Property] public float FreeCamZoomSpeed { get; set; } = 80f;
	[Property] public float FreeCamMinDistance { get; set; } = 75f;
	[Property] public float FreeCamMaxDistance { get; set; } = 1200f;
	[Property] public float FreeCamBoundsPadding { get; set; } = 48f;
	public float TokenModeDistance { get; set; } = 48f;
	public float TokenModeHeight { get; set; } = 14f;
	public float TokenModePitch { get; set; } = 12f;
	[Property] public float TokenModeMinPitch { get; set; } = -12f;
	[Property] public float TokenModeMaxPitch { get; set; } = 55f;
	[Property] public float TokenModeLookSensitivity { get; set; } = 1f;
	[Property] public bool AutoExpsureEnabled { get; set; } = false;

	public float DiceFramingPadding { get; set; } = 30f;
	public float DiceFramingMinDistance { get; set; } = 250f;
	public float DefaultModeDicePitch { get; set; } = 75f;
	[Property] public float DiceFollowLerpSpeed = 4f;
	[Property] public float DiceRotationLerpSpeed = 3.5f;


	public BoardCameraMode Mode { get; private set; } = BoardCameraMode.Default;
	public bool IsTokenModeRequested => requestedMode == BoardCameraMode.Token;
	public bool IsTokenCameraActive => Mode == BoardCameraMode.Token;
	public bool IsGameplayMovementLocked { get; private set; }

	private Vector3 freeCamFocus;
	private float freeCamDistance;
	private bool hasFreeCamFocus;
	private BoardCameraMode requestedMode = BoardCameraMode.Default;
	private float tokenCameraYaw;
	private float tokenCameraPitch;
	private bool didHideMouse;

	private CameraComponent cameraComponent;

	protected override void OnStart()
	{
		instance = this;
		cameraComponent = GetComponentInChildren<CameraComponent>();
		freeCamDistance = Distance;
		cameraComponent?.FieldOfView = Fov;
	}

	protected override void OnDestroy()
	{
		ReleaseTokenMouse();
	}

	protected override void OnUpdate()
	{
		UpdateRequestedModeAvailability();

		if ( TryUpdateGambleCamera() )
			return;

		if ( Mode == BoardCameraMode.FreeCam )
		{
			UpdateFreeCam();
			return;
		}

		if ( Mode == BoardCameraMode.Token )
		{
			UpdateTokenCamera();
			return;
		}

		UpdateStandardCamera( Mode );

		cameraComponent?.AutoExposure.Enabled = AutoExpsureEnabled;
		//cameraComponent?.FieldOfView = Fov;
	}

	public void SetMode( BoardCameraMode mode )
	{
		requestedMode = mode;
		ApplyMode( mode );
	}

	private void ApplyMode( BoardCameraMode mode )
	{
		if ( Mode == mode )
			return;
	
		Mode = mode;

		if ( mode == BoardCameraMode.FreeCam )
		{
			freeCamFocus = GetModeFocus( Mode );
			freeCamDistance = FreecamModeStartDistance;
			hasFreeCamFocus = true;
		}
		else if ( mode == BoardCameraMode.Token )
		{
			var token = GetLocalPlayerToken();
			tokenCameraYaw = token is not null ? GetYawFromForward( token.GameObject.WorldRotation.Forward ) : GetCurrentTokenYaw();
			tokenCameraPitch = TokenModePitch;
		}
		else
		{
			ReleaseTokenMouse();
		}
	}

	private void UpdateRequestedModeAvailability()
	{
		if ( requestedMode != BoardCameraMode.Token )
		{
			ApplyMode( requestedMode );
			return;
		}

		var token = GetLocalPlayerToken();
		var desiredMode = token is not null ? BoardCameraMode.Token : BoardCameraMode.Default;
		ApplyMode( desiredMode );
	}

	private bool TryUpdateGambleCamera()
	{
		if ( GameController.Instance?.TryGetLocalGambleCameraTarget( out var station ) != true )
		{
			IsGameplayMovementLocked = false;
			return false;
		}

		IsGameplayMovementLocked = true;
		ReleaseTokenMouse();
		ApplyGambleView( station );
		return true;
	}

	private void ApplyGambleView( GambleStation station )
	{
		var pitch = MathX.Clamp( station.CameraPitch, -89.9f, 89.9f ).DegreeToRadian();
		var yaw = station.CameraYaw.DegreeToRadian();
		var horizontal = MathF.Cos( pitch );
		var viewDirection = new Vector3(
			horizontal * MathF.Cos( yaw ),
			horizontal * MathF.Sin( yaw ),
			-MathF.Sin( pitch )
		).Normal;

		var targetPosition = station.FocusPosition - viewDirection * station.CameraDistance;
		var upReference = Math.Abs( Vector3.Dot( viewDirection, Vector3.Up ) ) > 0.98f
			? Vector3.Forward
			: Vector3.Up;
		var targetRotation = Rotation.LookAt( viewDirection, upReference );

		GameObject.WorldPosition = GameObject.WorldPosition.LerpTo(
			targetPosition,
			Time.Delta * FollowLerpSpeed
		);
		GameObject.WorldRotation = GameObject.WorldRotation.LerpTo(
			targetRotation,
			Time.Delta * RotationLerpSpeed
		);
	}

	private void UpdateFreeCam()
	{
		if ( !hasFreeCamFocus )
		{
			freeCamFocus = GetModeFocus( Mode );
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

		ApplyView( freeCamFocus, freeCamDistance, 0f, Pitch, FollowLerpSpeed, RotationLerpSpeed, true );
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

	private void UpdateTokenCamera()
	{
		var token = GetLocalPlayerToken();
		if ( token is null )
		{
			ApplyMode( BoardCameraMode.Default );
			return;
		}

		if ( !token.IsLocallyControllable )
		{
			ReleaseTokenMouse();
			UpdateStandardCamera( BoardCameraMode.Default );
			return;
		}

		var isTokenUiMouseReleased = token.ShouldReleaseTokenMouseForTokenUi;
		if ( isTokenUiMouseReleased )
			ReleaseTokenMouse();
		else
			CaptureTokenMouse();

		var look = isTokenUiMouseReleased ? Angles.Zero : Input.AnalogLook * TokenModeLookSensitivity;
		tokenCameraYaw += look.yaw;
		tokenCameraPitch = MathX.Clamp( tokenCameraPitch + look.pitch, TokenModeMinPitch, TokenModeMaxPitch );

		var rotation = Rotation.From( tokenCameraPitch, tokenCameraYaw, 0f );
		var focus = token.GameObject.WorldPosition + Vector3.Up * TokenModeHeight;

		GameObject.WorldPosition = focus - rotation.Forward * TokenModeDistance;
		GameObject.WorldRotation = rotation;
	}

	private void UpdateStandardCamera( BoardCameraMode effectiveMode )
	{
		var center = GetModeFocus( effectiveMode );
		var yaw = effectiveMode == BoardCameraMode.Default
			? GetCurrentTokenYaw()
			: 0f;
		var distance = effectiveMode == BoardCameraMode.Default ? DefaultModeDistance : Distance;
		float pitch = effectiveMode == BoardCameraMode.Default ? DefaultModePitch : Pitch;
		var followLerpSpeed = FollowLerpSpeed;
		var rotLerpSpeed = RotationLerpSpeed;

		if ( effectiveMode == BoardCameraMode.Default )
		{
			var controller = GameController.Instance;
			if ( controller?.IsResolvingPhysicalDice == true &&
				controller.TryGetPhysicalDice( out var dieA, out var dieB ) )
			{
				distance = GetDiceFramingDistance( center, yaw, dieA.GameObject.WorldPosition, dieB.GameObject.WorldPosition );
				pitch = DefaultModeDicePitch;
				followLerpSpeed = DiceFollowLerpSpeed;
				rotLerpSpeed = DiceRotationLerpSpeed;
			}
		}

		ApplyView( center, distance, yaw, pitch, followLerpSpeed, rotLerpSpeed, true );
	}

	private void CaptureTokenMouse()
	{
		Mouse.Visibility = MouseVisibility.Hidden;
		didHideMouse = true;
	}

	private void ReleaseTokenMouse()
	{
		if ( !didHideMouse )
			return;

		Mouse.Visibility = MouseVisibility.Visible;
		didHideMouse = false;
	}

	private PlayerToken GetLocalPlayerToken()
	{
		if ( Scene is null )
			return null;

		foreach ( var token in Scene.GetAllComponents<PlayerToken>() )
		{
			if ( token?.IsLocalPlayerToken == true )
				return token;
		}

		return null;
	}

	private static float GetYawFromForward( Vector3 forward )
	{
		if ( forward.Length <= 0.001f )
			return 0f;

		return MathF.Atan2( forward.y, forward.x ) * 180f / MathF.PI;
	}

	private Vector3 GetModeFocus( BoardCameraMode effectiveMode )
	{
		if ( effectiveMode == BoardCameraMode.Default )
		{
			var controller = GameController.Instance;
			if ( controller?.IsResolvingPhysicalDice == true &&
				controller.TryGetPhysicalDice( out var dieA, out var dieB ) )
			{
				return (dieA.GameObject.WorldPosition + dieB.GameObject.WorldPosition) * 0.5f;
			}

			var player = controller?.CurrentPlayer;
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

	private float GetDiceFramingDistance( Vector3 center, float yaw, Vector3 dieAPosition, Vector3 dieBPosition )
	{
		var rotation = Rotation.From( DefaultModeDicePitch, yaw, 0f );
		var right = rotation.Right;
		var up = rotation.Up;

		var localA = dieAPosition - center;
		var localB = dieBPosition - center;

		var halfWidth = Math.Max( Math.Abs( Vector3.Dot( localA, right ) ), Math.Abs( Vector3.Dot( localB, right ) ) ) + DiceFramingPadding;
		var halfHeight = Math.Max( Math.Abs( Vector3.Dot( localA, up ) ), Math.Abs( Vector3.Dot( localB, up ) ) ) + DiceFramingPadding;

		var verticalFovRadians = Fov.DegreeToRadian();
		var aspect = Math.Max( 0.01f, Screen.Width / (float)Math.Max( 1, Screen.Height ) );
		var horizontalFovRadians = 2f * MathF.Atan( MathF.Tan( verticalFovRadians * 0.5f ) * aspect );

		var requiredByHeight = halfHeight / Math.Max( 0.01f, MathF.Tan( verticalFovRadians * 0.5f ) );
		var requiredByWidth = halfWidth / Math.Max( 0.01f, MathF.Tan( horizontalFovRadians * 0.5f ) );
		var requiredDistance = Math.Max( requiredByWidth, requiredByHeight );

		return Math.Max( Math.Max( DefaultModeDistance, DiceFramingMinDistance ), requiredDistance );
	}

	private void ApplyView( Vector3 center, float distance, float yaw, float pitch, float followLerpSpeed, float rotLerpSpeed, bool smooth )
	{
		var rotation = Rotation.From( pitch, yaw, 0f );
		var offset = -rotation.Forward * distance;
		var targetPosition = center + offset;

		GameObject.WorldPosition = smooth
			? GameObject.WorldPosition.LerpTo( targetPosition, Time.Delta * followLerpSpeed )
			: targetPosition;
		GameObject.WorldRotation = smooth
			? GameObject.WorldRotation.LerpTo( rotation, Time.Delta * rotLerpSpeed )
			: rotation;
	}
}
