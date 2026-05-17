using System;
using Sandbox;

namespace BoardTesting;

/// <summary>
/// Experimental physical Monopoly board generator. Attach this to an empty
/// GameObject in board_testing.scene to build a mesh-based board at runtime.
/// </summary>
public sealed class ProceduralMeshBoard : Component
{
	[Property] public bool RebuildOnStart { get; set; } = true;
	[Property] public bool RebuildInEditorOnEnable { get; set; } = true;
	[Property] public bool ClearExistingGeneratedObjects { get; set; } = true;
	[Property] public Material BoardMaterial { get; set; }
	[Property] public Material SpaceMaterial { get; set; }
	[Property] public Material AccentMaterial { get; set; }
	[Property] public float SpaceLength { get; set; } = 40f;
	[Property] public float SpaceDepth { get; set; } = 64f;
	[Property] public float CornerSize { get; set; } = 64f;
	[Property] public float BoardThickness { get; set; } = 2f;
	[Property] public float SpaceThickness { get; set; } = 1.4f;
	[Property] public float SpaceGap { get; set; } = 0.7f;
	[Property] public float AccentDepth { get; set; } = 10f;
	[Property] public float AccentHeight { get; set; } = 0.35f;
	[Property] public float LabelHeight { get; set; } = 2.6f;
	[Property] public bool CreateLabels { get; set; } = true;

	private readonly List<GameObject> generatedObjects = new();

	protected override void OnEnabled()
	{
		if ( RebuildInEditorOnEnable && !Game.IsPlaying )
			Rebuild();
	}

	protected override void OnStart()
	{
		if ( RebuildOnStart )
			Rebuild();
	}

	[Button( "Rebuild Mesh Board" )]
	public void Rebuild()
	{
		if ( ClearExistingGeneratedObjects )
			ClearGeneratedObjects();

		BoardMaterial ??= Material.Load( "materials/dev/simple/simple_tile.vmat" );
		SpaceMaterial ??= Material.Load( "materials/dev/simple/simple_tile.vmat" );
		AccentMaterial ??= SpaceMaterial;

		var root = CreateGeneratedObject( "GeneratedMeshBoard" );
		root.SetParent( GameObject );
		root.LocalPosition = Vector3.Zero;
		root.LocalRotation = Rotation.Identity;

		CreateBoxMesh(
			root,
			"BoardBase",
			Vector3.Zero,
			new Vector3( BoardSize, BoardSize, BoardThickness ),
			BoardMaterial,
			new Color( 0.04f, 0.42f, 0.62f )
		);

		foreach ( var def in BoardData.CreateSpaceDefs() )
		{
			var rect = GetSpaceRect( def.Index );
			var center = new Vector3( rect.Center.x, rect.Center.y, BoardThickness * 0.5f + SpaceThickness * 0.5f );
			var size = new Vector3( MathF.Max( 1f, rect.Width - SpaceGap ), MathF.Max( 1f, rect.Height - SpaceGap ), SpaceThickness );

			var spaceObject = CreateBoxMesh(
				root,
				$"Space_{def.Index:00}_{def.Key}",
				center,
				size,
				SpaceMaterial,
				GetSpaceColor( def )
			);

			if ( def.Type == SpaceType.Property && def.ColorGroup != ColorGroup.None )
				CreatePropertyAccent( spaceObject, def, size );

			if ( CreateLabels )
				CreateLabel( spaceObject, def, size );
		}

		Log.Info( $"ProceduralMeshBoard generated {generatedObjects.Count} objects under {GameObject.Name}." );
	}

	private float BoardSize => CornerSize * 2f + SpaceLength * 9f;
	private float HalfBoard => BoardSize * 0.5f;
	private float InnerMin => -HalfBoard + CornerSize;
	private float InnerMax => HalfBoard - CornerSize;

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

		foreach ( var child in GameObject.Children.Where( x => x.Name.StartsWith( "GeneratedMeshBoard" ) ).ToArray() )
			child.Destroy();
	}

	private GameObject CreateBoxMesh( GameObject parent, string name, Vector3 center, Vector3 size, Material material, Color color )
	{
		var go = CreateGeneratedObject( name );
		go.SetParent( parent );
		go.LocalPosition = center;

		var mesh = new PolygonMesh();
		AddBox( mesh, new BBox( size * -0.5f, size * 0.5f ), material, color );
		mesh.Rebuild();

		var meshComponent = go.Components.Create<MeshComponent>( false );
		meshComponent.Mesh = mesh;
		meshComponent.SmoothingAngle = 0f;
		meshComponent.Color = color;
		meshComponent.RebuildMesh();
		meshComponent.Enabled = true;

		return go;
	}

	private void AddBox( PolygonMesh mesh, BBox box, Material material, Color color )
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

	private void AddFace( PolygonMesh mesh, Material material, Color color, params Vector3[] points )
	{
		var vertices = mesh.AddVertices( points );
		var face = mesh.AddFace( vertices );

		if ( material is not null )
			mesh.SetFaceMaterial( face, material );

		foreach ( var halfEdge in mesh.GetFaceEdges( face ) )
			mesh.SetVertexColor( halfEdge, color );
	}

	private void CreatePropertyAccent( GameObject parent, SpaceDef def, Vector3 parentSize )
	{
		var side = GetBoardSide( def.Index );
		var accentSize = parentSize;
		var accentCenter = new Vector3( 0f, 0f, parentSize.z * 0.5f + AccentHeight * 0.5f );

		if ( side is BoardSide.Bottom or BoardSide.Top )
		{
			accentSize.y = AccentDepth;
			accentSize.z = AccentHeight;
			accentCenter.y = side == BoardSide.Bottom
				? (parentSize.y - AccentDepth) * 0.5f
				: (AccentDepth - parentSize.y) * 0.5f;
		}
		else
		{
			accentSize.x = AccentDepth;
			accentSize.z = AccentHeight;
			accentCenter.x = side == BoardSide.Left
				? (parentSize.x - AccentDepth) * 0.5f
				: (AccentDepth - parentSize.x) * 0.5f;
		}

		CreateBoxMesh(
			parent,
			$"Accent_{def.Index:00}_{def.ColorGroup}",
			accentCenter,
			accentSize,
			AccentMaterial,
			GetColorGroupColor( def.ColorGroup )
		);
	}

	private void CreateLabel( GameObject parent, SpaceDef def, Vector3 parentSize )
	{
		if ( string.IsNullOrWhiteSpace( def.DisplayName ) )
			return;

		var labelObject = CreateGeneratedObject( $"Label_{def.Index:00}" );
		labelObject.SetParent( parent );
		labelObject.LocalPosition = new Vector3( 0f, 0f, parentSize.z * 0.5f + LabelHeight );
		labelObject.LocalRotation = GetLabelRotation( def.Index );
		labelObject.LocalScale = Vector3.One * GetLabelScale( def );

		var label = labelObject.Components.Create<TextRenderer>();
		label.Text = def.DisplayName.Replace( "\n", " " );
		label.FontSize = def.Index % 10 == 0 ? 56 : 34;
		label.FontWeight = 800;
		label.Color = new Color( 0.06f, 0.05f, 0.04f );
		label.Scale = 0.035f;
	}

	private Rect GetSpaceRect( int index )
	{
		if ( index == 0 )
			return FromCenter( HalfBoard - CornerSize * 0.5f, -HalfBoard + CornerSize * 0.5f, CornerSize, CornerSize );
		if ( index == 10 )
			return FromCenter( -HalfBoard + CornerSize * 0.5f, -HalfBoard + CornerSize * 0.5f, CornerSize, CornerSize );
		if ( index == 20 )
			return FromCenter( -HalfBoard + CornerSize * 0.5f, HalfBoard - CornerSize * 0.5f, CornerSize, CornerSize );
		if ( index == 30 )
			return FromCenter( HalfBoard - CornerSize * 0.5f, HalfBoard - CornerSize * 0.5f, CornerSize, CornerSize );

		if ( index is > 0 and < 10 )
			return FromCenter( InnerMax - (index - 0.5f) * SpaceLength, -HalfBoard + CornerSize * 0.5f, SpaceLength, CornerSize );

		if ( index is > 10 and < 20 )
			return FromCenter( -HalfBoard + CornerSize * 0.5f, InnerMin + (index - 10.5f) * SpaceLength, CornerSize, SpaceLength );

		if ( index is > 20 and < 30 )
			return FromCenter( InnerMin + (index - 20.5f) * SpaceLength, HalfBoard - CornerSize * 0.5f, SpaceLength, CornerSize );

		return FromCenter( HalfBoard - CornerSize * 0.5f, InnerMax - (index - 30.5f) * SpaceLength, CornerSize, SpaceLength );
	}

	private static Rect FromCenter( float x, float y, float width, float height )
	{
		return new Rect( x - width * 0.5f, y - height * 0.5f, width, height );
	}

	private static BoardSide GetBoardSide( int index )
	{
		if ( index < 10 )
			return BoardSide.Bottom;
		if ( index < 20 )
			return BoardSide.Left;
		if ( index < 30 )
			return BoardSide.Top;

		return BoardSide.Right;
	}

	private static Rotation GetLabelRotation( int index )
	{
		return GetBoardSide( index ) switch
		{
			BoardSide.Bottom => Rotation.Identity,
			BoardSide.Left => Rotation.FromYaw( -90f ),
			BoardSide.Top => Rotation.FromYaw( 180f ),
			BoardSide.Right => Rotation.FromYaw( 90f ),
			_ => Rotation.Identity
		};
	}

	private static float GetLabelScale( SpaceDef def )
	{
		return def.Index % 10 == 0 ? 1.2f : 1f;
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

internal enum BoardSide
{
	Bottom,
	Left,
	Top,
	Right
}

internal static class ProceduralMeshBoardColorExtensions
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
