using System;
using System.Linq;
using Sandbox;

public sealed class PiecePreview : Component
{
	private static PiecePreview instance;

	[Property] public float CameraDistance { get; set; } = 165f;
	[Property] public float CameraHorizontalOffset { get; set; } = -58f;
	[Property] public float CameraVerticalOffset { get; set; } = -32f;
	[Property] public float RotationSpeed { get; set; } = 22f;
	[Property] public float PreviewScaleMultiplier { get; set; } = 1f;

	private GameObject visualObject;
	private string activePieceId = "";
	private bool isVisible;

	protected override void OnStart()
	{
		instance = this;
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
	}

	private void BuildVisual( PieceDefinition piece )
	{
		DestroyVisual();

		// Spawn prefab for woman officer bc shes fucked
		if ( piece.Id == "officer_woman" )
		{
			
		}

		var model = Model.Load( piece.ModelPath );
		if ( model is null )
		{
			Log.Warning( $"PiecePreview could not load model '{piece.ModelPath}' for piece '{piece.Id}'." );
			activePieceId = "";
			return;
		}

		visualObject = new GameObject( true, $"PiecePreviewVisual_{piece.Id}" );
		visualObject.SetParent( GameObject );
		visualObject.LocalPosition = piece.LocalVisualOffset;
		visualObject.LocalScale = piece.LocalVisualScale * PreviewScaleMultiplier;

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

		activePieceId = piece.Id;
		UpdateCameraPlacement();
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
