using Sandbox;
using System;

public sealed partial class LobbyController
{
	public bool TrySetSelectedPiece( long ownerId, string pieceId )
	{
		if ( !Networking.IsHost )
			return false;

		if ( ownerId == 0 || IsMarkedDisconnected( ownerId ) )
			return false;

		if ( !GetKnownOwnerIds().Contains( ownerId ) )
			return false;

		var normalizedPieceId = PieceCatalog.GetByIdOrDefault( pieceId ).Id;
		if ( !AchievementServices.Cosmetics.CanUseCosmetic( ownerId, CosmeticCatalog.NormalizePieceCosmeticId( normalizedPieceId ) ) )
			return false;

		SelectedPieces[GetReadyKey( ownerId )] = normalizedPieceId;
		return true;
	}

	public bool TrySetSelectedDiceSkin( long ownerId, string diceSkinId )
	{
		if ( !Networking.IsHost )
			return false;

		if ( ownerId == 0 || IsMarkedDisconnected( ownerId ) )
			return false;

		if ( !GetKnownOwnerIds().Contains( ownerId ) )
			return false;

		var normalizedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( diceSkinId ).Id;
		if ( !AchievementServices.Cosmetics.CanUseCosmetic( ownerId, CosmeticCatalog.NormalizeDiceSkinCosmeticId( normalizedDiceSkinId ) ) )
			return false;

		SelectedDiceSkins[GetReadyKey( ownerId )] = normalizedDiceSkinId;
		return true;
	}

	public string GetSelectedPieceForOwner( long ownerId )
	{
		if ( ownerId == 0 )
			return PieceCatalog.DefaultPieceId;

		if ( SelectedPieces.TryGetValue( GetReadyKey( ownerId ), out var selectedPieceId ) && PieceCatalog.IsValidPieceId( selectedPieceId ) )
		{
			var normalizedPieceId = PieceCatalog.GetByIdOrDefault( selectedPieceId ).Id;
			if ( AchievementServices.Cosmetics.CanUseCosmetic( ownerId, CosmeticCatalog.NormalizePieceCosmeticId( normalizedPieceId ) ) )
				return normalizedPieceId;
		}

		return PieceCatalog.DefaultPieceId;
	}

	public string GetSelectedDiceSkinForOwner( long ownerId )
	{
		if ( ownerId == 0 )
			return DiceSkinCatalog.DefaultDiceSkinId;

		if ( SelectedDiceSkins.TryGetValue( GetReadyKey( ownerId ), out var selectedDiceSkinId ) && DiceSkinCatalog.IsValidDiceSkinId( selectedDiceSkinId ) )
		{
			var normalizedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( selectedDiceSkinId ).Id;
			if ( AchievementServices.Cosmetics.CanUseCosmetic( ownerId, CosmeticCatalog.NormalizeDiceSkinCosmeticId( normalizedDiceSkinId ) ) )
				return normalizedDiceSkinId;
		}

		return DiceSkinCatalog.DefaultDiceSkinId;
	}

	public bool TrySetReady( long ownerId, bool isReady )
	{
		if ( !Networking.IsHost )
			return false;

		if ( IsMarkedDisconnected( ownerId ) )
			return false;

		if ( !HasConnection( ownerId ) )
			return false;

		ReadyPlayers[GetReadyKey( ownerId )] = isReady;
		return true;
	}

	private List<LobbyPlayer> BuildPlayers()
	{
		var players = new List<LobbyPlayer>();
		var localSteamId = GetLocalSteamId();
		var effectiveHostOwnerId = ResolveEffectiveHostOwnerId();
		var connectionsBySteamId = GetConnections().ToDictionary( connection => connection.SteamId, connection => connection );
		var ownerIds = GetKnownOwnerIds()
			.Where( ownerId => ownerId != 0 )
			.Distinct()
			.Take( MaxPlayers )
			.ToList();

		foreach ( var ownerId in ownerIds )
		{
			connectionsBySteamId.TryGetValue( ownerId, out var connection );
			var isDisconnected = IsMarkedDisconnected( ownerId );
			var player = new LobbyPlayer
			{
				OwnerId = ownerId,
				Name = connection?.DisplayName ?? GetKnownNameForOwner( ownerId ),
				IsLocal = localSteamId.HasValue && ownerId == localSteamId.Value,
				SelectedPieceId = GetSelectedPieceForOwner( ownerId ),
				IsReady = ReadyPlayers.TryGetValue( GetReadyKey( ownerId ), out var ready ) && ready,
				IsConnected = connection is not null && !isDisconnected,
				IsHost = ownerId == effectiveHostOwnerId,
				IsAbandoned = false,
				AbandonEndsAt = GetDisconnectedDeadline( ownerId ),
				SelectedDiceSkinId = GetSelectedDiceSkinForOwner( ownerId )
			};

			players.Add( player );
		}

		return players.OrderByDescending( player => player.IsHost ).ThenBy( player => player.Name ).ToList();
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

	private long ResolveEffectiveHostOwnerId()
	{
		var preferredHost = PreferredHostOwnerId;
		if ( preferredHost != 0 && HasConnection( preferredHost ) )
			return preferredHost;

		var hostConnection = Connection.Host;
		if ( hostConnection is not null )
			return hostConnection.SteamId;

		return 0;
	}

	private IEnumerable<long> GetKnownOwnerIds()
	{
		var knownOwnerIds = new HashSet<long>();

		foreach ( var connection in GetConnections() )
			knownOwnerIds.Add( connection.SteamId );

		foreach ( var key in ReadyPlayers.Keys )
		{
			if ( long.TryParse( key, out var ownerId ) )
				knownOwnerIds.Add( ownerId );
		}

		foreach ( var key in KnownPlayerNames.Keys )
		{
			if ( long.TryParse( key, out var ownerId ) )
				knownOwnerIds.Add( ownerId );
		}

		foreach ( var key in DisconnectedPlayers.Keys )
		{
			if ( long.TryParse( key, out var ownerId ) )
				knownOwnerIds.Add( ownerId );
		}

		return knownOwnerIds;
	}

	private string GetKnownNameForOwner( long ownerId )
	{
		if ( KnownPlayerNames.TryGetValue( GetReadyKey( ownerId ), out var name ) && !string.IsNullOrWhiteSpace( name ) )
			return name;

		return "Player";
	}

	private bool IsMarkedDisconnected( long ownerId )
	{
		return DisconnectedPlayers.ContainsKey( GetReadyKey( ownerId ) );
	}

	private float GetDisconnectedDeadline( long ownerId )
	{
		return DisconnectedPlayers.TryGetValue( GetReadyKey( ownerId ), out var abandonEndsAt ) ? abandonEndsAt : 0f;
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
