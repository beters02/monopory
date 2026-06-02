using System;
using System.Reflection;
using System.Threading.Tasks;

public class MonopolyApp : Component
{
    public static bool IsStandalone = false;
    public static bool IsDebugEnabled = false;
    public static bool GameLaunchedWithDebugConvar = false;

    [ConVar( "achievements_backend_url" )]
    public static string AchievementsBackendUrl { get; set; } = "";

    [ConVar( "achievements_access_token" )]
    public static string AchievementsAccessToken { get; set; } = "";

    private const string DefaultAchievementsServiceName = Sandbox.Services.RentRushService.DefaultServiceName;

    [ConVar( "achievements_service_name" )]
    public static string AchievementsServiceName { get; set; } = DefaultAchievementsServiceName;

    [ConVar( "achievements_auth_token" )]
    public static string AchievementsAuthToken { get; set; } = "";

    [ConVar( "achievements_auth_ticket_target_steam_id" )]
    public static string AchievementsAuthTicketTargetSteamId { get; set; } = "0";

    [ConVar( "achievements_dev_steam_id" )]
    public static string AchievementsDevSteamId { get; set; } = "";

#if STANDALONE
    private static bool achievementsBackendInitialized;
    private static bool achievementsBackendInitializationInFlight;
    private static string lastAchievementsBackendUrl = "";
#endif

	protected override void OnAwake()
	{
#if STANDALONE
        IsStandalone = true;
#endif

        var debugConvarParsed = bool.TryParse(ConsoleSystem.GetValue( "debug" ), out bool debugConvar);
        GameLaunchedWithDebugConvar = debugConvar;
		if (debugConvarParsed && debugConvar)
			IsDebugEnabled = true;

        Log.Info($"IsStandalone: {IsStandalone}");

#if STANDALONE
        _ = InitializeAchievementsBackendAsync();
#endif
	}

    [ConCmd( "achievements_connect_backend" )]
    private static void ConnectAchievementsBackend( Connection connection )
    {
#if STANDALONE
        _ = InitializeAchievementsBackendAsync( true );
#else
        Log.Warning( "Achievements backend connection is only available in standalone builds." );
#endif
    }

    [ConCmd( "achievements_auth_diagnostics" )]
    private static void PrintAchievementsAuthDiagnostics( Connection connection )
    {
#if STANDALONE
        Log.Info( Sandbox.Services.RentRushService.GetSteamAuthDiagnostics() );
#else
        Log.Warning( "Achievements auth diagnostics are only available in standalone builds." );
#endif
    }

#if STANDALONE
    private static async Task InitializeAchievementsBackendAsync( bool force = false )
    {
        if ( string.IsNullOrWhiteSpace( AchievementsBackendUrl ) )
        {
            if ( force )
                Log.Warning( "Set achievements_backend_url before connecting achievements backend." );
            return;
        }

        if ( achievementsBackendInitializationInFlight )
            return;

        if ( achievementsBackendInitialized && !force && string.Equals( lastAchievementsBackendUrl, AchievementsBackendUrl, StringComparison.Ordinal ) )
            return;

        try
        {
            achievementsBackendInitializationInFlight = true;
            lastAchievementsBackendUrl = AchievementsBackendUrl;

            var token = AchievementsAccessToken;
            if ( string.IsNullOrWhiteSpace( token ) )
            {
                var steamId = GetConfiguredOrStandaloneSteamId( out var steamIdSource );
                var steamTicket = string.IsNullOrWhiteSpace( AchievementsAuthToken )
                    ? await GetDefaultAuthToken( steamId )
                    : AchievementsAuthToken;

                if ( steamId == 0 )
                {
                    Log.Warning( "Achievements backend configured, but no Steam ID was available. Set achievements_dev_steam_id for local dev auth or wait until Steam is initialized." );
                    return;
                }

                if ( string.IsNullOrWhiteSpace( steamTicket ) )
                {
                    Log.Warning( $"Achievements backend configured, but no Steam Web API auth ticket was available. SteamId={steamId} source={steamIdSource} identity={GetAchievementsServiceName()}. Set achievements_auth_token to a ticket hex string or achievements_access_token to a backend bearer token to override." );
                    return;
                }

                Log.Info( $"Authenticating achievements backend with Steam ticket. SteamId={steamId} source={steamIdSource} ticketLength={steamTicket.Length} identity={GetAchievementsServiceName()}." );
                token = await Sandbox.Services.RentRushService.AuthenticateAchievementsBackendAsync(
                    AchievementsBackendUrl,
                    steamId,
                    steamTicket,
                    Connection.Local?.DisplayName ?? ""
                );
            }

            if ( string.IsNullOrWhiteSpace( token ) )
                return;

            AchievementsAccessToken = token;
            AchievementServices.UseBackend( AchievementsBackendUrl, token );
            achievementsBackendInitialized = true;
            Log.Info( "Achievements backend enabled." );
        }
        catch ( Exception exception )
        {
            achievementsBackendInitialized = false;
            Log.Warning( $"Failed to initialize achievements backend: {exception.Message}" );
        }
        finally
        {
            achievementsBackendInitializationInFlight = false;
        }
    }

    private static long GetConfiguredOrStandaloneSteamId( out string source )
    {
        if ( long.TryParse( AchievementsDevSteamId, out var configuredSteamId ) && configuredSteamId > 0 )
        {
            source = "achievements_dev_steam_id";
            return configuredSteamId;
        }

        return GetStandaloneSteamId( out source );
    }

    private static async Task<string> GetDefaultAuthToken( long steamId )
    {
        var token = await Sandbox.Services.RentRushService.GetSteamWebApiTicketAsync( GetAchievementsServiceName(), GetAuthTicketTargetSteamId() );
        if ( !string.IsNullOrWhiteSpace( token ) )
            return token;

        return !string.IsNullOrWhiteSpace( AchievementsDevSteamId ) && steamId > 0
            ? "dev-token"
            : "";
    }

    private static long GetStandaloneSteamId( out string source )
    {
        source = "none";
        try
        {
            if ( Connection.Local?.SteamId > 0 )
            {
                source = "Connection.Local";
                return Connection.Local.SteamId;
            }

            var steamClientType = FindLoadedType( "Steamworks.SteamClient" );
            var steamIdProperty = steamClientType?.GetProperty( "SteamId", BindingFlags.Public | BindingFlags.Static );
            var steamIdValue = steamIdProperty?.GetValue( null );
            if ( steamIdValue is null )
                return 0;

            var valueProperty = steamIdValue.GetType().GetProperty( "Value", BindingFlags.Public | BindingFlags.Instance ) ??
                steamIdValue.GetType().GetProperty( "ValueUnsigned", BindingFlags.Public | BindingFlags.Instance );
            var rawValue = valueProperty?.GetValue( steamIdValue );
            if ( rawValue is not null )
            {
                source = "Steamworks.SteamClient.SteamId.Value";
                return Convert.ToInt64( rawValue );
            }

            if ( long.TryParse( steamIdValue.ToString(), out var parsedSteamId ) && parsedSteamId > 0 )
            {
                source = "Steamworks.SteamClient.SteamId.ToString";
                return parsedSteamId;
            }

            return 0;
        }
        catch ( Exception exception )
        {
            Log.Warning( $"Failed to resolve standalone Steam ID: {exception.Message}" );
            return 0;
        }
    }

    private static string GetAchievementsServiceName()
    {
        return string.IsNullOrWhiteSpace( AchievementsServiceName )
            ? DefaultAchievementsServiceName
            : AchievementsServiceName.Trim();
    }

    private static ulong GetAuthTicketTargetSteamId()
    {
        return ulong.TryParse( AchievementsAuthTicketTargetSteamId, out var targetSteamId )
            ? targetSteamId
            : 0UL;
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

    protected override void OnUpdate()
    {
#if STANDALONE
        if ( !string.IsNullOrWhiteSpace( AchievementsBackendUrl ) &&
            !achievementsBackendInitialized &&
            !achievementsBackendInitializationInFlight )
        {
            _ = InitializeAchievementsBackendAsync();
        }
#endif

        if ( Scene.Camera is null )
            return;

        var hud = Scene.Camera.Hud;
        hud.DrawText( new TextRendering.Scope( "Hello!", Color.Red, 32 ), Screen.Width * 0.5f );
    }
}
