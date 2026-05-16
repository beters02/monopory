public sealed class MonopolyLobbyPlayer
{
	public long OwnerId { get; set; }
	public string Name { get; set; } = "";
	public bool IsReady { get; set; }
	public bool IsLocal { get; set; }
}
