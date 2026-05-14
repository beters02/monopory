using Sandbox;

public enum MonopolyUnownedLandingMode
{
	ForceAuction,
	SkipOrAuction,
	ForceBuyIfPossible
}

public sealed class MonopolyGameConfig
{
	[Property] public MonopolyUnownedLandingMode LandedUnownedMode { get; set; } = MonopolyUnownedLandingMode.SkipOrAuction;
	[Property] public int StartingMoney { get; set; } = 1500;
	[Property] public bool DoublesGoesAgain { get; set; } = false;
	[Property] public bool VacationCash { get; set; } = false;
	[Property] public bool DontCollectRentWhileInPrison { get; set; } = false;
	[Property] public bool EvenBuild { get; set; } = true;
}
