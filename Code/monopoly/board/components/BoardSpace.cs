using System;
using Sandbox;
using Sandbox.UI;

public sealed class BoardSpace : Component
{

	[Property] public int Index { get; set; }

	public Vector3 TokenPosition => GameObject.WorldPosition;

	public TextRenderer LabelRenderer { get; private set; }
	public Sandbox.ui.SpaceLabel WorldPanelLabel { get; private set; }

	//[Property] public int SpaceIndex { get; set; }

	public BoxCollider Collider {get; private set;}

	// probably unoptimized but idgaf
	public SpaceDef Def { get; set; }
	public string DisplayName {get; set;}

	private readonly List<GameObject> ImprovementVisuals = new();
	private int visibleImprovementCount = -1;
	private Model visibleHouseModel;
	private Model visibleHotelModel;
	private Vector3 visibleImprovementScale;
	private float visibleImprovementZOffset;

	protected override void OnStart()
	{
		Tags.Add( "monopoly_space" );
	}

	public void EnsureHitbox()
	{
		BoxCollider collider = Components.Get<BoxCollider>() ?? Components.Create<BoxCollider>();

		collider.Scale = Board.GetHitboxSize(Index);
		collider.Center = new Vector3(0, 0, -2f);
		collider.IsTrigger = true;

		Collider = collider;
	}

	public void EnsureDef(SpaceDef def)
	{
		Def = def;
		DisplayName = def.DisplayName;
	}

	public Vector3? GetColliderCenter()
	{
		if (Collider == null)
			return null;

		return GameObject.WorldTransform.PointToWorld(Collider.Center);
		//return Collider.Center;
	}

	public Vector3? GetColliderSize()
	{
		if (Collider == null)
			return null;

		return Collider.Scale;
	}

	public void ModifyColliderSize(float addWidth = 0f, float addHeight = 0f)
	{
		if (Collider == null)
			return;

		SpaceDef def = Board.GetSpaceDefStatic( Index );
		if (def == null)
		{
			Log.Info("Cannot modify collider width: SpaceDef is null");
			return;
		}

		var size = Collider.Scale;
		int quad = def.GetQuadrant();

		if (quad == 1 || quad == 3)
		{
			size.x += addHeight;
			size.y += addWidth;
		} else
		{
			size.x += addWidth;
			size.y += addHeight;
		}

		Collider.Scale = size;
	}

	public void ModifyColliderCenter(float addLeft = 0f, float addUp = 0f)
	{
		if (Collider == null)
			return;

		SpaceDef def = Board.GetSpaceDefStatic( Index );
		if (def == null)
		{
			Log.Info("Cannot modify collider width: SpaceDef is null");
			return;
		}

		var center = Collider.Center;
		int quad = def.GetQuadrant();

		if (quad == 4)
		{
			center.x += -addLeft;
			center.y += addUp;
		} else if (quad == 3)
		{
			center.x += -addUp;
			center.y += -addLeft;
		} else if (quad == 2)
		{
			center.x += addLeft;
			center.y += -addUp;
		} else if (quad == 1)
		{
			center.x += addLeft;
			center.y += addUp;
		}

		/*if (quad == 1 || quad == 3)
		{
			var abs = quad == 1 ? 1 : -1;
			center.x += addLeft * abs;
			center.y += addUp * abs;
		} else
		{
			var abs = quad == 2 ? 1 : -1;
			center.x += addUp * abs;
			center.y += addLeft * abs;
		}*/

		//var abs = quad == 1 || quad == 3 ? 1 : -1;
		

		Collider.Center = center;
	}
	public void SetHitboxEnabled(bool enabled)
	{
		Collider.Enabled = enabled;
	}

	public bool IsHitboxEnabled()
	{
		var collider = Components.Get<BoxCollider>();
		return collider != null && collider.Enabled;
	}

	public void SetImprovementVisuals( int improvementCount, Model houseModel, Model hotelModel, Vector3 modelScale, float zOffset )
	{
		improvementCount = Math.Clamp( improvementCount, 0, 5 );

		if ( improvementCount == visibleImprovementCount &&
			houseModel == visibleHouseModel &&
			hotelModel == visibleHotelModel &&
			modelScale == visibleImprovementScale &&
			MathF.Abs( zOffset - visibleImprovementZOffset ) < 0.001f )
		{
			return;
		}

		ClearImprovementVisuals();

		visibleImprovementCount = improvementCount;
		visibleHouseModel = houseModel;
		visibleHotelModel = hotelModel;
		visibleImprovementScale = modelScale;
		visibleImprovementZOffset = zOffset;

		if ( Def is null || Def.Type != SpaceType.Property || Collider is null || improvementCount <= 0 )
			return;

		if ( improvementCount >= 5 )
		{
			CreateImprovementVisual( 0, 1, hotelModel ?? houseModel, modelScale, zOffset, true );
			return;
		}

		if ( houseModel is null )
			return;

		for ( var i = 0; i < improvementCount; i++ )
			CreateImprovementVisual( i, improvementCount, houseModel, modelScale, zOffset, false );
	}

	private void ClearImprovementVisuals()
	{
		foreach ( var visual in ImprovementVisuals )
			visual.Destroy();

		ImprovementVisuals.Clear();
	}

	private void CreateImprovementVisual( int slot, int total, Model model, Vector3 modelScale, float zOffset, bool isHotel )
	{
		if ( model is null )
			return;

		var visualObject = new GameObject( true, $"{(isHotel ? "Hotel" : "House")}_{Index}_{slot}" );
		visualObject.SetParent( GameObject );
		visualObject.LocalPosition = GetImprovementLocalPosition( slot, total, zOffset, isHotel );
		visualObject.LocalRotation = GetImprovementLocalRotation();
		visualObject.LocalScale = modelScale;

		var renderer = visualObject.Components.Create<ModelRenderer>();
		renderer.Model = model;

		ImprovementVisuals.Add( visualObject );
	}

	private Vector3 GetImprovementLocalPosition( int slot, int total, float zOffset, bool isHotel )
	{
		var center = Collider.Center;
		var quadrant = Def.GetQuadrant();
		var across = GetImprovementAcrossOffset( slot, total, isHotel );
		var inward = isHotel ? 1.15f : 1.35f;
		var position = new Vector3( center.x, center.y, zOffset );

		switch ( quadrant )
		{
			case 1:
				position.x += inward;
				position.y += across;
				break;
			case 2:
				position.x += across;
				position.y -= inward;
				break;
			case 3:
				position.x -= inward;
				position.y += across;
				break;
			case 4:
				position.x += across;
				position.y += inward;
				break;
		}

		return position;
	}

	private static float GetImprovementAcrossOffset( int slot, int total, bool isHotel )
	{
		if ( isHotel || total <= 1 )
			return 0f;

		const float spacing = 0.7f;
		return (slot - ((total - 1) * 0.5f)) * spacing;
	}

	private Rotation GetImprovementLocalRotation()
	{
		return Def.GetQuadrant() switch
		{
			2 => Rotation.FromYaw( -90f ),
			3 => Rotation.FromYaw( 180f ),
			4 => Rotation.FromYaw( 90f ),
			_ => Rotation.Identity
		};
	}

	public void CreateLabel( )
	{
		if ( LabelRenderer != null )
			return;

		var labelObject = new GameObject( true, $"Label_{Index}" );
		labelObject.SetParent( GameObject );
		ApplyLabelTransform( labelObject );

		LabelRenderer = labelObject.Components.Create<TextRenderer>();

		TextRendering.Scope scope = LabelRenderer.TextScope;
		scope.Shadow.Enabled = true;
		scope.FilterMode = Sandbox.Rendering.FilterMode.Anisotropic;
		LabelRenderer.TextScope = scope;

		LabelRenderer.Text = Def.DisplayName;
		LabelRenderer.FontSize = 128;
		LabelRenderer.Color = new(0.5f, 0.5f, 0.5f);
		LabelRenderer.Scale = Def.TextScale;
		LabelRenderer.FontWeight = 800;		
	}

	public void CreateWorldPanelLabel()
	{
		if ( WorldPanelLabel != null || Collider is null )
			return;

		var labelObject = new GameObject( true, $"WorldPanelLabel_{Index}" );
		labelObject.SetParent( GameObject );
		ApplyWorldLabelTransform( labelObject );

		var worldPanel = labelObject.Components.Create<Sandbox.WorldPanel>();
		worldPanel.InteractionRange = 0f;
		worldPanel.PanelSize = GetWorldPanelLabelPanelSizeForQuadrant( Collider.Scale );
		worldPanel.RenderScale = SpaceLayoutSettings.WorldPanelLabelRenderScale; //1f / SpaceLayoutSettings.WorldPanelLabelPixelsPerWorldUnit;
		worldPanel.VerticalAlign = Sandbox.WorldPanel.VAlignment.Center;
		worldPanel.HorizontalAlign = Sandbox.WorldPanel.HAlignment.Center;

		WorldPanelLabel = labelObject.Components.Create<Sandbox.ui.SpaceLabel>();
		WorldPanelLabel.SpaceName = Def.DisplayName;
		WorldPanelLabel.ColorGroup = Def.ColorGroupClass;
		WorldPanelLabel.SpaceType = Def.Type;
		WorldPanelLabel.SpaceIndex = Def.Index;
		WorldPanelLabel.SpaceKey = Def.Key;
		WorldPanelLabel.StateHasChanged();
	}

	private void ApplyLabelTransform( GameObject labelObject )
	{
		if ( Def.Index >= 0 && Def.Index < 11 )
		{
			labelObject.LocalPosition = SpaceLayoutSettings.FirstQuadrantLocalPosition;
			labelObject.LocalRotation = SpaceLayoutSettings.FirstQuadrantLocalRotation;
		}
		else if ( Def.Index >= 11 && Def.Index < 20 )
		{
			labelObject.LocalPosition = SpaceLayoutSettings.SecondQuadrantLocalPosition;
			labelObject.LocalRotation = SpaceLayoutSettings.SecondQuadrantLocalRotation;
		}
		else if ( Def.Index >= 20 && Def.Index < 30 )
		{
			labelObject.LocalPosition = SpaceLayoutSettings.ThirdQuadrantLocalPosition;
			labelObject.LocalRotation = SpaceLayoutSettings.ThirdQuadrantLocalRotation;
		}
		else
		{
			labelObject.LocalPosition = SpaceLayoutSettings.FourthQuadrantLocalPosition;
			labelObject.LocalRotation = SpaceLayoutSettings.FourthQuadrantLocalRotation;
		}
	}

	private void ApplyWorldLabelTransform( GameObject labelObject )
	{
		int quad = Def.GetQuadrant();
		if (quad == 0)
			quad = 1;
		else
			quad -= 1;

		var colliderCenter = Collider.Center;
		labelObject.LocalPosition = new Vector3(
			colliderCenter.x,
			colliderCenter.y,
			SpaceLayoutSettings.WorldPanelLabelZOffset
		);
		labelObject.LocalRotation = SpaceLayoutSettings.WorldLabelRotations[quad];
		labelObject.LocalScale = Vector3.One;
	}

	private Vector2 GetWorldPanelLabelSizeForQuadrant( Vector3 hitboxSize )
	{
		var quad = Def.GetQuadrant();

		if ( quad == 2 || quad == 4 )
			return new Vector2( hitboxSize.y, hitboxSize.x );

		return new Vector2( hitboxSize.x, hitboxSize.y );
	}

	private Vector2 GetWorldPanelLabelPanelSizeForQuadrant( Vector3 hitboxSize )
	{
		return SpaceLayoutSettings.WorldPanelSizePixels;
		//return GetWorldPanelLabelSizeForQuadrant( hitboxSize ) *
		//	SpaceLayoutSettings.WorldPanelLabelPixelsPerWorldUnit;
	}

}
