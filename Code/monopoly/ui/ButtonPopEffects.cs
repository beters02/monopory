using Sandbox;

public static class ButtonPopEffects
{
	private const int MaxEffects = 80;
	private const float EffectLifetime = 0.55f;
	public const float AnimationDuration = 0.32f;

	private const int ParticleCount = 7;
	private const int PossibleParticlePositions = 14;

	private static int nextEffectId = 1;
	private static readonly List<ButtonPopEffect> effects = new();

	public static IReadOnlyList<ButtonPopEffect> Effects
	{
		get
		{
			Update();
			return effects;
		}
	}

	public static void Show( Vector2 screenPosition )
	{
		Update();

		effects.Add( new ButtonPopEffect
		{
			Id = nextEffectId++,
			X = screenPosition.x,
			Y = screenPosition.y,
			Particles = CreateParticles(),
			StartedAt = Time.Now,
			ExpiresAt = Time.Now + EffectLifetime
		} );

		while ( effects.Count > MaxEffects )
			effects.RemoveAt( 0 );
	}

	private static List<ButtonPopParticle> CreateParticles()
	{
		var particles = new List<ButtonPopParticle>();
		var randomOffset = RandomFloat( 0f, 360f / PossibleParticlePositions );
		var slotsPerParticle = PossibleParticlePositions / ParticleCount;

		for ( var i = 0; i < ParticleCount; i++ )
		{
			var positionIndex = (i * slotsPerParticle) + System.Random.Shared.Next( slotsPerParticle );

			particles.Add( new ButtonPopParticle
			{
				Angle = ((360f / PossibleParticlePositions) * positionIndex) + randomOffset
			} );
		}

		return particles;
	}

	private static float RandomFloat( float min, float max )
	{
		return min + (System.Random.Shared.NextSingle() * (max - min));
	}

	public static void Update()
	{
		if ( effects.Count == 0 )
			return;

		effects.RemoveAll( effect => Time.Now >= effect.ExpiresAt );
	}
}

public sealed class ButtonPopEffect
{
	public int Id { get; set; }
	public float X { get; set; }
	public float Y { get; set; }
	public List<ButtonPopParticle> Particles { get; set; } = new();
	public float StartedAt { get; set; }
	public float ExpiresAt { get; set; }
}

public sealed class ButtonPopParticle
{
	public float Angle { get; set; }
}
