using Sandbox;

public enum MonopolyUnownedLandingMode
{
	ForceAuction,
	SkipOrAuction,
	ForceBuyIfPossible
}

public sealed class MonopolyGameConfig
{
	[Property] public int MinPlayers { get; set; } = 1;
	[Property] public int MaxPlayers { get; set; } = 6;
	[Property] public bool OnlyHostStartsGame { get; set; } = true;
	[Property] public MonopolyUnownedLandingMode LandedUnownedMode { get; set; } = MonopolyUnownedLandingMode.SkipOrAuction;
	[Property] public int StartingMoney { get; set; } = 1500;
	[Property] public bool DoublesGoesAgain { get; set; } = false;
	[Property] public bool VacationCash { get; set; } = false;
	[Property] public bool DontCollectRentWhileInPrison { get; set; } = false;
	[Property] public bool EvenBuild { get; set; } = true;
	[Property] public int TurnTimeLimitSeconds { get; set; } = 180;
}
