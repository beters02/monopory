using System;
using System.Linq;
using Sandbox;

public sealed class PiecePreview : Component
{
	private class PiecePreviewDef
	{
		public Vector3? CameraLocalTransformPosition;
		public Rotation? CameraLocalTransformRotation;
		public float? CameraDistance;
		public float? CameraHorizontalOffset;
		public float? CameraVerticalOffset;
	}

	private Dictionary<string, PiecePreviewDef> PiecePreviewDefs = new()
	{
		["cowboy_man"] = new()
		{
			CameraLocalTransformPosition = new Vector3(298.80899f,-320.370758f,9.90066051f),
			CameraLocalTransformRotation = new Rotation(-0.0504027419f, 0.331814408f, 0.146045834f, 0.930607021f),
			CameraDistance = 51.81f,
			CameraHorizontalOffset = -9.89f,
			CameraVerticalOffset = -3.59f
		}
	};

	private static PiecePreview instance;

	[Property] public float CameraDistance { get; set; } = 42.37f;
	[Property] public float CameraHorizontalOffset { get; set; } = -8.92f;
	[Property] public float CameraVerticalOffset { get; set; } = -3.03f;
	[Property] public float RotationSpeed { get; set; } = 22f;
	[Property] public float PreviewScaleMultiplier { get; set; } = 1f;

	public float DefCameraDistance;
	public float DefCameraHorizontalOffset;
	public float DefCameraVerticalOffset;
	public Vector3 DefCameraLocalTransformPosition;
	public Rotation DefCameraLocalTransformRotation;

	private GameObject visualObject;
	private string activePieceId = "";
	private bool isVisible;

	protected override void OnStart()
	{
		instance = this;
		DefCameraDistance = CameraDistance;
		DefCameraHorizontalOffset = CameraHorizontalOffset;
		DefCameraVerticalOffset = CameraVerticalOffset;

		CameraComponent cam = Scene?.Camera;
		if (cam is null)
		{
			DefCameraLocalTransformPosition = new();
			DefCameraLocalTransformRotation = new();
			return;
		}

		DefCameraLocalTransformPosition = cam.GameObject.LocalPosition;
		DefCameraLocalTransformRotation = cam.GameObject.LocalRotation;
	}

	protected override void OnDestroy()
	{
		if ( instance == this )
			instance = null;

		DestroyVisual();
	}

	protected override void OnUpdate()
	{
		if ( !isVisible )
			return;

		UpdateCameraPlacement();

		if ( visualObject is not null && visualObject.IsValid() )
			visualObject.LocalRotation = Rotation.From( 0f, Time.Now * RotationSpeed, 0f );
	}

	public static void SetActivePiece( Scene scene, PieceDefinition piece, bool visible )
	{
		if ( scene is null )
			return;

		if ( !visible && piece is null )
		{
			if ( instance is not null && instance.IsValid() && instance.Scene == scene )
				instance.SetPiece( null, false );

			return;
		}

		var preview = GetOrCreate( scene );
		preview.SetPiece( piece, visible );
	}

	private static PiecePreview GetOrCreate( Scene scene )
	{
		if ( instance is not null && instance.IsValid() && instance.Scene == scene )
			return instance;

		instance = scene.GetAllComponents<PiecePreview>().FirstOrDefault();
		if ( instance is not null && instance.IsValid() )
			return instance;

		var previewObject = new GameObject( true, "PiecePreview" );
		return previewObject.Components.Create<PiecePreview>();
	}

	private void SetPiece( PieceDefinition piece, bool visible )
	{
		isVisible = visible && piece is not null;
		if ( !isVisible )
		{
			activePieceId = "";
			DestroyVisual();
			return;
		}

		if ( string.Equals( activePieceId, piece.Id, StringComparison.OrdinalIgnoreCase ) &&
			visualObject is not null &&
			visualObject.IsValid() )
			return;

		BuildVisual( piece );
		ApplyPiecePreviewDefs( piece );
	}

	private void BuildVisual( PieceDefinition piece, bool forceNoPrefab = false )
	{
		DestroyVisual();

		// Spawn prefab for woman officer bc shes fucked
		if ( piece.Id == "officer_woman" && !forceNoPrefab)
			BuildOfficerWomanGameObject( piece );
		else
			BuildNormalGameObject( piece );

		visualObject.SetParent( GameObject );
		visualObject.LocalPosition = piece.LocalVisualOffset;

		activePieceId = piece.Id;
		UpdateCameraPlacement();
	}

	private void BuildNormalGameObject( PieceDefinition piece )
	{
		var model = Model.Load( piece.ModelPath );
		if ( model is null )
		{
			Log.Warning( $"PiecePreview could not load model '{piece.ModelPath}' for piece '{piece.Id}'." );
			activePieceId = "";
			return;
		}

		visualObject = new GameObject( true, $"PiecePreviewVisual_{piece.Id}" );
			
		var renderer = visualObject.Components.Create<SkinnedModelRenderer>();
		renderer.Model = model;
		renderer.MaterialOverride = GameAssets.Materials.Piece.Material;

		if ( !string.IsNullOrWhiteSpace( piece.AnimgraphPath ) )
		{
			var animgraph = AnimationGraph.Load( piece.AnimgraphPath );
			if ( animgraph is null )
				Log.Warning( $"PiecePreview could not load animgraph '{piece.AnimgraphPath}' for piece '{piece.Id}'." );
			else
				renderer.AnimationGraph = animgraph;
		}

		visualObject.LocalScale = piece.LocalVisualScale * PreviewScaleMultiplier;
	}

	private void BuildOfficerWomanGameObject( PieceDefinition piece )
	{
		GameObject prefab = Scene.GetPrefab( "prefabs/previewdefaultprefab.prefab" );
		if ( prefab == null )
		{
			Log.Info("Could not find officer woman preview. Spawning model.");
			BuildVisual( piece, true );
			return;
		}
		visualObject = prefab.Clone();
	}

	private void ApplyPiecePreviewDefs( PieceDefinition piece )
	{
		if (PiecePreviewDefs.TryGetValue(piece.Id, out PiecePreviewDef def))
		{
			CameraHorizontalOffset = def.CameraHorizontalOffset?? DefCameraHorizontalOffset;
			CameraDistance = def.CameraDistance?? DefCameraDistance;
			CameraVerticalOffset = def.CameraVerticalOffset?? DefCameraVerticalOffset;

			CameraComponent cam1 = Scene?.Camera;
			if ( cam1 is null )
				return;
			
			cam1.GameObject.LocalPosition = def.CameraLocalTransformPosition?? DefCameraLocalTransformPosition;
			cam1.GameObject.LocalRotation = def.CameraLocalTransformRotation?? DefCameraLocalTransformRotation;
			return;	
		}

		CameraHorizontalOffset = DefCameraHorizontalOffset;
		CameraDistance = DefCameraDistance;
		CameraVerticalOffset = DefCameraVerticalOffset;

		CameraComponent cam = Scene?.Camera;
		if ( cam is null )
			return;
		
		cam.GameObject.LocalPosition = DefCameraLocalTransformPosition;
		cam.GameObject.LocalRotation = DefCameraLocalTransformRotation;
	}

	private void UpdateCameraPlacement()
	{
		var camera = Scene?.Camera;
		if ( camera is null || camera.GameObject is null )
			return;

		var rotation = camera.GameObject.WorldRotation;
		GameObject.WorldPosition =
			camera.GameObject.WorldPosition +
			rotation.Forward * CameraDistance +
			rotation.Right * CameraHorizontalOffset +
			rotation.Up * CameraVerticalOffset;
		GameObject.WorldRotation = Rotation.Identity;
	}

	private void DestroyVisual()
	{
		if ( visualObject is not null && visualObject.IsValid() )
			visualObject.Destroy();

		visualObject = null;
	}
}
