using System;
using Sandbox;

/// <summary>
/// Bridges generated board colliders to existing GameController selection flow.
/// </summary>
[Title( "Board Interaction Controller" )]
[Category( "Monopoly" )]
public sealed class BoardInteractionController : Component
{
	[Property] public Board Board { get; set; }
	[Property] public BoardVisualGenerator VisualGenerator { get; set; }

	private GameController gameRef;
	private int? hoveredSpaceIndex;
	private int? selectedSpaceIndex;

	protected override void OnStart()
	{
		Board ??= GameObject.GetComponent<Board>();
		VisualGenerator ??= GameObject.GetComponent<BoardVisualGenerator>();
	}

	protected override void OnUpdate()
	{
		if ( Board is null || !Board.UseGeneratedMeshBoard )
			return;

		UpdateHoverCursor();
		UpdateSelection();
	}

	public void SetSelectedSpace( int? spaceIndex )
	{
		if ( selectedSpaceIndex == spaceIndex )
			return;

		if ( selectedSpaceIndex.HasValue && VisualGenerator?.TryGetSpaceVisual( selectedSpaceIndex.Value, out var previous ) == true )
			previous.SetSelected( false );

		selectedSpaceIndex = spaceIndex;

		if ( spaceIndex.HasValue && VisualGenerator?.TryGetSpaceVisual( spaceIndex.Value, out var current ) == true )
			current.SetSelected( true );
	}

	public void SetHoveredSpace( int? spaceIndex )
	{
		if ( hoveredSpaceIndex == spaceIndex )
			return;

		if ( hoveredSpaceIndex.HasValue && VisualGenerator?.TryGetSpaceVisual( hoveredSpaceIndex.Value, out var previous ) == true )
			previous.SetHovered( false );

		hoveredSpaceIndex = spaceIndex;

		if ( spaceIndex.HasValue && VisualGenerator?.TryGetSpaceVisual( spaceIndex.Value, out var current ) == true )
			current.SetHovered( true );
	}

	private void UpdateHoverCursor()
	{
		EnsureGameControllerRef();

		var traceResult = GetSelectionMouseTraceResult();
		var isHoveringSpace = IsHoveringSelectableSpace( traceResult );
		var desiredCursor = isHoveringSpace ? "pointer" : "default";
		const float pointerLatchRadius = 4f;

		if ( desiredCursor is null &&
			Board.LastPointerHoverPosition.HasValue &&
			(Mouse.Position - Board.LastPointerHoverPosition.Value).Length <= pointerLatchRadius &&
			string.Equals( Board.LastCursorType, "pointer", StringComparison.Ordinal ) )
		{
			desiredCursor = "pointer";
		}

		Mouse.CursorType = desiredCursor;

		if ( string.Equals( desiredCursor, "pointer", StringComparison.Ordinal ) && isHoveringSpace )
			Board.LastPointerHoverPosition = Mouse.Position;

		Board.LastCursorType = desiredCursor;
		SetHoveredSpace( GetHoveredSpaceIndex( traceResult ) );
	}

	private static int? GetHoveredSpaceIndex( SceneTraceResult? traceResult )
	{
		if ( traceResult.HasValue &&
			traceResult.Value.Hit &&
			traceResult.Value.GameObject is not null &&
			traceResult.Value.GameObject.Components.TryGet<BoardSpaceVisual>( out var visual ) )
		{
			return visual.SpaceIndex;
		}

		return null;
	}

	private void UpdateSelection()
	{
		EnsureGameControllerRef();

		if ( !Input.Pressed( "Attack1" ) )
			return;

		var traceResult = GetSelectionMouseTraceResult();
		if ( !traceResult.HasValue )
			return;

		var trace = traceResult.Value;
		if ( Board.DebugEnabled )
			DebugOverlay.Trace( trace, 5f, true );

		if ( !trace.Hit || trace.GameObject is null || !trace.GameObject.Components.TryGet<BoardSpace>( out var space ) )
			return;

		if ( gameRef is null || space is null )
			return;

		if ( !gameRef.CanLeaveCurrentSelectedSpace() )
		{
			var def = Board.GetSpaceDef( gameRef.LocalSelectedSpaceIndex );
			gameRef.ShowLocalPopup(
				"Decision required",
				$"Buy or auction {def.DisplayName} before closing this card.",
				PopupKind.Warning,
				true,
				3f
			);
			return;
		}

		gameRef.SelectSpace( space.Index );
		SetSelectedSpace( space.Index );
	}

	private SceneTraceResult? GetSelectionMouseTraceResult()
	{
		var camera = Scene.Camera;
		if ( camera is null )
			return null;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		return Scene.Trace.Ray( ray, 5000f )
			.UseHitboxes()
			.HitTriggers()
			.WithTag( "board_space" )
			.Run();
	}

	private bool IsHoveringSelectableSpace( SceneTraceResult? traceResult )
	{
		EnsureGameControllerRef();

		if ( !traceResult.HasValue || !traceResult.Value.Hit || traceResult.Value.GameObject is null )
			return false;

		if ( gameRef is null || !gameRef.CanLeaveCurrentSelectedSpace() )
			return false;

		if ( !traceResult.Value.GameObject.Components.TryGet<BoardSpace>( out var space ) || space is null )
			return false;

		var def = Board.GetSpaceDef( space.Index );
		if ( def is null )
			return false;

		return gameRef.ShouldShowSelectedSpaceCard( def.Type );
	}

	private void EnsureGameControllerRef()
	{
		if ( gameRef is null )
			gameRef = Scene?.GetAllComponents<GameController>().FirstOrDefault();
	}
}
