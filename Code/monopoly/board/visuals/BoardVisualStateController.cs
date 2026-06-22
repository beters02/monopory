using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Pushes game state into generated board visuals (ownership, selection, can-buy).
/// </summary>
[Title( "Board Visual State Controller" )]
[Category( "Monopoly" )]
public sealed class BoardVisualStateController : Component
{
	[Property] public Board Board { get; set; }
	[Property] public BoardVisualGenerator VisualGenerator { get; set; }
	[Property] public BoardInteractionController InteractionController { get; set; }

	private GameController gameRef;
	private MonopolyTheme theme;
	private int? lastSelectedSpaceIndex;

	protected override void OnStart()
	{
		Board ??= GameObject.GetComponent<Board>();
		VisualGenerator ??= GameObject.GetComponent<BoardVisualGenerator>();
		InteractionController ??= GameObject.GetComponent<BoardInteractionController>();
		theme ??= Scene?.GetAllComponents<MonopolyTheme>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( Board is null || !Board.UseGeneratedMeshBoard )
			return;

		RefreshFromGameState();
	}

	public void RefreshFromGameState()
	{
		EnsureRefs();
		if ( gameRef is null || VisualGenerator is null )
			return;

		var selectedIndex = gameRef.LocalSelectedSpaceIndex;
		if ( selectedIndex < 0 )
			SetSelectedSpace( null );
		else
			SetSelectedSpace( selectedIndex );

		foreach ( var (spaceIndex, visual) in VisualGenerator.SpaceVisuals )
		{
			var ownerIndex = gameRef.GetOwnerIndexForSpace( spaceIndex );
			if ( ownerIndex >= 0 )
				SetSpaceOwner( spaceIndex, ownerIndex );
			else
				visual.SetOwner( null, Color.White );

			var canBuy = gameRef.CurrentPlayer is not null &&
				gameRef.CanBuyPendingProperty( gameRef.CurrentPlayer, spaceIndex );
			visual.SetCanBuy( canBuy );
		}
	}

	public void SetSelectedSpace( int? spaceIndex )
	{
		if ( lastSelectedSpaceIndex == spaceIndex )
			return;

		if ( lastSelectedSpaceIndex.HasValue && VisualGenerator?.TryGetSpaceVisual( lastSelectedSpaceIndex.Value, out var previous ) == true )
			previous.SetSelected( false );

		lastSelectedSpaceIndex = spaceIndex;

		if ( spaceIndex.HasValue && VisualGenerator?.TryGetSpaceVisual( spaceIndex.Value, out var current ) == true )
			current.SetSelected( true );

		InteractionController?.SetSelectedSpace( spaceIndex );
	}

	public void SetHoveredSpace( int? spaceIndex )
	{
		InteractionController?.SetHoveredSpace( spaceIndex );
	}

	public void SetSpaceOwner( int spaceIndex, int ownerSlotIndex )
	{
		if ( VisualGenerator?.TryGetSpaceVisual( spaceIndex, out var visual ) != true )
			return;

		theme ??= Scene?.GetAllComponents<MonopolyTheme>().FirstOrDefault();
		var color = theme?.GetPlayerColor( ownerSlotIndex ) ?? Color.White;
		visual.SetOwner( ownerSlotIndex, color );
	}

	public void SetCanBuySpace( int spaceIndex, bool canBuy )
	{
		if ( VisualGenerator?.TryGetSpaceVisual( spaceIndex, out var visual ) == true )
			visual.SetCanBuy( canBuy );
	}

	private void EnsureRefs()
	{
		Board ??= GameObject.GetComponent<Board>();
		VisualGenerator ??= GameObject.GetComponent<BoardVisualGenerator>();
		InteractionController ??= GameObject.GetComponent<BoardInteractionController>();

		if ( gameRef is null )
			gameRef = Scene?.GetAllComponents<GameController>().FirstOrDefault();
	}
}
