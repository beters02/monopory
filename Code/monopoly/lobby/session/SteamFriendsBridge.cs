using Sandbox;
using System;
using System.Collections;
using System.Reflection;

public sealed class SteamFriendListEntry
{
	public long SteamId { get; set; }
	public string Name { get; set; } = "";
	public string Status { get; set; } = "";
	public string RichPresence { get; set; } = "";
	public bool IsOnline { get; set; }
	public bool IsPlayingThisGame { get; set; }
}

public static class SteamFriendsBridge
{
	private const BindingFlags StaticReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
	private const BindingFlags InstanceReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
	private static readonly IReadOnlyList<SteamFriendListEntry> EditorFriends = new[]
	{
		new SteamFriendListEntry
		{
			SteamId = 1,
			Name = "Editor Test Friend",
			Status = "Online",
			RichPresence = "Testing friend popup",
			IsOnline = true,
			IsPlayingThisGame = true
		}
	};
	private static IReadOnlyList<SteamFriendListEntry> cachedFriends = Array.Empty<SteamFriendListEntry>();
	private static float nextRefreshTime;

	public static IReadOnlyList<SteamFriendListEntry> GetFriends()
	{
		if ( !MonopolyApp.IsStandalone() )
			return Application.IsEditor ? EditorFriends : Array.Empty<SteamFriendListEntry>();

		if ( Time.Now < nextRefreshTime )
			return cachedFriends;

		nextRefreshTime = Time.Now + 5f;
		cachedFriends = LoadFriends();
		return cachedFriends;
	}

	public static bool TryInviteFriend( SteamFriendListEntry friend )
	{
		if ( friend is null || friend.SteamId == 0 || !MonopolyApp.IsStandalone() )
			return false;

//#if STANDALONE
		try
		{
			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var inviteMethod = steamFriendsType?.GetMethod( "InviteUserToGame", StaticReflectionFlags );
			var connectTarget = GetInviteConnectTarget();

			if ( inviteMethod is not null && TryInvokeFriendAction( inviteMethod, friend.SteamId, connectTarget ) )
				return true;

			var friendObject = GetFriendObject( friend.SteamId );
			var friendInviteMethod = friendObject?.GetType().GetMethod( "InviteToGame", InstanceReflectionFlags );
			if ( friendInviteMethod is not null )
			{
				var result = friendInviteMethod.Invoke( friendObject, new object[] { connectTarget } );
				return result is not bool success || success;
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to invite Steam friend {friend.SteamId}: {exception.Message}" );
		}
//#endif

		return false;
	}

	public static bool TryOpenProfile( SteamFriendListEntry friend )
	{
		if ( friend is null || friend.SteamId == 0 || !MonopolyApp.IsStandalone() )
			return false;

//#if STANDALONE
		try
		{
			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var overlayMethod = steamFriendsType?.GetMethod( "OpenUserOverlay", StaticReflectionFlags );

			if ( overlayMethod is not null )
				return TryInvokeFriendAction( overlayMethod, friend.SteamId, "steamid" );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to open Steam profile {friend.SteamId}: {exception.Message}" );
		}
//#endif

		return false;
	}

	private static IReadOnlyList<SteamFriendListEntry> LoadFriends()
	{
//#if STANDALONE
		try
		{
			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var getFriendsMethod = steamFriendsType?.GetMethod( "GetFriends", StaticReflectionFlags );
			var friends = getFriendsMethod?.Invoke( null, null ) as IEnumerable;

			if ( friends is null )
				return Array.Empty<SteamFriendListEntry>();

			var entries = new List<SteamFriendListEntry>();
			foreach ( var friend in friends )
			{
				var entry = CreateEntry( friend );
				if ( entry is not null && !string.IsNullOrWhiteSpace( entry.Name ) )
					entries.Add( entry );
			}

			return entries
				.OrderByDescending( friend => friend.IsPlayingThisGame )
				.ThenByDescending( friend => friend.IsOnline )
				.ThenBy( friend => friend.Name )
				.ToList();
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to load Steam friends: {exception.Message}" );
		}
//#endif

		return Array.Empty<SteamFriendListEntry>();
	}

//#if STANDALONE
	private static SteamFriendListEntry CreateEntry( object friend )
	{
		if ( friend is null )
			return null;

		var steamId = GetSteamIdValue(
			GetPropertyValue( friend, "Id" ) ??
			GetPropertyValue( friend, "SteamId" ) ??
			GetPropertyValue( friend, "SteamID" ) );

		return new SteamFriendListEntry
		{
			SteamId = steamId,
			Name = GetStringValue( GetPropertyValue( friend, "Name" ) ),
			Status = GetStatus( friend ),
			RichPresence = GetRichPresence( friend ),
			IsOnline = GetBoolValue( GetPropertyValue( friend, "IsOnline" ) ),
			IsPlayingThisGame = GetBoolValue( GetPropertyValue( friend, "IsPlayingThisGame" ) )
		};
	}

	private static object GetFriendObject( long steamId )
	{
		var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
		var getFriendsMethod = steamFriendsType?.GetMethod( "GetFriends", StaticReflectionFlags );
		var friends = getFriendsMethod?.Invoke( null, null ) as IEnumerable;

		if ( friends is null )
			return null;

		foreach ( var friend in friends )
		{
			var candidateSteamId = GetSteamIdValue(
				GetPropertyValue( friend, "Id" ) ??
				GetPropertyValue( friend, "SteamId" ) ??
				GetPropertyValue( friend, "SteamID" ) );

			if ( candidateSteamId == steamId )
				return friend;
		}

		return null;
	}

	private static bool TryInvokeFriendAction( MethodInfo method, long steamId, string value )
	{
		var parameters = method.GetParameters();
		if ( parameters.Length == 0 )
			return false;

		var friendArgument = CreateSteamIdArgument( steamId, parameters[0].ParameterType );
		if ( friendArgument is null )
			return false;

		object result;
		if ( parameters.Length == 1 )
		{
			result = method.Invoke( null, new[] { friendArgument } );
		}
		else if ( parameters.Length == 2 )
		{
			result = method.Invoke( null, new object[] { friendArgument, value ?? "" } );
		}
		else
		{
			return false;
		}

		return result is not bool success || success;
	}

	private static string GetStatus( object friend )
	{
		var state = GetPropertyValue( friend, "State" ) ?? GetPropertyValue( friend, "PersonaState" );
		if ( state is not null )
			return state.ToString();

		if ( GetBoolValue( GetPropertyValue( friend, "IsOnline" ) ) )
			return "Online";

		return "Offline";
	}

	private static string GetRichPresence( object friend )
	{
		var method = friend.GetType().GetMethod( "GetRichPresence", InstanceReflectionFlags, null, new[] { typeof( string ) }, null );
		if ( method is null )
			return "";

		var status = GetStringValue( method.Invoke( friend, new object[] { "status" } ) );
		if ( !string.IsNullOrWhiteSpace( status ) )
			return status;

		return GetStringValue( method.Invoke( friend, new object[] { "steam_display" } ) );
	}

	private static string GetInviteConnectTarget()
	{
		var lobbyId = SteamInviteBridge.GetActiveLobbyIdValue();
		if ( lobbyId != 0 )
			return $"+connect_lobby {lobbyId}";

		return Networking.ServerName ?? "";
	}

	private static object GetPropertyValue( object instance, string propertyName )
	{
		return instance.GetType().GetProperty( propertyName, InstanceReflectionFlags )?.GetValue( instance );
	}

	private static string GetStringValue( object value )
	{
		return value?.ToString() ?? "";
	}

	private static bool GetBoolValue( object value )
	{
		return value is bool boolValue && boolValue;
	}

	private static long GetSteamIdValue( object value )
	{
		if ( value is null )
			return 0;

		if ( value is long signed )
			return signed;

		if ( value is ulong unsigned )
			return unchecked((long)unsigned);

		var rawValue = value.GetType().GetProperty( "Value", InstanceReflectionFlags )?.GetValue( value ) ??
			value.GetType().GetProperty( "ValueUnsigned", InstanceReflectionFlags )?.GetValue( value );

		return rawValue switch
		{
			long signedRaw => signedRaw,
			ulong unsignedRaw => unchecked((long)unsignedRaw),
			_ => long.TryParse( rawValue?.ToString() ?? value.ToString(), out var parsed ) ? parsed : 0
		};
	}

	private static object CreateSteamIdArgument( long steamId, Type steamIdType )
	{
		if ( steamIdType == typeof( long ) )
			return steamId;

		if ( steamIdType == typeof( ulong ) )
			return unchecked((ulong)steamId);

		var unsignedConstructor = steamIdType.GetConstructor( new[] { typeof( ulong ) } );
		if ( unsignedConstructor is not null )
			return unsignedConstructor.Invoke( new object[] { unchecked((ulong)steamId) } );

		var signedConstructor = steamIdType.GetConstructor( new[] { typeof( long ) } );
		if ( signedConstructor is not null )
			return signedConstructor.Invoke( new object[] { steamId } );

		var implicitConversion = steamIdType.GetMethod(
			"op_Implicit",
			StaticReflectionFlags,
			null,
			new[] { typeof( ulong ) },
			null );

		if ( implicitConversion is not null )
			return implicitConversion.Invoke( null, new object[] { unchecked((ulong)steamId) } );

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
