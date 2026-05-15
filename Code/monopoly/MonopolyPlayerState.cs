using System;
using Sandbox;

public sealed class MonopolyPlayerState : Component
{
	[Property, Sync] public long OwnerId {get; set;}

	[Property, Sync] public int SpaceIndex { get; set; } = 0;
	[Property, Sync] public int Money { get; set; } = 1500;
	[Property, Sync] public bool IsInJail { get; set; }
	[Property, Sync] public int ConsecutiveDoubles { get; set; }
	[Property, Sync] public bool SkipsNextTurn { get; set; }
	[Property, Sync] public bool IsBankrupt { get; set; }
	[Property, Sync] public bool IsReady { get; set; }

	[Property, Sync] public string PlayerName { get; set; } = "Player";

	public bool IsOwner => OwnerId == Connection.Local.SteamId;

	public bool IsAssigned => OwnerId != 0;
}
