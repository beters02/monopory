public enum MonopolyCardDeck
{
	Chance,
	CommunityChest
}

public enum MonopolyCardAction
{
	CollectFromBank,
	PayBank,
	MoveToSpace,
	MoveRelative,
	GoToJail,
	CollectFromEachPlayer,
	PayEachPlayer,
	PayPerImprovement
}

public sealed class MonopolyCardDef
{
	public string Key { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public MonopolyCardDeck Deck { get; set; }
	public MonopolyCardAction Action { get; set; }
	public int Amount { get; set; }
	public int TargetSpaceIndex { get; set; } = -1;
	public int RelativeSpaces { get; set; }
	public bool CollectGo { get; set; } = true;
	public bool ResolveDestination { get; set; } = true;
	public int HouseAmount { get; set; }
	public int HotelAmount { get; set; }
}
