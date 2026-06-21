using System;
using Sandbox;

public sealed class GambleStation : Component
{
	[Property] public int StationId { get; set; }
	[Property] public GamblePresentationMode Presentation { get; set; } = GamblePresentationMode.ThreeDimensional;
	[Property] public GameObject CameraFocus { get; set; }
	[Property] public float CameraDistance { get; set; } = 120f;
	[Property] public float CameraPitch { get; set; } = 90f;
	[Property] public float CoinRestHeight { get; set; } = 18f;
	[Property] public Vector3 CoinLocalOffset { get; set; } = Vector3.Zero;
	[Property] public float CoinFaceXRotation { get; set; } = 90f;
	[Property] public float InteractionRange { get; set; } = 70f;

	public bool IsRuntimeSessionStation { get; set; }

	private GameObject coinPivot;
	private GameObject coinVisual;
	private int visibleSessionId = -1;

	public Vector3 FocusPosition =>
		CameraFocus?.WorldPosition ??
		GameObject.WorldPosition + CoinLocalOffset + Vector3.Up * CoinRestHeight;

	protected override void OnStart()
	{
		if ( Presentation == GamblePresentationMode.ThreeDimensional )
			EnsureCoin();
	}

	protected override void OnUpdate()
	{
		var game = GameController.Instance;
		var session = game?.GetGambleSessionAtStation( StationId );
		UpdateCoin( session );

		if ( session is null && CanLocalPlayerInteract() && Input.Pressed( "Use" ) )
			Sandbox.ui.components.GambleScreen.Instance?.OpenGambleMenuFromWorld( StationId );
	}

	private void EnsureCoin()
	{
		if ( coinPivot is not null && coinPivot.IsValid() )
			return;

		var prefab = Scene.GetPrefab( "prefabs/gamble_coin.prefab" );
		if ( prefab is null )
		{
			Log.Warning( "GambleStation could not load prefabs/gamble_coin.prefab." );
			return;
		}

		coinPivot = new GameObject( true, $"GambleCoinPivot_{StationId}" );
		coinPivot.SetParent( GameObject );
		coinPivot.LocalPosition = CoinLocalOffset + Vector3.Up * CoinRestHeight;
		coinPivot.LocalRotation = Rotation.Identity;

		coinVisual = prefab.Clone();
		coinVisual.Name = $"GambleCoin_{StationId}";
		coinVisual.SetParent( coinPivot );
		coinVisual.LocalPosition = Vector3.Zero;
		ApplyCoinFaceOrientation();
		coinPivot.Enabled = false;

		var body = coinVisual.GetComponentInChildren<Rigidbody>();
		if ( body is not null )
			body.MotionEnabled = false;
	}

	private void ApplyCoinFaceOrientation()
	{
		if ( coinVisual is null )
			return;

		// Editor X rotation is the mesh correction that lays the face flat.
		// Keep it isolated from the animated pivot to avoid Euler cross-talk.
		coinVisual.LocalRotation = Rotation.FromAxis( Vector3.Forward, CoinFaceXRotation );
	}

	private void UpdateCoin( GambleSession session )
	{
		if ( Presentation != GamblePresentationMode.ThreeDimensional )
			return;

		EnsureCoin();
		if ( coinPivot is null || coinVisual is null )
			return;

		coinPivot.Enabled = session is not null;
		if ( session is null )
		{
			visibleSessionId = -1;
			return;
		}

		if ( visibleSessionId != session.Id )
		{
			visibleSessionId = session.Id;
			coinPivot.LocalPosition = CoinLocalOffset + Vector3.Up * CoinRestHeight;
			coinPivot.LocalRotation = Rotation.Identity;
			ApplyCoinFaceOrientation();
		}

		var duration = Math.Max( session.RevealAt - session.StartedAt, 0.01f );
		var progress = Math.Clamp( (Time.Now - session.StartedAt) / duration, 0f, 1f );
		var height = MathF.Sin( progress * MathF.PI ) * 34f;
		var turns = progress * 1440f;
		var resultTurn = session.OutcomeSide == CoinFlipSide.Heads ? 0f : 180f;

		coinPivot.LocalPosition = CoinLocalOffset + Vector3.Up * (CoinRestHeight + height);

		var flipDegrees = turns + resultTurn * progress;
		if ( progress >= 1f )
		{
			flipDegrees = resultTurn;
			ApplyCoinFaceOrientation();
		}

		// Flip around local Y, perpendicular to the visual's fixed face normal.
		// Landing uses the exact same axis at 0°/180°, so it cannot change planes.
		coinPivot.LocalRotation = Rotation.FromAxis( Vector3.Left, flipDegrees );
	}

	private bool CanLocalPlayerInteract()
	{
		var game = GameController.Instance;
		if ( game?.CanLocalPlayerStartNonCardGamble != true )
			return false;

		var token = game.GetLocalPlayerTokenForGambling();
		return token is not null &&
			token.GameObject.WorldPosition.Distance( GameObject.WorldPosition ) <= InteractionRange;
	}
}
