using System;
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

    [ConVar( "achievements_auth_service_name" )]
    public static string AchievementsAuthServiceName { get; set; } = Sandbox.Services.RentRushService.DefaultSboxAuthServiceName;

    [ConVar( "achievements_steamworks_auth_enabled" )]
    public static bool AchievementsSteamworksAuthEnabled { get; set; } = true;

    [ConVar( "achievements_steam_app_id" )]
    public static string AchievementsSteamAppId { get; set; } = Sandbox.Services.RentRushService.DefaultSteamAppId.ToString();

    [ConVar( "achievements_steamworks_ticket_identity" )]
    public static string AchievementsSteamworksTicketIdentity { get; set; } = Sandbox.Services.RentRushService.DefaultSteamworksTicketIdentity;

#if STANDALONE
    private static bool achievementsBackendInitialized;
    private static bool achievementsBackendInitializationInFlight;
    private static string lastAchievementsBackendUrl = "";
    private static long authenticatedAchievementsPlayerId;
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
        Log.Info( Sandbox.Services.RentRushService.GetDeviceAuthDiagnostics() );
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
            var playerId = authenticatedAchievementsPlayerId;
            if ( string.IsNullOrWhiteSpace( token ) )
            {
                if ( AchievementsSteamworksAuthEnabled )
                {
                    var steamworksSession = await Sandbox.Services.RentRushService.AuthenticateAchievementsBackendWithSteamworksAsync(
                        AchievementsBackendUrl,
                        GetSteamAppId(),
                        AchievementsSteamworksTicketIdentity,
                        Connection.Local?.DisplayName ?? ""
                    );
                    token = steamworksSession?.AccessToken ?? "";
                    playerId = steamworksSession?.PlayerId ?? 0;
                }

                if ( string.IsNullOrWhiteSpace( token ) )
                {
                    Log.Warning( "Facepunch Steamworks achievements auth was not available; trying s&box auth token." );
                    var sboxSession = await Sandbox.Services.RentRushService.AuthenticateAchievementsBackendWithSboxAsync(
                        AchievementsBackendUrl,
                        AchievementsAuthServiceName,
                        Connection.Local?.DisplayName ?? ""
                    );
                    token = sboxSession?.AccessToken ?? "";
                    playerId = sboxSession?.PlayerId ?? 0;
                }

                if ( string.IsNullOrWhiteSpace( token ) )
                {
                    Log.Warning( "s&box achievements auth was not available; falling back to local device identity." );
                    var deviceSession = await Sandbox.Services.RentRushService.AuthenticateAchievementsBackendWithDeviceAsync(
                        AchievementsBackendUrl,
                        Connection.Local?.DisplayName ?? ""
                    );
                    token = deviceSession?.AccessToken ?? "";
                    playerId = deviceSession?.PlayerId ?? 0;
                }
            }

            if ( string.IsNullOrWhiteSpace( token ) )
                return;

            AchievementsAccessToken = token;
            authenticatedAchievementsPlayerId = playerId;
            AchievementServices.UseBackend( AchievementsBackendUrl, token, playerId );
            achievementsBackendInitialized = true;
            Log.Info( $"Achievements backend enabled. playerId={playerId}." );
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

    private static uint GetSteamAppId()
    {
        return uint.TryParse( AchievementsSteamAppId, out var appId ) && appId != 0
            ? appId
            : Sandbox.Services.RentRushService.DefaultSteamAppId;
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
