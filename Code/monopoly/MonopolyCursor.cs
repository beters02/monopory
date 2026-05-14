using Sandbox;

public sealed class MonopolyCursor : Component
{
	protected override void OnUpdate()
	{
		Mouse.Visibility = MouseVisibility.Visible;
	}
}
