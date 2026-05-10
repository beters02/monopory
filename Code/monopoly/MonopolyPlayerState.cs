using System;
using Sandbox;

public sealed class MonopolyPlayerState : Component
{
	[Property, Sync] public long OwnerId {get; set;}

	[Property, Sync] public int SpaceIndex { get; set; } = 0;
	[Property, Sync] public int Money { get; set; } = 1500;
	[Property, Sync] public bool IsInJail { get; set; }

	[Property, Sync] public string PlayerName { get; set; } = "Player";

	public bool IsOwner => OwnerId == Connection.Local.SteamId;
}