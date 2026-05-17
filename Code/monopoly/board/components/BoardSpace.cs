using System;
using Sandbox;
using Sandbox.UI;

public sealed class BoardSpace : Component
{

	[Property] public int Index { get; set; }

	public Vector3 TokenPosition => GameObject.WorldPosition;

	public TextRenderer LabelRenderer { get; private set; }
	public Sandbox.ui.SpaceLabel WorldPanelLabel { get; private set; }

	public Vector3? OverrideHitboxSize { get; set; }
	public Vector3? OverrideHitboxCenter { get; set; }

	public BoxCollider Collider {get; private set;}

	// probably unoptimized but idgaf
	public SpaceDef Def { get; set; }
	public string DisplayName {get; set;}

	private readonly List<GameObject> ImprovementVisuals = new();
	private int visibleImprovementCount = -1;
	private GameObject visibleHousePrefab;
	private GameObject visibleHotelPrefab;
	private Vector3 houseVisibleImprovementScale;
	private Vector3 hotelVisibleImprovementScale;
	private float visibleImprovementZOffset;
	private float visibleImprovementEdgeInset;
	private float visibleImprovementSideInset;
	private float visibleImprovementSpacing;

	protected override void OnStart()
	{
		Tags.Add( "monopoly_space" );
	}

	protected override void OnUpdate()
	{
	}

	public void EnsureHitbox()
	{
		BoxCollider collider = Components.Get<BoxCollider>() ?? Components.Create<BoxCollider>();

		collider.Scale = OverrideHitboxSize ?? Board.GetHitboxSize(Index);
		collider.Center = OverrideHitboxCenter ?? new Vector3(0, 0, -2f);
		collider.IsTrigger = true;

		Collider = collider;
	}

	public void SetHitboxOverride( Vector3? size = null, Vector3? center = null )
	{
		OverrideHitboxSize = size;
		OverrideHitboxCenter = center;

		if ( Collider is null )
			return;

		if ( OverrideHitboxSize is not null )
			Collider.Scale = OverrideHitboxSize.Value;

		if ( OverrideHitboxCenter is not null )
			Collider.Center = OverrideHitboxCenter.Value;
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
		int quad = Board.GetSpaceQuadrant( def.Index );

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
		int quad = Board.GetSpaceQuadrant( def.Index );

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

	public void SetImprovementVisuals( int improvementCount, GameObject housePrefab, GameObject hotelPrefab, Vector3 housePrefabScale, Vector3 hotelPrefabScale, float zOffset, float edgeInset, float sideInset, float spacing )
	{
		improvementCount = Math.Clamp( improvementCount, 0, 5 );

		if ( improvementCount == visibleImprovementCount &&
			housePrefab == visibleHousePrefab &&
			hotelPrefab == visibleHotelPrefab &&
			housePrefabScale == houseVisibleImprovementScale &&
			hotelPrefabScale == hotelVisibleImprovementScale &&
			MathF.Abs( zOffset - visibleImprovementZOffset ) < 0.001f &&
			MathF.Abs( edgeInset - visibleImprovementEdgeInset ) < 0.001f &&
			MathF.Abs( sideInset - visibleImprovementSideInset ) < 0.001f &&
			MathF.Abs( spacing - visibleImprovementSpacing ) < 0.001f )
		{
			return;
		}

		ClearImprovementVisuals();

		visibleImprovementCount = improvementCount;
		visibleHousePrefab = housePrefab;
		visibleHotelPrefab = hotelPrefab;
		houseVisibleImprovementScale = housePrefabScale;
		hotelVisibleImprovementScale = hotelPrefabScale;
		visibleImprovementZOffset = zOffset;
		visibleImprovementEdgeInset = edgeInset;
		visibleImprovementSideInset = sideInset;
		visibleImprovementSpacing = spacing;

		if ( Def is null || Def.Type != SpaceType.Property || Collider is null || improvementCount <= 0 )
			return;

		if ( improvementCount >= 5 )
		{
			CreateImprovementVisual( 0, 1, hotelPrefab ?? housePrefab, housePrefabScale, hotelPrefabScale, zOffset, edgeInset, sideInset, spacing, true );
			return;
		}

		if ( housePrefab is null )
			return;

		for ( var i = 0; i < improvementCount; i++ )
			CreateImprovementVisual( i, improvementCount, housePrefab, housePrefabScale, hotelPrefabScale, zOffset, edgeInset, sideInset, spacing, false );
	}

	private void ClearImprovementVisuals()
	{
		foreach ( var visual in ImprovementVisuals )
			visual.Destroy();

		ImprovementVisuals.Clear();
	}

	private void CreateImprovementVisual( int slot, int total, GameObject prefab, Vector3 housePrefabScale, Vector3 hotelPrefabScale, float zOffset, float edgeInset, float sideInset, float spacing, bool isHotel )
	{
		if ( prefab is null )
			return;

		var visualObject = prefab.Clone();
		visualObject.Name = $"{(isHotel ? "Hotel" : "House")}_{Index}_{slot}";
		visualObject.SetParent( GameObject );
		visualObject.LocalPosition = GetImprovementLocalPosition( slot, total, zOffset, edgeInset, sideInset, spacing, isHotel );
		visualObject.LocalRotation = GetImprovementLocalRotation();
		visualObject.LocalScale = isHotel ? hotelPrefabScale : housePrefabScale;

		ModelRenderer modelRenderer = visualObject.GetComponentInChildren<ModelRenderer>();
		if (modelRenderer != null)
			modelRenderer.MaterialOverride = isHotel ? GameAssets.Materials.Hotel.Material : GameAssets.Materials.House.Material;

		ImprovementVisuals.Add( visualObject );
	}

	private Vector3 GetImprovementLocalPosition( int slot, int total, float zOffset, float edgeInset, float sideInset, float spacing, bool isHotel )
	{
		var center = Collider.Center;
		var quadrant = Board.GetSpaceQuadrant( Def.Index );
		var across = GetImprovementAcrossOffset( slot, quadrant, sideInset, spacing, isHotel );
		var inward = GetImprovementInwardOffset( quadrant, edgeInset );
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

	private float GetImprovementInwardOffset( int quadrant, float edgeInset )
	{
		var size = Collider.Scale;
		var inwardSize = quadrant is 1 or 3 ? size.x : size.y;
		return MathF.Max( 0f, (inwardSize * 0.5f) - edgeInset );
	}

	private float GetImprovementAcrossOffset( int slot, int quadrant, float sideInset, float spacing, bool isHotel )
	{
		if ( isHotel )
			return 0f;

		var size = Collider.Scale;
		var acrossSize = quadrant is 1 or 3 ? size.y : size.x;
		var start = MathF.Max( 0f, (acrossSize * 0.5f) - sideInset );
		return start - (slot * spacing);
	}

	private Rotation GetImprovementLocalRotation()
	{
		return Board.GetSpaceQuadrant( Def.Index ) switch
		{
			2 => Rotation.From(180, 90, 0),
			3 => Rotation.From(180, 0, 0),
			4 => Rotation.From(180, 90, 0),
			_ => Rotation.From(180, 0, 0),
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
		worldPanel.PanelSize = GetWorldPanelLabelPanelSizeForQuadrant();
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
		int quad = Board.GetSpaceQuadrant( Def.Index );
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

		if (Board.UseProceduralBoardPanelStatic)
			labelObject.LocalRotation = SpaceLayoutSettings.WorldLabelRotations[quad];
			//labelObject.LocalRotation = SpaceLayoutSettings.WorldLabelRotationsProcedural[quad];
		else
			labelObject.LocalRotation = SpaceLayoutSettings.WorldLabelRotations[quad];
			

		//labelObject.LocalRotation = SpaceLayoutSettings.WorldLabelRotations[quad];
		labelObject.LocalScale = Vector3.One;
	}

	private Vector2 GetWorldPanelLabelPanelSizeForQuadrant() => SpaceLayoutSettings.WorldPanelSizePixels;

}
