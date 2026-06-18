using System;

public static class CosmeticCatalog
{
	public static IReadOnlyList<CosmeticDefinition> All => BuildDefinitions();

	public static CosmeticDefinition GetById( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		var normalized = NormalizeId( id );
		return All.FirstOrDefault( cosmetic => string.Equals( cosmetic.Id, normalized, StringComparison.OrdinalIgnoreCase ) );
	}

	public static string NormalizePieceCosmeticId( string pieceId )
	{
		return NormalizeId( pieceId?.StartsWith( "piece.", StringComparison.OrdinalIgnoreCase ) == true ? pieceId : $"piece.{pieceId}" );
	}

	public static string NormalizeDiceSkinCosmeticId( string diceSkinId )
	{
		return NormalizeId( diceSkinId?.StartsWith( "dice.", StringComparison.OrdinalIgnoreCase ) == true ? diceSkinId : $"dice.{diceSkinId}" );
	}

	public static string StripKindPrefix( string cosmeticId )
	{
		if ( string.IsNullOrWhiteSpace( cosmeticId ) )
			return "";

		var normalized = NormalizeId( cosmeticId );
		var dotIndex = normalized.IndexOf( '.', StringComparison.Ordinal );
		return dotIndex < 0 ? normalized : normalized[(dotIndex + 1)..];
	}

	private static IReadOnlyList<CosmeticDefinition> BuildDefinitions()
	{
		var cosmetics = new List<CosmeticDefinition>();

		foreach ( var piece in PieceCatalog.All )
		{
			cosmetics.Add( new CosmeticDefinition
			{
				Id = NormalizePieceCosmeticId( piece.Id ),
				Kind = CosmeticKind.Piece,
				Label = piece.Label,
				Description = piece.Description,
				AssetPath = piece.ModelPath,
				RequiredAchievementId = piece.RequiredAchievementId
			} );
		}

		foreach ( var diceSkin in DiceSkinCatalog.All )
		{
			cosmetics.Add( new CosmeticDefinition
			{
				Id = NormalizeDiceSkinCosmeticId( diceSkin.Id ),
				Kind = CosmeticKind.DiceSkin,
				Label = diceSkin.Label,
				Description = diceSkin.Description,
				BackgroundColor = diceSkin.BackgroundColor,
				DotColor = diceSkin.DotColor,
				RequiredAchievementId = diceSkin.RequiredAchievementId
			} );
		}

		return cosmetics;
	}

	private static string NormalizeId( string id )
	{
		return (id ?? "").Trim().ToLowerInvariant();
	}
}
