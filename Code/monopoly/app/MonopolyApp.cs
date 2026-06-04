using System;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

public class MonopolyApp : Component
{

    private class SecretsJsonObject
    {
        public string WebApiKey { get; set; }
        public string AppId { get; set; }
        public string BackendUrl { get; set; }
        public bool UseSecretsAppId { get; set; }
        
    }

    public static bool IsStandalone = false;
    public static bool IsDebugEnabled = false;
    public static bool GameLaunchedWithDebugConvar = false;

    public static string SecretsFileName = "secrets.json";
    public static string SecretsDirectoryPath = "C:\\Users\\Bryce\\Documents\\s&box projects priv\\MonoporySecrets";

    [ConVar( "achievements_backend_url" )]
    public static string AchievementsBackendUrl { get; set; } = "";

    [ConVar( "achievements_access_token" )]
    public static string AchievementsAccessToken { get; set; } = "";

    [ConVar( "achievements_auth_service_name" )]
    public static string AchievementsAuthServiceName { get; set; } = Sandbox.Services.RentRushService.DefaultSboxAuthServiceName;

    [ConVar( "achievements_steamworks_auth_enabled" )]
    public static bool AchievementsSteamworksAuthEnabled { get; set; } = true;

    // DONT USE THIS - GET APP ID FROM APPLICATION
    [ConVar( "achievements_steam_app_id" )]
    public static string AchievementsSteamAppId { get; set; } = Sandbox.Services.RentRushService.DefaultSteamAppId.ToString();
    
    // DONT USE THIS - USE NULL
    [ConVar( "achievements_steamworks_ticket_identity" )]
    public static string AchievementsSteamworksTicketIdentity { get; set; } = Sandbox.Services.RentRushService.DefaultSteamworksTicketIdentity;

//do if standalone
    private static bool achievementsBackendInitialized;
    private static bool achievementsBackendInitializationInFlight;
    private static string lastAchievementsBackendUrl = "";
    private static long authenticatedAchievementsPlayerId;
//do endif

	protected override void OnAwake()
	{
//do if standalone
        IsStandalone = true;
        Sandbox.Services.RentRushService.TestInit();
        Log.Info(Application.AppId);
//do endif

        var debugConvarParsed = bool.TryParse(ConsoleSystem.GetValue( "debug" ), out bool debugConvar);
        GameLaunchedWithDebugConvar = debugConvar;
		if (debugConvarParsed && debugConvar)
			IsDebugEnabled = true;

        Log.Info($"IsStandalone: {IsStandalone}");

//do if standalone
        _ = InitializeAchievementsBackendAsync();
//do endif
	}

    [ConCmd( "achievements_connect_backend" )]
    private static void ConnectAchievementsBackend( Connection connection )
    {
//do if standalone
        _ = InitializeAchievementsBackendAsync( true );
//do else
/*
        Log.Warning( "Achievements backend connection is only available in standalone builds." );
*/
//do endif
    }

    [ConCmd( "achievements_auth_diagnostics" )]
    private static void PrintAchievementsAuthDiagnostics( Connection connection )
    {
//do if standalone
        Log.Info( Sandbox.Services.RentRushService.GetDeviceAuthDiagnostics() );
//do else
/*
        Log.Warning( "Achievements auth diagnostics are only available in standalone builds." );
*/
//do endif
    }

    private static string GetAppId(SecretsJsonObject secrets)
    {
        if (secrets.AppId is null || !secrets.UseSecretsAppId)
            return Application.AppId.ToString();
        
        return secrets.AppId;
    }

    [ConCmd( "achment_debug" )]
    private static void RunAchievementsDebug( Connection connection )
    {
        Log.Info(Application.AppId);
        string path = SecretsDirectoryPath + "\\" + SecretsFileName;
        if ( !File.Exists(path) )
        {
            Log.Warning( $"Secrets not found at: {path}" );
            return;
        }

        // 1. Read the full text of the file
        string jsonString = File.ReadAllText(path);
        Log.Info(jsonString);

        // 2. Parse (deserialize) the text into your C# object
        SecretsJsonObject secrets = JsonSerializer.Deserialize<SecretsJsonObject>(jsonString);
        Log.Info(secrets.BackendUrl);
        Log.Info("ya");

        string appId = GetAppId(secrets);
        Log.Info(appId);
        string backendUrl = secrets.BackendUrl;

        AchievementsSteamAppId = appId;
        Log.Info($"Set app id to: {appId}");
        
        AchievementsBackendUrl = backendUrl;
        Log.Info($"Set backend url to: {backendUrl}");

        Log.Info("Running diagnostics.");
        ConsoleSystem.Run("achievements_auth_diagnostics");

        Log.Info("Connecting to backend.");
        ConsoleSystem.Run("achievements_connect_backend");
    }

//do if standalone
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

//do endif


    protected override void OnUpdate()
    {
//do if standalone
        if ( !string.IsNullOrWhiteSpace( AchievementsBackendUrl ) &&
            !achievementsBackendInitialized &&
            !achievementsBackendInitializationInFlight )
        {
            _ = InitializeAchievementsBackendAsync();
        }
//do endif

        if ( Scene.Camera is null )
            return;

        var hud = Scene.Camera.Hud;
        hud.DrawText( new TextRendering.Scope( "Hello!", Color.Red, 32 ), Screen.Width * 0.5f );
    }
}
