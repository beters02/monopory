namespace Sandbox;

public sealed class ObjectPrinter : Component
{
	protected override void OnUpdate()
	{
		if (Input.Keyboard.Pressed("W"))
			PrintObjects();
	}

	private void PrintObjects()
	{
		foreach(GameObject go in GameObject.Parent.Children)
		{
			Log.Info(go.Name);
		}
	}
}
