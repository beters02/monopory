using Sandbox;

/// <summary>
/// Per-space visual state for the generated mesh board.
/// Gameplay pushes hover/selection/ownership updates here.
/// </summary>
[Title( "Board Space Visual" )]
[Category( "Monopoly" )]
public sealed class BoardSpaceVisual : Component
{
	[Property] public int SpaceIndex { get; set; }
	[Property] public string SpaceKey { get; set; } = "";
	[Property] public GameObject HoverVisual { get; set; }
	[Property] public GameObject SelectedVisual { get; set; }
	[Property] public GameObject OwnershipVisual { get; set; }
	[Property] public GameObject CanBuyVisual { get; set; }

	public void SetHovered( bool hovered )
	{
		if ( HoverVisual is not null && HoverVisual.IsValid() )
			HoverVisual.Enabled = hovered;
	}

	public void SetSelected( bool selected )
	{
		if ( SelectedVisual is not null && SelectedVisual.IsValid() )
			SelectedVisual.Enabled = selected;
	}

	public void SetCanBuy( bool canBuy )
	{
		if ( CanBuyVisual is not null && CanBuyVisual.IsValid() )
			CanBuyVisual.Enabled = canBuy;
	}

	public void SetOwner( int? ownerSlotIndex, Color ownerColor )
	{
		if ( OwnershipVisual is null || !OwnershipVisual.IsValid() )
			return;

		var show = ownerSlotIndex.HasValue;
		OwnershipVisual.Enabled = show;

		if ( !show )
			return;

		foreach ( var renderer in OwnershipVisual.GetComponentsInChildren<ModelRenderer>() )
			renderer.Tint = ownerColor;

		foreach ( var mesh in OwnershipVisual.GetComponentsInChildren<MeshComponent>() )
			mesh.Color = ownerColor;
	}

	public void ClearState()
	{
		SetHovered( false );
		SetSelected( false );
		SetCanBuy( false );
		SetOwner( null, Color.White );
	}
}
