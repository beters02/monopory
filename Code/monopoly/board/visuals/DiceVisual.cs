using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

/// <summary>
/// Mesh-based dice face visuals. Replaces legacy Decal children on the die prefab.
/// </summary>
[Title( "Dice Visual" )]
[Category( "Monopoly" )]
[Icon( "casino" )]
public sealed class DiceVisual : Component
{
	private const string PlaneModelPath = "models/dev/plane.vmdl";

	/// <summary>Half-size of the die body box collider (50 unit cube).</summary>
	private const float CubeHalfExtent = 25f;

	/// <summary>models/dev/plane is 100 units wide; 0.5 => 50 unit face.</summary>
	private const float FacePlaneScale = 0.5f;

	private const float PipSurfaceOffset = 0.45f;
	private const float BackgroundExtraOffset = 0.55f;

	private static readonly FaceDefinition[] FaceDefinitions =
	{
		new( 1, Vector3.Down ),
		new( 2, Vector3.Up ),
		new( 3, -Vector3.Forward ),
		new( 4, Vector3.Forward ),
		new( 5, Vector3.Right ),
		new( 6, -Vector3.Right )
	};

	private readonly List<FaceLayer> faceLayers = new();
	private GameObject facesRoot;
	private bool facesBuilt;
	private ParticleGradient backgroundColor = Color.White;
	private ParticleGradient dotColor = Color.Black;
	private static Model planeModel;

	protected override void OnStart()
	{
		EnsureBuilt();
	}

	public void EnsureBuilt()
	{
		if ( facesBuilt )
			return;

		RemoveLegacyFaceChildren();
		BuildFaceMeshes();
		facesBuilt = true;
	}

	public void ApplySkin( DiceSkinDefinition diceSkin )
	{
		if ( diceSkin is null )
			return;

		EnsureBuilt();

		backgroundColor = diceSkin.BackgroundColor;
		dotColor = diceSkin.DotColor;

		var bodyRenderer = GameObject.GetComponent<ModelRenderer>();
		if ( bodyRenderer is not null )
		{
			bodyRenderer.MaterialOverride = string.IsNullOrWhiteSpace( diceSkin.BodyMaterialPath )
				? null
				: Material.Load( diceSkin.BodyMaterialPath );
		}

		RefreshFaceMaterials();
	}

	public void ApplyTintColors( ParticleGradient background, ParticleGradient dots )
	{
		EnsureBuilt();
		backgroundColor = background;
		dotColor = dots;
		RefreshFaceMaterials();
	}

	public ParticleGradient GetBackgroundColor() => backgroundColor;

	public ParticleGradient GetDotColor() => dotColor;

	private void RemoveLegacyFaceChildren()
	{
		foreach ( var child in GameObject.Children.ToArray() )
		{
			if ( child is null || !child.IsValid() )
				continue;

			if ( child.Name.StartsWith( "Side_", StringComparison.OrdinalIgnoreCase ) ||
				child.Name.Equals( "DiceFaces", StringComparison.OrdinalIgnoreCase ) )
			{
				child.Destroy();
			}
		}

		faceLayers.Clear();
		facesRoot = null;
	}

	private void BuildFaceMeshes()
	{
		planeModel ??= Model.Load( PlaneModelPath );
		if ( planeModel is null )
		{
			Log.Warning( "DiceVisual could not load plane model for dice faces." );
			return;
		}

		facesRoot = new GameObject( true, "DiceFaces" );
		facesRoot.SetParent( GameObject );
		facesRoot.LocalPosition = Vector3.Zero;
		facesRoot.LocalRotation = Rotation.Identity;
		facesRoot.LocalScale = Vector3.One;

		foreach ( var definition in FaceDefinitions )
		{
			CreateFaceLayer( definition, isBackground: true );
			CreateFaceLayer( definition, isBackground: false );
		}

		RefreshFaceMaterials();
	}

	private void CreateFaceLayer( FaceDefinition definition, bool isBackground )
	{
		var normal = definition.OutwardNormal.Normal;
		var surfaceDistance = CubeHalfExtent + (isBackground ? PipSurfaceOffset + BackgroundExtraOffset : PipSurfaceOffset);

		var layerObject = new GameObject( true, $"Face_{definition.PipValue}{(isBackground ? "_Bg" : "")}" );
		layerObject.SetParent( facesRoot );
		layerObject.LocalPosition = normal * surfaceDistance;
		layerObject.LocalRotation = GetFaceRotation( normal );
		layerObject.LocalScale = Vector3.One * FacePlaneScale;

		var renderer = layerObject.Components.Create<ModelRenderer>();
		renderer.Model = planeModel;

		faceLayers.Add( new FaceLayer( definition.PipValue, isBackground, renderer ) );
	}

	/// <summary>
	/// models/dev/plane lies in XY with its visible normal along +Z.
	/// Rotate +Z to the outward cube normal.
	/// </summary>
	private static Rotation GetFaceRotation( Vector3 outwardNormal )
	{
		outwardNormal = outwardNormal.Normal;

		if ( outwardNormal.Dot( Vector3.Up ) > 0.99f )
			return Rotation.Identity;

		if ( outwardNormal.Dot( Vector3.Up ) < -0.99f )
			return Rotation.FromAxis( Vector3.Forward, 180f );

		var axis = Vector3.Cross( Vector3.Up, outwardNormal );
		if ( axis.Length < 0.001f )
			return Rotation.Identity;

		var angleDegrees = MathF.Acos( Math.Clamp( Vector3.Dot( Vector3.Up, outwardNormal ), -1f, 1f ) ) * (180f / MathF.PI);
		return Rotation.FromAxis( axis.Normal, angleDegrees );
	}

	private void RefreshFaceMaterials()
	{
		foreach ( var layer in faceLayers )
		{
			if ( layer.Renderer is null || !layer.Renderer.IsValid() )
				continue;

			var tint = layer.IsBackground ? backgroundColor : dotColor;
			var material = GameAssets.DiceFaces.GetMaterial( layer.PipValue, layer.IsBackground );
			if ( material is null )
			{
				Log.Warning( $"DiceVisual could not load face material for pip value {layer.PipValue} (background={layer.IsBackground})." );
				continue;
			}

			layer.Renderer.MaterialOverride = material;
			layer.Renderer.Tint = ToColor( tint );
		}
	}

	public static bool TryApplySkin( GameObject dieObject, DiceSkinDefinition diceSkin )
	{
		if ( dieObject is null || diceSkin is null )
			return false;

		var visual = dieObject.GetComponent<DiceVisual>() ?? dieObject.Components.Create<DiceVisual>();
		visual.ApplySkin( diceSkin );
		return true;
	}

	private static Color ToColor( ParticleGradient gradient ) => gradient.Evaluate( 0f, 0f );

	private readonly record struct FaceDefinition( int PipValue, Vector3 OutwardNormal );

	private sealed class FaceLayer
	{
		public FaceLayer( int pipValue, bool isBackground, ModelRenderer renderer )
		{
			PipValue = pipValue;
			IsBackground = isBackground;
			Renderer = renderer;
		}

		public int PipValue { get; }
		public bool IsBackground { get; }
		public ModelRenderer Renderer { get; }
	}
}
