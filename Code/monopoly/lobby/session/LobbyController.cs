using Sandbox;
using System;

public sealed partial class LobbyController : Component
{
	public MatchConfig Config;
	[Property, Sync] public string HostedConfigSnapshot { get; set; } = "";
	[Property, Sync] public NetDictionary<string, bool> ReadyPlayers { get; set; } = new();
	[Property, Sync] public NetDictionary<string, string> KnownPlayerNames { get; set; } = new();
	[Property, Sync] public NetDictionary<string, float> DisconnectedPlayers { get; set; } = new();
	[Property, Sync] public long PreferredHostOwnerId { get; set; }
	private string lastAppliedHostedConfigSnapshot = "";

	public int MinPlayers => Math.Max( Config?.MinPlayers ?? 1, 1 );
	public int MaxPlayers => Math.Max( Config?.MaxPlayers ?? MinPlayers, MinPlayers );
	public bool OnlyHostStartsGame => Config?.OnlyHostStartsGame ?? true;

	public List<LobbyPlayer> Players { get; private set; } = new();

	public LobbyPlayer LocalPlayer =>
		GetLocalPlayer();

	public bool CanStartGame =>
		CanStartWithPlayers( Players );

	public bool CanLocalPlayerStartGame =>
		CanStartGame && (!OnlyHostStartsGame || IsLocalEffectiveHost);

	public bool IsLocalEffectiveHost => GetLocalSteamId() == EffectiveHostOwnerId;
	public long EffectiveHostOwnerId => ResolveEffectiveHostOwnerId();

	protected override void OnStart()
	{
		GameAssets.PrewarmUiAssets();
		SteamInviteBridge.Register( Scene );

		ApplyHostedConfig();
		if ( Networking.IsHost && PreferredHostOwnerId == 0 )
			PreferredHostOwnerId = Connection.Local?.SteamId ?? 0L;

		Players = BuildPlayers();
	}

	protected override void OnDestroy()
	{
	}

	protected override void OnUpdate()
	{
		ApplyHostedConfigSnapshot();

		if ( Networking.IsHost )
			UpdateDisconnectedPlayers();

		Players = BuildPlayers();

		if ( !Networking.IsHost )
			return;
	}

	private void UpdateDisconnectedPlayers()
	{
		var now = Time.Now;
		var timeoutSeconds = Math.Max( Config?.AbandonTimeoutSeconds ?? 180, 1 );
		var connectedBySteamId = GetConnections().ToDictionary( connection => connection.SteamId, connection => connection );

		foreach ( var connection in connectedBySteamId.Values )
		{
			var key = GetReadyKey( connection.SteamId );
			KnownPlayerNames[key] = connection.DisplayName ?? "Player";
			if ( DisconnectedPlayers.ContainsKey( key ) )
				DisconnectedPlayers.Remove( key );
		}

		foreach ( var key in KnownPlayerNames.Keys.ToList() )
		{
			if ( !long.TryParse( key, out var ownerId ) || ownerId == 0 )
				continue;

			if ( connectedBySteamId.ContainsKey( ownerId ) )
				continue;

			if ( !DisconnectedPlayers.ContainsKey( key ) )
				DisconnectedPlayers[key] = now + timeoutSeconds;
		}

		foreach ( var entry in DisconnectedPlayers.ToList() )
		{
			if ( entry.Value > now )
				continue;

			DisconnectedPlayers.Remove( entry.Key );
			ReadyPlayers.Remove( entry.Key );
			KnownPlayerNames.Remove( entry.Key );

			if ( long.TryParse( entry.Key, out var expiredOwnerId ) && PreferredHostOwnerId == expiredOwnerId )
				PreferredHostOwnerId = Connection.Host?.SteamId ?? 0L;
	}
}
}
