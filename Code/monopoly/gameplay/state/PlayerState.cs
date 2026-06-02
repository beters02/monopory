using System;
using Sandbox;

public sealed class PlayerState : Component
{
	[Sync] public long OwnerId {get; set;}
	[Sync] public long SteamId { get; set; }

	[Sync] public int SpaceIndex { get; set; } = 0;
	[Sync] public int Money { get; set; } = 1500;
	[Sync] public bool IsInJail { get; set; }
	[Sync] public int JailTurnsRemaining { get; set; }
	[Sync] public int ChanceGetOutOfJailFreeCards { get; set; }
	[Sync] public int CommunityChestGetOutOfJailFreeCards { get; set; }
	[Sync] public int ConsecutiveDoubles { get; set; }
	[Sync] public bool SkipsNextTurn { get; set; }
	[Sync] public bool IsBankrupt { get; set; }
	[Sync] public bool IsReady { get; set; }
	[Sync] public bool IsDisconnected { get; set; }
	[Sync] public float AbandonEndsAt { get; set; }
	[Sync] public int TurnTimeoutCount { get; set; }

	[Sync] public string PlayerName { get; set; } = "Player";
	[Sync] public int ColorSlot { get; set; } = -1;
	[Sync] public string SelectedPieceId { get; set; } = PieceCatalog.DefaultPieceId;
	[Sync] public string SelectedDiceSkinId { get; set; } = DiceSkinCatalog.DefaultDiceSkinId;

	public bool IsOwner => OwnerId == Connection.Local.SteamId;

	public bool IsAssigned => OwnerId != 0;
}
