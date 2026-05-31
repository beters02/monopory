public enum CardDeck
{
	Chance,
	CommunityChest
}

public enum GambleType
{
	CoinFlip
}

public enum CardAction
{
	CollectFromBank,
	PayBank,
	MoveToSpace,
	MoveToNearestRailroad,
	MoveToNearestUtility,
	MoveRelative,
	GoToJail,
	GetOutOfJailFree,
	CollectFromEachPlayer,
	PayEachPlayer,
	PayPerImprovement,
	Gamble
}

public static class TradableCardIds
{
	public const string ChanceGetOutOfJailFree = "chance_get_out_jail";
	public const string CommunityChestGetOutOfJailFree = "chest_get_out_jail";
}

public sealed class CardDef
{
	public string Key { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public CardDeck Deck { get; set; }
	public CardAction Action { get; set; }
	public int Weight { get; set; } = 1;
	public int Amount { get; set; }
	public int TargetSpaceIndex { get; set; } = -1;
	public int RelativeSpaces { get; set; }
	public bool CollectGo { get; set; } = true;
	public bool ResolveDestination { get; set; } = true;
	public int HouseAmount { get; set; }
	public int HotelAmount { get; set; }
}
