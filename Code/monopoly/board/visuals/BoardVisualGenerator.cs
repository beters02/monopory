using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

/// <summary>
/// Builds a mesh-based Monopoly board from SpaceDef data.
/// </summary>
[Title( "Board Visual Generator" )]
[Category( "Monopoly" )]
public sealed class BoardVisualGenerator : Component
{
	[Property] public Board Board { get; set; }
	[Property] public Material BoardMaterial { get; set; }
	[Property] public Material SpaceMaterial { get; set; }
	[Property] public Material HoverMaterial { get; set; }
	[Property] public float BoardThickness { get; set; } = 1.2f;
	[Property] public float SpaceThickness { get; set; } = 0.9f;
	[Property] public float SpaceGap { get; set; } = 0.35f;
	[Property] public float AccentDepth { get; set; } = 2.4f;
	[Property] public float AccentHeight { get; set; } = 0.28f;
	[Property] public float HoverLift { get; set; } = 0.15f;

	private readonly List<GameObject> generatedObjects = new();
	private readonly Dictionary<int, BoardSpaceVisual> spaceVisuals = new();
	private GameObject visualRoot;

	public IReadOnlyDictionary<int, BoardSpaceVisual> SpaceVisuals => spaceVisuals;

	protected override void OnStart()
	{
		Board ??= GameObject.GetComponent<Board>();
	}

	[Button( "Rebuild Generated Board" )]
	public void Rebuild()
	{
		Board ??= GameObject.GetComponent<Board>();
		if ( Board is null )
		{
			Log.Warning( "BoardVisualGenerator requires a Board component on the same GameObject." );
			return;
		}

		Board.RefreshProceduralBoardPanelReferences();
		if ( Board.ProceduralBoardPanel is null )
		{
			Log.Warning( "BoardVisualGenerator requires a BoardPanel reference for layout." );
			return;
		}

		ClearGeneratedObjects();

		BoardMaterial ??= Material.Load( "materials/dev/simple/simple_tile.vmat" );
		SpaceMaterial ??= BoardMaterial;
		HoverMaterial ??= SpaceMaterial;

		visualRoot = CreateGeneratedObject( "BoardVisualRoot" );
		visualRoot.SetParent( GameObject );
		visualRoot.LocalTransform = global::Transform.Zero;

		var baseSize = BoardVisualLayout.GetBoardBaseSize( Board );
		CreateBoxMesh(
			visualRoot,
			"BoardBase",
			new Vector3( 0f, 0f, -BoardThickness * 0.5f ),
			new Vector3( baseSize, baseSize, BoardThickness ),
			BoardMaterial,
			new Color( 0.04f, 0.42f, 0.62f )
		);

		var boardSpaces = new List<BoardSpace>( Board.SpaceCount );
		for ( var i = 0; i < Board.SpaceCount; i++ )
			boardSpaces.Add( null );

		foreach ( var def in Board.SpaceDefs.OrderBy( space => space.Index ) )
		{
			var layout = BoardVisualLayout.GetSpaceLayout(
				def.Index,
				Board,
				SpaceThickness,
				BoardThickness,
				SpaceGap
			);

			var spaceObject = CreateGeneratedObject( $"Space_{def.Index:00}_{def.Key}" );
			spaceObject.SetParent( visualRoot );
			spaceObject.LocalPosition = layout.Position;
			spaceObject.LocalRotation = layout.Rotation;
			spaceObject.LocalScale = Vector3.One;

			var boardSpace = spaceObject.Components.Create<BoardSpace>();
			boardSpace.Index = def.Index;
			boardSpace.SetHitboxOverride( layout.ColliderSize, layout.ColliderCenter );

			var spaceVisual = spaceObject.Components.Create<BoardSpaceVisual>();
			spaceVisual.SpaceIndex = def.Index;
			spaceVisual.SpaceKey = def.Key ?? "";
			spaceVisuals[def.Index] = spaceVisual;

			var tileCenterZ = BoardThickness + SpaceThickness * 0.5f;
			CreateBoxMesh(
				spaceObject,
				"TileMesh",
				new Vector3( 0f, 0f, tileCenterZ ),
				layout.VisualSize,
				SpaceMaterial,
				GetSpaceColor( def )
			);

			if ( def.Type == SpaceType.Property && def.ColorGroup != ColorGroup.None )
				CreatePropertyAccent( spaceObject, def, layout.VisualSize, tileCenterZ );

			spaceVisual.HoverVisual = CreateStateOverlay(
				spaceObject,
				"HoverVisual",
				layout.VisualSize,
				BoardThickness + SpaceThickness + HoverLift,
				new Color( 1f, 1f, 1f, 0.22f )
			);

			spaceVisual.SelectedVisual = CreateStateOverlay(
				spaceObject,
				"SelectedVisual",
				layout.VisualSize,
				BoardThickness + SpaceThickness + HoverLift * 1.5f,
				new Color( 1f, 0.85f, 0.2f, 0.35f )
			);

			spaceVisual.CanBuyVisual = CreateStateOverlay(
				spaceObject,
				"CanBuyVisual",
				new Vector3( layout.VisualSize.x * 0.35f, layout.VisualSize.y * 0.35f, 0.2f ),
				BoardThickness + SpaceThickness + HoverLift * 2f,
				new Color( 0.2f, 0.95f, 0.35f, 0.55f )
			);

			spaceVisual.OwnershipVisual = CreateStateOverlay(
				spaceObject,
				"OwnershipVisual",
				new Vector3( layout.VisualSize.x * 0.22f, layout.VisualSize.y * 0.22f, 0.25f ),
				BoardThickness + SpaceThickness + HoverLift * 0.5f,
				Color.White
			);

			spaceVisual.ClearState();
			boardSpaces[def.Index] = boardSpace;
		}

		Board.SetGeneratedSpaces( boardSpaces );
		Log.Info( $"BoardVisualGenerator built {spaceVisuals.Count} spaces under {GameObject.Name}." );
	}

	public bool TryGetSpaceVisual( int spaceIndex, out BoardSpaceVisual visual ) =>
		spaceVisuals.TryGetValue( spaceIndex, out visual );

	private GameObject CreateStateOverlay(
		GameObject parent,
		string name,
		Vector3 size,
		float z,
		Color color )
	{
		var overlay = CreateBoxMesh( parent, name, new Vector3( 0f, 0f, z ), size, HoverMaterial, color );
		overlay.Enabled = false;
		return overlay;
	}

	private GameObject CreateGeneratedObject( string name )
	{
		var go = new GameObject( true, name );
		generatedObjects.Add( go );
		return go;
	}

	private void ClearGeneratedObjects()
	{
		foreach ( var generatedObject in generatedObjects )
			generatedObject.Destroy();

		generatedObjects.Clear();
		spaceVisuals.Clear();
		visualRoot = null;

		foreach ( var child in GameObject.Children.Where( x => x.Name is "BoardVisualRoot" ).ToArray() )
			child.Destroy();
	}

	private GameObject CreateBoxMesh(
		GameObject parent,
		string name,
		Vector3 center,
		Vector3 size,
		Material material,
		Color color )
	{
		var go = CreateGeneratedObject( name );
		go.SetParent( parent );
		go.LocalPosition = center;

		var mesh = new PolygonMesh();
		BoardVisualMeshBuilder.AddBox( mesh, new BBox( size * -0.5f, size * 0.5f ), material, color );
		mesh.Rebuild();

		var meshComponent = go.Components.Create<MeshComponent>( false );
		meshComponent.Mesh = mesh;
		meshComponent.SmoothingAngle = 0f;
		meshComponent.Color = color;
		meshComponent.RebuildMesh();
		meshComponent.Enabled = true;

		return go;
	}

	private void CreatePropertyAccent( GameObject parent, SpaceDef def, Vector3 tileSize, float tileCenterZ )
	{
		var layout = Board.Layout ?? BoardLayoutDefinition.Classic();
		var sideIndex = layout.GetSideIndex( def.Index, Board.SpaceCount );

		var accentSize = tileSize;
		var accentCenter = new Vector3( 0f, 0f, tileCenterZ + tileSize.z * 0.5f + AccentHeight * 0.5f );

		// Inner edge faces board center: bottom +X, left +Y, top -X, right -Y.
		switch ( sideIndex )
		{
			case 0:
				accentSize.x = AccentDepth;
				accentSize.z = AccentHeight;
				accentCenter.x = (tileSize.x - AccentDepth) * 0.5f;
				break;
			case 1:
				accentSize.y = AccentDepth;
				accentSize.z = AccentHeight;
				accentCenter.y = (tileSize.y - AccentDepth) * 0.5f;
				break;
			case 2:
				accentSize.x = AccentDepth;
				accentSize.z = AccentHeight;
				accentCenter.x = (AccentDepth - tileSize.x) * 0.5f;
				break;
			default:
				accentSize.y = AccentDepth;
				accentSize.z = AccentHeight;
				accentCenter.y = (AccentDepth - tileSize.y) * 0.5f;
				break;
		}

		CreateBoxMesh(
			parent,
			$"Accent_{def.Index:00}_{def.ColorGroup}",
			accentCenter,
			accentSize,
			SpaceMaterial,
			GetColorGroupColor( def.ColorGroup )
		);
	}

	private static Color GetSpaceColor( SpaceDef def )
	{
		return def.Type switch
		{
			SpaceType.Chance => new Color( 0.95f, 0.82f, 0.28f ),
			SpaceType.CommunityChest => new Color( 0.35f, 0.73f, 0.85f ),
			SpaceType.Tax => new Color( 0.92f, 0.92f, 0.86f ),
			SpaceType.Railroad => new Color( 0.82f, 0.84f, 0.82f ),
			SpaceType.Utility => new Color( 0.81f, 0.88f, 0.72f ),
			_ => new Color( 0.92f, 0.88f, 0.78f )
		};
	}

	private static Color GetColorGroupColor( ColorGroup colorGroup )
	{
		return colorGroup switch
		{
			ColorGroup.Brown => new Color( 0.40f, 0.20f, 0.10f ),
			ColorGroup.LightBlue => new Color( 0.43f, 0.82f, 0.95f ),
			ColorGroup.Pink => new Color( 0.87f, 0.28f, 0.60f ),
			ColorGroup.Orange => new Color( 0.95f, 0.47f, 0.12f ),
			ColorGroup.Red => new Color( 0.82f, 0.08f, 0.10f ),
			ColorGroup.Yellow => new Color( 0.96f, 0.84f, 0.15f ),
			ColorGroup.Green => new Color( 0.12f, 0.55f, 0.24f ),
			ColorGroup.DarkBlue => new Color( 0.05f, 0.13f, 0.48f ),
			_ => Color.White
		};
	}
}

internal static class BoardVisualMeshBuilder
{
	public static void AddBox( PolygonMesh mesh, BBox box, Material material, Color color )
	{
		var mins = box.Mins;
		var maxs = box.Maxs;

		AddFace( mesh, material, color,
			new Vector3( mins.x, mins.y, maxs.z ),
			new Vector3( maxs.x, mins.y, maxs.z ),
			new Vector3( maxs.x, maxs.y, maxs.z ),
			new Vector3( mins.x, maxs.y, maxs.z )
		);

		AddFace( mesh, material, color.Darken( 0.08f ),
			new Vector3( mins.x, maxs.y, mins.z ),
			new Vector3( maxs.x, maxs.y, mins.z ),
			new Vector3( maxs.x, mins.y, mins.z ),
			new Vector3( mins.x, mins.y, mins.z )
		);

		AddFace( mesh, material, color.Darken( 0.04f ),
			new Vector3( mins.x, maxs.y, mins.z ),
			new Vector3( mins.x, mins.y, mins.z ),
			new Vector3( mins.x, mins.y, maxs.z ),
			new Vector3( mins.x, maxs.y, maxs.z )
		);

		AddFace( mesh, material, color.Darken( 0.04f ),
			new Vector3( maxs.x, maxs.y, maxs.z ),
			new Vector3( maxs.x, mins.y, maxs.z ),
			new Vector3( maxs.x, mins.y, mins.z ),
			new Vector3( maxs.x, maxs.y, mins.z )
		);

		AddFace( mesh, material, color.Darken( 0.02f ),
			new Vector3( maxs.x, maxs.y, mins.z ),
			new Vector3( mins.x, maxs.y, mins.z ),
			new Vector3( mins.x, maxs.y, maxs.z ),
			new Vector3( maxs.x, maxs.y, maxs.z )
		);

		AddFace( mesh, material, color.Darken( 0.02f ),
			new Vector3( maxs.x, mins.y, maxs.z ),
			new Vector3( mins.x, mins.y, maxs.z ),
			new Vector3( mins.x, mins.y, mins.z ),
			new Vector3( maxs.x, mins.y, mins.z )
		);
	}

	private static void AddFace( PolygonMesh mesh, Material material, Color color, params Vector3[] points )
	{
		var vertices = mesh.AddVertices( points );
		var face = mesh.AddFace( vertices );

		if ( material is not null )
			mesh.SetFaceMaterial( face, material );

		foreach ( var halfEdge in mesh.GetFaceEdges( face ) )
			mesh.SetVertexColor( halfEdge, color );
	}
}

internal static class BoardVisualColorExtensions
{
	public static Color Darken( this Color color, float amount )
	{
		return new Color(
			MathF.Max( 0f, color.r - amount ),
			MathF.Max( 0f, color.g - amount ),
			MathF.Max( 0f, color.b - amount ),
			color.a
		);
	}
}
