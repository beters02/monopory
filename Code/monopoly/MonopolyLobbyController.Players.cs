using Sandbox;
using System;

public sealed partial class MonopolyLobbyController
{
	public bool TrySetReady( long ownerId, bool isReady )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !HasConnection( ownerId ) )
			return false;

		ReadyPlayers[GetReadyKey( ownerId )] = isReady;
		return true;
	}

	private List<LobbyPlayer> BuildPlayers()
	{
		var players = new List<LobbyPlayer>();
		var connections = GetConnections();
		var localSteamId = GetLocalSteamId();
		var limit = Math.Min( MaxPlayers, connections.Count );

		for ( var i = 0; i < limit; i++ )
		{
			var connection = connections[i];
			var player = new LobbyPlayer
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

	private LobbyPlayer GetLocalPlayer()
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

	private bool CanStartWithPlayers( List<LobbyPlayer> players )
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

	private static string GetReadyKey( long steamId )
	{
		return steamId.ToString();
	}
}
