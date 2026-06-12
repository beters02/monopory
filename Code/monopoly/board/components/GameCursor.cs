using Sandbox;

public sealed class GameCursor : Component
{

	protected override void OnUpdate()
	{
		if ( GameCamera.Instance?.IsTokenCameraActive == true )
			return;

		Mouse.Visibility = MouseVisibility.Visible;
	}
}
