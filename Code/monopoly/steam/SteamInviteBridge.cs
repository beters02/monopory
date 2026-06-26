using Sandbox;
#if STANDALONE
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
#endif

public static class SteamInviteBridge
{
#if STANDALONE
	private static bool isRegistered;
	private static Scene currentScene;
	private static Delegate joinRequestedHandler;
	private static Delegate richPresenceJoinRequestedHandler;
	private static string lastHandledConnectTarget = "";
#endif

	public static void Register( Scene scene )
	{
#if STANDALONE
		currentScene = scene;
		TryHandleLaunchConnectTarget();

		if ( isRegistered )
			return;

		try
		{
			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var joinRequestedProperty = steamFriendsType?.GetProperty( "OnGameLobbyJoinRequested", BindingFlags.Public | BindingFlags.Static );
			var handlerMethod = typeof( SteamInviteBridge ).GetMethod( nameof( OnGameLobbyJoinRequested ), BindingFlags.NonPublic | BindingFlags.Static );

			if ( joinRequestedProperty is null || handlerMethod is null )
			{
				Log.Warning( "Steam lobby join callback is not available." );
				return;
			}

			joinRequestedHandler = Delegate.CreateDelegate( joinRequestedProperty.PropertyType, handlerMethod );
			var existingHandler = joinRequestedProperty.GetValue( null ) as Delegate;
			var combinedHandler = Delegate.Combine( existingHandler, joinRequestedHandler );

			joinRequestedProperty.SetValue( null, combinedHandler );
			isRegistered = true;
			TryRegisterRichPresenceJoinCallback( steamFriendsType );
			Log.Info( "Registered Steam lobby join callback." );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to register Steam lobby join callback: {exception.Message}" );
		}
#endif
	}

	public static long GetActiveLobbyIdValue()
	{
#if STANDALONE
		try
		{
			var lobbyManagerType = FindLoadedType( "Sandbox.LobbyManager" );
			var activeLobbiesProperty = lobbyManagerType?.GetProperty( "ActiveLobbies", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
			var activeLobbies = activeLobbiesProperty?.GetValue( null ) as IEnumerable;
			if ( activeLobbies is null )
				return 0;

			foreach ( var lobby in activeLobbies )
			{
				var lobbyId = Convert.ToUInt64( lobby );
				if ( lobbyId != 0 )
					return unchecked((long)lobbyId);
			}
		}
		catch
		{
		}
#endif
		return 0;
	}
	
	

#if STANDALONE
	private static void PrintAssemblyTypes( )
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for ( var i = 0; i < assemblies.Length; i++ )
		{
			var types = assemblies[i].GetTypes();
			if ( types is null ) continue;
			Log.Info(assemblies[i].ToString());
			for ( var bi = 0; bi < types.Length; bi++ )
			{
				Log.Info(types[bi].FullName);
			}
		}
	}

	private static void TryRegisterRichPresenceJoinCallback( Type steamFriendsType )
	{
		try
		{
			var joinRequestedProperty = steamFriendsType?.GetProperty( "OnGameRichPresenceJoinRequested", BindingFlags.Public | BindingFlags.Static );
			var handlerMethod = typeof( SteamInviteBridge ).GetMethod( nameof( OnGameRichPresenceJoinRequested ), BindingFlags.NonPublic | BindingFlags.Static );
			if ( joinRequestedProperty is null || handlerMethod is null )
				return;

			richPresenceJoinRequestedHandler = Delegate.CreateDelegate( joinRequestedProperty.PropertyType, handlerMethod );
			var existingHandler = joinRequestedProperty.GetValue( null ) as Delegate;
			joinRequestedProperty.SetValue( null, Delegate.Combine( existingHandler, richPresenceJoinRequestedHandler ) );
			Log.Info( "Registered Steam rich presence join callback." );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to register Steam rich presence join callback: {exception.Message}" );
		}
	}

	private static void TryHandleLaunchConnectTarget()
	{
		var args = Environment.GetCommandLineArgs();
		for ( var i = 0; i < args.Length; i++ )
		{
			if ( !string.Equals( args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase ) || i + 1 >= args.Length )
				continue;

			HandleConnectTarget( $"+connect_lobby {args[i + 1]}" );
			return;
		}
	}

	private static async void HandleConnectTarget( string connectTarget )
	{
		if ( string.IsNullOrWhiteSpace( connectTarget ) || string.Equals( connectTarget, lastHandledConnectTarget, StringComparison.Ordinal ) )
			return;

		lastHandledConnectTarget = connectTarget;
		if ( !TryParseConnectLobbyId( connectTarget, out var lobbyIdValue ) )
			return;

		var lobbyId = (SteamId)unchecked((long)lobbyIdValue);
		await JoinLobbyInviteAsync( lobbyId );
	}

	private static bool TryParseConnectLobbyId( string connectTarget, out ulong lobbyId )
	{
		lobbyId = 0;
		var parts = connectTarget.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		for ( var i = 0; i < parts.Length - 1; i++ )
		{
			if ( string.Equals( parts[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase ) && ulong.TryParse( parts[i + 1], out lobbyId ) )
				return lobbyId != 0;
		}

		return false;
	}
	private static Type FindLoadedType( string typeName )
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for ( var i = 0; i < assemblies.Length; i++ )
		{
			Log.Info(assemblies[i].ToString());
			var type = assemblies[i].GetType( typeName, false );
			if ( type is not null )
				return type;
		}

		return null;
	}

	private static void OnGameRichPresenceJoinRequested( object friend, string connectTarget )
	{
		HandleConnectTarget( connectTarget );
	}

	private static async void OnGameLobbyJoinRequested( SteamId lobbyId )
	{
		await JoinLobbyInviteAsync( lobbyId );
	}

	private static async Task JoinLobbyInviteAsync( SteamId lobbyId )
	{
		var lobbyIdValue = lobbyId.ValueUnsigned;
		if ( lobbyIdValue == 0 )
			return;

		Log.Info( $"Joining Steam lobby invite {lobbyIdValue}." );
		var connected = await Networking.TryConnectSteamId( lobbyId, 3 );
		if ( !connected )
		{
			Log.Warning( $"Failed to join Steam lobby invite {lobbyIdValue}." );
			return;
		}

		Log.Info( $"Joined Steam lobby invite {lobbyIdValue}." );
		SceneFlow.LoadLobby( currentScene );
	}
#endif
}
