using System;
using System.Collections.Generic;
using System.Linq;

public static class MatchSettingsPresetCatalog
{
	public static IReadOnlyList<MatchSettingsPreset> BuildGameRulePresets()
	{
		return BuildPresets<MatchConfigPresetAttribute>();
	}

	public static IReadOnlyList<MatchSettingsPreset> BuildBoardSpaceNamePresets()
	{
		return BuildPresets<BoardSpaceNamePresetAttribute>();
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
	private static IReadOnlyList<MatchSettingsPreset> all;

	public static IReadOnlyList<MatchSettingsPreset> All => all ??= MatchSettingsPresetCatalog.BuildGameRulePresets();

	[MatchConfigPreset( "rules_default", "Default Rules", 0 )]
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

public static class BoardSpaceNames
{
	private static IReadOnlyList<MatchSettingsPreset> all;

	public static IReadOnlyList<MatchSettingsPreset> All => all ??= MatchSettingsPresetCatalog.BuildBoardSpaceNamePresets();

	[BoardSpaceNamePreset( "board_default", "Default Board", 0 )]
	public static class DefaultBoard {}

	[BoardSpaceNamePreset( "board_classic", "Classic Names", 10 )]
	public static class ClassicBoard
	{
		[BoardSpaceNameModifier( "property_brown_0" )]
		public static readonly string MediterraneanAvenue = "Mediterranean Avenue";

		[BoardSpaceNameModifier( "property_brown_1" )]
		public static readonly string BalticAvenue = "Baltic Avenue";

		[BoardSpaceNameModifier( "tax_income" )]
		public static readonly string IncomeTax = "Income Tax";

		[BoardSpaceNameModifier( "railroad_0" )]
		public static readonly string ReadingRailroad = "Reading Railroad";

		[BoardSpaceNameModifier( "property_light_blue_0" )]
		public static readonly string OrientalAvenue = "Oriental Avenue";

		[BoardSpaceNameModifier( "property_light_blue_1" )]
		public static readonly string VermontAvenue = "Vermont Avenue";

		[BoardSpaceNameModifier( "property_light_blue_2" )]
		public static readonly string ConnecticutAvenue = "Connecticut Avenue";

		[BoardSpaceNameModifier( "property_pink_0" )]
		public static readonly string StCharlesPlace = "St. Charles Place";

		[BoardSpaceNameModifier( "utility_0" )]
		public static readonly string ElectricCompany = "Electric Company";

		[BoardSpaceNameModifier( "property_pink_1" )]
		public static readonly string StatesAvenue = "States Avenue";

		[BoardSpaceNameModifier( "property_pink_2" )]
		public static readonly string VirginiaAvenue = "Virginia Avenue";

		[BoardSpaceNameModifier( "railroad_1" )]
		public static readonly string PennsylvaniaRailroad = "Pennsylvania Railroad";

		[BoardSpaceNameModifier( "property_orange_0" )]
		public static readonly string StJamesPlace = "St. James Place";

		[BoardSpaceNameModifier( "property_orange_1" )]
		public static readonly string TennesseeAvenue = "Tennessee Avenue";

		[BoardSpaceNameModifier( "property_orange_2" )]
		public static readonly string NewYorkAvenue = "New York Avenue";

		[BoardSpaceNameModifier( "property_red_0" )]
		public static readonly string KentuckyAvenue = "Kentucky Avenue";

		[BoardSpaceNameModifier( "property_red_1" )]
		public static readonly string IndianaAvenue = "Indiana Avenue";

		[BoardSpaceNameModifier( "property_red_2" )]
		public static readonly string IllinoisAvenue = "Illinois Avenue";

		[BoardSpaceNameModifier( "railroad_2" )]
		public static readonly string BOrailroad = "B&O Railroad";

		[BoardSpaceNameModifier( "property_yellow_0" )]
		public static readonly string AtlanticAvenue = "Atlantic Avenue";

		[BoardSpaceNameModifier( "property_yellow_1" )]
		public static readonly string VentnorAvenue = "Ventnor Avenue";

		[BoardSpaceNameModifier( "utility_1" )]
		public static readonly string WaterWorks = "Water Works";

		[BoardSpaceNameModifier( "property_yellow_2" )]
		public static readonly string MarvinGardens = "Marvin Gardens";

		[BoardSpaceNameModifier( "property_green_0" )]
		public static readonly string PacificAvenue = "Pacific Avenue";

		[BoardSpaceNameModifier( "property_green_1" )]
		public static readonly string NorthCarolinaAvenue = "North Carolina Avenue";

		[BoardSpaceNameModifier( "property_green_2" )]
		public static readonly string PennsylvaniaAvenue = "Pennsylvania Avenue";

		[BoardSpaceNameModifier( "railroad_3" )]
		public static readonly string ShortLine = "Short Line";

		[BoardSpaceNameModifier( "property_dark_blue_0" )]
		public static readonly string ParkPlace = "Park Place";

		[BoardSpaceNameModifier( "tax_luxury" )]
		public static readonly string LuxuryTax = "Luxury Tax";

		[BoardSpaceNameModifier( "property_dark_blue_1" )]
		public static readonly string Boardwalk = "Boardwalk";
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

public sealed class BoardSpaceNamePresetAttribute : MatchSettingsPresetAttribute
{
	public BoardSpaceNamePresetAttribute( string id, string name, int order = 0 ) : base( id, name, order )
	{
	}

	protected override string CreateSnapshot( TypeDescription type )
	{
		var modifiers = new BoardSpaceNameModifiersObject();
		foreach ( var field in GetModifierFields<BoardSpaceNameModifierAttribute>( type ) )
		{
			var attribute = field.GetCustomAttribute<BoardSpaceNameModifierAttribute>();
			modifiers.Add( attribute.Build( field ) );
		}

		return BoardSpaceNameConfig.SerializeModifiers( modifiers );
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

[AttributeUsage( AttributeTargets.Field )]
public sealed class BoardSpaceNameModifierAttribute : Attribute
{
	public string Id { get; }

	public BoardSpaceNameModifierAttribute( string id )
	{
		Id = id;
	}

	public BoardSpaceNameModifier Build( FieldDescription field )
	{
		return new( Id, field.GetValue( null )?.ToString() ?? "" );
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
