using System;
using System.Threading.Tasks;

public enum AchievementVisibility
{
	Public,
	Hidden
}

public enum AchievementCriteriaKind
{
	EventCount,
	EventValueAtLeast
}

public enum CosmeticKind
{
	Piece,
	DiceSkin
}

public sealed class AchievementCriteria
{
	public AchievementCriteriaKind Kind { get; init; } = AchievementCriteriaKind.EventCount;
	public string EventType { get; init; } = "";
	public int Target { get; init; } = 1;
	public string RequiredValue { get; init; } = "";
}

public sealed class AchievementDefinition
{
	public string Id { get; init; } = "";
	public string SteamApiName { get; init; } = "";
	public string Title { get; init; } = "";
	public string Description { get; init; } = "";
	public AchievementVisibility Visibility { get; init; } = AchievementVisibility.Public;
	public AchievementCriteria Criteria { get; init; } = new();
	public IReadOnlyList<string> RewardIds { get; init; } = Array.Empty<string>();
}

public sealed class AchievementProgress
{
	public string AchievementId { get; set; } = "";
	public int Current { get; set; }
	public int Target { get; set; } = 1;
	public DateTimeOffset? UnlockedAt { get; set; }
	public bool IsUnlocked => UnlockedAt.HasValue;
}

public sealed class PlayerAchievementState
{
	public long SteamId { get; init; }
	public IReadOnlyList<AchievementProgress> Achievements { get; init; } = Array.Empty<AchievementProgress>();
	public IReadOnlyList<string> UnlockedCosmeticIds { get; init; } = Array.Empty<string>();
	public string SelectedPieceId { get; init; } = PieceCatalog.DefaultPieceId;
	public string SelectedDiceSkinId { get; init; } = DiceSkinCatalog.DefaultDiceSkinId;
}

public sealed class AchievementEvent
{
	public string EventId { get; init; } = "";
	public string SourceMatchId { get; init; } = "";
	public long SteamId { get; init; }
	public string Type { get; init; } = "";
	public int Amount { get; init; } = 1;
	public string Value { get; init; } = "";
	public long OccurredAtUnixSeconds { get; init; }
}

public sealed class CosmeticDefinition
{
	public string Id { get; init; } = "";
	public CosmeticKind Kind { get; init; }
	public string Label { get; init; } = "";
	public string Description { get; init; } = "";
	public string AssetPath { get; init; } = "";
	public string RequiredAchievementId { get; init; } = "";
}

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
