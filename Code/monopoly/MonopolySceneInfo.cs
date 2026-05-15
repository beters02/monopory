using Sandbox;

public sealed class MonopolySceneInfo : Component, ISceneMetadata
{
	[Property] public string Title { get; set; } = "";
	[Property] public string Group { get; set; } = "";
	[Property, TextArea] public string Description { get; set; } = "";

	public Dictionary<string, string> GetMetadata()
	{
		return new Dictionary<string, string>
		{
			{ "Title", Title },
			{ "Group", Group },
			{ "Description", Description }
		};
	}
}
