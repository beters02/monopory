public static class AchievementServices
{
	private static readonly LocalAuthoritativeAchievementService localService = new( new SteamworksAchievementBridge() );

	public static IAchievementService Achievements { get; private set; } = localService;
	public static ICosmeticUnlockService Cosmetics { get; private set; } = localService;

	public static void Use( IAchievementService achievements, ICosmeticUnlockService cosmetics )
	{
		Achievements = achievements ?? localService;
		Cosmetics = cosmetics ?? localService;
	}

	public static void UseLocal()
	{
		Use( localService, localService );
	}

	public static void Shutdown()
	{
		UseLocal();
	}
}
