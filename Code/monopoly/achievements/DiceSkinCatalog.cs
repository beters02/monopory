using System;

public sealed class DiceSkinDefinition
{
	public string Id { get; init; } = "";
	public string Label { get; init; } = "";
	public string Description { get; init; } = "";
	public string RequiredAchievementId { get; init; } = "";
	public string BodyMaterialPath { get; init; } = "";
	public ParticleGradient BackgroundColor { get; init; } = Color.White;
	public ParticleGradient DotColor { get; init; } = Color.Black;
}

public static class DiceSkinCatalog
{

	public const string DefaultDiceSkinId = "classic";
	public static IReadOnlyList<DiceSkinDefinition> cache;

	private static IReadOnlyList<DiceSkinDefinition> BuildDefinitions() =>
	[
		new()
		{
			Id = DefaultDiceSkinId,
			Label = "Classic Dice",
			Description = "The default Rent Rush dice.",
		},
		new()
		{
			Id = "gold",
			Label = "Gold NINJA Dice",
			Description = "Unlocked by collecting properties.",
			BodyMaterialPath = GameAssets.Materials.Shiny.Path,
			BackgroundColor = MonopolyTheme.MonopolyGoldColor
		},
		new()
		{
			Id = "midnight",
			Label = "Midnight Dice",
			Description = "Unlocked by rolling snake eyes.",
			BackgroundColor = ColorUtils.FromHex("#2E1A47"),
			DotColor = ColorUtils.FromHex("#00E5FF")
		}
	];

	private static IReadOnlyList<DiceSkinDefinition> BuildDefinitionsCache()
	{
		return cache ?? BuildDefinitions();
	}

	public static IReadOnlyList<DiceSkinDefinition> All => BuildDefinitionsCache();

	public static void ReloadAll()
	{
		cache = null;
	}

	public static DiceSkinDefinition GetByIdOrDefault( string id )
	{
		var all = All;
		if ( string.IsNullOrWhiteSpace( id ) )
			return all[0];

		var normalized = id.Trim();
		return all.FirstOrDefault( definition => string.Equals( definition.Id, normalized, StringComparison.OrdinalIgnoreCase ) ) ?? all[0];
	}

	public static bool IsValidDiceSkinId( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return false;

		var normalized = id.Trim();
		return All.Any( definition => string.Equals( definition.Id, normalized, StringComparison.OrdinalIgnoreCase ) );
	}
}