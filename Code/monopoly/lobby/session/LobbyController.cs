using Sandbox;
using System;

public sealed partial class LobbyController : Component
{
	public MatchConfig Config;
	[Property, Sync] public string HostedConfigSnapshot { get; set; } = "";
	[Property, Sync] public NetDictionary<string, bool> ReadyPlayers { get; set; } = new();
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
		CanStartGame && (!OnlyHostStartsGame || Networking.IsHost);

	protected override void OnStart()
	{
		GameAssets.PrewarmUiAssets();
		SteamInviteBridge.Register( Scene );

		ApplyHostedConfig();

		Players = BuildPlayers();
	}

	protected override void OnDestroy()
	{
	}

	protected override void OnUpdate()
	{
		ApplyHostedConfigSnapshot();
		Players = BuildPlayers();

		if ( !Networking.IsHost )
			return;

		var ownerIds = new List<string>( ReadyPlayers.Keys );
		foreach ( var ownerId in ownerIds )
		{
			if ( HasConnection( ownerId ) )
				continue;

			ReadyPlayers.Remove( ownerId );
		}
	}
}
