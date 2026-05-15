using Sandbox;
using System;

public sealed class MonopolyLobbyPlayer
{
	public long OwnerId { get; set; }
	public string Name { get; set; } = "";
	public bool IsReady { get; set; }
	public bool IsLocal { get; set; }
}

public sealed class MonopolyLobbyController : Component
{
	[Property] public MonopolyGameConfig Config { get; set; } = new();
	[Property, Sync] public NetDictionary<string, bool> ReadyPlayers { get; set; } = new();

	public int MinPlayers => Math.Max( Config.MinPlayers, 1 );
	public int MaxPlayers => Math.Max( Config.MaxPlayers, MinPlayers );

	public List<MonopolyLobbyPlayer> Players { get; private set; } = new();

	public MonopolyLobbyPlayer LocalPlayer =>
		GetLocalPlayer();

	public bool CanStartGame =>
		CanStartWithPlayers( Players );

	protected override void OnStart()
	{
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

	public bool TrySetReady( long ownerId, bool isReady )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !HasConnection( ownerId ) )
			return false;

		ReadyPlayers[GetReadyKey( ownerId )] = isReady;
		return true;
	}
	
	private List<MonopolyLobbyPlayer> BuildPlayers()
	{
		var players = new List<MonopolyLobbyPlayer>();
		var connections = GetConnections();
		var localSteamId = GetLocalSteamId();
		var limit = Math.Min( MaxPlayers, connections.Count );

		for ( var i = 0; i < limit; i++ )
		{
			var connection = connections[i];
			var player = new MonopolyLobbyPlayer
			{
				OwnerId = connection.SteamId,
				Name = connection.DisplayName ?? "Player",
				IsLocal = localSteamId.HasValue && connection.SteamId == localSteamId.Value
			};

			if ( ReadyPlayers.TryGetValue( GetReadyKey( player.OwnerId ), out var isReady ) )
				player.IsReady = isReady;

			players.Add( player );
		}

		return players;
	}

	private MonopolyLobbyPlayer GetLocalPlayer()
	{
		var localSteamId = GetLocalSteamId();
		if ( !localSteamId.HasValue )
			return null;

		var players = Players ?? new();
		for ( var i = 0; i < players.Count; i++ )
		{
			if ( players[i].OwnerId == localSteamId.Value )
				return players[i];
		}

		return null;
	}

	private bool CanStartWithPlayers( List<MonopolyLobbyPlayer> players )
	{
		if ( players is null || players.Count < MinPlayers || players.Count == 0 )
			return false;

		for ( var i = 0; i < players.Count; i++ )
		{
			if ( !players[i].IsReady )
				return false;
		}

		return true;
	}

	private static List<Connection> GetConnections()
	{
		try
		{
			var connections = new List<Connection>();
			foreach ( var connection in Connection.All )
			{
				if ( connection is not null )
					connections.Add( connection );
			}

			return connections;
		}
		catch
		{
			return new List<Connection>();
		}
	}

	private static bool HasConnection( long steamId )
	{
		return HasConnection( GetReadyKey( steamId ) );
	}

	private static bool HasConnection( string steamId )
	{
		var connections = GetConnections();
		for ( var i = 0; i < connections.Count; i++ )
		{
			if ( GetReadyKey( connections[i].SteamId ) == steamId )
				return true;
		}

		return false;
	}

	private static long? GetLocalSteamId()
	{
		try
		{
			return Connection.Local?.SteamId;
		}
		catch
		{
			return null;
		}
	}
	
	public bool TryStartGame()
	{
		if ( !Networking.IsHost || !CanStartGame )
			return false;

		MonopolyMatchBootstrap.PrepareGame( Config, Players.Count );
		LoadGameScene();
		return true;
	}

	private static string GetReadyKey( long steamId )
	{
		return steamId.ToString();
	}

	[Rpc.Broadcast]
	private void LoadGameScene()
	{
		MonopolySceneFlow.LoadGame( Scene );
	}

	[Rpc.Broadcast]
	private void LoadMenuScene()
	{
		MonopolySceneFlow.LoadMenu( Scene );
	}

	[Rpc.Host]
	public void RequestSetReady( bool isReady )
	{
		if ( Rpc.Caller is null )
			return;

		TrySetReady( Rpc.Caller.SteamId, isReady );
	}

	[Rpc.Host]
	public void RequestStartGame()
	{
		TryStartGame();
	}

	[Rpc.Host]
	public void RequestBackToMenu()
	{
		if ( !Networking.IsHost )
			return;

		LoadMenuScene();
	}
}
