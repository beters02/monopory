using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;
#if STANDALONE
using Steamworks;
#endif

namespace Sandbox.Services;

public static class RentRushService
{
	public const string DefaultSboxAuthServiceName = "sbox-network-storage";
	public const uint DefaultSteamAppId = 4745160;
	public const string DefaultSteamworksTicketIdentity = "RentRushAchievements";

#if STANDALONE
	public static async Task<SteamworksSessionResponse> AuthenticateAchievementsBackendWithSteamworksAsync(
		string backendUrl,
		uint appId,
		string ticketIdentity,
		string displayName )
	{
		if ( string.IsNullOrWhiteSpace( backendUrl ) )
			return null;

		if ( !TryInitializeSteamworks( appId ) )
			return null;

		var steamId = (long)SteamClient.SteamId.Value;
		if ( steamId == 0 )
		{
			Log.Warning( "Cannot authenticate achievements backend with Facepunch Steamworks because SteamClient.SteamId was not available." );
			return null;
		}

		var identity = string.IsNullOrWhiteSpace( ticketIdentity )
			? DefaultSteamworksTicketIdentity
			: ticketIdentity.Trim();
		Log.Info( $"Requesting Facepunch Steamworks Web API auth ticket. appId={appId} steamId={steamId} identity={identity}." );
		var ticket = await SteamUser.GetAuthTicketForWebApiAsync( identity, 10.0 );
		if ( ticket is null || ticket.Data is null || ticket.Data.Length == 0 )
		{
			Log.Warning( "Facepunch Steamworks Web API auth ticket request returned no ticket." );
			return null;
		}

		try
		{
			var ticketHex = Convert.ToHexString( ticket.Data ).ToLowerInvariant();
			Log.Info( $"Authenticating achievements backend with Facepunch Steamworks ticket. steamId={steamId} ticketLength={ticketHex.Length}." );
			return await HttpAchievementService.AuthenticateSteamworksAsync(
				backendUrl,
				steamId,
				ticketHex,
				displayName ?? ""
			);
		}
		finally
		{
			ticket.Cancel();
		}
	}

	public static async Task<SboxSessionResponse> AuthenticateAchievementsBackendWithSboxAsync( string backendUrl, string authServiceName, string displayName )
	{
		if ( string.IsNullOrWhiteSpace( backendUrl ) )
			return null;

		var steamId = GetLocalSteamId();
		if ( steamId == 0 )
		{
			Log.Warning( "Cannot authenticate achievements backend with s&box token because Game.SteamId was not available." );
			return null;
		}

		var serviceName = string.IsNullOrWhiteSpace( authServiceName )
			? DefaultSboxAuthServiceName
			: authServiceName.Trim();
		Log.Info( $"Requesting s&box auth token for achievements. service={serviceName} steamId={steamId}." );
		var token = await Auth.GetToken( serviceName );
		if ( string.IsNullOrWhiteSpace( token ) )
		{
			Log.Warning( $"s&box auth token request returned an empty token. service={serviceName}." );
			return null;
		}

		Log.Info( $"Authenticating achievements backend with s&box token. service={serviceName} steamId={steamId} tokenLength={token.Length}." );
		return await HttpAchievementService.AuthenticateSboxAsync(
			backendUrl,
			steamId,
			token,
			displayName ?? ""
		);
	}

	public static async Task<DeviceSessionResponse> AuthenticateAchievementsBackendWithDeviceAsync( string backendUrl, string displayName )
	{
		if ( string.IsNullOrWhiteSpace( backendUrl ) )
			return null;

		var identity = LoadOrCreateIdentity();
		Log.Info( $"Authenticating achievements backend with device identity. deviceId={identity.DeviceId}." );
		return await HttpAchievementService.AuthenticateDeviceAsync(
			backendUrl,
			identity.DeviceId,
			identity.Secret,
			displayName ?? ""
		);
	}

	public static string GetDeviceAuthDiagnostics()
	{
		var identity = LoadOrCreateIdentity();
		return $"steamworksValid={SteamClient.IsValid} steamworksSteamId={(SteamClient.IsValid ? SteamClient.SteamId.Value : 0)}; s&box auth service={DefaultSboxAuthServiceName}; device fallback ready. deviceId={identity.DeviceId} path={GetIdentityPath()} steamId={GetLocalSteamId()}";
	}

	private static bool TryInitializeSteamworks( uint appId )
	{
		try
		{
			if ( SteamClient.IsValid )
				return true;

			SteamClient.Init( appId, true );
			Log.Info( $"Facepunch Steamworks initialized. appId={SteamClient.AppId} steamId={SteamClient.SteamId.Value} name={SteamClient.Name}." );
			return SteamClient.IsValid;
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to initialize Facepunch Steamworks. appId={appId} error={exception.Message}" );
			return false;
		}
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

	private static LocalDeviceIdentity LoadOrCreateIdentity()
	{
		var path = GetIdentityPath();
		try
		{
			if ( File.Exists( path ) )
			{
				var existing = JsonSerializer.Deserialize<LocalDeviceIdentity>( File.ReadAllText( path ) );
				if ( IsValid( existing ) )
					return existing;
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to read achievements device identity; a new one will be created. {exception.Message}" );
		}

		var identity = new LocalDeviceIdentity
		{
			DeviceId = Guid.NewGuid().ToString( "D" ),
			Secret = Convert.ToHexString( RandomNumberGenerator.GetBytes( 32 ) ).ToLowerInvariant()
		};

		Directory.CreateDirectory( Path.GetDirectoryName( path ) );
		File.WriteAllText( path, JsonSerializer.Serialize( identity ) );
		return identity;
	}

	private static bool IsValid( LocalDeviceIdentity identity )
	{
		return identity is not null &&
			Guid.TryParse( identity.DeviceId, out _ ) &&
			!string.IsNullOrWhiteSpace( identity.Secret ) &&
			identity.Secret.Length >= 32;
	}

	private static string GetIdentityPath()
	{
		var root = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
		return Path.Combine( root, "RentRush", "achievements-device.json" );
	}
#else
	public static Task<SteamworksSessionResponse> AuthenticateAchievementsBackendWithSteamworksAsync(
		string backendUrl,
		uint appId,
		string ticketIdentity,
		string displayName )
	{
		return Task.FromResult<SteamworksSessionResponse>( null );
	}

	public static Task<SboxSessionResponse> AuthenticateAchievementsBackendWithSboxAsync( string backendUrl, string authServiceName, string displayName )
	{
		return Task.FromResult<SboxSessionResponse>( null );
	}

	public static Task<DeviceSessionResponse> AuthenticateAchievementsBackendWithDeviceAsync( string backendUrl, string displayName )
	{
		return Task.FromResult<DeviceSessionResponse>( null );
	}

	public static string GetDeviceAuthDiagnostics()
	{
		return "Device auth is only available in standalone builds.";
	}
#endif

	private sealed class LocalDeviceIdentity
	{
		public string DeviceId { get; set; } = "";
		public string Secret { get; set; } = "";
	}
}
