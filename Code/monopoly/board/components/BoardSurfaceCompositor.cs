using System;
using Sandbox;

public sealed class BoardSurfaceCompositor : Component
{
	private const string CaptureTag = "board_albedo_source";
	private const int CaptureResolution = 2048;
	private const int RefreshFrameCount = 3;

	private WorldPanel sourceWorldPanel;
	private Sandbox.ui.BoardPanel sourcePanel;
	private ModelRenderer surfaceRenderer;
	private BoardSurfaceFinish finish;
	private float boardWorldSize;
	private GameObject captureCameraObject;
	private GameObject debugSwatchObject;
	private CameraComponent captureCamera;
	private Texture renderTarget;
	private Material surfaceMaterial;
	private int lastRenderRevision = int.MinValue;
	private int refreshFramesRemaining;
	private bool initialized;

	public void Initialize(
		WorldPanel worldPanel,
		Sandbox.ui.BoardPanel boardPanel,
		ModelRenderer renderer,
		BoardSurfaceFinish surfaceFinish,
		float worldSize,
		bool showDebugSwatch )
	{
		sourceWorldPanel = worldPanel;
		sourcePanel = boardPanel;
		surfaceRenderer = renderer;
		finish = Enum.IsDefined( surfaceFinish )
			? surfaceFinish
			: BoardSurfaceFinish.SatinLaminate;
		boardWorldSize = Math.Max( 1f, worldSize );

		BuildPipeline();
		if ( showDebugSwatch )
			CreateDebugSwatch();
	}

	protected override void OnUpdate()
	{
		if ( !initialized || captureCamera is null || sourcePanel is null )
			return;

		var revision = sourcePanel.RenderRevision;
		if ( revision != lastRenderRevision )
		{
			lastRenderRevision = revision;
			refreshFramesRemaining = RefreshFrameCount;
			captureCamera.Enabled = true;
			return;
		}

		if ( refreshFramesRemaining <= 0 )
			return;

		refreshFramesRemaining--;
		if ( refreshFramesRemaining == 0 )
			captureCamera.Enabled = false;
	}

	protected override void OnDestroy()
	{
		if ( Scene?.Camera is not null )
			Scene.Camera.RenderExcludeTags.Remove( CaptureTag );

		sourceWorldPanel?.GameObject?.Tags.Remove( CaptureTag );
		captureCameraObject?.Destroy();
		debugSwatchObject?.Destroy();
		renderTarget?.Dispose();
	}

	private void BuildPipeline()
	{
		if ( sourceWorldPanel is null || sourcePanel is null || surfaceRenderer is null )
		{
			Log.Warning( "Board surface compositor is missing its panel or mesh reference." );
			return;
		}

		sourceWorldPanel.GameObject.Tags.Add( CaptureTag );
		if ( Scene?.Camera is not null )
			Scene.Camera.RenderExcludeTags.Add( CaptureTag );

		renderTarget?.Dispose();
		renderTarget = Texture.CreateRenderTarget(
			"board_albedo",
			ImageFormat.RGBA8888,
			new Vector2( CaptureResolution, CaptureResolution )
		);

		captureCameraObject?.Destroy();
		captureCameraObject = new GameObject( true, "Board Albedo Camera" );
		captureCameraObject.SetParent( sourceWorldPanel.GameObject );
		captureCameraObject.LocalPosition = Vector3.Backward * 64f;
		captureCameraObject.LocalRotation = Rotation.Identity;
		captureCameraObject.Tags.Add( "board_capture_camera" );

		captureCamera = captureCameraObject.Components.Create<CameraComponent>();
		captureCamera.IsMainCamera = false;
		captureCamera.EnablePostProcessing = false;
		captureCamera.Orthographic = true;
		captureCamera.OrthographicHeight = boardWorldSize;
		captureCamera.ZNear = 1f;
		captureCamera.ZFar = 128f;
		captureCamera.BackgroundColor = Color.Black;
		captureCamera.RenderTags.Add( CaptureTag );
		captureCamera.RenderTarget = renderTarget;

		surfaceMaterial = Material.Create(
			"materials/board_plastic/board_surface_composited.vmat",
			"shaders/complex.shader",
			true
		);
		surfaceMaterial.Set( "TextureColor", renderTarget );
		surfaceMaterial.Set(
			"TextureNormal",
			Texture.Load( "materials/board_plastic/plastic015a_2k-jpg_normaldx.jpg" )
		);
		surfaceMaterial.Set( "TextureRoughness", Texture.White );
		surfaceMaterial.Set( "g_flMetalness", 0f );
		surfaceMaterial.Set( "g_vColorTint", Color.White );
		surfaceMaterial.Set( "g_flModelTintAmount", 0f );
		surfaceMaterial.Set( "g_vTexCoordOffset", new Vector2( 0f, 1f ) );
		surfaceMaterial.Set( "g_vTexCoordScale", new Vector2( 1f, -1f ) );
		ApplyFinish();

		surfaceRenderer.MaterialOverride = surfaceMaterial;
		surfaceRenderer.Tint = Color.White;
		lastRenderRevision = int.MinValue;
		refreshFramesRemaining = RefreshFrameCount;
		captureCamera.Enabled = true;
		initialized = true;
	}

	private void CreateDebugSwatch()
	{
		debugSwatchObject?.Destroy();
		debugSwatchObject = new GameObject( true, "Board Lighting Debug Swatch" );
		debugSwatchObject.SetParent( surfaceRenderer.GameObject.Parent );
		debugSwatchObject.LocalPosition = surfaceRenderer.GameObject.LocalPosition
			+ new Vector3( boardWorldSize * 0.36f, 0f, 5f );
		debugSwatchObject.LocalRotation = Rotation.Identity;
		debugSwatchObject.LocalScale = new Vector3( 0.12f, 0.12f, 0.06f );

		var renderer = debugSwatchObject.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		var material = Material.Create(
			"materials/board_plastic/board_lighting_debug.vmat",
			"shaders/complex.shader",
			true
		);
		material.Set( "TextureColor", Texture.White );
		material.Set( "TextureRoughness", Texture.White );
		material.Set( "g_flRoughnessScaleFactor", 0.08f );
		material.Set( "g_flMetalness", 0f );
		renderer.MaterialOverride = material;
	}

	private void ApplyFinish()
	{
		var roughness = finish switch
		{
			BoardSurfaceFinish.MatteCardboard => 0.82f,
			BoardSurfaceFinish.GlossyVarnish => 0.16f,
			_ => 0.46f
		};

		surfaceMaterial.Set( "g_flRoughnessScaleFactor", roughness );
	}
}
