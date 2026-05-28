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

	private SkinnedModelRenderer renderer;
	private ModelRenderer markerRenderer;
	private GameObject markerObject;
	private HighlightOutline markerHighlight;
	private bool walkingAnim;
	private bool turningAnim;
	private bool hasAppliedWalkingAnim;
	private bool requestedWalking;
	private Vector3 lastXDirection = new(0, 1, 0);
	private bool isGrabbed;
	private bool isThrowing;
	private Vector3 grabOffset;
	private Vector3 lastGrabPosition;
	private Vector3 throwVelocity;
	private bool isReplicatedPhysics;
	private string activeReplicatedPhysicsKey;
	private string completedReplicatedPhysicsKey;

	protected override void OnStart()
	{
		renderer = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		EnsurePlayerMarker();
	}

	protected override void OnUpdate()
	{
		UpdatePhysicsTestGrab();
		if ( GameController.Instance?.IsLocalRentCutsceneActive == true )
		{
			ApplyWalkingAnim( false );
			return;
		}

		if ( isGrabbed || isThrowing )
		{
			ApplyWalkingAnim( false );
			return;
		}

		if ( Board is null || PlayerState is null )
			return;

		var target = GetSpaceTargetPosition();
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

	private void UpdatePhysicsTestGrab()
	{
		if ( GameController.Instance?.IsLocalRentCutsceneActive == true )
		{
			if ( isGrabbed || isThrowing )
				StopPhysicsTestMotion();
			return;
		}

		if ( !EnablePhysicsTestGrab )
			return;

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
			skinnedRenderer.Model = model;

		if ( modelRenderer is not null )
			modelRenderer.Model = model;

		renderHost.LocalScale = selectedPiece.LocalVisualScale;
		renderHost.LocalPosition = selectedPiece.LocalVisualOffset;
		HeightOffset = selectedPiece.HeightOffset;
	}

	public void ApplyPlayerColor( Color color )
	{
		EnsurePlayerMarker();
		if ( markerRenderer is not null )
			markerRenderer.Tint = color.WithAlpha( 0f );
		
		if (markerHighlight is not null)
		{
			markerHighlight.Color = color.WithAlpha( 1f ).Saturate( 1f );
			markerHighlight.InsideColor = color.WithAlpha ( 0.55f );
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

	private void EnsurePlayerMarker()
	{
		if ( markerRenderer is not null && markerObject is not null && markerObject.IsValid() )
			return;

		markerObject = new GameObject( true, "PlayerMarker" );
		markerObject.SetParent( GameObject );
		markerObject.LocalPosition = new Vector3( 0.69f, 0.32f, 0.82f );
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
