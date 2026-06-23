using Sandbox;
using System;

public sealed class TurnCrownParticles : Component
{
	[Property] public bool IsActive { get; set; }

    [Header( "Scale" )]
    public float HoloScale { get; set; } = 0.15f;

	[Header( "Shape" )]
	public float Height { get; set; } = 2f;
	[Property] public float Radius { get; set; } = 18f;
	[Property] public float UpperRadius { get; set; } = 12f;
	[Property] public int RingParticles { get; set; } = 28;
	[Property] public int CrownPoints { get; set; } = 5;

	[Header( "Motion" )]
	[Property] public float SpinSpeed { get; set; } = 85f;
	public float BobAmount { get; set; } = 1.5f;
	[Property] public float BobSpeed { get; set; } = 2.25f;
	[Property] public float PulseSpeed { get; set; } = 4f;

	[Header( "Particle" )]
	[Property] public Sprite ParticleSprite { get; set; }
	[Property] public float ParticleLifetime { get; set; } = 0.22f;
	[Property] public float ParticleSize { get; set; } = 3.2f;
	[Property] public float EmitRate { get; set; } = 1f / 30f;

	[Header( "Color" )]
	[Property] public Color RingColor { get; set; } = new Color( 1.0f, 0.78f, 0.18f, 0.75f );
	[Property] public Color SparkColor { get; set; } = new Color( 1.0f, 0.95f, 0.55f, 0.95f );

    private Vector3 LocalOffset { get; set; } = new( 1.5f, 0, 0 );

	private GameObject EffectObject;
	private ParticleEffect Effect;
	private ParticleSpriteRenderer Renderer;

	private TimeUntil NextEmit;

	protected override void OnStart()
	{
		EffectObject = new GameObject( true, "Turn Crown Particle Effect" );
		EffectObject.SetParent( GameObject );

        GameObject.LocalPosition += LocalOffset;

		Effect = EffectObject.Components.Create<ParticleEffect>();
		Effect.MaxParticles = 256;
		Effect.Lifetime = ParticleLifetime;
		Effect.LocalSpace = 256f;

		Renderer = EffectObject.Components.Create<ParticleSpriteRenderer>();
		Renderer.Additive = true;
		Renderer.Lighting = false;
		Renderer.Shadows = false;
		Renderer.Scale = 1f;
		Renderer.DepthFeather = 8f;

		if ( ParticleSprite is not null )
			Renderer.Sprite = ParticleSprite;

		NextEmit = 0f;
		SetActive( false );
	}

	protected override void OnUpdate()
	{
		if ( EffectObject is null )
			return;

		if ( !IsActive )
		{
			EffectObject.Enabled = false;
			return;
		}

		EffectObject.Enabled = true;

		var time = Time.Now;

		var bob = MathF.Sin( time * BobSpeed ) * BobAmount;
		EffectObject.LocalPosition = Vector3.Up * (Height + bob);

		if ( NextEmit )
		{
			NextEmit = EmitRate;
			EmitCrownFrame();
		}
	}

	public void SetActive( bool active )
	{
		IsActive = active;

		if ( EffectObject is not null )
			EffectObject.Enabled = active;

		if ( !active && Effect is not null )
			Effect.Clear();
	}

	private void EmitCrownFrame()
	{
		var time = Time.Now;
		var spin = time * SpinSpeed;
		var pulse = 1f + MathF.Sin( time * PulseSpeed ) * 0.08f;

		EmitRing(
			radius: Radius * pulse,
			z: 0f,
			angleOffset: spin,
			color: RingColor,
			size: ParticleSize
		);

		EmitRing(
			radius: UpperRadius * pulse,
			z: 8f,
			angleOffset: -spin * 1.25f,
			color: RingColor.WithAlpha( 0.55f ),
			size: ParticleSize * 0.85f
		);

		EmitCrownTips(
			radius: Radius * 0.8f * pulse,
			z: 13f,
			angleOffset: spin,
			color: SparkColor,
			size: ParticleSize * 1.35f
		);
	}

	private void EmitRing( float radius, float z, float angleOffset, Color color, float size )
	{
		for ( var i = 0; i < RingParticles; i++ )
		{
			var t = i / (float)RingParticles;
			var angle = t * MathF.PI * 2f + angleOffset.DegreeToRadian();

			var pos = new Vector3(
				MathF.Cos( angle ) * radius,
				MathF.Sin( angle ) * radius,
				z
			);

			var tangent = new Vector3(
				-MathF.Sin( angle ),
				MathF.Cos( angle ),
				0f
			);

			EmitParticle(
				pos,
				tangent * 2.5f + Vector3.Up * 0.25f,
				color,
				size
			);
		}
	}

	private void EmitCrownTips( float radius, float z, float angleOffset, Color color, float size )
	{
		for ( var i = 0; i < CrownPoints; i++ )
		{
			var t = i / (float)CrownPoints;
			var angle = t * MathF.PI * 2f + angleOffset.DegreeToRadian();

			var basePos = new Vector3(
				MathF.Cos( angle ) * radius,
				MathF.Sin( angle ) * radius,
				z
			);

			var tipPos = basePos + Vector3.Up * 8f;

			EmitParticle(
				tipPos,
				Vector3.Up * 3f,
				color,
				size
			);
		}
	}

	/*private void EmitParticle( Vector3 localPosition, Vector3 localVelocity, Color color, float size )
    {
        if ( Effect is null || EffectObject is null )
            return;

        var worldPosition =
            EffectObject.WorldPosition +
            EffectObject.WorldRotation * localPosition;

        var worldVelocity =
            EffectObject.WorldRotation * localVelocity;

        var particle = Effect.Emit( worldPosition, Time.Delta );

        particle.Position = worldPosition;
        particle.StartPosition = worldPosition;
        particle.Velocity = worldVelocity;
        particle.Color = color;
        particle.Alpha = color.a;
        particle.Size = Vector3.One * size;
        particle.Radius = size;
        particle.BornTime = Time.Now;
        particle.DeathTime = Time.Now + ParticleLifetime;
    }*/

    private void EmitParticle( Vector3 localPosition, Vector3 localVelocity, Color color, float size )
    {
        if ( Effect is null || EffectObject is null )
            return;

            var scaledLocalPosition = localPosition * HoloScale;
            var scaledLocalVelocity = localVelocity * HoloScale;
            var scaledSize = size * HoloScale;

            var worldPosition =
                EffectObject.WorldPosition +
                EffectObject.WorldRotation * scaledLocalPosition;

            var worldVelocity =
                EffectObject.WorldRotation * scaledLocalVelocity;

            var particle = Effect.Emit( worldPosition, Time.Delta );

            particle.Position = worldPosition;
            particle.StartPosition = worldPosition;
            particle.Velocity = worldVelocity;
            particle.Color = color;
            particle.Alpha = color.a;
            particle.Size = Vector3.One * scaledSize;
            particle.Radius = scaledSize;
            particle.BornTime = Time.Now;
            particle.DeathTime = Time.Now + ParticleLifetime;
        }

    
}