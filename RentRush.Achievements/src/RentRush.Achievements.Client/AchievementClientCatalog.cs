namespace RentRush.Achievements;

public sealed class AchievementClientCatalog
{
	public static AchievementClientCatalog Empty { get; } = new( [], [] );

	public AchievementClientCatalog( IReadOnlyList<AchievementDefinition> achievements, IReadOnlyList<CosmeticDefinition> cosmetics )
	{
		Achievements = achievements ?? [];
		Cosmetics = cosmetics ?? [];
	}

	public IReadOnlyList<AchievementDefinition> Achievements { get; }
	public IReadOnlyList<CosmeticDefinition> Cosmetics { get; }

	public CosmeticDefinition? GetCosmeticById( string? id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		var normalized = NormalizeCosmeticId( id );
		return Cosmetics.FirstOrDefault( cosmetic => string.Equals( NormalizeCosmeticId( cosmetic.Id ), normalized, StringComparison.OrdinalIgnoreCase ) );
	}

	public static string NormalizeCosmeticId( string? id )
	{
		return (id ?? "").Trim().ToLowerInvariant();
	}
}
