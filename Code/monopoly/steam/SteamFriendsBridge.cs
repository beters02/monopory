using Sandbox;
using System;
using System.Collections;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class SteamFriendListEntry
{
	public long SteamId { get; set; }
	public string Name { get; set; } = "";
	public string Status { get; set; } = "";
	public string RichPresence { get; set; } = "";
	public string GameName { get; set; } = "";
	public long JoinLobbyId { get; set; }
	public string JoinConnectTarget { get; set; } = "";
	public bool IsOnline { get; set; }
	public bool IsAway { get; set; }
	public bool IsPlayingAnyGame { get; set; }
	public bool IsPlayingThisGame { get; set; }
	public Texture AvatarTexture { get; set; }
	public bool CanJoinLobby => JoinLobbyId != 0 || !string.IsNullOrWhiteSpace( JoinConnectTarget );

	public SteamFriendPresenceGroup PresenceGroup
	{
		get
		{
			if ( IsPlayingThisGame )
				return SteamFriendPresenceGroup.InRentRush;

			if ( IsPlayingAnyGame )
				return SteamFriendPresenceGroup.InOtherGame;

			if ( IsAway )
				return SteamFriendPresenceGroup.Away;

			if ( IsOnline )
				return SteamFriendPresenceGroup.Online;

			return SteamFriendPresenceGroup.Offline;
		}
	}
}

public enum SteamFriendPresenceGroup
{
	InRentRush,
	InOtherGame,
	Online,
	Away,
	Offline
}

/// "steamid" - Opens the overlay web browser to the specified user or groups profile.
/// "chat" - Opens a chat window to the specified user, or joins the group chat.
/// "jointrade" - Opens a window to a Steam Trading session that was started with the ISteamEconomy/StartTrade Web API.
/// "stats" - Opens the overlay web browser to the specified user's stats.
/// "achievements" - Opens the overlay web browser to the specified user's achievements.
/// "friendadd" - Opens the overlay in minimal mode prompting the user to add the target user as a friend.
/// "friendremove" - Opens the overlay in minimal mode prompting the user to remove the target friend.
/// "friendrequestaccept" - Opens the overlay in minimal mode prompting the user to accept an incoming friend invite.
/// "friendrequestignore" - Opens the overlay in minimal mode prompting the user to ignore an incoming friend invite.
public enum SteamUserOverlayType
{
	steamid,
	chat,
	friendadd
}

public static class SteamFriendsBridge
{
	private const BindingFlags StaticReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
	private const BindingFlags InstanceReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
	private static readonly IReadOnlyList<SteamFriendListEntry> EditorFriends =
	[
		new SteamFriendListEntry
		{
			SteamId = 1,
			Name = "Editor Test Friend",
			Status = "Online",
			RichPresence = "Testing friend popup",
			GameName = "Rent Rush",
			IsOnline = true,
			IsPlayingAnyGame = true,
			IsPlayingThisGame = true
		}
	];
	private static IReadOnlyList<SteamFriendListEntry> cachedFriends = Array.Empty<SteamFriendListEntry>();
	private static readonly Dictionary<long, Texture> cachedAvatarTextures = new();
	private static readonly HashSet<long> loadingAvatarTextures = new();
#if STANDALONE
	private static readonly Dictionary<ulong, string> cachedAppNames = new();
	private static readonly HashSet<ulong> loadingAppNames = new();
	private static readonly HttpClient appNameHttp = new();
#endif
	private static readonly HashSet<string> avatarLogKeys = new();
	private static float nextRefreshTime;
	private static int avatarRevision = 0;

	public static int AvatarRevision => avatarRevision;

	public static IReadOnlyList<SteamFriendListEntry> GetFriends( bool forceRefresh = false )
	{
		if ( !MonopolyApp.IsStandalone() )
			return Application.IsEditor ? EditorFriends : Array.Empty<SteamFriendListEntry>();

		if ( !forceRefresh && Time.Now < nextRefreshTime )
		{
			HydrateCachedAvatarTextures( cachedFriends );
			HydrateCachedGameNames( cachedFriends );
			return cachedFriends;
		}

		nextRefreshTime = Time.Now + 5f;
		cachedFriends = LoadFriends();
		HydrateCachedAvatarTextures( cachedFriends );
		HydrateCachedGameNames( cachedFriends );
		return cachedFriends;
	}

	public static SteamFriendListEntry GetProfileEntry( long steamId, string fallbackName = "" )
	{
		if ( steamId == 0 )
		{
			return new SteamFriendListEntry
			{
				Name = string.IsNullOrWhiteSpace( fallbackName ) ? "Player" : fallbackName,
				Status = "Steam unavailable"
			};
		}

		var friend = GetFriends().FirstOrDefault( friend => friend is not null && friend.SteamId == steamId );
		if ( friend is not null )
			return friend;

		var connection = Connection.All.FirstOrDefault( connection => connection is not null && connection.SteamId == steamId );
		var entry = new SteamFriendListEntry
		{
			SteamId = steamId,
			Name = !string.IsNullOrWhiteSpace( fallbackName ) ? fallbackName : connection?.Name ?? $"Player {steamId}",
			Status = connection is not null ? "In match" : "Offline",
			IsOnline = connection is not null,
			IsPlayingAnyGame = connection is not null,
			IsPlayingThisGame = connection is not null,
			GameName = connection is not null ? "Rent Rush" : ""
		};

		if ( cachedAvatarTextures.TryGetValue( steamId, out var texture ) )
			entry.AvatarTexture = texture;

#if STANDALONE
		if ( entry.AvatarTexture is null )
			entry.AvatarTexture = GetAvatarTexture( steamId, null );
#endif

		return entry;
	}

	private static SteamFriendListEntry GetFreshFriendEntry( SteamFriendListEntry friend )
	{
		if ( friend is null || friend.SteamId == 0 )
			return friend;

		return GetFriends( true ).FirstOrDefault( candidate => candidate is not null && candidate.SteamId == friend.SteamId ) ?? friend;
	}
	public static bool TryInviteFriend( SteamFriendListEntry friend )
	{
		if ( friend is null || friend.SteamId == 0 || !MonopolyApp.IsStandalone() )
			return false;

#if STANDALONE
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
#endif

		return false;
	}

	public static bool TryOpenProfile( SteamFriendListEntry friend, string overlayType = "steamid" )
	{
		if ( friend is null || friend.SteamId == 0 || !MonopolyApp.IsStandalone() )
		{
			Log.Info($"Friend is null: {friend is null}");
			Log.Info($"friend.steamid == 0 {friend.SteamId == 0}  {friend.SteamId}");
			Log.Info($"standalone {MonopolyApp.IsStandalone()}");
			Log.Info("Open profile failed sanity checks");
		}

#if STANDALONE
		try
		{
			var friendObject = GetFriendObject( friend.SteamId );
			var friendOverlayMethod = friendObject?.GetType().GetMethod( "OpenInOverlay", InstanceReflectionFlags, null, new[] { typeof( string ) }, null );
			if ( TryInvokeInstanceFriendAction( friendObject, friendOverlayMethod, overlayType ) )
				return true;

			Log.Info("OpenInOverlay failed");

			var steamFriendsType = FindLoadedType( "Steamworks.SteamFriends" );
			var overlayMethod = steamFriendsType?.GetMethod( "OpenUserOverlay", StaticReflectionFlags );

			if ( overlayMethod is not null )
				return TryInvokeFriendAction( overlayMethod, friend.SteamId, overlayType );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to open Steam profile {friend.SteamId}: {exception.Message}" );
		}
#endif

		return false;
	}

	public static async Task<bool> TryJoinFriend( SteamFriendListEntry friend, Scene scene )
	{
		friend = GetFreshFriendEntry( friend );
		if ( friend is null || !friend.CanJoinLobby || !MonopolyApp.IsStandalone() )
			return false;

		try
		{
			if ( Networking.IsActive )
				Networking.Disconnect();

			var connected = false;
			if ( friend.JoinLobbyId != 0 )
				connected = await Networking.TryConnectSteamId( friend.JoinLobbyId, 3 );

			if ( !connected && !string.IsNullOrWhiteSpace( friend.JoinConnectTarget ) )
			{
				Networking.Connect( friend.JoinConnectTarget );
				connected = await WaitForConnectionAsync( 6f );
			}

			if ( connected )
			{
				SceneFlow.LoadLobby( scene );
				return true;
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to join Steam friend {friend.SteamId}: {exception.Message}" );
		}

		return false;
	}

	private static IReadOnlyList<SteamFriendListEntry> LoadFriends()
	{
#if STANDALONE
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
				.OrderBy( friend => friend.PresenceGroup )
				.ThenBy( friend => friend.Name )
				.ToList();
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to load Steam friends: {exception.Message}" );
		}
#endif

		return Array.Empty<SteamFriendListEntry>();
	}

	private static void HydrateCachedGameNames( IReadOnlyList<SteamFriendListEntry> friends )
	{
#if STANDALONE
		if ( friends is null || friends.Count == 0 || cachedAppNames.Count == 0 )
			return;

		foreach ( var friend in friends )
		{
			if ( friend is null || !friend.IsPlayingAnyGame || friend.IsPlayingThisGame )
				continue;

			if ( !friend.GameName.StartsWith( "Steam app ", StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( ulong.TryParse( friend.GameName["Steam app ".Length..], out var gameId ) && cachedAppNames.TryGetValue( gameId, out var appName ) )
				friend.GameName = appName;
		}
#endif
		return;
	}

	private static void HydrateCachedAvatarTextures( IReadOnlyList<SteamFriendListEntry> friends )
	{
#if STANDALONE
		if ( friends is null || friends.Count == 0 || cachedAvatarTextures.Count == 0 )
			return;

		foreach ( var friend in friends )
		{
			if ( friend is null || friend.AvatarTexture is not null )
				continue;

			if ( cachedAvatarTextures.TryGetValue( friend.SteamId, out var texture ) )
				friend.AvatarTexture = texture;
		}
#endif
		return;
	}

#if STANDALONE
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
			GameName = GetGameName( friend ),
			JoinLobbyId = GetJoinLobbyId( friend ),
			JoinConnectTarget = GetJoinConnectTarget( friend ),
			IsOnline = GetBoolValue( GetPropertyValue( friend, "IsOnline" ) ),
			IsAway = IsAway( friend ),
			IsPlayingAnyGame = IsPlayingAnyGame( friend ),
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

	private static string GetGameName( object friend )
	{
		var gameTitle = GetRichPresenceValue( friend, "gametitle" );
		if ( !string.IsNullOrWhiteSpace( gameTitle ) && gameTitle != "menu" )
			return gameTitle;

		var gameName = GetRichPresenceValue( friend, "gamename" );
		if ( !string.IsNullOrWhiteSpace( gameName ) && gameName != "menu" )
			return gameName;

		if ( GetBoolValue( GetPropertyValue( friend, "IsPlayingThisGame" ) ) )
			return "Rent Rush";

		var gameId = GetGameId( friend );
		if ( gameId == 0 )
			return "";

		if ( cachedAppNames.TryGetValue( gameId, out var cachedName ) )
			return cachedName;

		if ( loadingAppNames.Add( gameId ) )
			_ = LoadSteamAppNameAsync( gameId );

		return $"Steam app {gameId}";
	}

	private static string GetRichPresenceValue( object friend, string key )
	{
		var method = friend.GetType().GetMethod( "GetRichPresence", InstanceReflectionFlags, null, new[] { typeof( string ) }, null );
		return GetStringValue( method?.Invoke( friend, new object[] { key } ) );
	}

	private static long GetJoinLobbyId( object friend )
	{
		var connectTarget = GetJoinConnectTarget( friend );
		if ( TryParseLobbyIdFromConnectTarget( connectTarget, out var richPresenceLobbyId ) )
			return richPresenceLobbyId;

		var directLobbyId = GetSteamIdValue(
			GetPropertyValue( friend, "LobbyId" ) ??
			GetPropertyValue( friend, "LobbyID" ) ??
			GetPropertyValue( friend, "GameLobbyId" ) ??
			GetPropertyValue( friend, "GameLobbyID" ) ??
			GetPropertyValue( friend, "SteamIDLobby" ) ??
			GetPropertyValue( friend, "LobbySteamId" ) );
		if ( directLobbyId != 0 )
			return directLobbyId;

		var gameInfo = GetPropertyValue( friend, "GameInfo" );
		if ( gameInfo is null )
			return 0;

		return GetSteamIdValue(
			GetPropertyValue( gameInfo, "LobbyId" ) ??
			GetPropertyValue( gameInfo, "LobbyID" ) ??
			GetPropertyValue( gameInfo, "GameLobbyId" ) ??
			GetPropertyValue( gameInfo, "GameLobbyID" ) ??
			GetPropertyValue( gameInfo, "SteamIDLobby" ) ??
			GetPropertyValue( gameInfo, "LobbySteamId" ) ??
			GetPropertyValue( gameInfo, "Lobby" ) );
	}

	private static string GetJoinConnectTarget( object friend )
	{
		var connect = GetRichPresenceValue( friend, "connect" );
		if ( !string.IsNullOrWhiteSpace( connect ) )
			return connect;

		var connectLobby = GetRichPresenceValue( friend, "connect_lobby" );
		if ( !string.IsNullOrWhiteSpace( connectLobby ) )
			return connectLobby.StartsWith( "+connect_lobby ", StringComparison.OrdinalIgnoreCase )
				? connectLobby
				: $"+connect_lobby {connectLobby}";

		return "";
	}

	private static bool TryParseLobbyIdFromConnectTarget( string connectTarget, out long lobbyId )
	{
		lobbyId = 0;
		if ( string.IsNullOrWhiteSpace( connectTarget ) )
			return false;

		var parts = connectTarget.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		for ( var i = 0; i < parts.Length - 1; i++ )
		{
			if ( parts[i].Equals( "+connect_lobby", StringComparison.OrdinalIgnoreCase )
				|| parts[i].Equals( "connect_lobby", StringComparison.OrdinalIgnoreCase ) )
			{
				return long.TryParse( parts[i + 1], out lobbyId ) && lobbyId != 0;
			}
		}

		return long.TryParse( connectTarget, out lobbyId ) && lobbyId != 0;
	}

	private static bool IsAway( object friend )
	{
		return GetBoolValue( GetPropertyValue( friend, "IsAway" ) )
			|| GetBoolValue( GetPropertyValue( friend, "IsBusy" ) )
			|| GetBoolValue( GetPropertyValue( friend, "IsSnoozing" ) )
			|| IsAwayStatus( GetStatus( friend ) );
	}

	private static bool IsAwayStatus( string status )
	{
		if ( string.IsNullOrWhiteSpace( status ) )
			return false;

		return status.Equals( "Away", StringComparison.OrdinalIgnoreCase )
			|| status.Equals( "Busy", StringComparison.OrdinalIgnoreCase )
			|| status.Equals( "Snooze", StringComparison.OrdinalIgnoreCase )
			|| status.Equals( "Snoozing", StringComparison.OrdinalIgnoreCase );
	}

	private static bool IsPlayingAnyGame( object friend )
	{
		if ( GetBoolValue( GetPropertyValue( friend, "IsPlayingAGame" ) ) )
			return true;

		if ( GetBoolValue( GetPropertyValue( friend, "IsPlaying" ) ) )
			return true;

		if ( GetBoolValue( GetPropertyValue( friend, "IsPlayingThisGame" ) ) )
			return true;

		return GetGameId( friend ) != 0;
	}

	private static ulong GetGameId( object friend )
	{
		var gameInfo = GetPropertyValue( friend, "GameInfo" );
		if ( gameInfo is null )
			return 0;

		var gameId = GetPropertyValue( gameInfo, "GameId" ) ?? GetPropertyValue( gameInfo, "GameID" );
		return gameId switch
		{
			ulong unsigned => unsigned,
			long signed => unchecked((ulong)signed),
			uint unsignedInt => unsignedInt,
			int signedInt => unchecked((ulong)signedInt),
			_ => ulong.TryParse( gameId?.ToString() ?? "", out var parsed ) ? parsed : 0
		};
	}

	private static async Task LoadSteamAppNameAsync( ulong appId )
	{
		try
		{
			using var response = await appNameHttp.GetAsync( $"https://store.steampowered.com/api/appdetails?appids={appId}&filters=basic" );
			if ( !response.IsSuccessStatusCode )
			{
				Log.Warning( $"Steam app name lookup failed for {appId}: {(int)response.StatusCode} {response.ReasonPhrase}" );
				return;
			}

			var body = await response.Content.ReadAsStringAsync();
			using var document = JsonDocument.Parse( body );
			if ( !document.RootElement.TryGetProperty( appId.ToString(), out var appElement ) )
				return;

			if ( !appElement.TryGetProperty( "success", out var successElement ) || !successElement.GetBoolean() )
				return;

			if ( appElement.TryGetProperty( "data", out var dataElement )
				&& dataElement.TryGetProperty( "name", out var nameElement ) )
			{
				var name = nameElement.GetString();
				if ( !string.IsNullOrWhiteSpace( name ) )
					cachedAppNames[appId] = name;
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to load Steam app name {appId}: {exception.Message}" );
		}
		finally
		{
			loadingAppNames.Remove( appId );
		}
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

	private static async Task LoadAvatarTextureAsync( long steamId, object friend )
	{
		try
		{
			var avatarImage = await GetAvatarImageAsync( steamId, friend );
			var avatarTexture = CreateAvatarTexture( steamId, avatarImage );
			if ( avatarTexture is not null )
			{
				cachedAvatarTextures[steamId] = avatarTexture;
				avatarRevision++;
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
		/*if ( avatarLogKeys.Add( $"{steamId}:{key}" ) )
			Log.Info( message );*/
	}

	private static string GetInviteConnectTarget()
	{
		var lobbyId = SteamInviteBridge.GetActiveLobbyIdValue();
		if ( lobbyId != 0 )
			return $"+connect_lobby {lobbyId}";

		return Networking.ServerName ?? "";
	}

	private static async Task<bool> WaitForConnectionAsync( float timeoutSeconds )
	{
		var deadline = Time.Now + Math.Max( timeoutSeconds, 0.25f );
		while ( Time.Now < deadline )
		{
			if ( Networking.IsActive )
				return true;

			await Task.Delay( 100 );
		}

		return Networking.IsActive;
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
#endif

#if !STANDALONE
	private static async Task<bool> WaitForConnectionAsync( float timeoutSeconds )
	{
		await Task.Delay( 1 );
		return Networking.IsActive;
	}
#endif
}