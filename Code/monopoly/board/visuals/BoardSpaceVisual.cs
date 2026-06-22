using Sandbox;
using System;

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
	[Property] public GameObject LandedVisual { get; set; }

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

	public void SetLandedPulse( float scaleMultiplier, float alpha )
	{
		if ( LandedVisual is null || !LandedVisual.IsValid() )
			return;

		LandedVisual.Enabled = alpha > 0.02f;
		if ( !LandedVisual.Enabled )
			return;

		var pulseScale = MathF.Max( 0.5f, scaleMultiplier );
		LandedVisual.LocalScale = new Vector3( pulseScale, pulseScale, 1f );

		var pulseColor = new Color( 0.55f, 0.85f, 1f, alpha );
		foreach ( var mesh in LandedVisual.GetComponentsInChildren<MeshComponent>() )
			mesh.Color = pulseColor;

		foreach ( var renderer in LandedVisual.GetComponentsInChildren<ModelRenderer>() )
			renderer.Tint = pulseColor;
	}

	public void SetOwner( int? ownerSlotIndex, Color ownerColor )
	{
		if ( OwnershipVisual is null || !OwnershipVisual.IsValid() )
			return;

		var show = ownerSlotIndex.HasValue;
		OwnershipVisual.Enabled = show;

		if ( !show )
			return;

		var tint = ownerColor.Saturate( 1.15f );

		foreach ( var mesh in OwnershipVisual.GetComponentsInChildren<MeshComponent>() )
			mesh.Color = tint;

		foreach ( var renderer in OwnershipVisual.GetComponentsInChildren<ModelRenderer>() )
			renderer.Tint = tint;
	}

	public void ClearState()
	{
		SetHovered( false );
		SetSelected( false );
		SetCanBuy( false );
		SetLandedPulse( 1f, 0f );
		SetOwner( null, Color.White );
	}
}
