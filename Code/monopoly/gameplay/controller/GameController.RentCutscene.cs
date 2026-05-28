using System;
using Sandbox;

public sealed partial class GameController : Component
{
	private const float RentCutsceneDuration = 2.0f;
	private const float RentCutsceneCameraDistance = 165f;
	private const float RentCutsceneCameraPitch = 34f;
	private const float RentCutsceneMaxLifetime = 2.0f;
	private const float RentCutsceneFadePhaseDuration = 0.18f;

	private bool isRentCutsceneActive;
	private float rentCutsceneStartedAt;
	private int rentCutscenePayerIndex = -1;
	private int rentCutsceneReceiverIndex = -1;
	private int rentCutsceneAmount;
	private Vector3 rentCutscenePayerBasePosition;
	private Vector3 rentCutsceneReceiverBasePosition;

	public bool IsLocalRentCutsceneActive => isRentCutsceneActive;
	public float LocalRentCutsceneFadeAlpha { get; private set; }

	[Rpc.Broadcast]
	private void PlayRentCutscene( int payerIndex, int receiverIndex, int amount )
	{
		if ( MatchState != MatchLifecycleState.InGame )
			return;

		var payer = Players.ElementAtOrDefault( payerIndex );
		var receiver = Players.ElementAtOrDefault( receiverIndex );
		if ( payer is null || receiver is null || payer == receiver )
			return;

		var payerToken = GetPlayerToken( payer );
		var receiverToken = GetPlayerToken( receiver );
		if ( payerToken?.GameObject is null || receiverToken?.GameObject is null )
			return;

		isRentCutsceneActive = true;
		rentCutsceneStartedAt = Time.Now;
		rentCutscenePayerIndex = payerIndex;
		rentCutsceneReceiverIndex = receiverIndex;
		rentCutsceneAmount = Math.Max( amount, 0 );
		rentCutscenePayerBasePosition = payerToken.GameObject.WorldPosition;
		rentCutsceneReceiverBasePosition = receiverToken.GameObject.WorldPosition;
		LocalRentCutsceneFadeAlpha = 1f;

		Log.Info( $"Rent cutscene started. payer={payerIndex}, receiver={receiverIndex}, amount={rentCutsceneAmount}" );
	}

	private void UpdateRentCutscene()
	{
		if ( !isRentCutsceneActive )
			return;

		var payer = Players.ElementAtOrDefault( rentCutscenePayerIndex );
		var receiver = Players.ElementAtOrDefault( rentCutsceneReceiverIndex );
		var payerToken = GetPlayerToken( payer );
		var receiverToken = GetPlayerToken( receiver );
		if ( payerToken?.GameObject is null || receiverToken?.GameObject is null )
		{
			StopRentCutscene( "missing token" );
			return;
		}

		if ( Input.Pressed( "Jump" ) || Input.Pressed( "attack1" ) || Input.Pressed( "attack2" ) )
		{
			StopRentCutscene( "skipped" );
			return;
		}

		var elapsed = Time.Now - rentCutsceneStartedAt;
		if ( elapsed >= RentCutsceneDuration || elapsed >= RentCutsceneMaxLifetime )
		{
			StopRentCutscene( "completed" );
			return;
		}

		var progress = MathX.Clamp( elapsed / RentCutsceneDuration, 0f, 1f );
		LocalRentCutsceneFadeAlpha = GetFadeAlpha( progress );
		var direction = (rentCutscenePayerBasePosition - rentCutsceneReceiverBasePosition).WithZ( 0f );
		if ( direction.Length < 0.001f )
			direction = Vector3.Forward;
		direction = direction.Normal;

		var receiverLunge = GetReceiverLungeOffset( progress );
		var payerKnockback = GetPayerKnockbackOffset( progress );
		receiverToken.GameObject.WorldPosition = rentCutsceneReceiverBasePosition + direction * receiverLunge;
		payerToken.GameObject.WorldPosition = rentCutscenePayerBasePosition + direction * payerKnockback;

		var center = (payerToken.GameObject.WorldPosition + receiverToken.GameObject.WorldPosition) * 0.5f;
		var yaw = Rotation.LookAt( direction, Vector3.Up ).Angles().yaw;
		GameCamera.Instance?.SetCinematicView( center, RentCutsceneCameraDistance, yaw, RentCutsceneCameraPitch, false );
	}

	private void StopRentCutscene( string reason )
	{
		if ( !isRentCutsceneActive )
			return;

		var payer = Players.ElementAtOrDefault( rentCutscenePayerIndex );
		var receiver = Players.ElementAtOrDefault( rentCutsceneReceiverIndex );
		var payerToken = GetPlayerToken( payer );
		var receiverToken = GetPlayerToken( receiver );

		if ( payerToken?.GameObject is not null )
			payerToken.GameObject.WorldPosition = rentCutscenePayerBasePosition;

		if ( receiverToken?.GameObject is not null )
			receiverToken.GameObject.WorldPosition = rentCutsceneReceiverBasePosition;

		GameCamera.Instance?.ClearCinematicView();
		isRentCutsceneActive = false;
		LocalRentCutsceneFadeAlpha = 0f;
		rentCutsceneStartedAt = 0f;
		rentCutscenePayerIndex = -1;
		rentCutsceneReceiverIndex = -1;
		rentCutsceneAmount = 0;
		Log.Info( $"Rent cutscene ended ({reason})." );
	}

	private static float GetReceiverLungeOffset( float progress )
	{
		if ( progress < 0.15f )
			return 0f;

		if ( progress < 0.35f )
		{
			var t = (progress - 0.15f) / 0.20f;
			return MathX.Lerp( 0f, 7f, t );
		}

		if ( progress < 0.55f )
		{
			var t = (progress - 0.35f) / 0.20f;
			return MathX.Lerp( 7f, 0f, t );
		}

		return 0f;
	}

	private static float GetPayerKnockbackOffset( float progress )
	{
		if ( progress < 0.35f )
			return 0f;

		if ( progress < 0.55f )
		{
			var t = (progress - 0.35f) / 0.20f;
			return MathX.Lerp( 0f, 13f, t );
		}

		if ( progress < 0.85f )
		{
			var t = (progress - 0.55f) / 0.30f;
			return MathX.Lerp( 13f, 0f, t );
		}

		return 0f;
	}

	private static float GetFadeAlpha( float progress )
	{
		if ( progress < RentCutsceneFadePhaseDuration )
		{
			var t = progress / RentCutsceneFadePhaseDuration;
			return MathX.Lerp( 1f, 0f, t );
		}

		if ( progress > 1f - RentCutsceneFadePhaseDuration )
		{
			var t = (progress - (1f - RentCutsceneFadePhaseDuration)) / RentCutsceneFadePhaseDuration;
			return MathX.Lerp( 0f, 1f, t );
		}

		return 0f;
	}
}
