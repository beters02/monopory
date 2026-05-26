using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	public PlayerState LocalPlayer
	{
		get
		{
			var localSteamId = GetLocalSteamId();
			if ( !localSteamId.HasValue || Players is null )
				return null;

			return Players.FirstOrDefault( p => p is not null && p.OwnerId == localSteamId.Value );
		}
	}

	public int LocalPlayerIndex => Players.IndexOf( LocalPlayer );
	public bool IsLocalEffectiveHost
	{
		get
		{
			var localSteamId = GetLocalSteamId();
			return localSteamId.HasValue && PreferredHostOwnerId != 0 && localSteamId.Value == PreferredHostOwnerId;
		}
	}

	public int GetPlayerIndex( PlayerState player )
	{
		return Players.IndexOf( player );
	}

	public PlayerState GetPlayerForString( string playerString )
	{
		return ResolvePlayerReference( playerString, null );
	}

	public PlayerState GetPlayerForIndex( int index )
	{
		return Players[index];
	}

	public PlayerState ResolvePlayerReference( string playerString, Connection caller = null )
	{
		if ( string.IsNullOrWhiteSpace( playerString ) )
			return null;

		playerString = TrimPlayerReference( playerString );

		if ( playerString.Equals( "self", StringComparison.OrdinalIgnoreCase ) ||
			playerString.Equals( "me", StringComparison.OrdinalIgnoreCase ) )
		{
			return GetPlayerForConnection( caller ) ?? LocalPlayer;
		}

		if ( long.TryParse( playerString, out var steamId ) )
		{
			var steamIdMatch = Players.FirstOrDefault( player => player is not null && player.OwnerId == steamId );
			if ( steamIdMatch is not null )
				return steamIdMatch;
		}

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.Exact, out var exactMatch ) )
			return exactMatch;

		if ( playerString.Contains( '_' ) &&
			TryResolvePlayerByName( playerString.Replace( '_', ' ' ), PlayerNameMatchMode.Exact, out var underscoreMatch ) )
		{
			return underscoreMatch;
		}

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.RegexNormalizedExact, out var normalizedMatch ) )
			return normalizedMatch;

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.Partial, out var partialMatch ) )
			return partialMatch;

		if ( playerString.Contains( '_' ) &&
			TryResolvePlayerByName( playerString.Replace( '_', ' ' ), PlayerNameMatchMode.Partial, out var underscorePartialMatch ) )
		{
			return underscorePartialMatch;
		}

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.RegexNormalizedPartial, out var normalizedPartialMatch ) )
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

	private bool TryResolvePlayerByName( string playerName, PlayerNameMatchMode matchMode, out PlayerState match )
	{
		match = null;

		var reference = matchMode switch
		{
			PlayerNameMatchMode.RegexNormalizedExact or PlayerNameMatchMode.RegexNormalizedPartial => NormalizePlayerReference( playerName ),
			_ => playerName
		};

		if ( string.IsNullOrWhiteSpace( reference ) )
			return false;

		var matches = Players
			.Where( player => player is not null && player.IsAssigned && !player.IsBankrupt )
			.Where( player => IsPlayerNameMatch( player.PlayerName, reference, matchMode ) )
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

	public bool TryNormalizePlayerIndex( int index, out int normalizedIndex )
	{
		normalizedIndex = NormalizePlayerIndex(index);
		return normalizedIndex != index;
	}

	public int NormalizePlayerIndex( int index )
	{
		if ( Players.Count <= 0 )
			return -1;

		return ((index % Players.Count) + Players.Count) % Players.Count;
	}

	private int GetPlayerIndexForCaller( Connection caller )
	{
		var player = GetPlayerForConnection( caller );
		if ( player is null )
			return -1;

		return GetPlayerIndex( player );
	}

	private List<int> GetAssignedPlayerIndexes()
	{
		return Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null && entry.player.IsAssigned && !entry.player.IsBankrupt )
			.Select( entry => entry.index )
			.ToList();
	}

	// returns false if index did not need to be normalized.
	public static bool TryNormalizeSpaceIndex(int spaceIndex, out int normalizedSpaceIndex)
	{
		normalizedSpaceIndex = NormalizeSpaceIndex(spaceIndex);
		return normalizedSpaceIndex != spaceIndex;
	}

	public static int NormalizeSpaceIndex( int spaceIndex )
	{
		return ((spaceIndex % 40) + 40) % 40;
	}
}
