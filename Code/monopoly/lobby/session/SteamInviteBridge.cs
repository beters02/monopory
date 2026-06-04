using Sandbox;
using System;
using System.Collections;
using System.Reflection;

public static class SteamInviteBridge
{

	private static bool isRegistered;
	private static Scene currentScene;
	private static Delegate joinRequestedHandler;


	public static void Register( Scene scene )
	{
		currentScene = scene;

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
			Log.Info( "Registered Steam lobby join callback." );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to register Steam lobby join callback: {exception.Message}" );
		}
	}

	public static long GetActiveLobbyIdValue()
	{
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

		return 0;
	}
	
	
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

	private static async void OnGameLobbyJoinRequested( SteamId lobbyId )
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

}
