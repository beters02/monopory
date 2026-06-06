using System;
using System.Collections.Generic;

public static class AchievementIds
{
	public const string FirstWin = "first_win";
	public const string PropertyCollector = "property_collector";
	public const string DoublesTrouble = "doubles_trouble";
	public const string SnakeEyes = "snake_eyes";
	public const string Jailbird = "jailbird";
}

public static class AchievementEventTypes
{
	public const string MatchStarted = "match_started";
	public const string MatchCompleted = "match_completed";
	public const string MatchWon = "match_won";
	public const string WentBankrupt = "went_bankrupt";
	public const string BankruptedOpponent = "bankrupted_opponent";
	public const string PropertyAcquired = "property_acquired";
	public const string OwnedSetCompleted = "owned_set_completed";
	public const string SentToJail = "sent_to_jail";
	public const string RolledDoubles = "rolled_doubles";
	public const string RolledSnakeEyes = "rolled_snake_eyes";
}

public sealed class SharedAchievementDefinition
{
	public string Id { get; init; } = "";
	public string SteamApiName { get; init; } = "";
	public string Title { get; init; } = "";
	public string Description { get; init; } = "";
	public string EventType { get; init; } = "";
	public int Target { get; init; } = 1;
	public IReadOnlyList<string> RewardIds { get; init; } = Array.Empty<string>();
}

public static class SharedAchievementCatalog
{
	public static readonly IReadOnlyList<SharedAchievementDefinition> All =
	[
		new()
		{
			Id = AchievementIds.FirstWin,
			SteamApiName = "ACH_FIRST_WIN",
			Title = "First Victory",
			Description = "Win your first match.",
			EventType = AchievementEventTypes.MatchWon,
			Target = 1,
			RewardIds = ["piece.detective_man"]
		},
		new()
		{
			Id = AchievementIds.PropertyCollector,
			SteamApiName = "ACH_PROPERTY_COLLECTOR",
			Title = "Property Collector",
			Description = "Acquire 10 properties across matches.",
			EventType = AchievementEventTypes.PropertyAcquired,
			Target = 10,
			RewardIds = ["dice.gold"]
		},
		new()
		{
			Id = AchievementIds.DoublesTrouble,
			SteamApiName = "ACH_DOUBLES_TROUBLE",
			Title = "Doubles Trouble",
			Description = "Roll doubles 10 times.",
			EventType = AchievementEventTypes.RolledDoubles,
			Target = 10
		},
		new()
		{
			Id = AchievementIds.SnakeEyes,
			SteamApiName = "ACH_SNAKE_EYES",
			Title = "Snake Eyes",
			Description = "Roll double ones.",
			EventType = AchievementEventTypes.RolledSnakeEyes,
			Target = 1,
			RewardIds = ["dice.midnight"]
		},
		new()
		{
			Id = AchievementIds.Jailbird,
			SteamApiName = "ACH_JAILBIRD",
			Title = "Jailbird",
			Description = "Get sent to jail 5 times.",
			EventType = AchievementEventTypes.SentToJail,
			Target = 5
		}
	];

	public static SharedAchievementDefinition GetById( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		for ( var i = 0; i < All.Count; i++ )
		{
			if ( string.Equals( All[i].Id, id.Trim(), StringComparison.OrdinalIgnoreCase ) )
				return All[i];
		}

		return null;
	}
}
