using System;
using System.Text.RegularExpressions;

public partial class MonopolyApp
{
	public static string GetPlayerDisplayName( long steamId )
	{
		var lobbyPlayer = LobbyRef?.Players.FirstOrDefault( player => player is not null && player.SteamId == steamId );
		if ( lobbyPlayer is not null )
			return lobbyPlayer.Name;

		var gamePlayer = GameRef?.Players.FirstOrDefault( player => player is not null && player.SteamId == steamId );
		if ( gamePlayer is not null )
			return gamePlayer.PlayerName;

		return null;
	}

	public static Connection GetConnectionForPlayer( PlayerState player )
	{
		return Connection.All.FirstOrDefault( c => c.SteamId == player.OwnerId );
	}

	public static Connection GetConnectionForPlayer( LobbyPlayer player )
	{
		return Connection.All.FirstOrDefault( c => c.SteamId == player.OwnerId );
	}

    public static bool TryGetPlayerFullNameFromString( string playerString, out string playerName )
    {
        
        if ( LobbyRef is not null && LobbyRef.IsValid )
        {
            LobbyPlayer lobbyPlayer = ResolvePlayerReference( LobbyRef.Players, playerString );
            if ( lobbyPlayer is not null )
            {
                playerName = lobbyPlayer.Name;
                return true;
            }
        }

        if ( GameRef is not null && GameRef.IsValid )
        {
            PlayerState gamePlayer = ResolvePlayerReference( GameRef.Players, playerString );
            if ( gamePlayer is not null )
            {
                playerName = gamePlayer.PlayerName;
                return true;
            }
        }

        playerName = "";
        return false;
    }

    public static bool TryResolvePlayerReferenceSmart( string playerString, out PlayerState gamePlayer, out LobbyPlayer lobbyPlayer )
    {
        gamePlayer = null;
        lobbyPlayer = null;
        if ( LobbyRef is not null && LobbyRef.IsValid )
        {
            LobbyPlayer attemptedLobbyPlayer = ResolvePlayerReference( LobbyRef.Players, playerString );
            if ( attemptedLobbyPlayer is not null )
            {
                lobbyPlayer = attemptedLobbyPlayer;
                return true;
            }
        }

        if ( GameRef is not null && GameRef.IsValid )
        {
            PlayerState attemptedGamePlayer = ResolvePlayerReference( GameRef.Players, playerString );
            if ( attemptedGamePlayer is not null )
            {
                gamePlayer = attemptedGamePlayer;
                return true;
            }
        }

        return false;
    }

	public static IEnumerable<string> GetPlayerReferenceAutocompleteNames( string playerString = "" )
	{
		var candidates = new List<string>();

		if ( LobbyRef is not null && LobbyRef.IsValid )
		{
			candidates.AddRange( LobbyRef.Players
				.Where( player => player is not null && !player.IsAbandoned )
				.Select( player => (player.Name ?? "").Trim() ) );
		}

		if ( GameRef is not null && GameRef.IsValid )
		{
			candidates.AddRange( GameRef.Players
				.Where( player => player is not null && player.IsAssigned && !player.IsBankrupt )
				.Select( player => (player.PlayerName ?? "").Trim() ) );
		}

		var reference = TrimPlayerReference( playerString ?? "" );
		var normalizedReference = NormalizePlayerReference( reference );

		return candidates
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.Where( name => IsPlayerAutocompleteMatch( name, reference, normalizedReference ) )
			.OrderBy( name => GetPlayerAutocompleteRank( name, reference, normalizedReference ) )
			.ThenBy( name => name.Length )
			.ThenBy( name => name );
	}

	public static PlayerState ResolvePlayerReference( IEnumerable<PlayerState> players, string playerString, Connection caller = null, PlayerState localPlayer = null )
	{
		return ResolvePlayerReference(
			players,
			playerString,
			caller,
			localPlayer,
			player => player.OwnerId,
			player => player.PlayerName,
			player => player is not null && player.IsAssigned && !player.IsBankrupt );
	}

	public static LobbyPlayer ResolvePlayerReference( IEnumerable<LobbyPlayer> players, string playerString, Connection caller = null, LobbyPlayer localPlayer = null )
	{
		return ResolvePlayerReference(
			players,
			playerString,
			caller,
			localPlayer,
			player => player.OwnerId,
			player => player.Name,
			player => player is not null && !player.IsAbandoned );
	}

	private static TPlayer ResolvePlayerReference<TPlayer>(
		IEnumerable<TPlayer> players,
		string playerString,
		Connection caller,
		TPlayer localPlayer,
		Func<TPlayer, long> getOwnerId,
		Func<TPlayer, string> getPlayerName,
		Func<TPlayer, bool> canMatchByName )
		where TPlayer : class
	{
		if ( players is null || string.IsNullOrWhiteSpace( playerString ) )
			return null;

		var playerList = players.Where( player => player is not null ).ToList();
		playerString = TrimPlayerReference( playerString );

		if ( playerString.Equals( "self", StringComparison.OrdinalIgnoreCase ) ||
			playerString.Equals( "me", StringComparison.OrdinalIgnoreCase ) )
		{
			return GetPlayerForConnection( playerList, caller, getOwnerId ) ?? localPlayer;
		}

		if ( long.TryParse( playerString, out var steamId ) )
		{
			var steamIdMatch = playerList.FirstOrDefault( player => getOwnerId( player ) == steamId );
			if ( steamIdMatch is not null )
				return steamIdMatch;
		}

		if ( TryResolvePlayerByName( playerList, playerString, PlayerNameMatchMode.Exact, getPlayerName, canMatchByName, out var exactMatch ) )
			return exactMatch;

		if ( playerString.Contains( '_' ) &&
			TryResolvePlayerByName( playerList, playerString.Replace( '_', ' ' ), PlayerNameMatchMode.Exact, getPlayerName, canMatchByName, out var underscoreMatch ) )
		{
			return underscoreMatch;
		}

		if ( TryResolvePlayerByName( playerList, playerString, PlayerNameMatchMode.RegexNormalizedExact, getPlayerName, canMatchByName, out var normalizedMatch ) )
			return normalizedMatch;

		if ( TryResolvePlayerByName( playerList, playerString, PlayerNameMatchMode.Partial, getPlayerName, canMatchByName, out var partialMatch ) )
			return partialMatch;

		if ( playerString.Contains( '_' ) &&
			TryResolvePlayerByName( playerList, playerString.Replace( '_', ' ' ), PlayerNameMatchMode.Partial, getPlayerName, canMatchByName, out var underscorePartialMatch ) )
		{
			return underscorePartialMatch;
		}

		if ( TryResolvePlayerByName( playerList, playerString, PlayerNameMatchMode.RegexNormalizedPartial, getPlayerName, canMatchByName, out var normalizedPartialMatch ) )
			return normalizedPartialMatch;

		return null;
	}

	private enum PlayerNameMatchMode
	{
		Exact,
		Partial,
		RegexNormalizedExact,
		RegexNormalizedPartial
	}

	private static bool TryResolvePlayerByName<TPlayer>(
		IEnumerable<TPlayer> players,
		string playerName,
		PlayerNameMatchMode matchMode,
		Func<TPlayer, string> getPlayerName,
		Func<TPlayer, bool> canMatchByName,
		out TPlayer match )
		where TPlayer : class
	{
		match = null;

		var reference = matchMode switch
		{
			PlayerNameMatchMode.RegexNormalizedExact or PlayerNameMatchMode.RegexNormalizedPartial => NormalizePlayerReference( playerName ),
			_ => playerName
		};

		if ( string.IsNullOrWhiteSpace( reference ) )
			return false;

		var matches = players
			.Where( player => canMatchByName( player ) )
			.Where( player => IsPlayerNameMatch( getPlayerName( player ), reference, matchMode ) )
			.ToList();

		if ( matches.Count == 1 )
		{
			match = matches[0];
			return true;
		}

		if ( matches.Count > 1 )
		{
			Log.Warning( $"Multiple players matched \"{playerName}\" with {matchMode} matching." );
			return false;
		}

		return false;
	}

	private static TPlayer GetPlayerForConnection<TPlayer>( IEnumerable<TPlayer> players, Connection connection, Func<TPlayer, long> getOwnerId )
		where TPlayer : class
	{
		if ( connection is null )
			return null;

		return players.FirstOrDefault( player => getOwnerId( player ) == connection.SteamId );
	}

	private static bool IsPlayerNameMatch( string playerName, string reference, PlayerNameMatchMode matchMode )
	{
		if ( string.IsNullOrWhiteSpace( playerName ) )
			return false;

		return matchMode switch
		{
			PlayerNameMatchMode.Exact => string.Equals( playerName, reference, StringComparison.OrdinalIgnoreCase ),
			PlayerNameMatchMode.Partial => playerName.Contains( reference, StringComparison.OrdinalIgnoreCase ),
			PlayerNameMatchMode.RegexNormalizedExact => NormalizePlayerReference( playerName ) == reference,
			PlayerNameMatchMode.RegexNormalizedPartial => NormalizePlayerReference( playerName ).Contains( reference, StringComparison.OrdinalIgnoreCase ),
			_ => false
		};
	}

	private static bool IsPlayerAutocompleteMatch( string playerName, string reference, string normalizedReference )
	{
		if ( string.IsNullOrWhiteSpace( playerName ) )
			return false;

		if ( string.IsNullOrWhiteSpace( reference ) )
			return true;

		return playerName.StartsWith( reference, StringComparison.OrdinalIgnoreCase ) ||
			playerName.Contains( reference, StringComparison.OrdinalIgnoreCase ) ||
			NormalizePlayerReference( playerName ).StartsWith( normalizedReference, StringComparison.OrdinalIgnoreCase ) ||
			NormalizePlayerReference( playerName ).Contains( normalizedReference, StringComparison.OrdinalIgnoreCase );
	}

	private static int GetPlayerAutocompleteRank( string playerName, string reference, string normalizedReference )
	{
		if ( string.IsNullOrWhiteSpace( reference ) )
			return 0;

		if ( playerName.StartsWith( reference, StringComparison.OrdinalIgnoreCase ) )
			return 0;

		if ( NormalizePlayerReference( playerName ).StartsWith( normalizedReference, StringComparison.OrdinalIgnoreCase ) )
			return 1;

		if ( playerName.Contains( reference, StringComparison.OrdinalIgnoreCase ) )
			return 2;

		return 3;
	}

	private static string TrimPlayerReference( string playerString )
	{
		playerString = playerString.Trim();

		if ( playerString.Length >= 2 &&
			((playerString[0] == '"' && playerString[^1] == '"') ||
			(playerString[0] == '\'' && playerString[^1] == '\'')) )
		{
			return playerString[1..^1].Trim();
		}

		return playerString;
	}

	private static string NormalizePlayerReference( string playerString )
	{
		if ( string.IsNullOrWhiteSpace( playerString ) )
			return "";

		return Regex.Replace( playerString, @"[\W_]+", "" ).ToLowerInvariant();
	}
}
