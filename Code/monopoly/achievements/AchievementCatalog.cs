using System;

public static class AchievementIds
{
	public const string FirstWin = "first_win";
	public const string PropertyCollector = "property_collector";
	public const string DoublesTrouble = "doubles_trouble";
	public const string SnakeEyes = "snake_eyes";
	public const string Jailbird = "jailbird";
}

public static class AchievementCatalog
{
	private static readonly IReadOnlyList<AchievementDefinition> definitions = new List<AchievementDefinition>
	{
		new()
		{
			Id = AchievementIds.FirstWin,
			SteamApiName = "ACH_FIRST_WIN",
			Title = "First Victory",
			Description = "Win your first match.",
			Criteria = new AchievementCriteria
			{
				Kind = AchievementCriteriaKind.EventCount,
				EventType = AchievementEventTypes.MatchWon,
				Target = 1
			},
			RewardIds = new[] { "piece.detective_man" }
		},
		new()
		{
			Id = AchievementIds.PropertyCollector,
			SteamApiName = "ACH_PROPERTY_COLLECTOR",
			Title = "Property Collector",
			Description = "Acquire 10 properties across matches.",
			Criteria = new AchievementCriteria
			{
				Kind = AchievementCriteriaKind.EventCount,
				EventType = AchievementEventTypes.PropertyAcquired,
				Target = 10
			},
			RewardIds = new[] { "dice.gold" }
		},
		new()
		{
			Id = AchievementIds.DoublesTrouble,
			SteamApiName = "ACH_DOUBLES_TROUBLE",
			Title = "Doubles Trouble",
			Description = "Roll doubles 10 times.",
			Criteria = new AchievementCriteria
			{
				Kind = AchievementCriteriaKind.EventCount,
				EventType = AchievementEventTypes.RolledDoubles,
				Target = 10
			}
		},
		new()
		{
			Id = AchievementIds.SnakeEyes,
			SteamApiName = "ACH_SNAKE_EYES",
			Title = "Snake Eyes",
			Description = "Roll double ones.",
			Criteria = new AchievementCriteria
			{
				Kind = AchievementCriteriaKind.EventCount,
				EventType = AchievementEventTypes.RolledSnakeEyes,
				Target = 1
			},
			RewardIds = new[] { "dice.midnight" }
		},
		new()
		{
			Id = AchievementIds.Jailbird,
			SteamApiName = "ACH_JAILBIRD",
			Title = "Jailbird",
			Description = "Get sent to jail 5 times.",
			Criteria = new AchievementCriteria
			{
				Kind = AchievementCriteriaKind.EventCount,
				EventType = AchievementEventTypes.SentToJail,
				Target = 5
			}
		}
	};

	public static IReadOnlyList<AchievementDefinition> All => definitions;

	public static AchievementDefinition GetById( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		return definitions.FirstOrDefault( definition => string.Equals( definition.Id, id.Trim(), StringComparison.OrdinalIgnoreCase ) );
	}
}
