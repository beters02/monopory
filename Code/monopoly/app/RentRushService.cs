using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Sandbox;

namespace Sandbox.Services;

public static class RentRushService
{
	public const string DefaultServiceName = "RentRushService";

	public static async Task<string> GetSteamWebApiTicketAsync( string identity, ulong targetSteamId = 0 )
	{
#if STANDALONE
		try
		{
			var sandboxTicket = GetSandboxAuthTicketHex( targetSteamId );
			if ( !string.IsNullOrWhiteSpace( sandboxTicket ) )
				return sandboxTicket;

			var steamUserType = FindSteamUserType();
			if ( steamUserType is null )
			{
				Log.Warning( $"No Steam user auth type was found; cannot request Steam Web API auth ticket. Candidates={DescribeSteamAuthCandidates()}." );
				return "";
			}

			var method = FindAuthTicketMethod( steamUserType );
			if ( method is null )
			{
				Log.Warning( "Steamworks.SteamUser.GetAuthTicketForWebApi was not found." );
				return "";
			}

			Log.Info( $"Requesting Steam Web API auth ticket. identity={GetTicketIdentity( identity )} method={method.Name}." );
			var result = method.Invoke( null, BuildAuthTicketArguments( method, identity ) );
			var ticket = await ExtractTicketHexAsync( result );
			if ( !string.IsNullOrWhiteSpace( ticket ) )
			{
				Log.Info( $"Steam Web API auth ticket acquired. length={ticket.Length}." );
				return ticket;
			}

			Log.Warning( $"Steam Web API auth ticket request returned no ticket. returnType={method.ReturnType.FullName}." );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to get Steam Web API auth ticket: {exception.Message}" );
		}
#else
		await Task.CompletedTask;
#endif

		return "";
	}

#if STANDALONE
	public static string GetSteamAuthDiagnostics()
	{
		var authTypes = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany( assembly =>
			{
				try
				{
					return assembly.GetTypes();
				}
				catch
				{
					return [];
				}
			} )
			.Where( type =>
				type.FullName?.Contains( "Steam", StringComparison.OrdinalIgnoreCase ) == true ||
				type.FullName?.Contains( "Auth", StringComparison.OrdinalIgnoreCase ) == true )
			.Select( type =>
			{
				var methods = type.GetMethods( BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance )
					.Where( method =>
						method.Name.Contains( "Auth", StringComparison.OrdinalIgnoreCase ) ||
						method.Name.Contains( "Ticket", StringComparison.OrdinalIgnoreCase ) )
					.Select( method => method.ToString() )
					.Take( 8 );
				return $"{type.FullName}: {string.Join( "; ", methods )}";
			} )
			.Where( text => text.Contains( "Ticket", StringComparison.OrdinalIgnoreCase ) || text.Contains( "Auth", StringComparison.OrdinalIgnoreCase ) )
			.Take( 20 );

		return string.Join( "\n", authTypes );
	}

	public static async Task<string> AuthenticateAchievementsBackendAsync(
		string backendUrl,
		long steamId,
		string serviceAuthToken,
		string displayName )
	{
		if ( string.IsNullOrWhiteSpace( backendUrl ) || steamId == 0 || string.IsNullOrWhiteSpace( serviceAuthToken ) )
			return "";

		return await HttpAchievementService.AuthenticateAsync(
			backendUrl,
			steamId,
			serviceAuthToken,
			displayName ?? ""
		);
	}

	private static string GetSandboxAuthTicketHex( ulong targetSteamId )
	{
		try
		{
			var authType = FindLoadedType( "Sandbox.Services.Auth" );
			var method = authType?.GetMethod(
				"GetAuthTicket",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
				null,
				[typeof( ulong ), typeof( byte[] ).MakeByRefType()],
				null );
			if ( method is null )
			{
				Log.Warning( "Sandbox.Services.Auth.GetAuthTicket was not found at runtime." );
				return "";
			}

			Log.Info( $"Requesting s&box Steam auth ticket. targetSteamId={targetSteamId}." );
			var arguments = new object[] { targetSteamId, null };
			var handle = method.Invoke( null, arguments );
			var ticketHex = BytesToHex( arguments[1] as byte[] );
			if ( !string.IsNullOrWhiteSpace( ticketHex ) )
			{
				Log.Info( $"s&box Steam auth ticket acquired. length={ticketHex.Length} handle={handle}." );
				return ticketHex;
			}

			Log.Warning( $"s&box Steam auth ticket request returned no bytes. handle={handle}." );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to get s&box Steam auth ticket: {exception.Message}" );
		}

		return "";
	}

	private static Type FindSteamUserType()
	{
		foreach ( var typeName in new[]
		{
			"Steamworks.SteamUser",
			"Steamworks.User",
			"Steamworks.SteamClient",
			"Sandbox.Utility.Steam"
		} )
		{
			var type = FindLoadedType( typeName );
			if ( type is not null )
				return type;
		}

		return AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany( assembly =>
			{
				try
				{
					return assembly.GetTypes();
				}
				catch
				{
					return [];
				}
			} )
			.FirstOrDefault( type =>
				type.FullName?.Contains( "Steam", StringComparison.OrdinalIgnoreCase ) == true &&
				type.GetMethods( BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance )
					.Any( method => method.Name.Contains( "AuthTicket", StringComparison.OrdinalIgnoreCase ) ) );
	}

	private static string DescribeSteamAuthCandidates()
	{
		var candidates = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany( assembly =>
			{
				try
				{
					return assembly.GetTypes();
				}
				catch
				{
					return [];
				}
			} )
			.Where( type =>
				type.FullName?.Contains( "Steam", StringComparison.OrdinalIgnoreCase ) == true &&
				type.GetMethods( BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance )
					.Any( method => method.Name.Contains( "Auth", StringComparison.OrdinalIgnoreCase ) || method.Name.Contains( "Ticket", StringComparison.OrdinalIgnoreCase ) ) )
			.Select( type => type.FullName )
			.Take( 10 );

		return string.Join( ", ", candidates );
	}

	private static MethodInfo FindAuthTicketMethod( Type steamUserType )
	{
		return steamUserType
			.GetMethods( BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance )
			.FirstOrDefault( method =>
				string.Equals( method.Name, "GetAuthTicketForWebApi", StringComparison.Ordinal ) ||
				string.Equals( method.Name, "GetAuthTicketForWebAPI", StringComparison.Ordinal ) ||
				string.Equals( method.Name, "GetAuthSessionTicket", StringComparison.Ordinal ) ||
				string.Equals( method.Name, "GetAuthTicket", StringComparison.Ordinal ) );
	}

	private static object[] BuildAuthTicketArguments( MethodInfo method, string identity )
	{
		var parameters = method.GetParameters();
		if ( parameters.Length == 0 )
			return [];

		if ( parameters.Length == 1 && parameters[0].ParameterType == typeof( string ) )
			return [GetTicketIdentity( identity )];

		Log.Warning( $"Steam Web API ticket method has unexpected signature: {method}." );
		return parameters.Select( parameter => parameter.HasDefaultValue ? parameter.DefaultValue : GetDefaultValue( parameter.ParameterType ) ).ToArray();
	}

	private static string GetTicketIdentity( string identity )
	{
		return string.IsNullOrWhiteSpace( identity ) ? DefaultServiceName : identity.Trim();
	}

	private static object GetDefaultValue( Type type )
	{
		return type.IsValueType ? Activator.CreateInstance( type ) : null;
	}

	private static async Task<string> ExtractTicketHexAsync( object value )
	{
		if ( value is null )
			return "";

		if ( value is Task task )
		{
			await task;
			var resultProperty = task.GetType().GetProperty( "Result", BindingFlags.Public | BindingFlags.Instance );
			return ExtractTicketHex( resultProperty?.GetValue( task ) );
		}

		var asTaskMethod = value.GetType().GetMethod( "AsTask", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null );
		if ( asTaskMethod is not null && value.GetType().FullName?.StartsWith( "System.Threading.Tasks.ValueTask", StringComparison.Ordinal ) == true )
			return await ExtractTicketHexAsync( asTaskMethod.Invoke( value, null ) );

		return ExtractTicketHex( value );
	}

	private static string ExtractTicketHex( object value, int depth = 0 )
	{
		if ( value is null || depth > 3 )
			return "";

		if ( value is string text )
			return IsProbablyHex( text ) ? text : "";

		if ( value is byte[] bytes )
			return BytesToHex( bytes );

		if ( value is Array array && array.GetType().GetElementType() == typeof( byte ) )
		{
			var bytesFromArray = new byte[array.Length];
			Array.Copy( array, bytesFromArray, array.Length );
			return BytesToHex( bytesFromArray );
		}

		var toArrayMethod = value.GetType().GetMethod( "ToArray", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null );
		if ( toArrayMethod?.ReturnType == typeof( byte[] ) )
			return BytesToHex( (byte[])toArrayMethod.Invoke( value, null ) );

		foreach ( var propertyName in new[] { "Ticket", "Data", "AuthTicket", "Bytes", "Value", "Result" } )
		{
			var property = value.GetType().GetProperty( propertyName, BindingFlags.Public | BindingFlags.Instance );
			if ( property is null )
				continue;

			var ticket = ExtractTicketHex( property.GetValue( value ), depth + 1 );
			if ( !string.IsNullOrWhiteSpace( ticket ) )
				return ticket;
		}

		return "";
	}

	private static bool IsProbablyHex( string text )
	{
		return text.Length > 0 && text.Length % 2 == 0 && text.All( Uri.IsHexDigit );
	}

	private static string BytesToHex( byte[] bytes )
	{
		return bytes is null || bytes.Length == 0
			? ""
			: Convert.ToHexString( bytes ).ToLowerInvariant();
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
}
