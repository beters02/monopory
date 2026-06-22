using Sandbox.UI;

namespace Sandbox.ui.components;

public sealed class OptionsPanelSection
{
	public string Key { get; init; } = "";
	public string Kicker { get; init; } = "";
	public string Title { get; init; } = "";
	public string Copy { get; init; } = "";
	public string EmptyTitle { get; init; } = "";
	public string EmptyMessage { get; init; } = "";
	public bool HasItems { get; init; }
	public IReadOnlyList<OptionsPanelSubsection> Subsections { get; init; } = new List<OptionsPanelSubsection>();
	public RenderFragment HeaderContent { get; init; }
}

public sealed class OptionsPanelSubsection
{
	public string Key { get; init; } = "";
	public string Title { get; init; } = "";
	public string EmptyTitle { get; init; } = "";
	public string EmptyMessage { get; init; } = "";
	public bool HasItems { get; init; }
}
