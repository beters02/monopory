namespace Sandbox;

public sealed class ScenePersistentObject : Component
{
	protected override void OnAwake()
	{
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
	}
	protected override void OnUpdate()
	{

	}
}
