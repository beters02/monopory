using Sandbox;
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

public sealed class SteamFriendListEntry
{
	public long SteamId { get; set; }
	public string Name { get; set; } = "";
	public string Status { get; set; } = "";
	public string RichPresence { get; set; } = "";
	public bool IsOnline { get; set; }
	public bool IsPlayingThisGame { get; set; }
	public Texture AvatarTexture { get; set; }
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
	private static readonly Dictionary<long, Texture> cachedAvatarTextures = new();
	private static readonly HashSet<long> loadingAvatarTextures = new();
	private static readonly HashSet<string> avatarLogKeys = new();
	private static float nextRefreshTime;

	public static IReadOnlyList<SteamFriendListEntry> GetFriends()
	{
		if ( !MonopolyApp.IsStandalone() )
			return Application.IsEditor ? EditorFriends : Array.Empty<SteamFriendListEntry>();

		if ( Time.Now < nextRefreshTime )
		{
			HydrateCachedAvatarTextures( cachedFriends );
			return cachedFriends;
		}

		nextRefreshTime = Time.Now + 5f;
		cachedFriends = LoadFriends();
		HydrateCachedAvatarTextures( cachedFriends );
		return cachedFriends;
	}

	public static bool TryInviteFriend( SteamFriendListEntry friend )
	{
		if ( friend is null || friend.SteamId == 0 || !MonopolyApp.IsStandalone() )
			return false;

//#if STANDALONE
		try
		{
			var connectTarget = GetInviteConnectTarget();
			var friendObject = GetFriendObject( friend.SteamId );
			var friendInviteMethod = friendObject?.GetType().GetMethod( "InviteToGame", InstanceReflectionFlags, null, new[] { typeof( string ) }, null );

			if ( TryInvokeInstanceFriendAction( friendObject, friendInviteMethod, connectTarget ) )
				return true;

			return TryInviteUserToGame( friend.SteamId, connectTarget );
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
		{
			Log.Info($"Friend is null: {friend is null}");
			Log.Info($"friend.steamid == 0 {friend.SteamId == 0}  {friend.SteamId}");
			Log.Info($"standalone {MonopolyApp.IsStandalone()}");
			Log.Info("Open profile failed sanity checks");
		}

//#if STANDALONE
		try
		{
			var friendObject = GetFriendObject( friend.SteamId );
			var friendOverlayMethod = friendObject?.GetType().GetMethod( "OpenInOverlay", InstanceReflectionFlags, null, new[] { typeof( string ) }, null );
			if ( TryInvokeInstanceFriendAction( friendObject, friendOverlayMethod, "steamid" ) )
				return true;

			Log.Info("OpenInOverlay failed");

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
			IsPlayingThisGame = GetBoolValue( GetPropertyValue( friend, "IsPlayingThisGame" ) ),
			AvatarTexture = GetAvatarTexture( steamId, friend )
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

	private static bool TryInvokeInstanceFriendAction( object target, MethodInfo method, string value )
	{
		if ( target is null || method is null )
			return false;

		var parameters = method.GetParameters();
		object result;

		if ( parameters.Length == 0 )
		{
			result = method.Invoke( target, null );
		}
		else if ( parameters.Length == 1 )
		{
			result = method.Invoke( target, new object[] { value ?? "" } );
		}
		else
		{
			return false;
		}

		return result is not bool success || success;
	}

	private static bool TryInviteUserToGame( long steamId, string connectTarget )
	{
		var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
		var internalProperty = steamFriendsType?.GetProperty( "Internal", StaticReflectionFlags );
		var steamFriendsInternal = internalProperty?.GetValue( null );
		var inviteMethod = steamFriendsInternal?.GetType().GetMethod( "InviteUserToGame", InstanceReflectionFlags );
		if ( inviteMethod is null )
			return false;

		var parameters = inviteMethod.GetParameters();
		if ( parameters.Length != 2 )
			return false;

		var friendArgument = CreateSteamIdArgument( steamId, parameters[0].ParameterType );
		if ( friendArgument is null )
			return false;

		var result = inviteMethod.Invoke( steamFriendsInternal, new object[] { friendArgument, connectTarget ?? "" } );
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

	private static Texture GetAvatarTexture( long steamId, object friend )
	{
		if ( steamId == 0 )
		{
			LogAvatarOnce( steamId, "zero-id", "Steam avatar skipped because the friend SteamId is 0." );
			return null;
		}

		if ( cachedAvatarTextures.TryGetValue( steamId, out var texture ) )
			return texture;

		if ( loadingAvatarTextures.Add( steamId ) )
		{
			LogAvatarOnce( steamId, "start", $"Loading Steam avatar for {steamId}." );
			_ = LoadAvatarTextureAsync( steamId, friend );
		}

		return null;
	}

	private static void HydrateCachedAvatarTextures( IReadOnlyList<SteamFriendListEntry> friends )
	{
		if ( friends is null || friends.Count == 0 || cachedAvatarTextures.Count == 0 )
			return;

		foreach ( var friend in friends )
		{
			if ( friend is null || friend.AvatarTexture is not null )
				continue;

			if ( cachedAvatarTextures.TryGetValue( friend.SteamId, out var texture ) )
				friend.AvatarTexture = texture;
		}
	}

	private static async Task LoadAvatarTextureAsync( long steamId, object friend )
	{
		try
		{
			var avatarImage = await GetAvatarImageAsync( steamId, friend );
			var avatarTexture = CreateAvatarTexture( steamId, avatarImage );
			if ( avatarTexture is not null )
			{
				cachedAvatarTextures[steamId] = avatarTexture;
				LogAvatarOnce( steamId, "success", $"Loaded Steam avatar texture for {steamId}." );
			}
			else
			{
				LogAvatarOnce( steamId, "null-texture", $"Steam avatar texture was not created for {steamId}." );
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to load Steam avatar for {steamId}: {exception.Message}" );
		}
		finally
		{
			loadingAvatarTextures.Remove( steamId );
		}
	}

	private static async Task<object> GetAvatarImageAsync( long steamId, object friend )
	{
		var friendAvatarMethod = friend?.GetType().GetMethod( "GetMediumAvatarAsync", InstanceReflectionFlags, null, Type.EmptyTypes, null );
		var friendAvatarTask = friendAvatarMethod?.Invoke( friend, null );
		if ( friendAvatarTask is not null )
		{
			LogAvatarOnce( steamId, "friend-method", $"Steam avatar using Friend.GetMediumAvatarAsync for {steamId}." );
			return await GetTaskResultAsync( friendAvatarTask );
		}

		LogAvatarOnce( steamId, "friend-method-missing", $"Friend.GetMediumAvatarAsync was not available for {steamId}; trying SteamFriends.GetMediumAvatarAsync." );

		var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
		var avatarMethod = steamFriendsType?.GetMethod( "GetMediumAvatarAsync", StaticReflectionFlags );
		if ( avatarMethod is null )
		{
			LogAvatarOnce( steamId, "static-method-missing", $"SteamFriends.GetMediumAvatarAsync was not found for {steamId}." );
			return null;
		}

		var parameters = avatarMethod.GetParameters();
		if ( parameters.Length != 1 )
		{
			LogAvatarOnce( steamId, "bad-signature", $"SteamFriends.GetMediumAvatarAsync had {parameters.Length} parameters for {steamId}." );
			return null;
		}

		var steamIdArgument = CreateSteamIdArgument( steamId, parameters[0].ParameterType );
		if ( steamIdArgument is null )
		{
			LogAvatarOnce( steamId, "bad-steamid-argument", $"Could not convert {steamId} to {parameters[0].ParameterType.FullName} for Steam avatar loading." );
			return null;
		}

		LogAvatarOnce( steamId, "static-method", $"Steam avatar using SteamFriends.GetMediumAvatarAsync for {steamId}." );
		return await GetTaskResultAsync( avatarMethod.Invoke( null, new[] { steamIdArgument } ) );
	}

	private static async Task<object> GetTaskResultAsync( object taskObject )
	{
		if ( taskObject is not Task task )
			return null;

		await task;
		return task.GetType().GetProperty( "Result", InstanceReflectionFlags )?.GetValue( task );
	}

	private static Texture CreateAvatarTexture( long steamId, object avatarImage )
	{
		if ( avatarImage is null )
		{
			LogAvatarOnce( steamId, "null-image", $"Steam avatar image result was null for {steamId}." );
			return null;
		}

		var width = Convert.ToInt32( GetPropertyValue( avatarImage, "Width" ) ?? 0 );
		var height = Convert.ToInt32( GetPropertyValue( avatarImage, "Height" ) ?? 0 );
		var data = GetPropertyValue( avatarImage, "Data" ) as byte[];
		if ( width <= 0 || height <= 0 || data is null || data.Length == 0 )
		{
			LogAvatarOnce( steamId, "bad-image", $"Steam avatar image was invalid for {steamId}: {width}x{height}, {data?.Length ?? 0} bytes." );
			return null;
		}

		return Texture.Create( width, height, ImageFormat.RGBA8888 )
			.WithName( $"steam-avatar-{steamId}" )
			.WithData( data )
			.Finish();
	}

	private static void LogAvatarOnce( long steamId, string key, string message )
	{
		if ( avatarLogKeys.Add( $"{steamId}:{key}" ) )
			Log.Info( message );
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
		var type = instance.GetType();
		return type.GetProperty( propertyName, InstanceReflectionFlags )?.GetValue( instance ) ??
			type.GetField( propertyName, InstanceReflectionFlags )?.GetValue( instance );
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

		var rawValue = GetPropertyValue( value, "Value" ) ?? GetPropertyValue( value, "ValueUnsigned" );

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
