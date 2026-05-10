using Sandbox;

public enum SpaceType
{
	Go,
	Property,
	Railroad,
	Utility,
	Chance,
	CommunityChest,
	Tax,
	Jail,
	FreeParking,
	GoToJail
}

public sealed class MonopolySpace : Component
{
	[Property] public int Index { get; set; }
	[Property] public string DisplayName { get; set; } = "";
	[Property] public SpaceType Type { get; set; }

	public Vector3 TokenPosition => GameObject.WorldPosition;
}