using Sandbox;

public partial class MonopolyApp
{
	private static MonopolyApp instance;

	[Sync] public long PreferredHostOwnerId { get; set; }
	[Sync] public bool PreferredHostDisconnected { get; set; }

	public static MonopolyApp Instance => GetInstance();

	public static long CurrentHostOwnerId => ResolveCurrentHostOwnerId();

	public static bool IsLocalEffectiveHost
	{
		get
		{
			var localSteamId = GetLocalSteamId();
			return localSteamId.HasValue && localSteamId.Value == CurrentHostOwnerId;
		}
	}

	public static bool IsEffectiveHostCaller( Connection caller )
	{
		if ( !Networking.IsHost )
			return false;

		if ( caller is null || caller == Connection.Local )
			return true;

		return caller.SteamId == CurrentHostOwnerId;
	}

	public static long GetPreferredHostOwnerId()
	{
		return Instance?.PreferredHostOwnerId ?? 0L;
	}

	public static void SetPreferredHostOwnerId( long ownerId )
	{
		var app = Instance;
		if ( app is null )
			return;

		app.PreferredHostOwnerId = ownerId;
	}

	public static bool GetPreferredHostDisconnected()
	{
		return Instance?.PreferredHostDisconnected ?? false;
	}

	public static void SetPreferredHostDisconnected( bool disconnected )
	{
		var app = Instance;
		if ( app is null )
			return;

		app.PreferredHostDisconnected = disconnected;
	}

	public static void EnsurePreferredHostOwnerId()
	{
		var app = Instance;
		if ( app is null || app.PreferredHostOwnerId != 0 )
			return;

		app.PreferredHostOwnerId = Connection.Local?.SteamId ?? Connection.Host?.SteamId ?? 0L;
		app.PreferredHostDisconnected = false;
	}

	private void RegisterInstance()
	{
		instance = this;
	}

	private void UnregisterInstance()
	{
		if ( instance == this )
			instance = null;
	}

	private static MonopolyApp GetInstance()
	{
		if ( instance is not null && instance.IsValid )
			return instance;

		foreach ( var scene in Scene.All )
		{
			var app = scene?.GetComponentInChildren<MonopolyApp>();
			if ( app is not null && app.IsValid )
			{
				instance = app;
				return instance;
			}
		}

		instance = null;
		return null;
	}

	private static long ResolveCurrentHostOwnerId()
	{
		var hostConnection = Connection.Host ?? Connection.Local;
		return hostConnection?.SteamId ?? 0L;
	}

	private static long? GetLocalSteamId()
	{
		return Connection.Local?.SteamId;
	}
}
