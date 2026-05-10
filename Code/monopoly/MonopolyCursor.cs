using Sandbox;

public sealed class MonopolyCursor : Component
{
	protected override void OnUpdate()
	{
		Mouse.Visibility = MouseVisibility.Visible;
		Mouse.Position = Mouse.Position;
	}
}