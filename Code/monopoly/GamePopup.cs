public enum PopupKind
{
	Info,
	Success,
	Warning,
	Danger
}

public sealed class GamePopup
{
	public int Id { get; set; }
	public string Title { get; set; } = "";
	public string Message { get; set; } = "";
	public PopupKind Kind { get; set; } = PopupKind.Info;
	public bool CanDismiss { get; set; } = true;
	public float Lifetime { get; set; } = 5f;
	public bool SoundEnabled { get; set; } = true;
}
