using Sandbox;
using System;

public sealed class PieceDefinition
{
	public string Id { get; init; } = "";
	public string Label { get; init; } = "";
	public string Description { get; init; } = "";
	public string ModelPath { get; init; } = "";
	public Vector3 LocalVisualScale { get; init; } = Vector3.One;
	public Vector3 LocalVisualOffset { get; init; } = Vector3.Zero;
	public float HeightOffset { get; init; } = 4f;
}

public static class PieceCatalog
{
	public const string DefaultPieceId = "cat";

	public static IReadOnlyList<PieceDefinition> All { get; } = new List<PieceDefinition>
	{
		new()
		{
			Id = "cat",
			Label = "Cat",
			Description = "Classic cat token.",
			ModelPath = "models/cat.vmdl",
			LocalVisualScale = new Vector3( 0.1f, 0.1f, 0.1f ),
			LocalVisualOffset = new Vector3( 0f, 0f, -1.3f ),
			HeightOffset = 4.8f
		},
		new()
		{
			Id = "officer",
			Label = "Officer",
			Description = "Stylish officer character token.",
			ModelPath = "models/cutieguys_officer_woman/cutieguys_officer_woman.vmdl",
			LocalVisualScale = new Vector3( 20f, 20f, 20f ),
			LocalVisualOffset = new Vector3( 0f, 0f, -1.3f ),
			HeightOffset = -0.58f
		}
	};

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
