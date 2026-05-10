using Sandbox;

public sealed class MonopolyCamera : Component
{
	[Property] public GameObject Target { get; set; }

	[Property] public float Distance { get; set; } = 900f;
	[Property] public float Pitch { get; set; } = 60f;
	[Property] public float Fov { get; set; } = 35f;

	protected override void OnStart()
	{
		CameraComponent camera = Components.Get<CameraComponent>();

		if ( camera != null )
		{
			camera.FieldOfView = Fov;
		}
	}

	protected override void OnUpdate()
	{
		if ( Target is null )
			return;

		var center = Target.WorldPosition;

		var rotation = Rotation.From( Pitch, 0f, 0f );

		var offset =
			-rotation.Forward * Distance;

		GameObject.WorldPosition = center + offset;
		GameObject.WorldRotation = rotation;
	}
}