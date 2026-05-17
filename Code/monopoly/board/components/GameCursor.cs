using Sandbox;

public sealed class GameCursor : Component
{
	protected override void OnUpdate()
	{
		Mouse.Visibility = MouseVisibility.Visible;
	}
}
