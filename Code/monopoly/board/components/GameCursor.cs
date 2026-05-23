using Sandbox;

public sealed class GameCursor : Component
{
	private Vector2? lastMousePosition;
	private string lastCursorType;

	protected override void OnUpdate()
	{
		Mouse.Visibility = MouseVisibility.Visible;

		var currentCursorType = Mouse.CursorType;
		var isStationaryMouse =
			lastMousePosition.HasValue &&
			(Mouse.Position - lastMousePosition.Value).Length <= 0.5f;

		// Global guard for cursor drop on click-release without mouse movement.
		if ( string.IsNullOrEmpty( currentCursorType ) &&
			isStationaryMouse &&
			string.Equals( lastCursorType, "pointer", System.StringComparison.Ordinal ) )
		{
			currentCursorType = "pointer";
			Mouse.CursorType = currentCursorType;
		}

		lastMousePosition = Mouse.Position;
		lastCursorType = currentCursorType;
	}
}
