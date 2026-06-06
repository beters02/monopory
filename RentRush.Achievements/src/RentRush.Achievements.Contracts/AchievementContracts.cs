namespace RentRush.Achievements;

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
	public string SelectedPieceId { get; init; } = "";
	public string SelectedDiceSkinId { get; init; } = "";
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

public sealed class CosmeticSelectionRequest
{
	public string PieceId { get; init; } = "";
	public string DiceSkinId { get; init; } = "";
}

public sealed class DeviceSessionRequest
{
	public string DeviceId { get; init; } = "";
	public string Secret { get; init; } = "";
	public string DisplayName { get; init; } = "";
}

public sealed class DeviceSessionResponse
{
	public string AccessToken { get; init; } = "";
	public DateTimeOffset ExpiresAt { get; init; }
	public long PlayerId { get; init; }
	public string DeviceId { get; init; } = "";
}

public sealed class SboxSessionRequest
{
	public long SteamId { get; init; }
	public string Token { get; init; } = "";
	public string DisplayName { get; init; } = "";
}

public sealed class SboxSessionResponse
{
	public string AccessToken { get; init; } = "";
	public DateTimeOffset ExpiresAt { get; init; }
	public long PlayerId { get; init; }
}

public sealed class SteamworksSessionRequest
{
	public long SteamId { get; init; }
	public string Ticket { get; init; } = "";
	public string DisplayName { get; init; } = "";
}

public sealed class SteamworksSessionResponse
{
	public string AccessToken { get; init; } = "";
	public DateTimeOffset ExpiresAt { get; init; }
	public long PlayerId { get; init; }
}

public sealed class ForkboxSessionRequest
{
	public long SteamId { get; init; }
	public string Token { get; init; } = "";
	public string DisplayName { get; init; } = "";
}

public sealed class ForkboxSessionResponse
{
	public string AccessToken { get; init; } = "";
	public DateTimeOffset ExpiresAt { get; init; }
	public long PlayerId { get; init; }
}
