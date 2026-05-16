using Sandbox;
#if STANDALONE
using System;
using System.Reflection;
#endif

public static class MonopolySteamInviteBridge
{
#if STANDALONE
	private static bool isRegistered;
	private static Scene currentScene;
	private static Delegate joinRequestedHandler;
#endif

	public static void Register( Scene scene )
	{
#if STANDALONE
		currentScene = scene;

		if ( isRegistered )
			return;

		try
		{
			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var joinRequestedProperty = steamFriendsType?.GetProperty( "OnGameLobbyJoinRequested", BindingFlags.Public | BindingFlags.Static );
			var handlerMethod = typeof( MonopolySteamInviteBridge ).GetMethod( nameof( OnGameLobbyJoinRequested ), BindingFlags.NonPublic | BindingFlags.Static );

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
#endif
	}

#if STANDALONE
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
		MonopolySceneFlow.LoadLobby( currentScene );
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
