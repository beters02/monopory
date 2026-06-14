using System;
using System.Collections;
using System.Reflection;
using Sandbox;

public partial class MonopolyApp : Component
{

    private static uint GetSteamAppId()
	{
		return uint.TryParse( AchievementsSteamAppIdConVar.Value, out var appId ) && appId != 0
			? appId
			: Sandbox.Services.RentRushService.DefaultSteamAppId;
	}

    public static bool TryGetLobbySocket( out object steamLobbySocket, out string msg )
    {
        return TryGetActiveSteamLobbySocket(out steamLobbySocket, out msg);
    }

    private static bool TryGetActiveSteamLobbySocket( out object steamLobbySocket, out string message )
    {
        steamLobbySocket = null;
        message = "";

//#if STANDALONE
        var networkingType = FindLoadedType( "Sandbox.Network.Networking" ) ?? FindLoadedType( "Sandbox.Networking" );
        var networkSystem = networkingType?.GetField( "System", SteamStaticReflectionFlags )?.GetValue( null );
        if ( networkSystem is null )
        {
            message = "Could not find active Sandbox networking system.";
            return false;
        }

        var sockets = networkSystem.GetType().GetProperty( "Sockets", SteamInstanceReflectionFlags )?.GetValue( networkSystem ) as IEnumerable;
        if ( sockets is null )
        {
            message = "Could not inspect active network sockets.";
            return false;
        }

        foreach ( var socket in sockets )
        {
            if ( socket?.GetType().FullName == "Sandbox.Network.SteamLobbySocket" )
            {
                message = "Lobby socket found successfully";
                steamLobbySocket = socket;
                return true;
            }
        }

//#endif
        message = "No active Steam lobby socket was found.";
        return false;
    }

    public static bool TryTransferSteamLobbyHost( string toPlayerSteamId, out string msg )
    {
        msg = "Transfer lobby host is available in standalone";

        if ( IsStandalone() )
        {
//#if STANDALONE
            if ( !long.TryParse( toPlayerSteamId, out var targetSteamId ) || targetSteamId == 0 )
            {
                msg = $"Could not parse target SteamId \"{toPlayerSteamId}\".";
                return false;
            }

            return TryTransferSteamLobbyHostSync( targetSteamId, out msg );
//#endif
        }
        
        return false;
    }

//#if STANDALONE
	private const BindingFlags SteamStaticReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
	private const BindingFlags SteamInstanceReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	public static bool TryTransferSteamLobbyHostSync( long targetSteamId, out string message )
	{
		message = "";

		if ( !TryUseStandaloneOnlyFunction( "Steam lobby host transfer" ) )
		{
			message = "Steam lobby host transfer is only available in standalone.";
			return false;
		}

		if ( targetSteamId == 0 )
		{
			message = "Target SteamId was empty.";
			return false;
		}

		if ( !Networking.IsActive )
		{
			message = "Networking is not active.";
			return false;
		}

		if ( !Networking.IsHost )
		{
			message = "Only the current networking host can transfer the Steam lobby owner.";
			return false;
		}

		var targetConnection = Connection.All.FirstOrDefault( connection => connection is not null && connection.SteamId == targetSteamId );
		if ( targetConnection is null )
		{
			message = $"No connected player has SteamId {targetSteamId}.";
			return false;
		}

		if ( Connection.Local is not null && Connection.Local.SteamId == targetSteamId )
		{
			SetPreferredHostOwnerId( targetSteamId );
			SetPreferredHostDisconnected( false );
			message = $"{targetConnection.Name} is already the local host.";
			return true;
		}

		try
		{
			if ( !TryGetActiveSteamLobbySocket( out var steamLobbySocket, out message ) )
				return false;

			if ( !TryGetSteamLobbyFromSocket( steamLobbySocket, out var steamLobby, out message ) )
				return false;

			if ( !TrySetSteamLobbyData( steamLobby, "_ownerid", targetSteamId.ToString(), out message ) )
				return false;

			if ( !TrySetSteamLobbyOwner( steamLobby, targetSteamId, out message ) )
				return false;

			TrySetSteamLobbySocketOwner( steamLobbySocket, targetSteamId );

			SetPreferredHostOwnerId( targetSteamId );
			SetPreferredHostDisconnected( false );
			message = $"Transferred Steam lobby host to {targetConnection.Name} ({targetSteamId}).";
			return true;
		}
		catch ( Exception exception )
		{
			message = $"Failed to transfer Steam lobby host: {exception.Message}";
			return false;
		}
	}

	private static bool TryGetSteamLobbyFromSocket( object steamLobbySocket, out object steamLobby, out string message )
	{
		steamLobby = null;
		message = "";

		steamLobby = steamLobbySocket.GetType().GetField( "SteamLobby", SteamInstanceReflectionFlags )?.GetValue( steamLobbySocket );
		if ( steamLobby is not null )
			return true;

		message = "Steam lobby socket did not expose a SteamLobby field.";
		return false;
	}

	private static bool TrySetSteamLobbyData( object steamLobby, string key, string value, out string message )
	{
		message = "";

		var setDataMethod = steamLobby.GetType().GetMethod( "SetData", SteamInstanceReflectionFlags, null, new[] { typeof( string ), typeof( string ) }, null );
		if ( setDataMethod is null )
		{
			message = "Steam lobby SetData method was not found.";
			return false;
		}

		var result = setDataMethod.Invoke( steamLobby, new object[] { key, value } );
		if ( result is bool success && !success )
		{
			message = $"Steam lobby rejected SetData({key}).";
			return false;
		}

		return true;
	}

	private static bool TrySetSteamLobbyOwner( object steamLobby, long targetSteamId, out string message )
	{
		message = "";

		var setOwnerMethod = steamLobby.GetType().GetMethod( "SetOwner", SteamInstanceReflectionFlags );
		if ( setOwnerMethod is null )
		{
			message = "Steam lobby SetOwner method was not found.";
			return false;
		}

		var parameters = setOwnerMethod.GetParameters();
		if ( parameters.Length != 1 )
		{
			message = "Steam lobby SetOwner method had an unexpected signature.";
			return false;
		}

		var ownerArgument = CreateNumericArgument( targetSteamId, parameters[0].ParameterType );
		if ( ownerArgument is null )
		{
			message = $"Could not convert {targetSteamId} to {parameters[0].ParameterType.FullName}.";
			return false;
		}

		var result = setOwnerMethod.Invoke( steamLobby, new[] { ownerArgument } );
		if ( result is bool success && !success )
		{
			message = "Steam rejected the lobby owner transfer.";
			return false;
		}

		return true;
	}

	private static void TrySetSteamLobbySocketOwner( object steamLobbySocket, long targetSteamId )
	{
		var ownerField = steamLobbySocket.GetType().GetField( "Owner", SteamInstanceReflectionFlags );
		if ( ownerField is null )
			return;

		var friend = CreateNumericArgument( targetSteamId, ownerField.FieldType );
		if ( friend is not null )
			ownerField.SetValue( steamLobbySocket, friend );
	}

	private static object CreateNumericArgument( long value, Type targetType )
	{
		if ( targetType == typeof( long ) )
			return value;

		if ( targetType == typeof( ulong ) )
			return unchecked((ulong)value);

		var unsignedConstructor = targetType.GetConstructor( new[] { typeof( ulong ) } );
		if ( unsignedConstructor is not null )
			return unsignedConstructor.Invoke( new object[] { unchecked((ulong)value) } );

		var signedConstructor = targetType.GetConstructor( new[] { typeof( long ) } );
		if ( signedConstructor is not null )
			return signedConstructor.Invoke( new object[] { value } );

		var implicitFromUnsigned = targetType.GetMethod(
			"op_Implicit",
			SteamStaticReflectionFlags,
			null,
			new[] { typeof( ulong ) },
			null );
		if ( implicitFromUnsigned is not null )
			return implicitFromUnsigned.Invoke( null, new object[] { unchecked((ulong)value) } );

		var implicitFromSigned = targetType.GetMethod(
			"op_Implicit",
			SteamStaticReflectionFlags,
			null,
			new[] { typeof( long ) },
			null );
		if ( implicitFromSigned is not null )
			return implicitFromSigned.Invoke( null, new object[] { value } );

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
//#endif

}
