using Sandbox;
#if STANDALONE
using System;
using System.Collections;
using System.Reflection;
#endif

public sealed partial class LobbyController
{
	public bool TryOpenInviteOverlay()
	{
#if STANDALONE
		if ( !Networking.IsActive )
		{
			Log.Warning( "Cannot open invite overlay because networking is not active." );
			return false;
		}

		if ( !TryGetActiveLobbyId( out var lobbyId ) )
		{
			Log.Warning( "Cannot open invite overlay because no active Steam lobby id was found." );
			return false;
		}

		if ( TryOpenGameInviteOverlay( lobbyId ) )
			return true;

		Log.Warning( "Steam friend invite overlay is not available from the public game API." );
		return false;
#else
		Log.Warning( "Steam friend invite overlay is only enabled in standalone builds." );
		return false;
#endif
	}

#if STANDALONE
	private static bool TryGetActiveLobbyId( out ulong lobbyId )
	{
		lobbyId = 0;

		try
		{
			var lobbyManagerType = FindLoadedType( "Sandbox.LobbyManager" );
			var activeLobbiesProperty = lobbyManagerType?.GetProperty( "ActiveLobbies", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
			var activeLobbies = activeLobbiesProperty?.GetValue( null ) as IEnumerable;

			if ( activeLobbies is null )
				return false;

			foreach ( var activeLobby in activeLobbies )
			{
				lobbyId = Convert.ToUInt64( activeLobby );
				if ( lobbyId != 0 )
					return true;
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to resolve active Steam lobby id: {exception.Message}" );
		}

		lobbyId = 0;
		return false;
	}

	private static bool TryOpenGameInviteOverlay( ulong lobbyId )
	{
		try
		{
			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var inviteMethod = steamFriendsType?.GetMethod( "OpenGameInviteOverlay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );

			if ( inviteMethod is null )
			{
				Log.Warning( "Steamworks.SteamFriends.OpenGameInviteOverlay was not found." );
				return false;
			}

			var parameters = inviteMethod.GetParameters();
			if ( parameters.Length != 1 )
			{
				Log.Warning( "Steamworks.SteamFriends.OpenGameInviteOverlay had an unexpected signature." );
				return false;
			}

			var lobbySteamId = CreateSteamIdArgument( lobbyId, parameters[0].ParameterType );
			if ( lobbySteamId is null )
			{
				Log.Warning( $"Could not convert lobby id {lobbyId} to {parameters[0].ParameterType.FullName}." );
				return false;
			}

			inviteMethod.Invoke( null, new[] { lobbySteamId } );
			Log.Info( $"Opened Steam invite overlay for lobby {lobbyId}." );
			return true;
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to open invite overlay: {exception.Message}" );
			return false;
		}
	}

	private static object CreateSteamIdArgument( ulong lobbyId, Type steamIdType )
	{
		if ( steamIdType == typeof( ulong ) )
			return lobbyId;

		if ( steamIdType == typeof( long ) )
			return unchecked((long)lobbyId);

		var unsignedConstructor = steamIdType.GetConstructor( new[] { typeof( ulong ) } );
		if ( unsignedConstructor is not null )
			return unsignedConstructor.Invoke( new object[] { lobbyId } );

		var signedConstructor = steamIdType.GetConstructor( new[] { typeof( long ) } );
		if ( signedConstructor is not null )
			return signedConstructor.Invoke( new object[] { unchecked((long)lobbyId) } );

		var implicitConversion = steamIdType.GetMethod(
			"op_Implicit",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
			null,
			new[] { typeof( ulong ) },
			null );

		if ( implicitConversion is not null )
			return implicitConversion.Invoke( null, new object[] { lobbyId } );

		return null;
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
