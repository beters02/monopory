using System;

public static class AchievementCatalog
{
	private static readonly IReadOnlyList<AchievementDefinition> definitions = SharedAchievementCatalog.All
		.Select( definition => new AchievementDefinition
		{
			Id = definition.Id,
			SteamApiName = definition.SteamApiName,
			Title = definition.Title,
			Description = definition.Description,
			Criteria = new AchievementCriteria
			{
				Kind = AchievementCriteriaKind.EventCount,
				EventType = definition.EventType,
				Target = definition.Target
			},
			RewardIds = definition.RewardIds
		} )
		.ToList();

	public static IReadOnlyList<AchievementDefinition> All => definitions;

	public static AchievementDefinition GetById( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		return definitions.FirstOrDefault( definition => string.Equals( definition.Id, id.Trim(), StringComparison.OrdinalIgnoreCase ) );
	}
}
