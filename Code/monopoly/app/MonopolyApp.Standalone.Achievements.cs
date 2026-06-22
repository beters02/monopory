using System;
using System.Threading.Tasks;

public partial class MonopolyApp
{

    [ConCmd( "achievements_connect_backend" )]
    private static void ConnectAchievementsBackend( Connection connection )
    {
        if ( !AchievementServices.IsEnabled )
        {
            Log.Info( "Achievements service is disabled." );
            return;
        }

        if ( !TryUseStandaloneOnlyFunction(AchievementsServiceMethodName) )
            return;

        _ = InitializeAchievementsBackendAsync( true );
        Log.Warning( "Achievements backend connection is only available in standalone builds." );
    }

    [ConCmd( "achievements_auth_diagnostics" )]
    private static void PrintAchievementsAuthDiagnostics( Connection connection )
    {

        if ( !AchievementServices.IsEnabled )
        {
            Log.Info( "Achievements service is disabled." );
            return;
        }

        if ( !TryUseStandaloneOnlyFunction(AchievementsServiceMethodName) )
            return;

        Log.Info( Sandbox.Services.RentRushService.GetDeviceAuthDiagnostics() );
    }

    private static async Task InitializeAchievementsBackendAsync( bool force = false )
    {
        if ( !AchievementServices.IsEnabled )
            return;

        if ( !TryUseStandaloneOnlyFunction(AchievementsServiceMethodName) )
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
}