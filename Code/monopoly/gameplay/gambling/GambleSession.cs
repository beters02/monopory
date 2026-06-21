using System;
using System.Text.Json;

public enum GamblePresentationMode
{
	TwoDimensional,
	ThreeDimensional
}

public enum CoinFlipSide
{
	Heads,
	Tails
}

public sealed class GambleSession
{
	public int Id { get; set; }
	public int StationId { get; set; }
	public int PlayerIndex { get; set; } = -1;
	public GambleType GameType { get; set; } = GambleType.CoinFlip;
	public GamblePresentationMode Presentation { get; set; } = GamblePresentationMode.ThreeDimensional;
	public bool IsCardGame { get; set; }
	public CoinFlipSide ChosenSide { get; set; }
	public CoinFlipSide OutcomeSide { get; set; }
	public int Wager { get; set; }
	public float StartedAt { get; set; }
	public float RevealAt { get; set; }
	public float EndsAt { get; set; }
	public bool IsResolved { get; set; }
	public bool Won { get; set; }
	public string Title { get; set; } = "Coin Flip";
	public string Description { get; set; } = "";
	public string ResultMessage { get; set; } = "";
}

public static class GambleSessionSerializer
{
	public static string Serialize( GambleSession session ) => JsonSerializer.Serialize( session );

	public static GambleSession Deserialize( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return null;

		try
		{
			return JsonSerializer.Deserialize<GambleSession>( value );
		}
		catch
		{
			return null;
		}
	}
}
