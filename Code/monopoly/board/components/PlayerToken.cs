using System;
using System.Diagnostics;
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
	[Property] public bool EnablePhysicsTestGrab { get; set; } = true;
	[Property] public float ThrowSpeedScale { get; set; } = 1.1f;
	[Property] public float ThrowDamping { get; set; } = 4f;
	[Property] public float ThrowStopSpeed { get; set; } = 8f;
	[Property] public float GrabPlanePadding { get; set; } = 6f;
	[Property] public float GrabRadius { get; set; } = 36f;
	[Property] public float CollisionRadius { get; set; } = 24f;
	[Property] public float CollisionRestitution { get; set; } = 0.75f;
	[Property] public float CollisionImpulseScale { get; set; } = 0.85f;
	[Property] public bool ReturnToSpaceAfterPhysics { get; set; } = true;
	[Property] public float SharedSpaceOffsetDistance { get; set; } = 10f;
	[Property] public float TokenControlGroundMoveSpeed { get; set; } = 95f;
	[Property] public float TokenControlAirMoveSpeed { get; set; } = 95f;
	[Property] public float TokenControlTurnSpeed { get; set; } = 12f;
	[Property] public float TokenControlGroundAcceleration { get; set; } = 900f;
	[Property] public float TokenControlAirAcceleration { get; set; } = 120f;
	[Property] public float TokenControlFriction { get; set; } = 8f;
	[Property] public float TokenControlStopSpeed { get; set; } = 35f;
	[Property] public float TokenControlGravity { get; set; } = 800f;
	[Property] public float TokenControlJumpSpeed { get; set; } = 260f;
	[Property] public float TokenControlGroundSnapDistance { get; set; } = 8f;
	[Property] public float TokenControlHullRadius { get; set; } = 12f;
	[Property] public float TokenControlHullHeight { get; set; } = 36f;
	[Property] public int TokenControlMaxSlideBumps { get; set; } = 4;
	[Property] public float TokenControlReplicationRate { get; set; } = 20f;
	[Property] public float TokenControlRemoteTimeout { get; set; } = 0.35f;
	[Property] public float TokenControlRemoteLerpSpeed { get; set; } = 18f;
	public float TokenControlBoardPadding { get; set; } = 20f;
	[Property] public float BoardSpotSilhouetteAlpha { get; set; } = 0.28f;

	private SkinnedModelRenderer renderer;
	private ModelRenderer markerRenderer;
	private GameObject markerObject;
	private HighlightOutline markerHighlight;
	private bool hasOriginalPieceMaterialOverrides;
	private Material originalModelMaterialOverride;
	private Material originalSkinnedMaterialOverride;
	private bool walkingAnim;
	private bool hasAppliedWalkingAnim;
	private bool requestedWalking;
	private bool isGrabbed;
	private bool isThrowing;
	private Vector3 grabOffset;
	private Vector3 lastGrabPosition;
	private Vector3 throwVelocity;
	private bool isReplicatedPhysics;
	private string activeReplicatedPhysicsKey;
	private string completedReplicatedPhysicsKey;
	private PieceDefinition currentPieceDefinition;
	private Color currentPlayerColor = Color.White;
	private GameObject boardSpotSilhouetteObject;
	private ModelRenderer boardSpotSilhouetteRenderer;
	private HighlightOutline boardSpotSilhouetteHighlight;
	private Vector3 tokenControlVelocity;
	private bool tokenControlGrounded;
	private float lastTokenControlReplicatedAt;
	private Vector3 remoteTokenControlPosition;
	private Rotation remoteTokenControlRotation;
	private Vector3 remoteTokenControlVelocity;
	private bool remoteTokenControlWalking;
	private float remoteTokenControlActiveUntil;

	public bool IsLocalPlayerToken => PlayerState?.IsOwner == true;
	public bool IsLocallyControllable => CanUseTokenController();


	protected override void OnStart()
	{
		renderer = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		EnsurePlayerMarker();
	}

	protected override void OnDestroy()
	{
		DestroyBoardSpotSilhouette();
	}

	protected override void OnUpdate()
	{
		UpdatePhysicsTestGrab();

		if ( isGrabbed || isThrowing )
		{
			ApplyWalkingAnim( false );
			return;
		}

		if ( Board is null || PlayerState is null )
			return;
			

		var target = GetSpaceTargetPosition();
		var targetRot = GetRotation( PlayerState.SpaceIndex );

		if ( IsLocallyControllable )
		{
			UpdateBoardSpotSilhouette( target, targetRot );
			UpdateLocalTokenController();
			return;
		}

		DestroyBoardSpotSilhouette();
		tokenControlVelocity = Vector3.Zero;
		tokenControlGrounded = false;

		if ( IsRemoteTokenControlActive() )
		{
			UpdateRemoteTokenControl();
			return;
		}

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

	private bool CanUseTokenController()
	{
		if ( Board is null || PlayerState is null || PlayerState.IsBankrupt || !IsLocalPlayerToken )
			return false;

		var game = GameController.Instance;
		if ( game is null || game.MatchState != MatchLifecycleState.InGame )
			return false;

		if ( game.CurrentPlayer == PlayerState )
			return false;

		if ( GameCamera.Instance?.IsTokenModeRequested != true )
			return false;

		if ( game.HasBlockingPopup )
			return false;

		return !isGrabbed && !isThrowing;
	}

	private void UpdateLocalTokenController()
	{
		var input = GetTokenMoveInput();
		var move = GetCameraRelativeMove( input );
		var wishDirection = move.Length > 0.001f ? move.Normal : Vector3.Zero;

		UpdateTokenGroundState();
		var maxWishSpeed = tokenControlGrounded ? TokenControlGroundMoveSpeed : TokenControlAirMoveSpeed;
		var wishSpeed = maxWishSpeed * MathX.Clamp( input.Length, 0f, 1f );

		if ( tokenControlGrounded )
		{
			ApplyTokenGroundFriction();

			if ( Input.Pressed( "Jump" ) )
			{
				tokenControlVelocity.z = TokenControlJumpSpeed;
				tokenControlGrounded = false;
			}
		}

		AccelerateToken( wishDirection, wishSpeed, tokenControlGrounded ? TokenControlGroundAcceleration : TokenControlAirAcceleration );

		if ( !tokenControlGrounded )
			tokenControlVelocity.z -= TokenControlGravity * Time.Delta;

		MoveTokenWithSlide( tokenControlVelocity * Time.Delta );

		if ( tokenControlGrounded && tokenControlVelocity.z < 0f )
			tokenControlVelocity.z = 0f;

		var horizontalVelocity = tokenControlVelocity.WithZ( 0f );
		if ( horizontalVelocity.Length > 1f )
		{
			var targetYaw = MathF.Atan2( horizontalVelocity.y, horizontalVelocity.x ) * 180f / MathF.PI;
			GameObject.WorldRotation = GameObject.WorldRotation.LerpTo(
				Rotation.From( 0f, targetYaw, 0f ),
				Time.Delta * TokenControlTurnSpeed
			);
		}

		ApplyWalkingAnim( horizontalVelocity.Length > 2f );
		PublishTokenControlTransform( horizontalVelocity.Length > 2f );
	}

	private void PublishTokenControlTransform( bool walking )
	{
		if ( GameController.Instance is null )
			return;

		var interval = TokenControlReplicationRate > 0f ? 1f / TokenControlReplicationRate : 0f;
		if ( interval > 0f && Time.Now - lastTokenControlReplicatedAt < interval )
			return;

		lastTokenControlReplicatedAt = Time.Now;
		GameController.Instance.RequestTokenControlTransform(
			GameObject.WorldPosition,
			GameObject.WorldRotation,
			tokenControlVelocity,
			walking
		);
	}

	public void ApplyReplicatedTokenControlTransform( int playerIndex, Vector3 position, Rotation rotation, Vector3 velocity, bool walking )
	{
		if ( IsLocalPlayerToken || GameController.Instance is null || PlayerState is null )
			return;

		if ( GameController.Instance.GetPlayerIndex( PlayerState ) != playerIndex )
			return;

		remoteTokenControlPosition = position;
		remoteTokenControlRotation = rotation;
		remoteTokenControlVelocity = velocity;
		remoteTokenControlWalking = walking;
		remoteTokenControlActiveUntil = Time.Now + Math.Max( TokenControlRemoteTimeout, 0.05f );
	}

	private bool IsRemoteTokenControlActive()
	{
		return remoteTokenControlActiveUntil > Time.Now;
	}

	private void UpdateRemoteTokenControl()
	{
		var lerpAmount = Time.Delta * TokenControlRemoteLerpSpeed;
		GameObject.WorldPosition = GameObject.WorldPosition.LerpTo( remoteTokenControlPosition, lerpAmount );
		GameObject.WorldRotation = GameObject.WorldRotation.LerpTo( remoteTokenControlRotation, lerpAmount );
		ApplyWalkingAnim( remoteTokenControlWalking || remoteTokenControlVelocity.WithZ( 0f ).Length > 2f );
	}

	private Vector2 GetTokenMoveInput()
	{
		var analog = Input.AnalogMove;
		var input = new Vector2( -analog.y, analog.x );

		if ( input.Length <= 0.001f )
		{
			if ( Input.Down( "Forward" ) )
				input.y += 1f;

			if ( Input.Down( "Backward" ) )
				input.y -= 1f;

			if ( Input.Down( "Right" ) )
				input.x += 1f;

			if ( Input.Down( "Left" ) )
				input.x -= 1f;
		}

		return input.Length > 1f ? input.Normal : input;
	}

	private Vector3 GetCameraRelativeMove( Vector2 input )
	{
		var cameraRotation = Scene?.Camera?.GameObject.WorldRotation ?? GameObject.WorldRotation;
		var forward = cameraRotation.Forward.WithZ( 0f );
		var right = cameraRotation.Right.WithZ( 0f );

		if ( forward.Length <= 0.001f )
			forward = GameObject.WorldRotation.Forward.WithZ( 0f );

		if ( right.Length <= 0.001f )
			right = GameObject.WorldRotation.Right.WithZ( 0f );

		return forward.Normal * input.y + right.Normal * input.x;
	}

	private void UpdateTokenGroundState()
	{
		var groundHeight = GetSpaceTargetPosition().z;
		var wasGrounded = tokenControlGrounded;
		var distanceToGround = GameObject.WorldPosition.z - groundHeight;
		tokenControlGrounded = distanceToGround <= TokenControlGroundSnapDistance && tokenControlVelocity.z <= 0f;

		if ( tokenControlGrounded )
		{
			GameObject.WorldPosition = GameObject.WorldPosition.WithZ( groundHeight );
			if ( tokenControlVelocity.z < 0f )
				tokenControlVelocity.z = 0f;
		}
		else if ( wasGrounded )
		{
			tokenControlVelocity.z = Math.Min( tokenControlVelocity.z, 0f );
		}
	}

	private void ApplyTokenGroundFriction()
	{
		var horizontalVelocity = tokenControlVelocity.WithZ( 0f );
		var speed = horizontalVelocity.Length;
		if ( speed <= 0.001f )
			return;

		var control = Math.Max( speed, TokenControlStopSpeed );
		var drop = control * TokenControlFriction * Time.Delta;
		var newSpeed = Math.Max( speed - drop, 0f );
		tokenControlVelocity = horizontalVelocity * (newSpeed / speed) + Vector3.Up * tokenControlVelocity.z;
	}

	private void AccelerateToken( Vector3 wishDirection, float wishSpeed, float acceleration )
	{
		if ( wishDirection.Length <= 0.001f || wishSpeed <= 0f || acceleration <= 0f )
			return;

		var currentSpeed = Vector3.Dot( tokenControlVelocity, wishDirection );
		var addSpeed = wishSpeed - currentSpeed;
		if ( addSpeed <= 0f )
			return;

		var accelSpeed = Math.Min( acceleration * Time.Delta * wishSpeed, addSpeed );
		tokenControlVelocity += wishDirection * accelSpeed;
	}

	private void MoveTokenWithSlide( Vector3 move )
	{
		if ( move.Length <= 0.001f )
			return;

		var position = GameObject.WorldPosition;
		var remaining = move;
		var maxBumps = Math.Max( TokenControlMaxSlideBumps, 1 );

		for ( var bump = 0; bump < maxBumps; bump++ )
		{
			var targetPosition = ClampPositionToBoard( position + remaining );
			var trace = TraceTokenHull( position, targetPosition );

			if ( !trace.Hit || trace.StartedSolid )
			{
				position = targetPosition;
				break;
			}

			position = trace.EndPosition;

			var normal = trace.Normal;
			tokenControlVelocity -= normal * Vector3.Dot( tokenControlVelocity, normal );
			remaining -= normal * Vector3.Dot( remaining, normal );
			remaining *= MathX.Clamp( 1f - trace.Fraction, 0f, 1f );

			if ( remaining.Length <= 0.001f )
				break;
		}

		GameObject.WorldPosition = ClampPositionToBoard( position );
	}

	private SceneTraceResult TraceTokenHull( Vector3 startPosition, Vector3 endPosition )
	{
		var hull = GetTokenControlHull();

		if ( Scene is null )
			return default;

		return Scene.Trace
			.Box( hull, startPosition, endPosition )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.Run();
	}

	private BBox GetTokenControlHull()
	{
		var radius = Math.Max( TokenControlHullRadius, 1f );
		var height = Math.Max( TokenControlHullHeight, radius * 2f );
		return new BBox(
			new Vector3( -radius, -radius, 0f ),
			new Vector3( radius, radius, height )
		);
	}

	private Vector3 ClampPositionToBoard( Vector3 position )
	{
		if ( Board?.Spaces is null || Board.Spaces.Count == 0 || TokenControlBoardPadding <= 0f )
			return position;

		var min = new Vector3( float.MaxValue, float.MaxValue, position.z );
		var max = new Vector3( float.MinValue, float.MinValue, position.z );

		foreach ( var space in Board.Spaces )
		{
			if ( space is null )
				continue;

			var spacePosition = space.TokenPosition;
			min.x = Math.Min( min.x, spacePosition.x );
			min.y = Math.Min( min.y, spacePosition.y );
			max.x = Math.Max( max.x, spacePosition.x );
			max.y = Math.Max( max.y, spacePosition.y );
		}

		position.x = MathX.Clamp( position.x, min.x - TokenControlBoardPadding, max.x + TokenControlBoardPadding );
		position.y = MathX.Clamp( position.y, min.y - TokenControlBoardPadding, max.y + TokenControlBoardPadding );
		return position;
	}

	private void UpdateBoardSpotSilhouette( Vector3 targetPosition, Rotation targetRotation )
	{
		EnsureBoardSpotSilhouette();

		if ( boardSpotSilhouetteObject is null )
			return;

		boardSpotSilhouetteObject.WorldPosition = targetPosition;
		boardSpotSilhouetteObject.WorldRotation = targetRotation;
	}

	private void EnsureBoardSpotSilhouette()
	{
		if ( boardSpotSilhouetteObject is not null && boardSpotSilhouetteObject.IsValid() )
			return;

		var piece = currentPieceDefinition ?? PieceCatalog.GetByIdOrDefault( PlayerState?.SelectedPieceId ?? PieceCatalog.DefaultPieceId );
		if ( piece is null )
			return;

		var model = Model.Load( piece.ModelPath );
		if ( model is null )
			return;

		boardSpotSilhouetteObject = new GameObject( true, $"{GameObject.Name}_BoardSpotSilhouette" );
		var visualObject = new GameObject( true, "Visual" );
		visualObject.SetParent( boardSpotSilhouetteObject );
		visualObject.LocalPosition = piece.LocalVisualOffset;
		visualObject.LocalRotation = Rotation.Identity;
		visualObject.LocalScale = piece.LocalVisualScale;

		boardSpotSilhouetteRenderer = visualObject.Components.Create<ModelRenderer>();
		boardSpotSilhouetteRenderer.Model = model;
		boardSpotSilhouetteRenderer.Tint = currentPlayerColor.WithAlpha( BoardSpotSilhouetteAlpha );

		boardSpotSilhouetteHighlight = visualObject.Components.Create<HighlightOutline>();
		boardSpotSilhouetteHighlight.Color = currentPlayerColor.WithAlpha( 0.85f ).Saturate( 1f );
		boardSpotSilhouetteHighlight.InsideColor = currentPlayerColor.WithAlpha( BoardSpotSilhouetteAlpha );
	}

	private void DestroyBoardSpotSilhouette()
	{
		if ( boardSpotSilhouetteObject is null )
			return;

		boardSpotSilhouetteObject.Destroy();
		boardSpotSilhouetteObject = null;
		boardSpotSilhouetteRenderer = null;
		boardSpotSilhouetteHighlight = null;
	}

	private void UpdatePhysicsTestGrab()
	{
		if ( !EnablePhysicsTestGrab )
			return;

		var game = GameController.Instance;
		if ( PlayerState is not null && game is not null && !game.CanLocalPlayerThrowToken( PlayerState ) )
		{
			if ( isGrabbed || isThrowing )
				StopPhysicsTestMotion();
			return;
		}

		UpdateReplicatedPhysicsState();

		if ( PlayerState?.IsOwner == true && Input.Pressed( "Attack2" ) )
		{

			if ( isGrabbed )
			{
				ReleaseGrab();
				return;
			}

			if ( IsCursorOverToken() && TryGetCursorBoardPosition( out var grabPosition ) )
			{
				isGrabbed = true;
				isThrowing = false;
				throwVelocity = Vector3.Zero;
				grabOffset = GameObject.WorldPosition - grabPosition;
				lastGrabPosition = GameObject.WorldPosition;
			}
		}

		if ( isGrabbed )
		{
			if ( !TryGetCursorBoardPosition( out var cursorPosition ) )
				return;

			var nextPosition = cursorPosition + grabOffset;
			throwVelocity = Time.Delta > 0f
				? (nextPosition - lastGrabPosition) / Time.Delta * ThrowSpeedScale
				: Vector3.Zero;

			nextPosition = ResolveTokenCollisions( nextPosition, throwVelocity, true );
			GameObject.WorldPosition = nextPosition;
			lastGrabPosition = nextPosition;
			return;
		}

		if ( !isThrowing )
			return;

		var thrownPosition = GameObject.WorldPosition + throwVelocity * Time.Delta;
		GameObject.WorldPosition = ResolveTokenCollisions( thrownPosition, throwVelocity, false );
		throwVelocity = throwVelocity.LerpTo( Vector3.Zero, Time.Delta * ThrowDamping );

		if ( throwVelocity.Length < ThrowStopSpeed )
		{
			StopPhysicsTestMotion();
		}
	}

	private void ReleaseGrab()
	{
		isGrabbed = false;
		isThrowing = throwVelocity.Length >= ThrowStopSpeed;

		if ( isThrowing )
			PublishLocalThrow();
	}

	private Vector3 ResolveTokenCollisions( Vector3 nextPosition, Vector3 velocity, bool isDragged )
	{
		if ( CollisionRadius <= 0f || Scene is null )
			return nextPosition;

		foreach ( var other in Scene.GetAllComponents<PlayerToken>() )
		{
			if ( other is null || other == this || other.CollisionRadius <= 0f )
				continue;

			var otherPosition = other.GameObject.WorldPosition;
			var delta = new Vector2( nextPosition.x - otherPosition.x, nextPosition.y - otherPosition.y );
			var distance = delta.Length;
			var minDistance = CollisionRadius + other.CollisionRadius;

			if ( distance >= minDistance )
				continue;

			var normal2 = distance > 0.001f ? delta / distance : GetFallbackCollisionNormal( velocity );
			var penetration = minDistance - Math.Max( distance, 0.001f );
			var normal = new Vector3( normal2.x, normal2.y, 0f );

			nextPosition += normal * penetration;

			var impactSpeed = Vector3.Dot( velocity, -normal );
			if ( impactSpeed <= ThrowStopSpeed )
				continue;

			other.ReceivePhysicsImpulse( -normal * impactSpeed * CollisionImpulseScale );

			if ( !isDragged )
				throwVelocity += normal * impactSpeed * CollisionRestitution;
		}

		return nextPosition;
	}

	private static Vector2 GetFallbackCollisionNormal( Vector3 velocity )
	{
		var fallback = new Vector2( velocity.x, velocity.y );
		return fallback.Length > 0.001f ? fallback.Normal : new Vector2( 1f, 0f );
	}

	private void ReceivePhysicsImpulse( Vector3 impulse )
	{
		if ( !EnablePhysicsTestGrab || impulse.Length < ThrowStopSpeed )
			return;

		isGrabbed = false;
		isThrowing = true;
		throwVelocity = impulse;

		if ( Networking.IsHost )
			PublishLocalThrow();
	}

	private void StopPhysicsTestMotion()
	{
		var stoppedReplicatedKey = isReplicatedPhysics ? activeReplicatedPhysicsKey : null;

		isGrabbed = false;
		isThrowing = false;
		isReplicatedPhysics = false;
		throwVelocity = Vector3.Zero;
		activeReplicatedPhysicsKey = null;

		if ( !string.IsNullOrWhiteSpace( stoppedReplicatedKey ) )
			completedReplicatedPhysicsKey = stoppedReplicatedKey;

		if ( ReturnToSpaceAfterPhysics && Board is not null && PlayerState is not null )
			GameObject.WorldPosition = GameObject.WorldPosition.WithZ( GetSpaceTargetPosition().z );

		var playerIndex = GameController.Instance?.GetPlayerIndex( PlayerState ) ?? -1;
		if ( Networking.IsHost && playerIndex >= 0 )
			GameController.Instance?.ClearTokenPhysicsState( playerIndex );
	}

	private void UpdateReplicatedPhysicsState()
	{
		if ( isGrabbed || GameController.Instance is null || PlayerState is null )
			return;

		var playerIndex = GameController.Instance.GetPlayerIndex( PlayerState );
		if ( playerIndex < 0 )
			return;

		if ( !GameController.Instance.TryGetTokenPhysicsState( playerIndex, out var state, out var key ) )
		{
			if ( isReplicatedPhysics )
				StopPhysicsTestMotion();

			return;
		}

		if ( key == activeReplicatedPhysicsKey )
			return;

		if ( key == completedReplicatedPhysicsKey )
			return;

		activeReplicatedPhysicsKey = key;
		isReplicatedPhysics = true;
		isThrowing = true;
		throwVelocity = state.Velocity;
		GameObject.WorldPosition = state.Position;
	}

	private void PublishLocalThrow()
	{
		if ( GameController.Instance is null || PlayerState is null )
			return;

		var playerIndex = GameController.Instance.GetPlayerIndex( PlayerState );
		if ( playerIndex < 0 )
			return;

		if ( Networking.IsHost )
			GameController.Instance.PublishTokenPhysicsState( playerIndex, GameObject.WorldPosition, throwVelocity );
		else
			GameController.Instance.RequestTokenThrow( playerIndex, GameObject.WorldPosition, throwVelocity );
	}

	private bool IsCursorOverToken()
	{
		var camera = Scene?.Camera;
		if ( camera is null )
			return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		var trace = Scene.Trace.Ray( ray, 5000f )
			.UseHitboxes()
			.HitTriggers()
			.Run();

		if ( !trace.Hit || trace.GameObject is null )
			return IsCursorNearToken();

		return IsTokenObject( trace.GameObject ) || IsCursorNearToken();
	}

	private bool IsCursorNearToken()
	{
		if ( !TryGetCursorBoardPosition( out var cursorPosition ) )
			return false;

		var tokenPosition = GameObject.WorldPosition;
		var delta = new Vector2( cursorPosition.x - tokenPosition.x, cursorPosition.y - tokenPosition.y );
		return delta.Length <= GrabRadius;
	}

	private bool IsTokenObject( GameObject candidate )
	{
		for ( var current = candidate; current is not null; current = current.Parent )
		{
			if ( current == GameObject )
				return true;
		}

		return false;
	}

	private bool TryGetCursorBoardPosition( out Vector3 position )
	{
		position = default;

		var camera = Scene?.Camera;
		if ( camera is null )
			return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		var planeHeight = GetGrabPlaneHeight();
		var distance = (planeHeight - ray.Position.z) / ray.Forward.z;

		if ( distance < 0f || float.IsNaN( distance ) || float.IsInfinity( distance ) )
			return false;

		position = ray.Position + ray.Forward * distance;
		return true;
	}

	private float GetGrabPlaneHeight()
	{
		if ( Board is not null && PlayerState is not null )
			return GetSpaceTargetPosition().z + GrabPlanePadding;

		return GameObject.WorldPosition.z;
	}

	private Vector3 GetSpaceTargetPosition()
	{
		return Board.GetSpacePosition( PlayerState.SpaceIndex ) + GetSharedSpaceOffset() + Vector3.Up * HeightOffset;
	}

	private Vector3 GetSharedSpaceOffset()
	{
		if ( Scene is null || PlayerState is null || SharedSpaceOffsetDistance <= 0f )
			return Vector3.Zero;

		var sameSpaceTokens = Scene.GetAllComponents<PlayerToken>()
			.Where( token =>
				token is not null &&
				token.PlayerState is not null &&
				token.PlayerState.SpaceIndex == PlayerState.SpaceIndex )
			.OrderBy( token => GetStablePlayerOrder( token.PlayerState ) )
			.ToList();

		var myIndex = sameSpaceTokens.IndexOf( this );
		if ( myIndex < 0 || sameSpaceTokens.Count <= 1 )
			return Vector3.Zero;

		var rowSize = Math.Max( 1, (int)MathF.Ceiling( MathF.Sqrt( sameSpaceTokens.Count ) ) );
		var col = myIndex % rowSize;
		var row = myIndex / rowSize;
		var center = (rowSize - 1) * 0.5f;
		var offsetX = (col - center) * SharedSpaceOffsetDistance;
		var offsetY = (row - center) * SharedSpaceOffsetDistance;

		return new Vector3( offsetX, offsetY, 0f );
	}

	private static int GetStablePlayerOrder( PlayerState state )
	{
		if ( state is null )
			return int.MaxValue;

		var controller = GameController.Instance;
		if ( controller is null )
			return unchecked( (int)(state.OwnerId & 0x7FFFFFFF) );

		var index = controller.GetPlayerIndex( state );
		if ( index >= 0 )
			return index;

		if ( state.OwnerId != 0 )
			return unchecked( (int)(state.OwnerId & 0x7FFFFFFF) );

		return state.PlayerName?.GetHashCode() ?? int.MaxValue - 1;
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

	public void ApplyPieceDefinition( PieceDefinition piece )
	{
		var selectedPiece = piece ?? PieceCatalog.GetByIdOrDefault( PieceCatalog.DefaultPieceId );
		currentPieceDefinition = selectedPiece;
		DestroyBoardSpotSilhouette();

		var visualObject = GameObject.Children.FirstOrDefault( child => string.Equals( child?.Name, "Visual", StringComparison.OrdinalIgnoreCase ) );
		var renderHost = visualObject?.Children.FirstOrDefault() ?? visualObject;
		if ( renderHost is null )
			return;

		var modelRenderer = renderHost.Components.Get<ModelRenderer>();
		var skinnedRenderer = renderHost.Components.Get<SkinnedModelRenderer>();
		var model = Model.Load( selectedPiece.ModelPath );
		if ( model is null )
		{
			Log.Warning( $"Unable to load model '{selectedPiece.ModelPath}' for piece '{selectedPiece.Id}'." );
			return;
		}

		if ( skinnedRenderer is not null )
		{
			skinnedRenderer.Model = model;
			
			if (piece.AnimgraphPath is not null && piece.AnimgraphPath != "")
			{
				var animgraph = AnimationGraph.Load(piece.AnimgraphPath);
				if (animgraph is null)
					Log.Warning( $"Unable to load animgraph for '{selectedPiece.Id}'");
				else
					skinnedRenderer.AnimationGraph = animgraph;
			}
		}

		if ( modelRenderer is not null )
			modelRenderer.Model = model;

		renderHost.LocalScale = selectedPiece.LocalVisualScale;
		renderHost.LocalPosition = selectedPiece.LocalVisualOffset;
		HeightOffset = selectedPiece.HeightOffset;
	}

	public void ApplyPieceMaterialOverride( Material material )
	{
		var visualObject = GameObject.Children.FirstOrDefault( child => string.Equals( child?.Name, "Visual", StringComparison.OrdinalIgnoreCase ) );
		var renderHost = visualObject?.Children.FirstOrDefault() ?? visualObject;
		if ( renderHost is null )
			return;

		var modelRenderer = renderHost.Components.Get<ModelRenderer>();
		var skinnedRenderer = renderHost.Components.Get<SkinnedModelRenderer>();
		RememberOriginalPieceMaterialOverrides( modelRenderer, skinnedRenderer );

		if ( modelRenderer is not null )
			modelRenderer.MaterialOverride = material ?? originalModelMaterialOverride;

		if ( skinnedRenderer is not null )
			skinnedRenderer.MaterialOverride = material ?? originalSkinnedMaterialOverride;
	}

	private void RememberOriginalPieceMaterialOverrides( ModelRenderer modelRenderer, SkinnedModelRenderer skinnedRenderer )
	{
		if ( hasOriginalPieceMaterialOverrides )
			return;

		originalModelMaterialOverride = modelRenderer?.MaterialOverride;
		originalSkinnedMaterialOverride = skinnedRenderer?.MaterialOverride;
		hasOriginalPieceMaterialOverrides = true;
	}

	public void ApplyPlayerColor( Color color )
	{
		currentPlayerColor = color;
		EnsurePlayerMarker();
		if ( markerRenderer is not null )
			markerRenderer.Tint = color.WithAlpha( 0f );
		
		if (markerHighlight is not null)
		{
			markerHighlight.Color = color.WithAlpha( 1f ).Saturate( 1f );
			markerHighlight.InsideColor = color.WithAlpha ( 0.55f );
		}

		if ( boardSpotSilhouetteRenderer is not null )
			boardSpotSilhouetteRenderer.Tint = currentPlayerColor.WithAlpha( BoardSpotSilhouetteAlpha );

		if ( boardSpotSilhouetteHighlight is not null )
		{
			boardSpotSilhouetteHighlight.Color = currentPlayerColor.WithAlpha( 0.85f ).Saturate( 1f );
			boardSpotSilhouetteHighlight.InsideColor = currentPlayerColor.WithAlpha( BoardSpotSilhouetteAlpha );
		}
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

	private void EnsurePlayerMarker()
	{
		if ( markerRenderer is not null && markerObject is not null && markerObject.IsValid() )
			return;

		markerObject = new GameObject( true, "PlayerMarker" );
		markerObject.SetParent( GameObject );
		markerObject.LocalPosition = new Vector3( 0.69f, 0.32f, -2.16f );
		markerObject.LocalRotation = Rotation.Identity;
		markerObject.LocalScale = new Vector3( 0.1f, 0.1f, 0f );

		markerRenderer = markerObject.Components.Create<ModelRenderer>();
		markerRenderer.Model = ResolveMarkerModel();
		//markerRenderer.Tint = Color.White.WithAlpha( 1f );

		markerHighlight = markerObject.Components.Create<HighlightOutline>();
	}

	private static Model ResolveMarkerModel()
	{
		var preferredModels = new[]
		{
			"models/dev/sphere.vmdl",
			"models/dev/cylinder.vmdl",
			"models/dev/plane.vmdl"
		};

		foreach ( var path in preferredModels )
		{
			var model = Model.Load( path );
			if ( model is not null )
				return model;
		}

		return null;
	}

}
