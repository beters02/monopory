using System;
using Sandbox;

public sealed class MonopolyPlayerState : Component
{
	[Property, Sync] public Guid OwnerId {get; set;}

	[Property, Sync] public int SpaceIndex { get; set; } = 0;
	[Property, Sync] public int Money { get; set; } = 1500;
	[Property, Sync] public bool IsInJail { get; set; }

	public bool IsOwner => OwnerId == Connection.Local.Id;
}