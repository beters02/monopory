public static class AchievementServices
{
	private static readonly LocalAuthoritativeAchievementService localService = new( new NoOpSteamAchievementBridge() );

	public static IAchievementService Achievements { get; private set; } = localService;
	public static ICosmeticUnlockService Cosmetics { get; private set; } = localService;

	public static void Use( IAchievementService achievements, ICosmeticUnlockService cosmetics )
	{
		if ( achievements is not null )
			Achievements = achievements;

		if ( cosmetics is not null )
			Cosmetics = cosmetics;
	}

	public static void UseBackend( string baseUrl, string bearerToken, long playerId = 0 )
	{
#if STANDALONE
		var backend = new HttpAchievementService( baseUrl, bearerToken, playerId );
		Use( backend, backend );
#else
		Log.Warning( "HTTP achievements backend is only available in standalone builds. Using the local achievement service." );
#endif
	}
}
