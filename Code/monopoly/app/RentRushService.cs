using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace Sandbox.Services;

public static class RentRushService
{
	public const string DefaultSboxAuthServiceName = "sbox-network-storage";
	public const uint DefaultSteamAppId = 4745160;
	public const string DefaultSteamworksTicketIdentity = "RentRushAchievements";

#if STANDALONE
	private const BindingFlags StaticReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
	private const BindingFlags InstanceReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	public static void TestInit()
	{
		TryInitializeSteamworks( 4745160 );
	}

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

		var steamId = GetSteamworksSteamId();
		if ( steamId == 0 )
		{
			Log.Warning( "Cannot authenticate achievements backend with Facepunch Steamworks because SteamClient.SteamId was not available." );
			return null;
		}

		var identity = string.IsNullOrWhiteSpace( ticketIdentity )
			? DefaultSteamworksTicketIdentity
			: ticketIdentity.Trim();
		Log.Info( $"Requesting Facepunch Steamworks Web API auth ticket. appId={appId} steamId={steamId} identity={identity}." );
		var ticket = await GetAuthTicketForWebApiAsync( identity );
		var ticketData = GetTicketData( ticket );
		if ( ticket is null || ticketData is null || ticketData.Length == 0 )
		{
			Log.Warning( "Facepunch Steamworks Web API auth ticket request returned no ticket." );
			return null;
		}

		try
		{
			var ticketHex = Convert.ToHexString( ticketData ).ToLowerInvariant();
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
			CancelTicket( ticket );
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
		return $"steamworksValid={IsSteamworksValid()} steamworksSteamId={GetSteamworksSteamId()} {GetSteamworksReflectionDiagnostics()}; s&box auth service={DefaultSboxAuthServiceName}; device fallback ready. deviceId={identity.DeviceId} path={GetIdentityPath()} steamId={GetLocalSteamId()}";
	}

	private static bool TryInitializeSteamworks( uint appId )
	{
		try
		{
			var steamClientType = GetSteamworksType( "Steamworks.SteamClient" );
			if ( steamClientType is null )
			{
				Log.Warning( "Facepunch.Steamworks SteamClient type was not found." );
				return false;
			}

			if ( IsSteamworksValid() )
				return true;

			var initMethod = steamClientType.GetMethod( "Init", StaticReflectionFlags, null, [typeof( uint ), typeof( bool )], null );
			if ( initMethod is null )
			{
				Log.Warning( "Facepunch.Steamworks SteamClient.Init(uint, bool) was not found." );
				return false;
			}

			initMethod.Invoke( null, [appId, true] );
			Log.Info( $"Facepunch Steamworks initialized. appId={GetStaticPropertyValue( steamClientType, "AppId" )} steamId={GetSteamworksSteamId()} name={GetStaticPropertyValue( steamClientType, "Name" )}." );
			return IsSteamworksValid();
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to initialize Facepunch Steamworks. appId={appId} error={exception.Message}" );
			return false;
		}
	}

	private static bool IsSteamworksValid()
	{
		var steamClientType = GetSteamworksType( "Steamworks.SteamClient" );
		return GetStaticPropertyValue( steamClientType, "IsValid" ) is bool isValid && isValid;
	}

	private static long GetSteamworksSteamId()
	{
		var steamClientType = GetSteamworksType( "Steamworks.SteamClient" );
		var steamId = GetStaticPropertyValue( steamClientType, "SteamId" );
		return ReadSteamIdValue( steamId );
	}

	private static async Task<object> GetAuthTicketForWebApiAsync( string identity )
	{
		var steamUserType = GetSteamworksType( "Steamworks.SteamUser" );
		if ( steamUserType is null )
		{
			Log.Warning( "Facepunch.Steamworks SteamUser type was not found." );
			return null;
		}

		var asyncMethod = steamUserType.GetMethod( "GetAuthTicketForWebApiAsync", StaticReflectionFlags, null, [typeof( string ), typeof( double )], null );
		if ( asyncMethod is not null )
		{
			if ( asyncMethod.Invoke( null, [identity, 10.0] ) is not Task task )
				return null;

			await task;
			return task.GetType().GetProperty( "Result", InstanceReflectionFlags )?.GetValue( task );
		}

		var syncMethod = steamUserType.GetMethod( "GetAuthTicketForWebApi", StaticReflectionFlags, null, [typeof( string )], null );
		if ( syncMethod is not null )
			return syncMethod.Invoke( null, [identity] );

		Log.Warning( $"Facepunch.Steamworks SteamUser ticket methods were not found. methods={DescribeMethods( steamUserType, "GetAuth" )}" );
		return null;
	}

	private static byte[] GetTicketData( object ticket )
	{
		if ( ticket is byte[] bytes )
			return bytes;

		if ( ticket is null )
			return null;

		var ticketType = ticket.GetType();
		var data = ticketType.GetProperty( "Data", InstanceReflectionFlags )?.GetValue( ticket ) as byte[];
		if ( data is not null )
			return data;

		var ticketData = ticketType.GetProperty( "TicketData", InstanceReflectionFlags )?.GetValue( ticket ) as byte[];
		if ( ticketData is not null )
			return ticketData;

		var ticketBytes = ticketType.GetProperty( "Ticket", InstanceReflectionFlags )?.GetValue( ticket ) as byte[];
		if ( ticketBytes is not null )
			return ticketBytes;

		Log.Warning( $"Facepunch Steamworks auth ticket did not expose byte data. ticketType={ticketType.FullName} properties={DescribeProperties( ticketType )}" );
		return null;
	}

	private static void CancelTicket( object ticket )
	{
		try
		{
			ticket?.GetType().GetMethod( "Cancel", InstanceReflectionFlags )?.Invoke( ticket, null );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to cancel Facepunch Steamworks auth ticket: {exception.Message}" );
		}
	}

	private static Type GetSteamworksType( string typeName )
	{
		var loadedType = FindLoadedType( typeName );
		if ( loadedType is null )
		{
			Log.Warning( $"s&box Steamworks type {typeName} was not found. matches={DescribeLoadedTypeMatches( typeName )}" );
			return null;
		}

		return loadedType;
	}

	private static Type FindLoadedType( string typeName )
	{
		var shortName = typeName.Contains( '.' )
			? typeName[(typeName.LastIndexOf( '.' ) + 1)..]
			: typeName;

		foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
		{
			Type type = null;
			try
			{
				type = assembly.GetType( typeName, false, false );
			}
			catch
			{
			}

			if ( type is not null )
				return type;

			try
			{
				type = assembly.GetTypes()
					.FirstOrDefault( candidate =>
						string.Equals( candidate.FullName, typeName, StringComparison.Ordinal ) ||
						string.Equals( candidate.Name, shortName, StringComparison.Ordinal ) );
			}
			catch
			{
			}

			if ( type is not null )
				return type;
		}

		return null;
	}

	private static string GetSteamworksReflectionDiagnostics()
	{
		var steamClientType = FindLoadedType( "Steamworks.SteamClient" );
		var steamUserType = FindLoadedType( "Steamworks.SteamUser" );
		var nativeSteamUserType = FindLoadedType( "ISteamUser" );
		return $"steamClientType={DescribeType( steamClientType )} steamUserType={DescribeType( steamUserType )} steamUserTicketMethods={DescribeMethods( steamUserType, "GetAuthTicketForWebApi" )} nativeSteamUserType={DescribeType( nativeSteamUserType )} nativeSteamUserTicketMethods={DescribeMethods( nativeSteamUserType, "" )} steamUserMatches={DescribeLoadedTypeMatches( "SteamUser" )}";
	}

	private static string DescribeType( Type type )
	{
		return type is null ? "<missing>" : $"{type.FullName}@{type.Assembly.GetName().Name}";
	}

	private static string DescribeMethods( Type type, string prefix )
	{
		if ( type is null )
			return "<missing>";

		var methods = type.GetMethods( StaticReflectionFlags )
			.Where( method => string.IsNullOrWhiteSpace( prefix ) ||
				method.Name.Contains( prefix, StringComparison.OrdinalIgnoreCase ) ||
				method.Name.Contains( "Auth", StringComparison.OrdinalIgnoreCase ) ||
				method.Name.Contains( "Ticket", StringComparison.OrdinalIgnoreCase ) )
			.Select( method => $"{method.Name}({string.Join( ",", method.GetParameters().Select( parameter => parameter.ParameterType.Name ) )}):{method.ReturnType.Name}" )
			.Take( 12 )
			.ToArray();

		return methods.Length == 0 ? "<none>" : string.Join( ";", methods );
	}

	private static string DescribeProperties( Type type )
	{
		return string.Join( ",", type.GetProperties( InstanceReflectionFlags )
			.Select( property => $"{property.Name}:{property.PropertyType.Name}" )
			.Take( 12 ) );
	}

	private static string DescribeSteamworksTypes( Assembly assembly )
	{
		try
		{
			return string.Join( ",", assembly.GetTypes()
				.Where( type => type.FullName.StartsWith( "Steamworks.Steam", StringComparison.Ordinal ) )
				.Select( type => type.FullName )
				.Take( 12 ) );
		}
		catch
		{
			return "<unavailable>";
		}
	}

	private static string DescribeLoadedTypeMatches( string typeName )
	{
		var token = typeName.Contains( '.' )
			? typeName[(typeName.LastIndexOf( '.' ) + 1)..]
			: typeName;

		var matches = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany( assembly =>
			{
				try
				{
					return assembly.GetTypes()
						.Where( type => type.FullName.Contains( token, StringComparison.OrdinalIgnoreCase ) )
						.Select( type => DescribeType( type ) );
				}
				catch
				{
					return Enumerable.Empty<string>();
				}
			} )
			.Take( 12 )
			.ToArray();

		return matches.Length == 0 ? "<none>" : string.Join( ",", matches );
	}

	private static object GetStaticPropertyValue( Type type, string propertyName )
	{
		return type?.GetProperty( propertyName, StaticReflectionFlags )?.GetValue( null );
	}

	private static long ReadSteamIdValue( object steamId )
	{
		if ( steamId is null )
			return 0;

		var value = steamId.GetType().GetProperty( "Value", InstanceReflectionFlags )?.GetValue( steamId ) ??
			steamId.GetType().GetProperty( "ValueUnsigned", InstanceReflectionFlags )?.GetValue( steamId );

		return value is null ? 0 : Convert.ToInt64( value );
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
