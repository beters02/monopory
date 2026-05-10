using Sandbox;
public sealed class BoardSpaceName : Component
{
	[Property] public string Name { get; set; } = "Property";
	[Property] public TextRenderer Text { get; set; }

	protected override void OnStart()
	{
		Text.Text = Name;
	}
}