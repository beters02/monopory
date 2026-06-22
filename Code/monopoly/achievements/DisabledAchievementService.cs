using System;
using System.Threading.Tasks;

public sealed class DisabledAchievementService : IAchievementService, ICosmeticUnlockService
{
	public Task<PlayerAchievementState> GetMyStateAsync()
	{
		return Task.FromResult( GetCachedState( GetLocalSteamId() ?? 0 ) );
	}

	public Task<PlayerAchievementState> GetPublicStateAsync( long steamId )
	{
		return Task.FromResult( GetCachedState( steamId ) );
	}

	public Task ReportEventAsync( AchievementEvent achievementEvent )
	{
		return Task.CompletedTask;
	}

	public Task<bool> CanUseCosmeticAsync( long steamId, string cosmeticId )
	{
		return Task.FromResult( CanUseCosmetic( steamId, cosmeticId ) );
	}

	public bool CanUseCosmetic( long steamId, string cosmeticId )
	{
		return CosmeticCatalog.GetById( cosmeticId ) is not null;
	}

	public IReadOnlyList<CosmeticDefinition> GetAvailableCosmetics( long steamId )
	{
		return CosmeticCatalog.All.ToList();
	}

	public PlayerAchievementState GetCachedState( long steamId )
	{
		return new PlayerAchievementState
		{
			SteamId = steamId,
			Achievements = AchievementCatalog.All.Select( definition => new AchievementProgress
			{
				AchievementId = definition.Id,
				Current = 0,
				Target = Math.Max( definition.Criteria?.Target ?? 1, 1 )
			} ).ToList(),
			UnlockedCosmeticIds = CosmeticCatalog.All
				.Select( cosmetic => cosmetic.Id )
				.ToList(),
			SelectedPieceId = PieceCatalog.DefaultPieceId,
			SelectedDiceSkinId = DiceSkinCatalog.DefaultDiceSkinId
		};
	}

	private static long? GetLocalSteamId()
	{
		try
		{
			return Connection.Local?.SteamId;
		}
		catch
		{
			return null;
		}
	}
}
