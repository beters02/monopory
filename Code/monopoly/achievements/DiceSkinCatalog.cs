using System;

public sealed class DiceSkinMaterial
{
	public GameImage Metallic;
	public GameImage Roughness;

	private DiceSkinMaterial( GameImage metal = null, GameImage rough = null )
	{
		Metallic = metal;
		Roughness = rough;
	}

	public static readonly DiceSkinMaterial Default = new();
	public static readonly DiceSkinMaterial ShinyMetal = new( GameAssets.Textures.DiceMaterialTexture_MetalShiny_Metallic, GameAssets.Textures.DiceMaterialTexture_MetalShiny_Roughness );
}

public sealed class DiceSkinDefinition
{
	public string Id { get; init; } = "";
	public string Label { get; init; } = "";
	public string Description { get; init; } = "";
	public string RequiredAchievementId { get; init; } = "";
	public ParticleGradient BackgroundColor { get; init; } = Color.White;
	public ParticleGradient DotColor { get; init; } = Color.Black;
	public DiceSkinMaterial SkinMaterial { get; init; } = DiceSkinMaterial.Default;
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
		},
		new()
		{
			Id = "gold",
			Label = "Gold Dice",
			Description = "Unlocked by collecting properties.",
			BackgroundColor = MonopolyTheme.MonopolyGoldColor,
			SkinMaterial = DiceSkinMaterial.ShinyMetal,
		},
		new()
		{
			Id = "midnight",
			Label = "Midnight Dice",
			Description = "Unlocked by rolling snake eyes.",
			BackgroundColor = ColorUtils.FromHex("#2E1A47"),
			DotColor = ColorUtils.FromHex("#00E5FF ")
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
