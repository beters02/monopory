public enum MonopolyPopupKind
{
	Info,
	Success,
	Warning,
	Danger
}

public sealed class MonopolyPopup
{
	public int Id { get; set; }
	public string Title { get; set; } = "";
	public string Message { get; set; } = "";
	public MonopolyPopupKind Kind { get; set; } = MonopolyPopupKind.Info;
	public bool CanDismiss { get; set; } = true;
	public float Lifetime { get; set; } = 5f;
	public bool SoundEnabled { get; set; } = true;
}
