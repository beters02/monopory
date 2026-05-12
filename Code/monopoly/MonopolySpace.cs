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
}

public static class MonopolySpaceSettings
{
	public static readonly float DefaultScale = 0.009f;

	public static readonly Vector3 FirstQuadrantLocalPosition = new( 2.79999804f, 0, 0.5f );
	public static readonly Rotation FirstQuadrantLocalRotation = Rotation.FromPitch( 90f );

	public static readonly Vector3 SecondQuadrantLocalPosition = new(0f, -2.79999995f, 0.5f);
	public static readonly Rotation SecondQuadrantLocalRotation = new(0.5f,0.49999994f,-0.5f,0.49999994f);

	public static readonly Vector3 ThirdQuadrantLocalPosition = new(-2.79999995f, 0, 0.5f);
	public static readonly Rotation ThirdQuadrantLocalRotation = new(0.707106769f, -3.09086197E-08f, -0.707106769f, -3.09086197E-08f);

	public static readonly Vector3 FourthQuadrantLocalPosition = new(0, 4.39999056f, 0.5f);
	public static readonly Rotation FourthQuadrantLocalRotation = new(-0.49999997f, 0.49999997f, 0.49999997f, 0.49999997f);
	
}

public sealed class MonopolySpace : Component
{
	[Property] public int Index { get; set; }

	public Vector3 TokenPosition => GameObject.WorldPosition;

	public TextRenderer LabelRenderer { get; private set; }

	[Property] public int SpaceIndex { get; set; }

	protected override void OnStart()
	{
		Tags.Add( "monopoly_space" );
	}

	protected override void OnUpdate()
	{
		if ( !Input.Pressed( "attack1" ) )
			return;

		var camera = Scene.Camera;
		if ( camera is null )
			return;

		var ray = camera.ScreenPixelToRay( Mouse.Position );

		var tr = Scene.Trace.Ray( ray, 5000f )
			.WithTag( "monopoly_space" )
			.Run();

		if ( !tr.Hit || tr.GameObject != GameObject )
			return;

		var game = Scene.GetAllComponents<MonopolyGame>().FirstOrDefault();
		game?.SelectSpace( SpaceIndex );
	}

	public void CreateLabel( MonopolySpaceDef def )
	{
		if ( LabelRenderer != null )
			return;

		var labelObject = new GameObject( true, $"Label_{Index}" );
		labelObject.SetParent( GameObject );

		if (def.Index >= 0 && def.Index < 11)
		{
			labelObject.LocalPosition = MonopolySpaceSettings.FirstQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.FirstQuadrantLocalRotation;
		} else if (def.Index >= 11 && def.Index < 20)
		{
			labelObject.LocalPosition = MonopolySpaceSettings.SecondQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.SecondQuadrantLocalRotation;
		} else if (def.Index >= 20 && def.Index < 30)
		{
			labelObject.LocalPosition = MonopolySpaceSettings.ThirdQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.ThirdQuadrantLocalRotation;
		} else
		{
			labelObject.LocalPosition = MonopolySpaceSettings.FourthQuadrantLocalPosition;
			labelObject.LocalRotation = MonopolySpaceSettings.FourthQuadrantLocalRotation;
		}

		LabelRenderer = labelObject.Components.Create<TextRenderer>();

		TextRendering.Scope scope = LabelRenderer.TextScope;
		scope.Shadow.Enabled = true;
		scope.FilterMode = Sandbox.Rendering.FilterMode.Anisotropic;
		LabelRenderer.TextScope = scope;

		LabelRenderer.Text = def.DisplayName;
		LabelRenderer.FontSize = 128;
		LabelRenderer.Color = new(0.5f, 0.5f, 0.5f);
		LabelRenderer.Scale = def.TextScale;
		LabelRenderer.FontWeight = 800;		
	}
}