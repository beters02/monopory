using Sandbox;
using System;

public sealed class PieceDefinition
{
	public string Id { get; init; } = "";
	public string Label { get; init; } = "";
	public string Description { get; init; } = "";
	public string ModelPath { get; init; } = "";
	public string AnimgraphPath { get; init; } = "";
	public string RequiredAchievementId { get; init; } = "";
	public Vector3 LocalVisualScale { get; init; } = Vector3.One;
	public Vector3 LocalVisualOffset { get; init; } = Vector3.Zero;
	public float HeightOffset { get; init; } = 4f;
}

public static class PieceCatalog
{
	public const string DefaultPieceId = "officer_woman";

	public static IReadOnlyList<PieceDefinition> All => BuildDefinitions();

	private static IReadOnlyList<PieceDefinition> BuildDefinitions()
	{
		return new List<PieceDefinition>
		{
			new()
			{
				Id = "officer_woman",
				Label = "Officer (Woman)",
				Description = "Stylish officer character token.",
				ModelPath = "models/cutieguys_officer_woman/cutieguys_officer_woman.vmdl",
				AnimgraphPath = "animgraphs/cutieguys_officer_woman.vanmgrph",
				LocalVisualScale = new Vector3( 20f, 20f, 20f ),
				LocalVisualOffset = new Vector3( 0f, 0f, -1.3f ),
				HeightOffset = 1.25f
			},
			new()
			{
				Id = "detective_man",
				Label = "Detective (Man)",
				Description = "Stylish detective character token.",
				RequiredAchievementId = AchievementIds.FirstWin,
				ModelPath = "models/cutieguys_detective_man/cutieguys_detective_man.vmdl",
				AnimgraphPath = "animgraphs/cutieguys_detective_man.vanmgrph",
				LocalVisualScale = new Vector3( 0.2f, 0.2f, 0.2f ),
				LocalVisualOffset = new Vector3( 0f, 0f, -1.3f ),
				HeightOffset = 1.25f
			},
			new()
			{
				Id = "rock_woman",
				Label = "Rocker (Woman)",
				Description = "Stylish rock character token.",
				RequiredAchievementId = AchievementIds.FirstWin,
				ModelPath = "models/cutieguys_rock_woman/cutieguys_rock_woman.vmdl",
				AnimgraphPath = "animgraphs/cutieguys_rock_woman.vanmgrph",
				LocalVisualScale = new Vector3( 0.2f, 0.2f, 0.2f ),
				LocalVisualOffset = new Vector3( 0f, 0f, -1.3f ),
				HeightOffset = 1.25f
			},
			new()
			{
				Id = "cowboy_man",
				Label = "Cowboy (Man)",
				Description = "Meet Fred. He is an alcoholic cowboy.",
				RequiredAchievementId = AchievementIds.FirstWin,
				ModelPath = "models/cutieguys_cowboy_man/cutieguys_cowboy_man.vmdl",
				AnimgraphPath = "animgraphs/cutieguys_cowboy_man.vanmgrph",
				LocalVisualScale = new Vector3( 0.2f, 0.2f, 0.2f ),
				LocalVisualOffset = new Vector3( 0f, 0f, -1.3f ),
				HeightOffset = 1.25f
			},
		};
	}

	public static PieceDefinition GetByIdOrDefault( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return All[0];

		var normalized = id.Trim();
		for ( var i = 0; i < All.Count; i++ )
		{
			if ( string.Equals( All[i].Id, normalized, StringComparison.OrdinalIgnoreCase ) )
				return All[i];
		}

		return All[0];
	}

	public static bool IsValidPieceId( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return false;

		var normalized = id.Trim();
		for ( var i = 0; i < All.Count; i++ )
		{
			if ( string.Equals( All[i].Id, normalized, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}
}
