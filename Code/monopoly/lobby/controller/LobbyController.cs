using Sandbox;
using System;

public sealed partial class LobbyController : Component
{
	private static LobbyController instance;

	public MatchConfig Config;
	[Sync] public string HostedConfigSnapshot { get; set; } = "";
	[Sync] public NetDictionary<string, bool> ReadyPlayers { get; set; } = new();
	[Sync] public NetDictionary<string, string> KnownPlayerNames { get; set; } = new();
	[Sync] public NetDictionary<string, float> DisconnectedPlayers { get; set; } = new();
	[Sync] public NetDictionary<string, string> SelectedPieces { get; set; } = new();
	[Sync] public NetDictionary<string, string> SelectedDiceSkins { get; set; } = new();
	public long PreferredHostOwnerId
	{
		get => MonopolyApp.GetPreferredHostOwnerId();
		set => MonopolyApp.SetPreferredHostOwnerId( value );
	}
	[Sync] public string StagedLoadedSaveName { get; set; } = "";
	[Sync] public string StagedLoadedGameIdentifier { get; set; } = "";
	private string lastAppliedHostedConfigSnapshot = "";
	private float lastPreferredHostRestoreAttemptAt;
	private bool hasInitializedLobbySoundState;

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
	public bool HasStagedLoadedGame => !string.IsNullOrWhiteSpace( StagedLoadedSaveName );

	public bool IsLocalEffectiveHost => MonopolyApp.IsLocalEffectiveHost;
	public long CurrentHostOwnerId => MonopolyApp.CurrentHostOwnerId;
	public static LobbyController Instance => instance;

	protected override void OnStart()
	{
		instance = this;
		GameAssets.PrewarmUiAssets();
		SteamInviteBridge.Register( Scene );

		ApplyHostedConfig();
		if ( Networking.IsHost && PreferredHostOwnerId == 0 )
			PreferredHostOwnerId = Connection.Local?.SteamId ?? 0L;

		Players = BuildPlayers();
		LoadingState.HideAfterSceneReady();
	}

	protected override void OnDestroy()
	{
		if ( instance == this )
			instance = null;
	}

	protected override void OnUpdate()
	{
		ApplyHostedConfigSnapshot();

		if ( Networking.IsHost )
		{
			UpdateDisconnectedPlayers();
			TryRestorePreferredHost();
		}

		Players = BuildPlayers();

		if ( !Networking.IsHost )
			return;
	}

	private void PlayLobbySound( GameSound sound )
	{
		if ( !Networking.IsHost || sound is null || !sound.IsAssigned )
			return;

		PlayLobbySoundLocal( sound.Path );
	}

	[Rpc.Broadcast]
	private void PlayLobbySoundLocal( string soundPath )
	{
		if ( string.IsNullOrWhiteSpace( soundPath ) )
			return;

		new GameSound( soundPath ).Play();
	}
	private void TryRestorePreferredHost()
	{
		if ( PreferredHostOwnerId == 0 || Connection.Local?.SteamId == PreferredHostOwnerId )
			return;

		if ( PreferredHostOwnerId == CurrentHostOwnerId || IsMarkedDisconnected( PreferredHostOwnerId ) || !HasConnection( PreferredHostOwnerId ) )
			return;

		if ( Time.Now - lastPreferredHostRestoreAttemptAt < 5f )
			return;

		lastPreferredHostRestoreAttemptAt = Time.Now;

#if STANDALONE
		if ( MonopolyApp.TryTransferSteamLobbyHostSync( PreferredHostOwnerId, out var message ) )
			Log.Info( $"Restored Steam lobby host to preferred host. {message}" );
		else
			Log.Warning( $"Could not restore Steam lobby host to preferred host: {message}" );
#endif
	}

	private void UpdateDisconnectedPlayers()
	{
		var now = Time.Now;
		var timeoutSeconds = Math.Max( Config?.AbandonTimeoutSeconds ?? 180, 1 );
		var connectedBySteamId = GetConnections().ToDictionary( connection => connection.SteamId, connection => connection );

		foreach ( var connection in connectedBySteamId.Values )
		{
			var key = GetReadyKey( connection.SteamId );
			KnownPlayerNames[key] = GetConnectionPlayerName( connection );
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
			SelectedPieces.Remove( entry.Key );
			SelectedDiceSkins.Remove( entry.Key );

			if ( long.TryParse( entry.Key, out var expiredOwnerId ) && PreferredHostOwnerId == expiredOwnerId )
				PreferredHostOwnerId = Connection.Host?.SteamId ?? 0L;
	}
}
}
