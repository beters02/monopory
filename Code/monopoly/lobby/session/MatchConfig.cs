using Sandbox;
using System;

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

[AttributeUsage( AttributeTargets.Property )]
public sealed class MatchConfigOptionAttribute : Attribute
{
	public string Group { get; }
	public string Label { get; }
	public string Description { get; set; } = "";
	public int Order { get; set; }
	public int Min { get; set; } = int.MinValue;
	public int Max { get; set; } = int.MaxValue;
	public int Step { get; set; } = 1;

	public MatchConfigOptionAttribute( string group, string label )
	{
		Group = group;
		Label = label;
	}
}

public sealed class MatchConfig
{
	[MatchConfigOption( "Lobby", "Min Players", Description = "Minimum ready players required before the host can start.", Order = 0, Min = 1, Max = 6, Step = 1 )]
	public int MinPlayers { get; set; } = 1;

	[MatchConfigOption( "Lobby", "Max Players", Description = "Maximum seats allowed in the hosted lobby.", Order = 1, Min = 1, Max = 12, Step = 1 )]
	public int MaxPlayers { get; set; } = 12;

	[MatchConfigOption( "Lobby", "Only Host Starts Game", Description = "If enabled, only the host can launch the match.", Order = 2 )]
	public bool OnlyHostStartsGame { get; set; } = true;

	[MatchConfigOption( "Lobby", "Abandon Timeout Seconds", Description = "How long disconnected players can rejoin before they are abandoned and removed.", Order = 3, Min = 15, Max = 1800, Step = 15 )]
	public int AbandonTimeoutSeconds { get; set; } = 180;

	[MatchConfigOption( "Property Rules", "Affordable Unowned Landing", Description = "What happens when a player can afford an unowned property.", Order = 10 )]
	public UnownedAffordableLandingMode LandedUnownedCanAffordMode { get; set; } = UnownedAffordableLandingMode.Decision;

	[MatchConfigOption( "Property Rules", "Unaffordable Unowned Landing", Description = "What happens when a player cannot afford an unowned property.", Order = 11 )]
	public UnownedUnaffordableLandingMode LandedUnownedCantAffordMode { get; set; } = UnownedUnaffordableLandingMode.ForceAuction;

	[MatchConfigOption( "Property Rules", "Can Skip Unowned", Description = "Allows players to ignore an unowned property instead of buying or auctioning it.", Order = 12 )]
	public bool CanSkipUnowned { get; set; } = false;

	[MatchConfigOption( "Economy", "Starting Money", Description = "Cash each player begins the game with.", Order = 20, Min = 0, Max = 10000, Step = 100 )]
	public int StartingMoney { get; set; } = 1500;

	[MatchConfigOption( "Economy", "Land On GO Additional Money", Description = "Additional bonus paid on top of Pass GO Money when a move ends on GO.", Order = 21, Min = 0, Max = 5000, Step = 50 )]
	public int LandOnGoMoney { get; set; } = 100;

	[MatchConfigOption( "Economy", "Pass GO Money", Description = "Bonus for passing GO during movement.", Order = 22, Min = 0, Max = 5000, Step = 50 )]
	public int PassGoMoney { get; set; } = 200;

	[MatchConfigOption( "Economy", "Snake Eyes Bonus Money", Description = "Bonus awarded when a player rolls snake eyes.", Order = 23, Min = 0, Max = 5000, Step = 50 )]
	public int SnakeEyesBonusMoney { get; set; } = 0;

	[MatchConfigOption( "Turn Rules", "Doubles Goes Again", Description = "Lets players take another turn after rolling doubles.", Order = 30 )]
	public bool DoublesGoesAgain { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Randomize Turn Order", Description = "Shuffles the starting player order at match start.", Order = 31 )]
	public bool RandomizeTurnOrder { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Force Jail Fine After Failed Doubles", Description = "After the final failed jail roll, automatically pay the fine to leave jail.", Order = 32 )]
	public bool ForceJailFineAfterFailedDoubles { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Turn Time Limit Seconds", Description = "How long each turn can last before timeout handling kicks in.", Order = 33, Min = 15, Max = 900, Step = 15 )]
	public int TurnTimeLimitSeconds { get; set; } = 180;

	[MatchConfigOption( "Board Rules", "Vacation Cash", Description = "Awards pooled cash when landing on Free Parking, if enabled.", Order = 40 )]
	public bool VacationCash { get; set; } = true;

	[MatchConfigOption( "Board Rules", "No Rent While In Prison", Description = "Prevents jailed players from collecting rent.", Order = 41 )]
	public bool DontCollectRentWhileInPrison { get; set; } = false;

	[MatchConfigOption( "Board Rules", "Even Build", Description = "Requires houses to be built evenly across a color set.", Order = 42 )]
	public bool EvenBuild { get; set; } = true;

}

