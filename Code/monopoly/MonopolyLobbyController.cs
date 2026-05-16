using Sandbox;
using System;
#if STANDALONE
using System.Collections;
using System.Reflection;
#endif

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
	[Property, Sync] public int HostedMinPlayers { get; set; } = 1;
	[Property, Sync] public int HostedMaxPlayers { get; set; } = 6;
	[Property, Sync] public bool HostedOnlyHostStartsGame { get; set; } = true;
	[Property, Sync] public NetDictionary<string, bool> ReadyPlayers { get; set; } = new();

	public int MinPlayers => Math.Max( HostedMinPlayers, 1 );
	public int MaxPlayers => Math.Max( HostedMaxPlayers, MinPlayers );
	public bool OnlyHostStartsGame => HostedOnlyHostStartsGame;

	public List<MonopolyLobbyPlayer> Players { get; private set; } = new();

	public MonopolyLobbyPlayer LocalPlayer =>
		GetLocalPlayer();

	public bool CanStartGame =>
		CanStartWithPlayers( Players );

	public bool CanLocalPlayerStartGame =>
		CanStartGame && (!OnlyHostStartsGame || Networking.IsHost);

	protected override void OnStart()
	{
		MonopolySteamInviteBridge.Register( Scene );

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

	private void ApplyHostedConfig()
	{
		var bootstrap = MonopolyMatchBootstrap.Current;
		if ( bootstrap?.HasConfig == true )
			Config = MonopolyMatchBootstrap.CloneConfig( bootstrap.Config );

		HostedMinPlayers = Math.Max( Config?.MinPlayers ?? 1, 1 );
		HostedMaxPlayers = Math.Max( Config?.MaxPlayers ?? HostedMinPlayers, HostedMinPlayers );
		HostedOnlyHostStartsGame = Config?.OnlyHostStartsGame ?? true;
	}

	private MonopolyGameConfig GetGameConfig()
	{
		var config = MonopolyMatchBootstrap.CloneConfig( Config );
		config.MinPlayers = MinPlayers;
		config.MaxPlayers = MaxPlayers;
		config.OnlyHostStartsGame = OnlyHostStartsGame;
		return config;
	}
	
	public bool TryStartGame()
	{
		if ( !Networking.IsHost || !CanStartGame )
			return false;

		MonopolyMatchBootstrap.PrepareGame( GetGameConfig(), Players.Count );
		LoadGameScene();
		return true;
	}

	public bool TryOpenInviteOverlay()
	{
#if STANDALONE
		if ( !Networking.IsActive )
		{
			Log.Warning( "Cannot open invite overlay because networking is not active." );
			return false;
		}

		if ( !TryGetActiveLobbyId( out var lobbyId ) )
		{
			Log.Warning( "Cannot open invite overlay because no active Steam lobby id was found." );
			return false;
		}

		if ( TryOpenGameInviteOverlay( lobbyId ) )
			return true;

		Log.Warning( "Steam friend invite overlay is not available from the public game API." );
		return false;
#else
		Log.Warning( "Steam friend invite overlay is only enabled in standalone builds." );
		return false;
#endif
	}

#if STANDALONE
	private static bool TryGetActiveLobbyId( out ulong lobbyId )
	{
		lobbyId = 0;

		try
		{
			var lobbyManagerType = FindLoadedType( "Sandbox.LobbyManager" );
			var activeLobbiesProperty = lobbyManagerType?.GetProperty( "ActiveLobbies", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
			var activeLobbies = activeLobbiesProperty?.GetValue( null ) as IEnumerable;

			if ( activeLobbies is null )
				return false;

			foreach ( var activeLobby in activeLobbies )
			{
				lobbyId = Convert.ToUInt64( activeLobby );
				if ( lobbyId != 0 )
					return true;
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to resolve active Steam lobby id: {exception.Message}" );
		}

		lobbyId = 0;
		return false;
	}

	private static bool TryOpenGameInviteOverlay( ulong lobbyId )
	{
		try
		{
			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var inviteMethod = steamFriendsType?.GetMethod( "OpenGameInviteOverlay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );

			if ( inviteMethod is null )
			{
				Log.Warning( "Steamworks.SteamFriends.OpenGameInviteOverlay was not found." );
				return false;
			}

			var parameters = inviteMethod.GetParameters();
			if ( parameters.Length != 1 )
			{
				Log.Warning( "Steamworks.SteamFriends.OpenGameInviteOverlay had an unexpected signature." );
				return false;
			}

			var lobbySteamId = CreateSteamIdArgument( lobbyId, parameters[0].ParameterType );
			if ( lobbySteamId is null )
			{
				Log.Warning( $"Could not convert lobby id {lobbyId} to {parameters[0].ParameterType.FullName}." );
				return false;
			}

			inviteMethod.Invoke( null, new[] { lobbySteamId } );
			Log.Info( $"Opened Steam invite overlay for lobby {lobbyId}." );
			return true;
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to open invite overlay: {exception.Message}" );
			return false;
		}
	}

	private static object CreateSteamIdArgument( ulong lobbyId, Type steamIdType )
	{
		if ( steamIdType == typeof( ulong ) )
			return lobbyId;

		if ( steamIdType == typeof( long ) )
			return unchecked((long)lobbyId);

		var unsignedConstructor = steamIdType.GetConstructor( new[] { typeof( ulong ) } );
		if ( unsignedConstructor is not null )
			return unsignedConstructor.Invoke( new object[] { lobbyId } );

		var signedConstructor = steamIdType.GetConstructor( new[] { typeof( long ) } );
		if ( signedConstructor is not null )
			return signedConstructor.Invoke( new object[] { unchecked((long)lobbyId) } );

		var implicitConversion = steamIdType.GetMethod(
			"op_Implicit",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
			null,
			new[] { typeof( ulong ) },
			null );

		if ( implicitConversion is not null )
			return implicitConversion.Invoke( null, new object[] { lobbyId } );

		return null;
	}

	private static Type FindLoadedType( string typeName )
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for ( var i = 0; i < assemblies.Length; i++ )
		{
			var type = assemblies[i].GetType( typeName, false );
			if ( type is not null )
				return type;
		}

		return null;
	}
#endif

	private static string GetReadyKey( long steamId )
	{
		return steamId.ToString();
	}

	[Rpc.Broadcast]
	private void LoadGameScene()
	{
		MonopolySceneFlow.LoadGame( Scene );
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
		if ( OnlyHostStartsGame && !IsHostCaller( Rpc.Caller ) )
			return;

		TryStartGame();
	}

	private bool IsHostCaller( Connection caller )
	{
		return Networking.IsHost && (caller is null || caller == Connection.Local);
	}

	public void LeaveLobby()
	{
		MonopolyNetworkSession.LeaveCurrentLobby( Scene );
	}
}
