namespace Sandbox;

using System.Collections.Generic;

public sealed class ScenePersistentObject : Component
{
	private static readonly Dictionary<string, GameObject> PersistentObjects = new();

	public string PersistentKey { get; set; }

	protected override void OnAwake()
	{
		var key = GetPersistentKey();

		if ( PersistentObjects.TryGetValue( key, out var existingObject ) )
		{
			if ( existingObject is not null && existingObject.IsValid() && existingObject != GameObject )
			{
				GameObject.Destroy();
				return;
			}
		}

		PersistentObjects[key] = GameObject;
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
	}

	protected override void OnDestroy()
	{
		var key = GetPersistentKey();

		if ( PersistentObjects.TryGetValue( key, out var existingObject ) && existingObject == GameObject )
			PersistentObjects.Remove( key );
	}

	private string GetPersistentKey()
	{
		if ( !string.IsNullOrWhiteSpace( PersistentKey ) )
			return PersistentKey;

		return GameObject.Name;
	}
}
