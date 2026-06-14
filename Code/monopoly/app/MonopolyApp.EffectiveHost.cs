using Sandbox;

public partial class MonopolyApp
{
	private static MonopolyApp instance;

	[Sync] public long PreferredHostOwnerId { get; set; }
	[Sync] public bool PreferredHostDisconnected { get; set; }

	public static MonopolyApp Instance => instance;

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
		return instance?.PreferredHostOwnerId ?? 0L;
	}

	public static void SetPreferredHostOwnerId( long ownerId )
	{
		if ( instance is null )
			return;

		instance.PreferredHostOwnerId = ownerId;
	}

	public static bool GetPreferredHostDisconnected()
	{
		return instance?.PreferredHostDisconnected ?? false;
	}

	public static void SetPreferredHostDisconnected( bool disconnected )
	{
		if ( instance is null )
			return;

		instance.PreferredHostDisconnected = disconnected;
	}

	public static void EnsurePreferredHostOwnerId()
	{
		if ( instance is null || instance.PreferredHostOwnerId != 0 )
			return;

		instance.PreferredHostOwnerId = Connection.Local?.SteamId ?? Connection.Host?.SteamId ?? 0L;
		instance.PreferredHostDisconnected = false;
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
