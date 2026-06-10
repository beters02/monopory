using System;
using System.Threading.Tasks;

public class MonopolyApp : Component
{

    public static string GameVersion = "0.0.941";
    public static bool IsStandalone = false;
    public static bool IsDebugEnabled = false;
    public static bool GameLaunchedWithDebugConvar = false;

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
        if ( AchievementServices.IsEnabled )
            Sandbox.Services.RentRushService.TestInit();
#endif

        var debugConvarParsed = bool.TryParse(ConsoleSystem.GetValue( DebugConVar.Name ), out bool debugConvar);
        GameLaunchedWithDebugConvar = debugConvar;
		if (debugConvarParsed && debugConvar)
			IsDebugEnabled = true;

        Log.Info($"IsStandalone: {IsStandalone}");

#if STANDALONE
        if ( AchievementServices.IsEnabled )
            _ = InitializeAchievementsBackendAsync();
#endif
	}

    [ConCmd( "achievements_connect_backend" )]
    private static void ConnectAchievementsBackend( Connection connection )
    {
#if STANDALONE
        if ( !AchievementServices.IsEnabled )
        {
            Log.Info( "Achievements service is disabled." );
            return;
        }

        _ = InitializeAchievementsBackendAsync( true );
#else
        Log.Warning( "Achievements backend connection is only available in standalone builds." );
#endif
    }

    [ConCmd( "achievements_auth_diagnostics" )]
    private static void PrintAchievementsAuthDiagnostics( Connection connection )
    {
#if STANDALONE
        if ( !AchievementServices.IsEnabled )
        {
            Log.Info( "Achievements service is disabled." );
            return;
        }

        Log.Info( Sandbox.Services.RentRushService.GetDeviceAuthDiagnostics() );
#else
        Log.Warning( "Achievements auth diagnostics are only available in standalone builds." );
#endif
    }

#if STANDALONE
    private static async Task InitializeAchievementsBackendAsync( bool force = false )
    {
        if ( !AchievementServices.IsEnabled )
            return;

        if ( string.IsNullOrWhiteSpace( AchievementsBackendUrlConVar.Value ) )
        {
            if ( force )
                Log.Warning( "Set achievements_backend_url before connecting achievements backend." );
            return;
        }

        if ( achievementsBackendInitializationInFlight )
            return;

        if ( achievementsBackendInitialized && !force && string.Equals( lastAchievementsBackendUrl, AchievementsBackendUrlConVar.Value, StringComparison.Ordinal ) )
            return;

        try
        {
            achievementsBackendInitializationInFlight = true;
            lastAchievementsBackendUrl = AchievementsBackendUrlConVar.Value;

            var token = AchievementsAccessTokenConVar.Value;
            var playerId = authenticatedAchievementsPlayerId;
            if ( string.IsNullOrWhiteSpace( token ) )
            {
                if ( AchievementsSteamworksAuthEnabledConVar.Value )
                {
                    var steamworksSession = await Sandbox.Services.RentRushService.AuthenticateAchievementsBackendWithSteamworksAsync(
                        AchievementsBackendUrlConVar.Value,
                        GetSteamAppId(),
                        AchievementsSteamworksTicketIdentityConVar.Value,
                        GetLocalPlayerName()
                    );
                    token = steamworksSession?.AccessToken ?? "";
                    playerId = steamworksSession?.PlayerId ?? 0;
                }

                if ( string.IsNullOrWhiteSpace( token ) )
                {
                    Log.Warning( "Facepunch Steamworks achievements auth was not available; trying s&box auth token." );
                    var sboxSession = await Sandbox.Services.RentRushService.AuthenticateAchievementsBackendWithSboxAsync(
                        AchievementsBackendUrlConVar.Value,
                        AchievementsAuthServiceNameConVar.Value,
                        GetLocalPlayerName()
                    );
                    token = sboxSession?.AccessToken ?? "";
                    playerId = sboxSession?.PlayerId ?? 0;
                }

                if ( string.IsNullOrWhiteSpace( token ) )
                {
                    Log.Warning( "s&box achievements auth was not available; falling back to local device identity." );
                    var deviceSession = await Sandbox.Services.RentRushService.AuthenticateAchievementsBackendWithDeviceAsync(
                        AchievementsBackendUrlConVar.Value,
                        GetLocalPlayerName()
                    );
                    token = deviceSession?.AccessToken ?? "";
                    playerId = deviceSession?.PlayerId ?? 0;
                }
            }

            if ( string.IsNullOrWhiteSpace( token ) )
                return;

            AchievementsAccessTokenConVar.Value = token;
            authenticatedAchievementsPlayerId = playerId;
            AchievementServices.UseBackend( AchievementsBackendUrlConVar.Value, token, playerId );
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
        return uint.TryParse( AchievementsSteamAppIdConVar.Value, out var appId ) && appId != 0
            ? appId
            : Sandbox.Services.RentRushService.DefaultSteamAppId;
    }

    private static string GetLocalPlayerName()
    {
        var name = Connection.Local?.Name ?? "";
        return string.IsNullOrWhiteSpace( name ) ? "Player" : name;
    }

#endif

    protected override void OnUpdate()
    {
#if STANDALONE
        if ( AchievementServices.IsEnabled &&
            !string.IsNullOrWhiteSpace( AchievementsBackendUrlConVar.Value ) &&
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
