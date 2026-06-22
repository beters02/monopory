using Sandbox;

/// <summary>
/// Configures tabletop studio lighting for the generated board scene.
/// Reuses scene-authored lights when present; creates fill/rim only if missing.
/// </summary>
[Title( "Lighting Rig" )]
[Category( "Monopoly" )]
public sealed class LightingRig : Component
{
	[Property] public GameObject KeyLightObject { get; set; }
	[Property] public GameObject FillLightObject { get; set; }
	[Property] public GameObject RimLightObject { get; set; }
	[Property] public GameObject IndirectVolumeObject { get; set; }
	[Property] public GameObject BoardTarget { get; set; }
	[Property] public bool AutoFindSceneLights { get; set; } = true;
	[Property] public bool CreateMissingLights { get; set; } = true;

	[Property, Group( "Key Light" )] public Color KeyLightColor { get; set; } = new Color( 0.93f, 0.97f, 1f );
	[Property, Group( "Key Light" )] public float KeySkyColorStrength { get; set; } = 0.32f;

	[Property, Group( "Fill Light" )] public Color FillLightColor { get; set; } = new Color( 0.72f, 0.78f, 0.88f );
	[Property, Group( "Fill Light" )] public float FillLightIntensity { get; set; } = 0.42f;

	[Property, Group( "Rim Light" )] public Color RimLightColor { get; set; } = new Color( 0.95f, 0.92f, 1f );
	[Property, Group( "Rim Light" )] public float RimLightRadius { get; set; } = 620f;
	[Property, Group( "Rim Light" )] public float RimLightAttenuation { get; set; } = 1.35f;

	[Property, Group( "Indirect" )] public Vector3 IndirectBoundsHalfExtents { get; set; } = new Vector3( 130f, 130f, 210f );
	[Property, Group( "Indirect" )] public int IndirectProbeDensity { get; set; } = 14;
	[Property, Group( "Indirect" )] public float IndirectContrast { get; set; } = 1.05f;

	protected override void OnStart()
	{
		BoardTarget ??= Scene?.GetAllComponents<Board>().FirstOrDefault()?.GameObject;
		if ( AutoFindSceneLights )
			ResolveSceneLightReferences();

		if ( CreateMissingLights )
			EnsureFillAndRimLights();

		ApplyLightingSetup();
	}

	[Button( "Apply Lighting Setup" )]
	public void ApplyLightingSetup()
	{
		ConfigureKeyLight();
		ConfigureFillLight();
		ConfigureRimLight();
		ConfigureIndirectVolume();
	}

	private void ResolveSceneLightReferences()
	{
		KeyLightObject ??= FindSceneObjectByName( "Sun" );
		RimLightObject ??= FindSceneObjectByName( "Spot Light" );

		if ( IndirectVolumeObject is null )
		{
			foreach ( var volume in Scene.GetAllComponents<IndirectLightVolume>() )
			{
				if ( volume?.GameObject is not null )
				{
					IndirectVolumeObject = volume.GameObject;
					break;
				}
			}
		}
	}

	private GameObject FindSceneObjectByName( string objectName )
	{
		foreach ( var light in Scene.GetAllComponents<DirectionalLight>() )
		{
			if ( light?.GameObject is not null &&
				string.Equals( light.GameObject.Name, objectName, System.StringComparison.OrdinalIgnoreCase ) )
				return light.GameObject;
		}

		foreach ( var light in Scene.GetAllComponents<SpotLight>() )
		{
			if ( light?.GameObject is not null &&
				string.Equals( light.GameObject.Name, objectName, System.StringComparison.OrdinalIgnoreCase ) )
				return light.GameObject;
		}

		foreach ( var volume in Scene.GetAllComponents<IndirectLightVolume>() )
		{
			if ( volume?.GameObject is not null &&
				string.Equals( volume.GameObject.Name, objectName, System.StringComparison.OrdinalIgnoreCase ) )
				return volume.GameObject;
		}

		return null;
	}

	private void EnsureFillAndRimLights()
	{
		if ( FillLightObject is null || !FillLightObject.IsValid() )
		{
			FillLightObject = new GameObject( true, "FillLight" );
			FillLightObject.SetParent( GameObject );
			FillLightObject.LocalPosition = new Vector3( -80f, -120f, 280f );
			FillLightObject.LocalRotation = Rotation.From( 55f, 135f, 0f );
			FillLightObject.Components.Create<DirectionalLight>();
		}

		if ( RimLightObject is null || !RimLightObject.IsValid() )
		{
			RimLightObject = new GameObject( true, "RimLight" );
			RimLightObject.SetParent( GameObject );
			RimLightObject.LocalPosition = new Vector3( 40f, 160f, 360f );
			RimLightObject.LocalRotation = Rotation.From( 38f, -35f, 0f );
			RimLightObject.Components.Create<SpotLight>();
		}
	}

	private void ConfigureKeyLight()
	{
		if ( KeyLightObject is null || !KeyLightObject.IsValid() )
			return;

		KeyLightObject.Enabled = true;
		var key = KeyLightObject.GetComponent<DirectionalLight>();
		if ( key is null )
			return;

		key.LightColor = KeyLightColor;
		key.Shadows = true;
		key.ShadowHardness = 0f;
		key.SkyColor = KeyLightColor.WithAlpha( KeySkyColorStrength );
		key.FogStrength = 0.45f;
	}

	private void ConfigureFillLight()
	{
		if ( FillLightObject is null || !FillLightObject.IsValid() )
			return;

		FillLightObject.Enabled = true;
		var fill = FillLightObject.GetComponent<DirectionalLight>();
		if ( fill is null )
			return;

		fill.LightColor = FillLightColor.WithAlpha( FillLightIntensity );
		fill.Shadows = false;
		fill.FogStrength = 0.2f;
	}

	private void ConfigureRimLight()
	{
		if ( RimLightObject is null || !RimLightObject.IsValid() )
			return;

		RimLightObject.Enabled = true;
		var rim = RimLightObject.GetComponent<SpotLight>();
		if ( rim is null )
			return;

		rim.LightColor = RimLightColor;
		rim.Radius = RimLightRadius;
		rim.Attenuation = RimLightAttenuation;
		rim.ConeInner = 72f;
		rim.ConeOuter = 48f;
		rim.Shadows = true;
		rim.ShadowHardness = 0f;
		rim.FogStrength = 0.35f;
	}

	private void ConfigureIndirectVolume()
	{
		if ( IndirectVolumeObject is null || !IndirectVolumeObject.IsValid() )
			return;

		var volume = IndirectVolumeObject.GetComponent<IndirectLightVolume>();
		if ( volume is null )
			return;

		var center = BoardTarget is not null && BoardTarget.IsValid()
			? BoardTarget.WorldPosition + new Vector3( 0f, 0f, 90f )
			: IndirectVolumeObject.WorldPosition;

		IndirectVolumeObject.WorldPosition = center;
		volume.Bounds = new BBox( -IndirectBoundsHalfExtents, IndirectBoundsHalfExtents );
		volume.ProbeDensity = IndirectProbeDensity;
		volume.Contrast = IndirectContrast;
		volume.NormalBias = 12f;
	}
}
