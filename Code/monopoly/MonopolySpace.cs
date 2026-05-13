using System;
using Sandbox;
using Sandbox.UI;

public enum SpaceType
{
	Go,
	Property,
	Railroad,
	Utility,
	Chance,
	CommunityChest,
	Tax,
	Jail,
	FreeParking,
	GoToJail
}

public sealed class MonopolySpaceDef
{
	public int Index { get; set; }
	public string Key { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public SpaceType Type { get; set; }

	// Property Data
	public int Price { get; set; }
	public int BaseRent { get; set; }
	public int OneHouseRent { get; set; }
	public int TwoHouseRent { get; set; }
	public int ThreeHouseRent { get; set; }
	public int FourHouseRent { get; set; }
	public int HotelRent { get; set; }

	public string ColorGroup { get; set; } = "";

	// Tax spaces
	public int TaxAmount { get; set; }

	// Text info
	public float TextScale = MonopolySpaceSettings.DefaultScale;

	private int Quadrant {get; set;}

	public int GetQuadrant()
	{
		if (Quadrant == 0 && !IsCorner)
		{
			if (Index == 0 || Index == 10 || Index == 20 || Index == 30)
			{
				Quadrant = 0; // Corners don't have a quadrant
				IsCorner = true;
			} else if(Index < 10)
			{
				Quadrant = 1;
			} else if (Index < 20)
			{
				Quadrant = 2;
			} else if (Index < 30)
			{
				Quadrant = 3;
			} else if (Index < 40)
			{
				Quadrant = 4;
			}
		}

		return Quadrant;
	}

	public bool IsCorner {get; set;} = false;
}

public static class MonopolySpaceSettings
{
	public static readonly float DefaultScale = 0.009f;
	public static readonly float WorldPanelLabelZOffset = 0.75f;
	public static readonly float WorldPanelLabelPixelsPerWorldUnit = 64f;

	public static readonly Vector2 WorldPanelSizePixels = new(180,300);
	public static readonly float WorldPanelLabelRenderScale = 0.53f;

	// 2
	// pos -0.7099998,0.490000159,0.749999762
	// rot 0.49999997,-0.49999997,0.49999997,0.49999997

	// 3
	// pos 2.20000005,0,0.749999762
	// rot 0,-0.707106769,0,0.707106769

	// 4
	// pos -1.39999998,-0.880000353,0.749999762
	// rot -0.49999997,-0.49999997,-0.49999997,0.49999997

	//  pos
	//  rot

	public static Vector3[] WorldLabelPositions =
	{
		new (-1.39999998f, 0, 0.749999762f),
		new (-0.7099998f, 0.490000159f, 0.749999762f),
		new (2.20000005f, 0, 0.749999762f),
		new (-1.39999998f, -0.880000353f, 0.749999762f)
	};

	public static readonly List<Rotation> WorldLabelRotations =
	[
		new (0.707106769f, 3.09086197E-08f, 0.707106769f, -3.09086197E-08f),
		new (0.49999997f, -0.49999997f, 0.49999997f, 0.49999997f),
		new (0, -0.707106769f, 0, 0.707106769f),
		new (-0.49999997f, -0.49999997f, -0.49999997f, 0.49999997f)
	];

	public static readonly Vector3 WorldLabelLocalPosition = new (-1.39999998f, 0, 0.749999762f);
	public static readonly Rotation WorldLabelLocalRotation = new (0.707106769f, 3.09086197E-08f, 0.707106769f, -3.09086197E-08f);

	public static readonly Vector3 FirstQuadrantLocalPosition = new( 2.79999804f, 0, 0.5f );
	public static readonly Rotation FirstQuadrantLocalRotation = Rotation.FromPitch( 90f );

	public static readonly Vector3 SecondQuadrantLocalPosition = new(0f, -2.79999995f, 0.5f);
	public static readonly Rotation SecondQuadrantLocalRotation = new(0.5f,0.49999994f,-0.5f,0.49999994f);

	public static readonly Vector3 ThirdQuadrantLocalPosition = new(-2.79999995f, 0, 0.5f);
	public static readonly Rotation ThirdQuadrantLocalRotation = new(0.707106769f, -3.09086197E-08f, -0.707106769f, -3.09086197E-08f);

	public static readonly Vector3 FourthQuadrantLocalPosition = new(0, 4.39999056f, 0.5f);
	public static readonly Rotation FourthQuadrantLocalRotation = new(-0.49999997f, 0.49999997f, 0.49999997f, 0.49999997f);

	public static readonly Vector3 HitboxSize = Vector3.One * new Vector3(7f, 12f, 4f);
	public static readonly Vector3 CornerHitboxSize = Vector3.One * new Vector3(13f, 13f, 4f);
	
}

public sealed class MonopolySpace : Component
{

	[Property] public int Index { get; set; }

	public Vector3 TokenPosition => GameObject.WorldPosition;

	public TextRenderer LabelRenderer { get; private set; }
	public Sandbox.ui.MonopolySpaceLabel WorldPanelLabel { get; private set; }

	//[Property] public int SpaceIndex { get; set; }

	public BoxCollider Collider {get; private set;}

	// probably unoptimized but idgaf
	public MonopolySpaceDef Def { get; set; }

	protected override void OnStart()
	{
		Tags.Add( "monopoly_space" );
	}

	public void EnsureHitbox()
	{
		BoxCollider collider = Components.Get<BoxCollider>() ?? Components.Create<BoxCollider>();

		collider.Scale = MonopolyBoard.GetHitboxSize(Index);
		collider.Center = new Vector3(0, 0, -2f);
		collider.IsTrigger = true;

		Collider = collider;
	}

	public void EnsureDef(MonopolySpaceDef def)
	{
		Def = def;
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

		MonopolySpaceDef def = MonopolyBoard.GetSpaceDefStatic( Index );
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

		MonopolySpaceDef def = MonopolyBoard.GetSpaceDefStatic( Index );
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
		worldPanel.RenderScale = MonopolySpaceSettings.WorldPanelLabelRenderScale; //1f / MonopolySpaceSettings.WorldPanelLabelPixelsPerWorldUnit;
		worldPanel.VerticalAlign = Sandbox.WorldPanel.VAlignment.Center;
		worldPanel.HorizontalAlign = Sandbox.WorldPanel.HAlignment.Center;

		WorldPanelLabel = labelObject.Components.Create<Sandbox.ui.MonopolySpaceLabel>();
		WorldPanelLabel.SpaceName = Def.DisplayName;
		WorldPanelLabel.ColorGroup = Def.ColorGroup;
		WorldPanelLabel.SpaceType = Def.Type;
		WorldPanelLabel.SpaceIndex = Def.Index;
		WorldPanelLabel.SpaceKey = Def.Key;
		WorldPanelLabel.StateHasChanged();
	}

	private void ApplyLabelTransform( GameObject labelObject )
	{
		if ( Def.Index >= 0 && Def.Index < 11 )
		{
			labelObject.LocalPosition = MonopolySpaceSettings.FirstQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.FirstQuadrantLocalRotation;
		}
		else if ( Def.Index >= 11 && Def.Index < 20 )
		{
			labelObject.LocalPosition = MonopolySpaceSettings.SecondQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.SecondQuadrantLocalRotation;
		}
		else if ( Def.Index >= 20 && Def.Index < 30 )
		{
			labelObject.LocalPosition = MonopolySpaceSettings.ThirdQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.ThirdQuadrantLocalRotation;
		}
		else
		{
			labelObject.LocalPosition = MonopolySpaceSettings.FourthQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.FourthQuadrantLocalRotation;
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
			MonopolySpaceSettings.WorldPanelLabelZOffset
		);
		labelObject.LocalRotation = MonopolySpaceSettings.WorldLabelRotations[quad];
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
		return MonopolySpaceSettings.WorldPanelSizePixels;
		//return GetWorldPanelLabelSizeForQuadrant( hitboxSize ) *
		//	MonopolySpaceSettings.WorldPanelLabelPixelsPerWorldUnit;
	}

}
