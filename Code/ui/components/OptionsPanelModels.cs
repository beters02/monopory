using Sandbox.UI;

public sealed class OptionsPanelSection
{
	public string Key { get; init; } = "";
	public string Kicker { get; init; } = "";
	public string Title { get; init; } = "";
	public string Copy { get; init; } = "";
	public string EmptyTitle { get; init; } = "";
	public string EmptyMessage { get; init; } = "";
	public bool HasItems { get; init; }
	public RenderFragment HeaderContent { get; init; }
}
