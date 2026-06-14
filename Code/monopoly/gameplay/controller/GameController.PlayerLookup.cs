using System.Threading.Tasks;
using System;
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
		get => MonopolyApp.IsLocalEffectiveHost;
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
		return MonopolyApp.ResolvePlayerReference( Players, playerString, caller, LocalPlayer );
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
		var player = GetPlayerForCaller( caller );
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
