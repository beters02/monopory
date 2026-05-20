using System;
using System.Collections.Generic;
using Sandbox;

public enum CharacterAttachmentSlot
{
	Hat,
	Glasses,
	Back,
	Hand
}

public sealed class CharacterAttachmentDefinition
{
	public string Id { get; }
	public CharacterAttachmentSlot Slot { get; }
	public string ModelPath { get; }
	public string BoneName { get; }
	public Angles LocalAngles { get; }
	public Vector3 LocalPosition { get; }
	public Vector3 LocalScale { get; }

	public CharacterAttachmentDefinition( string id, CharacterAttachmentSlot slot, string modelPath, string boneName, Angles localAngles, Vector3 localPosition, Vector3 localScale )
	{
		Id = id;
		Slot = slot;
		ModelPath = modelPath;
		BoneName = boneName;
		LocalAngles = localAngles;
		LocalPosition = localPosition;
		LocalScale = localScale;
	}
}

public static class CharacterAttachmentData
{
	private static readonly Dictionary<string, CharacterAttachmentDefinition> Definitions =
		new( StringComparer.OrdinalIgnoreCase )
		{
			["cutieguys_officer_woman.hat.default"] = new(
				"cutieguys_officer_woman.hat.default",
				CharacterAttachmentSlot.Hat,
				"models/cutieguys_officer_woman/hat.vmdl",
				"head",
				new Angles( -90f, 0f, -90f ),
				new Vector3( 0f, -0.41f, 0.03f ),
				new Vector3( 0.01f, 0.01f, 0.01f ) )
		};

	public static bool TryGetDefinition( string id, out CharacterAttachmentDefinition definition )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
		{
			definition = null;
			return false;
		}

		return Definitions.TryGetValue( id.Trim(), out definition );
	}

	public static string GetDefaultId( CharacterAttachmentSlot slot )
	{
		return slot switch
		{
			CharacterAttachmentSlot.Hat => "cutieguys_officer_woman.hat.default",
			_ => null
		};
	}
}
