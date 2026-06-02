using System;

public sealed class DiceSkinDefinition
{
	public string Id { get; init; } = "";
	public string Label { get; init; } = "";
	public string Description { get; init; } = "";
	public string RequiredAchievementId { get; init; } = "";
	public string FaceTextureDirectory { get; init; } = "textures/dice/new";
}

public static class DiceSkinCatalog
{
	public const string DefaultDiceSkinId = "classic";

	private static readonly IReadOnlyList<DiceSkinDefinition> definitions = new List<DiceSkinDefinition>
	{
		new()
		{
			Id = DefaultDiceSkinId,
			Label = "Classic Dice",
			Description = "The default Rent Rush dice.",
			FaceTextureDirectory = "textures/dice/new"
		},
		new()
		{
			Id = "gold",
			Label = "Gold Dice",
			Description = "Unlocked by collecting properties.",
			RequiredAchievementId = AchievementIds.PropertyCollector,
			FaceTextureDirectory = "textures/dice/new"
		},
		new()
		{
			Id = "midnight",
			Label = "Midnight Dice",
			Description = "Unlocked by rolling snake eyes.",
			RequiredAchievementId = AchievementIds.SnakeEyes,
			FaceTextureDirectory = "textures/dice/new"
		}
	};

	public static IReadOnlyList<DiceSkinDefinition> All => definitions;

	public static DiceSkinDefinition GetByIdOrDefault( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return definitions[0];

		var normalized = id.Trim();
		return definitions.FirstOrDefault( definition => string.Equals( definition.Id, normalized, StringComparison.OrdinalIgnoreCase ) ) ?? definitions[0];
	}

	public static bool IsValidDiceSkinId( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return false;

		var normalized = id.Trim();
		return definitions.Any( definition => string.Equals( definition.Id, normalized, StringComparison.OrdinalIgnoreCase ) );
	}
}
