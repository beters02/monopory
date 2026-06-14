

public partial class MonopolyApp : Component
{
    private static bool achievementsBackendInitialized;
    private static bool achievementsBackendInitializationInFlight;
    private static string lastAchievementsBackendUrl = "";
    private static long authenticatedAchievementsPlayerId;
    private static string AchievementsServiceMethodName = "Achievements Service";

    private static bool TryUseStandaloneOnlyFunction( string methodName )
    {
        if ( !IsStandalone() )
        {
            Log.Warning($"{methodName} can only be used in standalone.");
            return false;
        }

        return true;
    }

    protected void AwakeStandalone()
    {
#if STANDALONE
        Standalone = true;
        InitializeSceneTraceStaticCollections();
        if ( AchievementServices.IsEnabled )
        {
            Sandbox.Services.RentRushService.TestInit();
            _ = InitializeAchievementsBackendAsync();
        }
#endif
        
    }    

    private static string GetLocalPlayerName()
    {
        var name = Connection.Local?.Name ?? "";
        return string.IsNullOrWhiteSpace( name ) ? "Player" : name;
    }

    protected void UpdateStandalone()
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
    }
}
