using System;
using System.Threading.Tasks;
using Forkbox.Steamworks;
using Sandbox;

namespace Sandbox.Services;

public static class RentRushService
{
	public const uint DefaultSteamAppId = 4745160;
	public const string DefaultForkboxTicketIdentity = null;

//do if standalone
	public static void TestInit()
	{
	}

	public static async Task<AuthToken> RequestForkboxAuthTokenAsync( string identity = null )
	{
		identity = NormalizeIdentity( identity );

		var token = await SteamUser.GetAuthTokenAsync( identity, 15.0 );
		if ( token is not null )
			return token;

		Log.Warning( "Forkbox async auth token request timed out; retrying once with the default identity." );
		await Task.Delay( 500 );

		token = await SteamUser.GetAuthTokenAsync( null, 15.0 );
		if ( token is not null )
			return token;

		Log.Warning( "Forkbox default async auth token request timed out; trying the synchronous token path." );
		return SteamUser.GetAuthToken( null );
	}

	public static async Task<string> GetForkboxAuthDiagnosticsAsync( string identity = null )
	{
		using var token = await RequestForkboxAuthTokenAsync( identity );
		return $"forkboxSteamId={GetLocalSteamId()}; tokenLength={token?.Value?.Length ?? 0}; identity={NormalizeIdentity( identity ) ?? "<default>"}";
	}

	private static string NormalizeIdentity( string identity )
	{
		if ( string.IsNullOrWhiteSpace( identity ) )
			return DefaultForkboxTicketIdentity;

		identity = identity.Trim();
		return string.Equals( identity, "null", StringComparison.OrdinalIgnoreCase ) ? null : identity;
	}

	private static long GetLocalSteamId()
	{
		try
		{
			var gameSteamId = Game.SteamId.Value;
			if ( gameSteamId != 0 )
				return gameSteamId;
		}
		catch
		{
		}

		return Connection.Local is not null ? Connection.Local.SteamId.Value : 0;
	}
//do else
/*
	public static void TestInit()
	{
	}

	public static Task<AuthToken> RequestForkboxAuthTokenAsync( string identity = null )
	{
		return Task.FromResult<AuthToken>( null );
	}

	public static Task<string> GetForkboxAuthDiagnosticsAsync( string identity = null )
	{
		return Task.FromResult( "Forkbox auth tokens are only available in standalone builds." );
	}
*/
//do endif
}
