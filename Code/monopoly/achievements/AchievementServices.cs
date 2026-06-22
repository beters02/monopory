public static class AchievementServices
{
	public static bool IsEnabled => false;

	private static readonly DisabledAchievementService disabledService = new();
	private static readonly LocalAuthoritativeAchievementService localService = new( new NoOpSteamAchievementBridge() );

	public static IAchievementService Achievements { get; private set; } = disabledService;
	public static ICosmeticUnlockService Cosmetics { get; private set; } = disabledService;

	public static void Use( IAchievementService achievements, ICosmeticUnlockService cosmetics )
	{
		if ( !IsEnabled )
			return;

		if ( achievements is not null )
			Achievements = achievements;

		if ( cosmetics is not null )
			Cosmetics = cosmetics;
	}

	public static void UseBackend( string baseUrl, string bearerToken, long playerId = 0 )
	{
		if ( !IsEnabled )
			return;

#if STANDALONE
		var backend = new HttpAchievementService( baseUrl, bearerToken, playerId );
		Use( backend, backend );
#else
		Log.Warning( "HTTP achievements backend is only available in standalone builds. Using the local achievement service." );
#endif
	}
}
