public sealed class LobbyPlayer
{
	public long OwnerId { get; set; }
	public string Name { get; set; } = "";
	public bool IsReady { get; set; }
	public bool IsLocal { get; set; }
	public bool IsConnected { get; set; } = true;
	public bool IsHost { get; set; }
	public bool IsAbandoned { get; set; }
	public float AbandonEndsAt { get; set; }
}
