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
	[Property] public Material SelectionMaterial { get; set; }
	[Property] public Material CanBuyMaterial { get; set; }
	[Property] public Material LandedPulseMaterial { get; set; }
	[Property] public float HoverBorderThickness { get; set; } = 0.22f;
	[Property] public float SelectedBorderThickness { get; set; } = 0.34f;
	[Property] public float BoardThickness { get; set; } = 2.8f;
	[Property] public float SpaceThickness { get; set; } = 1.1f;
	[Property] public float SpaceGap { get; set; } = 0.35f;
	[Property] public float TileBevelInset { get; set; } = 0.12f;
	[Property] public float AccentDepth { get; set; } = 2.4f;
	[Property] public float AccentHeight { get; set; } = 0.28f;
	[Property, Title( "Label Height" ), Description( "Distance in tile XY toward the outer edge. Does not change Z — use Label Surface Lift for depth." )]
	public float LabelHeight { get; set; } = 2.8f;
	[Property, Title( "Label Surface Lift" ), Description( "Z offset above the tile surface." )]
	public float LabelSurfaceLift { get; set; } = 0.08f;
	[Property] public float DetailIconSurfaceLift { get; set; } = 0.12f;
	[Property] public bool CreateLabels { get; set; } = true;
	[Property] public bool CreateDetailIcons { get; set; } = true;
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

		BoardMaterial ??= Material.Load( "materials/board/board_base.vmat" );
		SpaceMaterial ??= Material.Load( "materials/board/board_tile.vmat" );
		HoverMaterial ??= Material.Load( "materials/board/board_hover.vmat" );
		SelectionMaterial ??= Material.Load( "materials/board/board_selected.vmat" );
		CanBuyMaterial ??= Material.Load( "materials/board/board_can_buy.vmat" );
		LandedPulseMaterial ??= Material.Load( "materials/board/board_landed_pulse.vmat" );

		visualRoot = CreateGeneratedObject( "BoardVisualRoot" );
		visualRoot.SetParent( GameObject );
		visualRoot.LocalTransform = global::Transform.Zero;

		var baseSize = BoardVisualLayout.GetBoardBaseSize( Board );
		CreateBoardBase( visualRoot, baseSize );

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
			var surfaceZ = BoardThickness + SpaceThickness;
			CreateBeveledTile(
				spaceObject,
				layout.VisualSize,
				tileCenterZ,
				GetSpaceColor( def )
			);

			if ( def.Type == SpaceType.Property && def.ColorGroup != ColorGroup.None )
				CreatePropertyAccent( spaceObject, def, layout.VisualSize, tileCenterZ );

			if ( CreateDetailIcons && BoardVisualText.ShouldShowDetailIcon( def, CreateLabels ) )
				CreateSpaceDetailIcon( spaceObject, def, layout, surfaceZ );

			if ( CreateLabels )
				CreateSpaceLabel( spaceObject, def, layout, surfaceZ );

			spaceVisual.OwnershipVisual = CreateOwnershipTab(
				spaceObject,
				layout.SideIndex,
				layout.VisualSize,
				surfaceZ
			);

			var overlayZ = BoardThickness + SpaceThickness + HoverLift;
			spaceVisual.HoverVisual = CreateBorderOverlay(
				spaceObject,
				"HoverVisual",
				layout.VisualSize,
				overlayZ,
				HoverBorderThickness,
				HoverMaterial,
				new Color( 0.85f, 0.95f, 1f, 0.5f )
			);

			spaceVisual.SelectedVisual = CreateBorderOverlay(
				spaceObject,
				"SelectedVisual",
				layout.VisualSize,
				overlayZ + 0.04f,
				SelectedBorderThickness,
				SelectionMaterial,
				new Color( 1f, 0.82f, 0.18f, 0.72f )
			);

			spaceVisual.CanBuyVisual = CreateStateOverlay(
				spaceObject,
				"CanBuyVisual",
				new Vector3( layout.VisualSize.x * 0.28f, layout.VisualSize.y * 0.28f, 0.16f ),
				BoardThickness + SpaceThickness + HoverLift * 1.8f,
				new Color( 0.25f, 0.95f, 0.4f, 0.75f ),
				CanBuyMaterial
			);

			spaceVisual.LandedVisual = CreateBorderOverlay(
				spaceObject,
				"LandedVisual",
				layout.VisualSize,
				overlayZ - 0.02f,
				HoverBorderThickness * 0.85f,
				LandedPulseMaterial,
				new Color( 0.55f, 0.85f, 1f, 0.7f )
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
		Color color,
		Material material,
		Vector3? centerOverride = null )
	{
		var center = centerOverride ?? new Vector3( 0f, 0f, z );
		var overlay = CreateBoxMesh( parent, name, center, size, material, color );
		overlay.Enabled = false;
		return overlay;
	}

	private GameObject CreateBorderOverlay(
		GameObject parent,
		string name,
		Vector3 tileSize,
		float z,
		float borderThickness,
		Material material,
		Color color )
	{
		var root = CreateGeneratedObject( name );
		root.SetParent( parent );
		root.LocalPosition = Vector3.Zero;
		root.Enabled = false;

		var thickness = MathF.Max( 0.08f, borderThickness );
		var edgeHeight = 0.1f;
		var halfX = tileSize.x * 0.5f;
		var halfY = tileSize.y * 0.5f;
		var centerZ = z;

		CreateBoxMesh(
			root,
			"BorderTop",
			new Vector3( 0f, halfY - thickness * 0.5f, centerZ ),
			new Vector3( tileSize.x, thickness, edgeHeight ),
			material,
			color
		);
		CreateBoxMesh(
			root,
			"BorderBottom",
			new Vector3( 0f, -halfY + thickness * 0.5f, centerZ ),
			new Vector3( tileSize.x, thickness, edgeHeight ),
			material,
			color
		);
		CreateBoxMesh(
			root,
			"BorderLeft",
			new Vector3( -halfX + thickness * 0.5f, 0f, centerZ ),
			new Vector3( thickness, tileSize.y, edgeHeight ),
			material,
			color
		);
		CreateBoxMesh(
			root,
			"BorderRight",
			new Vector3( halfX - thickness * 0.5f, 0f, centerZ ),
			new Vector3( thickness, tileSize.y, edgeHeight ),
			material,
			color
		);

		return root;
	}

	private void CreateBoardBase( GameObject root, float baseSize )
	{
		var baseColor = new Color( 0.04f, 0.42f, 0.62f );
		CreateBoxMesh(
			root,
			"BoardBase",
			new Vector3( 0f, 0f, -BoardThickness * 0.5f ),
			new Vector3( baseSize, baseSize, BoardThickness ),
			BoardMaterial,
			baseColor
		);

		var rimHeight = 0.28f;
		var rimInset = SpaceThickness * 0.65f;
		var rimSize = new Vector3( baseSize - rimInset, baseSize - rimInset, rimHeight );
		CreateBoxMesh(
			root,
			"BoardRim",
			new Vector3( 0f, 0f, BoardThickness - rimHeight * 0.5f ),
			rimSize,
			BoardMaterial,
			baseColor.Darken( 0.12f )
		);

		var wellSize = new Vector3( baseSize * 0.72f, baseSize * 0.72f, 0.18f );
		CreateBoxMesh(
			root,
			"BoardCenterWell",
			new Vector3( 0f, 0f, BoardThickness - wellSize.z * 0.5f - 0.04f ),
			wellSize,
			BoardMaterial,
			new Color( 0.18f, 0.58f, 0.78f )
		);
	}

	private void CreateBeveledTile( GameObject parent, Vector3 size, float centerZ, Color color )
	{
		CreateBoxMesh(
			parent,
			"TileMesh",
			new Vector3( 0f, 0f, centerZ ),
			size,
			SpaceMaterial,
			color
		);

		var inset = MathF.Max( 0.02f, TileBevelInset );
		var capSize = new Vector3(
			MathF.Max( 0.35f, size.x - inset * 2f ),
			MathF.Max( 0.35f, size.y - inset * 2f ),
			MathF.Max( 0.12f, size.z * 0.34f )
		);
		var capCenterZ = centerZ + size.z * 0.28f;
		CreateBoxMesh(
			parent,
			"TileCap",
			new Vector3( 0f, 0f, capCenterZ ),
			capSize,
			SpaceMaterial,
			color.Lighten( 0.04f )
		);
	}

	private void CreateSpaceLabel( GameObject parent, SpaceDef def, BoardSpaceLayout layout, float surfaceZ )
	{
		var labelLayout = BoardVisualText.BuildSpaceLabelLayout( def, layout );
		if ( labelLayout.Lines is null || labelLayout.Lines.Count == 0 )
			return;

		var quadrant = Board.GetSpaceQuadrantIncludeCorners( def.Index );
		var anchor = BoardVisualLayout.GetLabelAnchor(
			quadrant,
			surfaceZ + LabelSurfaceLift,
			LabelHeight
		);
		var rotation = BoardVisualLayout.GetLabelRotationForQuadrant( quadrant );
		var lineCount = labelLayout.Lines.Count;
		var halfSpan = (lineCount - 1) * 0.5f;

		for ( var i = 0; i < lineCount; i++ )
		{
			var lineObject = CreateGeneratedObject( $"Label_{def.Index:00}_Line_{i}" );
			lineObject.SetParent( parent );

			var lineOffset = (i - halfSpan) * labelLayout.LineSpacing;
			var stackOffset = BoardVisualLayout.GetLabelLineTileOffset(
				rotation,
				quadrant,
				lineOffset
			);
			lineObject.LocalPosition = anchor + stackOffset;
			lineObject.LocalRotation = rotation;

			var label = lineObject.Components.Create<TextRenderer>();
			label.Text = labelLayout.Lines[i];
			label.FontSize = labelLayout.FontSize;
			label.FontWeight = 800;
			label.Color = new Color( 0.08f, 0.07f, 0.06f );
			label.Scale = labelLayout.Scale;

			var scope = label.TextScope;
			scope.Shadow.Enabled = true;
			scope.Shadow.Color = new Color( 1f, 1f, 1f, 0.65f );
			scope.Shadow.Offset = new Vector2( 2f, 2f );
			label.TextScope = scope;
		}
	}

	private void CreateSpaceDetailIcon( GameObject parent, SpaceDef def, BoardSpaceLayout layout, float surfaceZ )
	{
		var iconText = BoardVisualText.GetSpaceDetailIcon( def );
		if ( string.IsNullOrWhiteSpace( iconText ) )
			return;

		var quadrant = Board.GetSpaceQuadrantIncludeCorners( def.Index );

		var iconObject = CreateGeneratedObject( $"Detail_{def.Index:00}" );
		iconObject.SetParent( parent );
		iconObject.LocalPosition = BoardVisualLayout.GetDetailIconPosition(
			layout.SideIndex,
			layout.VisualSize,
			surfaceZ + DetailIconSurfaceLift
		);
		iconObject.LocalRotation = BoardVisualLayout.GetLabelRotationForQuadrant( quadrant );

		var label = iconObject.Components.Create<TextRenderer>();
		label.Text = iconText;
		label.FontSize = def.Type == SpaceType.CommunityChest ? 44 : 56;
		label.FontWeight = 900;
		label.Color = def.Type == SpaceType.CommunityChest
			? new Color( 0.12f, 0.45f, 0.62f )
			: new Color( 0.12f, 0.10f, 0.08f );
		label.Scale = layout.IsCorner ? 0.07f : 0.062f;
	}

	private GameObject CreateOwnershipTab( GameObject parent, int sideIndex, Vector3 tileSize, float surfaceZ )
	{
		BoardVisualLayout.GetOwnershipTabLayout( sideIndex, tileSize, surfaceZ, out var size, out var center );

		var tab = CreateBoxMesh(
			parent,
			"OwnershipVisual",
			center,
			size,
			SpaceMaterial,
			new Color( 0.85f, 0.85f, 0.85f, 0.95f )
		);
		tab.Enabled = false;
		return tab;
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
		var barDepth = BoardVisualLayout.GetColorBarDepth( sideIndex, tileSize );

		var accentSize = tileSize;
		var accentCenter = new Vector3( 0f, 0f, tileCenterZ + tileSize.z * 0.5f + AccentHeight * 0.5f );

		// BoardPanel GetColorBarRect: bar on inner edge toward board center.
		switch ( sideIndex )
		{
			case 0:
				accentSize.x = barDepth;
				accentSize.z = AccentHeight;
				accentCenter.x = (tileSize.x - barDepth) * 0.5f;
				break;
			case 1:
				accentSize.y = barDepth;
				accentSize.z = AccentHeight;
				accentCenter.y = (barDepth - tileSize.y) * 0.5f;
				break;
			case 2:
				accentSize.x = barDepth;
				accentSize.z = AccentHeight;
				accentCenter.x = (barDepth - tileSize.x) * 0.5f;
				break;
			default:
				accentSize.y = barDepth;
				accentSize.z = AccentHeight;
				accentCenter.y = (tileSize.y - barDepth) * 0.5f;
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

	public static Color Lighten( this Color color, float amount )
	{
		return new Color(
			MathF.Min( 1f, color.r + amount ),
			MathF.Min( 1f, color.g + amount ),
			MathF.Min( 1f, color.b + amount ),
			color.a
		);
	}
}
