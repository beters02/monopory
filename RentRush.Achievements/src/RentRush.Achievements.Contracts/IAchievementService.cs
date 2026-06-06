namespace RentRush.Achievements;

public interface IAchievementService
{
	Task<PlayerAchievementState> GetMyStateAsync();
	Task<PlayerAchievementState> GetPublicStateAsync( long steamId );
	Task ReportEventAsync( AchievementEvent achievementEvent );
	Task<bool> CanUseCosmeticAsync( long steamId, string cosmeticId );
	bool CanUseCosmetic( long steamId, string cosmeticId );
	PlayerAchievementState GetCachedState( long steamId );
}

public interface ICosmeticUnlockService
{
	IReadOnlyList<CosmeticDefinition> GetAvailableCosmetics( long steamId );
	bool CanUseCosmetic( long steamId, string cosmeticId );
}

public interface ISteamAchievementBridge
{
	Task PublishUnlockedAchievementAsync( AchievementDefinition achievement );
	Task<IReadOnlySet<string>> GetUnlockedSteamAchievementApiNamesAsync();
}
