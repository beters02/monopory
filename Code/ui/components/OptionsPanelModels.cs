using Sandbox.UI;

public sealed class OptionsPanelSection<TItem>
{
	public string Kicker { get; init; } = "";
	public string Title { get; init; } = "";
	public string Copy { get; init; } = "";
	public string EmptyTitle { get; init; } = "";
	public string EmptyMessage { get; init; } = "";
	public IReadOnlyList<TItem> Items { get; init; } = new List<TItem>();
	public RenderFragment HeaderContent { get; init; }
}
