using Sandbox;
using System;

public sealed partial class LobbyController : Component
{
	[Property] public MatchConfig Config { get; set; } = new();
	[Property, Sync] public int HostedMinPlayers { get; set; } = 1;
	[Property, Sync] public int HostedMaxPlayers { get; set; } = 6;
	[Property, Sync] public bool HostedOnlyHostStartsGame { get; set; } = true;
	[Property, Sync] public NetDictionary<string, bool> ReadyPlayers { get; set; } = new();

	public int MinPlayers => Math.Max( HostedMinPlayers, 1 );
	public int MaxPlayers => Math.Max( HostedMaxPlayers, MinPlayers );
	public bool OnlyHostStartsGame => HostedOnlyHostStartsGame;

	public List<LobbyPlayer> Players { get; private set; } = new();

	public LobbyPlayer LocalPlayer =>
		GetLocalPlayer();

	public bool CanStartGame =>
		CanStartWithPlayers( Players );

	public bool CanLocalPlayerStartGame =>
		CanStartGame && (!OnlyHostStartsGame || Networking.IsHost);

	protected override void OnStart()
	{
		SteamInviteBridge.Register( Scene );

		if ( Networking.IsHost )
			ApplyHostedConfig();

		Players = BuildPlayers();
	}

	protected override void OnDestroy()
	{
	}

	protected override void OnUpdate()
	{
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
