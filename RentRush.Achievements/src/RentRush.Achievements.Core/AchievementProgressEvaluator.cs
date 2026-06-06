namespace RentRush.Achievements;

public static class AchievementProgressEvaluator
{
	public static AchievementProgress ApplyEvent( AchievementDefinition definition, AchievementProgress progress, AchievementEvent achievementEvent, DateTimeOffset unlockedAt )
	{
		if ( definition.Criteria is null || progress.IsUnlocked )
			return Clone( progress );

		if ( !string.Equals( definition.Criteria.EventType, achievementEvent.Type, StringComparison.OrdinalIgnoreCase ) )
			return Clone( progress );

		var target = Math.Max( definition.Criteria.Target, 1 );
		var current = Math.Clamp( CalculateNextProgress( definition, progress.Current, achievementEvent ), 0, target );
		current = Math.Max( current, progress.Current );

		return new AchievementProgress
		{
			AchievementId = progress.AchievementId,
			Current = current,
			Target = target,
			UnlockedAt = current >= target ? unlockedAt : progress.UnlockedAt
		};
	}

	public static IReadOnlyList<string> GetUnlockedCosmeticIds( IEnumerable<CosmeticDefinition> cosmetics, IEnumerable<AchievementProgress> progress )
	{
		var unlockedAchievements = progress
			.Where( achievementProgress => achievementProgress.UnlockedAt.HasValue )
			.Select( achievementProgress => achievementProgress.AchievementId )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );

		return cosmetics
			.Where( cosmetic => string.IsNullOrWhiteSpace( cosmetic.RequiredAchievementId ) || unlockedAchievements.Contains( cosmetic.RequiredAchievementId ) )
			.Select( cosmetic => cosmetic.Id )
			.ToList();
	}

	public static Dictionary<string, AchievementProgress> CreateInitialProgress( IEnumerable<AchievementDefinition> achievements )
	{
		return achievements.ToDictionary(
			definition => definition.Id,
			definition => new AchievementProgress
			{
				AchievementId = definition.Id,
				Target = Math.Max( definition.Criteria?.Target ?? 1, 1 )
			},
			StringComparer.OrdinalIgnoreCase
		);
	}

	private static int CalculateNextProgress( AchievementDefinition definition, int current, AchievementEvent achievementEvent )
	{
		return definition.Criteria.Kind switch
		{
			AchievementCriteriaKind.EventValueAtLeast when string.Equals( achievementEvent.Value, definition.Criteria.RequiredValue, StringComparison.OrdinalIgnoreCase ) => definition.Criteria.Target,
			AchievementCriteriaKind.EventValueAtLeast => current,
			_ => current + Math.Max( achievementEvent.Amount, 1 )
		};
	}

	private static AchievementProgress Clone( AchievementProgress progress )
	{
		return new AchievementProgress
		{
			AchievementId = progress.AchievementId,
			Current = progress.Current,
			Target = progress.Target,
			UnlockedAt = progress.UnlockedAt
		};
	}
}
