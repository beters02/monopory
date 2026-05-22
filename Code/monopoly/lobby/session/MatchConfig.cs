using Sandbox;

public enum UnownedAffordableLandingMode
{
	ForceBuy,
	Decision
}

public enum UnownedUnaffordableLandingMode
{
	ForceAuction,
	Decision
}

public sealed class MatchConfig
{
	[Property] public int MinPlayers { get; set; } = 1;
	[Property] public int MaxPlayers { get; set; } = 6;
	[Property] public bool OnlyHostStartsGame { get; set; } = true;
	[Property] public UnownedAffordableLandingMode LandedUnownedCanAffordMode { get; set; } = UnownedAffordableLandingMode.Decision;
	[Property] public UnownedUnaffordableLandingMode LandedUnownedCantAffordMode { get; set; } = UnownedUnaffordableLandingMode.ForceAuction;
	[Property] public bool CanSkipUnowned { get; set; } = false;
	[Property] public int StartingMoney { get; set; } = 1500;
	[Property] public int LandOnGoMoney { get; set; } = 200;
	[Property] public int PassGoMoney { get; set; } = 200;
	[Property] public bool DoublesGoesAgain { get; set; } = false;
	[Property] public bool ForceJailFineAfterFailedDoubles { get; set; } = true;
	[Property] public bool VacationCash { get; set; } = false;
	[Property] public bool DontCollectRentWhileInPrison { get; set; } = false;
	[Property] public bool EvenBuild { get; set; } = true;
	[Property] public int TurnTimeLimitSeconds { get; set; } = 180;
}
