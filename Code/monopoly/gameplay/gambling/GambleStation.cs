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
	[Property] public float InteractionRange { get; set; } = 70f;
	public bool IsRuntimeSessionStation { get; set; }

	private GameObject coin;
	private int visibleSessionId = -1;

	public Vector3 FocusPosition => CameraFocus?.WorldPosition ?? GameObject.WorldPosition + CoinLocalOffset + Vector3.Up * CoinRestHeight;

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
		if ( coin is not null && coin.IsValid() )
			return;

		var prefab = Scene.GetPrefab( "prefabs/gamble_coin.prefab" );
		if ( prefab is null )
		{
			Log.Warning( "GambleStation could not load prefabs/gamble_coin.prefab." );
			return;
		}

		coin = prefab.Clone();
		coin.Name = $"GambleCoin_{StationId}";
		coin.SetParent( GameObject );
		coin.LocalPosition = CoinLocalOffset + Vector3.Up * CoinRestHeight;
		coin.Enabled = false;

		var body = coin.GetComponentInChildren<Rigidbody>();
		if ( body is not null )
			body.MotionEnabled = false;
	}

	private void UpdateCoin( GambleSession session )
	{
		if ( Presentation != GamblePresentationMode.ThreeDimensional )
			return;

		EnsureCoin();
		if ( coin is null )
			return;

		coin.Enabled = session is not null;
		if ( session is null )
		{
			visibleSessionId = -1;
			return;
		}

		if ( visibleSessionId != session.Id )
		{
			visibleSessionId = session.Id;
			coin.LocalPosition = CoinLocalOffset + Vector3.Up * CoinRestHeight;
			coin.LocalRotation = Rotation.Identity;
		}

		var duration = Math.Max( session.RevealAt - session.StartedAt, 0.01f );
		var progress = Math.Clamp( (Time.Now - session.StartedAt) / duration, 0f, 1f );
		var height = MathF.Sin( progress * MathF.PI ) * 34f;
		var turns = progress * 1440f;
		var finalPitch = session.OutcomeSide == CoinFlipSide.Heads ? 0f : 180f;
		coin.LocalPosition = CoinLocalOffset + Vector3.Up * (CoinRestHeight + height);
		coin.LocalRotation = Rotation.From( turns + finalPitch * progress, turns * 0.25f, 0f );
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
