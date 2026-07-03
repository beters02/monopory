using System;
using System.Collections.Generic;
using System.Linq;

public static class MatchSettingsPresetCatalog
{
	public static IReadOnlyList<MatchSettingsPreset> BuildGameRulePresets()
	{
		return BuildPresets<MatchConfigPresetAttribute>();
	}

	private static IReadOnlyList<MatchSettingsPreset> BuildPresets<TAttribute>() where TAttribute : MatchSettingsPresetAttribute
	{
		return TypeLibrary.GetTypesWithAttribute<TAttribute>()
			.Select( entry => new PresetTypeEntry<TAttribute>( entry.Type, entry.Attribute ) )
			.OrderBy( entry => entry.Attribute.Order )
			.ThenBy( entry => entry.Attribute.Name )
			.Select( entry => entry.Attribute.Build( entry.Type ) )
			.Where( preset => preset is not null )
			.ToList();
	}

	private sealed class PresetTypeEntry<TAttribute> where TAttribute : MatchSettingsPresetAttribute
	{
		public TypeDescription Type { get; }
		public TAttribute Attribute { get; }

		public PresetTypeEntry( TypeDescription type, TAttribute attribute )
		{
			Type = type;
			Attribute = attribute;
		}
	}
}

public static class GameRulePresets
{
	public const string DefaultPresetId = "rules_default";
	private static IReadOnlyList<MatchSettingsPreset> all;

	public static IReadOnlyList<MatchSettingsPreset> All => all ??= MatchSettingsPresetCatalog.BuildGameRulePresets();

	[MatchConfigPreset( "rules_default", "Rent Rush", 0 )]
	public static class DefaultRules
	{
	}

	[MatchConfigPreset( "rules_quick", "Quick Game", 10 )]
	public static class QuickGame
	{
		[MatchConfigModifier( nameof( MatchConfig.StartingMoney ) )]
		public static readonly int StartingMoney = 1000;

		[MatchConfigModifier( nameof( MatchConfig.TurnTimeLimitSeconds ) )]
		public static readonly int TurnTimeLimitSeconds = 90;

		[MatchConfigModifier( nameof( MatchConfig.VacationCash ) )]
		public static readonly bool VacationCash = false;
	}

	[MatchConfigPreset( "rules_relaxed", "Relaxed Lobby", 20 )]
	public static class RelaxedLobby
	{
		[MatchConfigModifier( nameof( MatchConfig.MaxPlayers ) )]
		public static readonly int MaxPlayers = 12;

		[MatchConfigModifier( nameof( MatchConfig.TurnTimeLimitSeconds ) )]
		public static readonly int TurnTimeLimitSeconds = 300;

		[MatchConfigModifier( nameof( MatchConfig.CanSkipUnowned ) )]
		public static readonly bool CanSkipUnowned = true;

		[MatchConfigModifier( nameof( MatchConfig.DoublesGoAgainOutOfJail ) )]
		public static readonly bool DoublesGoAgainOutOfJail = true;
	}
}

[AttributeUsage( AttributeTargets.Class )]
public abstract class MatchSettingsPresetAttribute : Attribute
{
	public string Id { get; }
	public string Name { get; }
	public int Order { get; }

	protected MatchSettingsPresetAttribute( string id, string name, int order = 0 )
	{
		Id = id;
		Name = name;
		Order = order;
	}

	public MatchSettingsPreset Build( TypeDescription type )
	{
		return new()
		{
			Id = Id,
			Name = Name,
			Snapshot = CreateSnapshot( type ),
			IsPredefined = true
		};
	}

	protected abstract string CreateSnapshot( TypeDescription type );

	protected static IEnumerable<FieldDescription> GetModifierFields<TAttribute>( TypeDescription type ) where TAttribute : Attribute
	{
		return type?.Fields?
			.Where( field => field.GetCustomAttribute<TAttribute>() is not null )
			.OrderBy( field => field.Name ) ?? Enumerable.Empty<FieldDescription>();
	}
}

public sealed class MatchConfigPresetAttribute : MatchSettingsPresetAttribute
{
	public MatchConfigPresetAttribute( string id, string name, int order = 0 ) : base( id, name, order )
	{
	}

	protected override string CreateSnapshot( TypeDescription type )
	{
		var modifiers = new MatchConfigModifiersObject();
		foreach ( var field in GetModifierFields<MatchConfigModifierAttribute>( type ) )
		{
			var attribute = field.GetCustomAttribute<MatchConfigModifierAttribute>();
			modifiers.Add( attribute.Build( field ) );
		}

		return MatchConfigModifierSerializer.Serialize( modifiers );
	}
}

[AttributeUsage( AttributeTargets.Field )]
public sealed class MatchConfigModifierAttribute : Attribute
{
	public string Id { get; }

	public MatchConfigModifierAttribute( string id )
	{
		Id = id;
	}

	public MatchConfigModifier Build( FieldDescription field )
	{
		return new( Id, field.GetValue( null ) );
	}
}

public sealed class MatchConfigModifiersObject : List<MatchConfigModifier>
{
}

public sealed class MatchConfigModifier
{
	public string Id { get; }
	public object Value { get; }

	public MatchConfigModifier( string id, object value )
	{
		Id = id;
		Value = value;
	}
}

public static class MatchConfigModifierSerializer
{
	public static string Serialize( IEnumerable<MatchConfigModifier> modifiers )
	{
		if ( modifiers is null )
			return "";

		var serializedModifiers = modifiers
			.Where( modifier => modifier is not null && !string.IsNullOrWhiteSpace( modifier.Id ) )
			.Select( SerializeModifier )
			.Where( value => !string.IsNullOrWhiteSpace( value ) );

		return string.Join( "|", serializedModifiers );
	}

	private static string SerializeModifier( MatchConfigModifier modifier )
	{
		var option = MatchConfigSchema.Options.FirstOrDefault( option => string.Equals( option.Key, modifier.Id, StringComparison.Ordinal ) );
		if ( option is null )
			return "";

		return $"{option.Key}={MatchConfigSchema.FormatValue( option, modifier.Value )}";
	}
}
