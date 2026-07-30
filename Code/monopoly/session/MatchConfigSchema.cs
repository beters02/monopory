using Sandbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;

public enum MatchConfigOptionKind
{
	Bool,
	Int,
	Enum,
	String
}

public sealed class MatchConfigDependency
{
	public string OptionKey { get; init; }
	public string ExpectedValue { get; init; }
	public MatchConfigDependencyComparison Comparison { get; init; }
}

public sealed class MatchConfigOption
{
	public string Key { get; init; }
	public string Group { get; init; }
	public string Label { get; init; }
	public string Description { get; init; } = "";
	public string CommandId { get; init; } = "";
	public bool IsVisible { get; init; } = true;
	public GameCommandCheatType GameCommandCheatType { get; init; } = GameCommandCheatType.Host;
	public bool RequiresRestart { get; init; }
	public int Order { get; init; }
	public int Min { get; init; } = int.MinValue;
	public int Max { get; init; } = int.MaxValue;
	public int Step { get; init; } = 1;
	public bool HasStandaloneValue { get; init; }
	public object StandaloneValue { get; init; }
	public MatchConfigOptionKind Kind { get; init; }
	public Type ValueType { get; init; }
	public string[] EnumNames { get; init; } = Array.Empty<string>();
	public IReadOnlyDictionary<string, string> EnumDescriptions { get; init; } = new Dictionary<string, string>();
	public IReadOnlyList<MatchConfigDependency> Dependencies { get; init; } = Array.Empty<MatchConfigDependency>();
}

public static class MatchConfigSchema
{
	private static IReadOnlyList<MatchConfigOption> options;
	private static object optionsTypeIdentity;

	public static IReadOnlyList<MatchConfigOption> Options
	{
		get
		{
			var configType = Game.TypeLibrary.GetType<MatchConfig>();
			if ( options is null || options.Count == 0 || !ReferenceEquals( optionsTypeIdentity, configType ) )
			{
				options = BuildOptions( configType );
				optionsTypeIdentity = configType;
			}

			return options;
		}
	}

	public static MatchConfig CreateDefault()
	{
		return ApplyUserDefaultPresets( CreateBaseDefault() );
	}

	private static MatchConfig CreateBaseDefault()
	{
		return Normalize( new MatchConfig(), 0 );
	}

	private static MatchConfig ApplyUserDefaultPresets( MatchConfig config )
	{
		config ??= CreateBaseDefault();

		var gameRulesPreset = GetUserDefaultGameRulesPreset();
		if ( gameRulesPreset is not null )
		{
			var presetConfig = Deserialize( gameRulesPreset.Snapshot );
			foreach ( var option in Options.Where( option =>
				option.Key != nameof( MatchConfig.BoardSpaceNamesSnapshot ) &&
				option.Key != nameof( MatchConfig.BoardId ) ) )
			{
				SetOptionValue( option, config, GetOptionValue( option, presetConfig ) );
			}
		}

		var boardPreset = GetUserDefaultBoardPreset();
		if ( boardPreset is not null )
			config.BoardSpaceNamesSnapshot = boardPreset.Snapshot ?? "";

		return Normalize( config, 0 );
	}

	private static MatchSettingsPreset GetUserDefaultGameRulesPreset()
	{
		var presetId = AppSettings.GetDefaultGameRulePresetId();
		return GameRulePresets.All.Concat( LoadCustomPresets( "match_settings/game_rule_presets.json" ) )
			.FirstOrDefault( preset => string.Equals( preset?.Id, presetId, StringComparison.Ordinal ) );
	}

	private static MatchSettingsPreset GetUserDefaultBoardPreset()
	{
		var presetId = AppSettings.GetDefaultBoardConfigPresetId();
		return BoardCatalog.GetNamePresets().Concat( LoadCustomPresets( "match_settings/board_config_presets.json" ) )
			.FirstOrDefault( preset => string.Equals( preset?.Id, presetId, StringComparison.Ordinal ) );
	}

	private static List<MatchSettingsPreset> LoadCustomPresets( string path )
	{
		if ( !FileSystem.Data.FileExists( path ) )
			return new();

		return FileSystem.Data.ReadJson<List<MatchSettingsPreset>>( path )?
			.Where( preset => preset is not null && !preset.IsPredefined && !string.IsNullOrWhiteSpace( preset.Snapshot ) )
			.ToList() ?? new();
	}

	public static MatchConfig Clone( MatchConfig config )
	{
		var source = Normalize( config ?? new MatchConfig(), 0 );
		var clone = new MatchConfig();

		foreach ( var option in Options )
		{
			if ( !TryGetOptionValue( option, source, out var value ) )
				continue;

			TrySetOptionValue( option, clone, value );
		}

		return Normalize( clone, 0 );
	}

	public static MatchConfig Normalize( MatchConfig config, int connectedPlayerCount )
	{
		config ??= new MatchConfig();
		ApplyStandaloneDefaults( config );

		foreach ( var option in Options )
		{
			if ( !TryGetOptionValue( option, config, out var rawValueObject ) )
				continue;

			if ( option.Kind != MatchConfigOptionKind.Int )
				continue;

			var rawValue = rawValueObject is int intValue ? intValue : 0;
			var normalizedValue = Math.Clamp( rawValue, option.Min, option.Max );
			TrySetOptionValue( option, config, normalizedValue );
		}

		var minPlayersOption = Options.FirstOrDefault( option => string.Equals( option.Key, nameof( MatchConfig.MinPlayers ), StringComparison.Ordinal ) );
		var maxPlayersOption = Options.FirstOrDefault( option => string.Equals( option.Key, nameof( MatchConfig.MaxPlayers ), StringComparison.Ordinal ) );
		var minPlayersMin = minPlayersOption?.Min ?? 1;
		var maxPlayersMax = maxPlayersOption?.Max ?? int.MaxValue;

		config.MinPlayers = Math.Clamp( config.MinPlayers, minPlayersMin, Math.Min( config.MaxPlayers, maxPlayersMax ) );
		config.MaxPlayers = Math.Clamp( config.MaxPlayers, Math.Min( Math.Max( config.MinPlayers, connectedPlayerCount ), maxPlayersMax ), maxPlayersMax );

		if ( string.Equals( config.BoardId, BoardCatalog.LegacyNamedBoardId, StringComparison.OrdinalIgnoreCase ) )
			config.BoardId = BoardCatalog.DefaultBoardId;

		if ( string.IsNullOrWhiteSpace( config.BoardSpaceNamesSnapshot ) )
		{
			var defaultPreset = BoardCatalog.GetNamePresetById( BoardNamePresets.DefaultPresetId );
			config.BoardSpaceNamesSnapshot = defaultPreset?.Snapshot ?? "";
		}

		config.BoardId = BoardCatalog.GetById( config.BoardId ).Id;
		return config;
	}

	public static MatchConfig ApplyStandaloneDefaults( MatchConfig config )
	{
		config ??= new MatchConfig();

		if ( !MonopolyApp.IsStandalone() )
			return config;

		foreach ( var option in Options )
		{
			if ( !option.HasStandaloneValue )
				continue;

			TrySetOptionValue( option, config, option.StandaloneValue );
		}

		return config;
	}

	public static string Serialize( MatchConfig config )
	{
		config = Clone( config );

		return string.Join(
			"|",
			Options.Select( option => $"{option.Key}={FormatValue( option, GetOptionValueOrDefault( option, config ) )}" )
		);
	}

	public static MatchConfig Deserialize( string snapshot )
	{
		var config = CreateBaseDefault();

		if ( string.IsNullOrWhiteSpace( snapshot ) )
			return config;

		var rawValues = new Dictionary<string, string>( StringComparer.Ordinal );
		var pairs = snapshot.Split( '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		foreach ( var pair in pairs )
		{
			var separatorIndex = pair.IndexOf( '=' );
			if ( separatorIndex <= 0 || separatorIndex >= pair.Length - 1 )
				continue;

			rawValues[pair[..separatorIndex]] = pair[(separatorIndex + 1)..];
		}

		foreach ( var option in Options )
		{
			if ( !rawValues.TryGetValue( option.Key, out var rawValue ) ||
				!TryParseValue( option, rawValue, out var parsedValue ) )
				continue;

			TrySetOptionValue( option, config, parsedValue );
		}

		if ( !rawValues.ContainsKey( nameof( MatchConfig.RentInPrisonPercentage ) ) &&
			rawValues.TryGetValue( "DontCollectRentWhileInPrison", out var legacyNoRent ) &&
			GameCommandManager.TryParseBool( legacyNoRent, out var noRentWhileInPrison ) )
		{
			config.RentInPrisonPercentage = noRentWhileInPrison ? 0 : 100;
		}

		if ( !rawValues.ContainsKey( nameof( MatchConfig.InstantMoveUnlockSeconds ) ) &&
			rawValues.TryGetValue( "InstantMoveButtonUnlockMinutes", out var legacyUnlockMinutes ) &&
			int.TryParse( legacyUnlockMinutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unlockMinutes ) )
		{
			config.InstantMoveUnlockSeconds = Math.Clamp( unlockMinutes, 0, 240 ) * 60;
		}

		if ( !rawValues.ContainsKey( nameof( MatchConfig.InstantMoveBehavior ) ) &&
			rawValues.TryGetValue( "InstantMoveAlways", out var legacyInstantMove ) &&
			GameCommandManager.TryParseBool( legacyInstantMove, out var instantMoveAlways ) )
		{
			config.InstantMoveBehavior = instantMoveAlways
				? InstantMoveBehavior.Forced
				: config.InstantMoveUnlockSeconds > 0
					? InstantMoveBehavior.AllowedAfterUnlock
					: InstantMoveBehavior.Allowed;
		}

		return Normalize( config, 0 );
	}

	public static string GetEnumValueDescription( MatchConfigOption option, string value )
	{
		if ( option?.Kind != MatchConfigOptionKind.Enum || string.IsNullOrWhiteSpace( value ) )
			return "";

		return option.EnumDescriptions.TryGetValue( value, out var description )
			? description ?? ""
			: "";
	}
	public static string FormatValue( MatchConfigOption option, object value )
	{
		if ( option.Kind == MatchConfigOptionKind.Bool )
			return (bool)(value ?? false) ? "true" : "false";

		if ( option.Kind == MatchConfigOptionKind.Int )
			return Convert.ToInt32( value, CultureInfo.InvariantCulture ).ToString( CultureInfo.InvariantCulture );

		if ( option.Kind == MatchConfigOptionKind.String )
			return Uri.EscapeDataString( value?.ToString() ?? "" );

		return value?.ToString() ?? "";
	}

	public static bool TryParseValue( MatchConfigOption option, string rawValue, out object parsedValue )
	{
		parsedValue = null;

		switch ( option.Kind )
		{
			case MatchConfigOptionKind.Bool:
				if ( GameCommandManager.TryParseBool( rawValue, out var boolValue ) )
				{
					parsedValue = boolValue;
					return true;
				}

				return false;

			case MatchConfigOptionKind.Int:
				if ( int.TryParse( rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue ) )
				{
					parsedValue = Math.Clamp( intValue, option.Min, option.Max );
					return true;
				}

				return false;

			case MatchConfigOptionKind.Enum:
				try
				{
					parsedValue = Enum.Parse( option.ValueType, rawValue, true );
					return true;
				}
				catch
				{
					return false;
				}

			case MatchConfigOptionKind.String:
				parsedValue = Uri.UnescapeDataString( rawValue ?? "" );
				return true;

			default:
				return false;
		}
	}

	public static object GetOptionValue( MatchConfigOption option, MatchConfig config )
	{
		return GetOptionValueOrDefault( option, config );
	}

	public static bool SetOptionValue( MatchConfigOption option, MatchConfig config, object value )
	{
		return TrySetOptionValue( option, config, value );
	}

	public static bool IsOptionApplicable( MatchConfigOption option, MatchConfig config )
	{
		if ( option is null || config is null )
			return false;

		return IsOptionApplicable( option, config, new HashSet<string>( StringComparer.Ordinal ) );
	}

	private static bool IsOptionApplicable( MatchConfigOption option, MatchConfig config, HashSet<string> evaluationStack )
	{
		if ( !evaluationStack.Add( option.Key ) )
			throw new InvalidOperationException( $"Circular MatchConfig dependency encountered at '{option.Key}'." );

		try
		{
			foreach ( var dependency in option.Dependencies )
			{
				var dependencyOption = Options.First( candidate => string.Equals( candidate.Key, dependency.OptionKey, StringComparison.Ordinal ) );
				if ( !IsOptionApplicable( dependencyOption, config, evaluationStack ) )
					return false;

				var actualValue = GetOptionValueOrDefault( dependencyOption, config );
				var matches = dependency.Comparison == MatchConfigDependencyComparison.OneOf
					? dependency.ExpectedValue
						.Split( '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
						.Any( rawValue => TryParseDependencyValue( dependencyOption, rawValue, out var expectedValue ) && Equals( actualValue, expectedValue ) )
					: TryParseDependencyValue( dependencyOption, dependency.ExpectedValue, out var expectedValue ) &&
						(dependency.Comparison switch
						{
							MatchConfigDependencyComparison.Equal => Equals( actualValue, expectedValue ),
							MatchConfigDependencyComparison.LessThan => actualValue is int actualInt && expectedValue is int expectedInt && actualInt < expectedInt,
							_ => false
						});

				if ( !matches )
					return false;
			}

			return true;
		}
		finally
		{
			evaluationStack.Remove( option.Key );
		}
	}

	private static IReadOnlyList<MatchConfigOption> BuildOptions( TypeDescription configType )
	{
		if ( configType is null )
			return Array.Empty<MatchConfigOption>();

		var builtOptions = configType.Members
			.OfType<PropertyDescription>()
			.Select( BuildOption )
			.Where( option => option is not null )
			.OrderBy( option => option.Order )
			.ThenBy( option => option.Label )
			.ToList();

		ValidateDependencies( builtOptions );
		return builtOptions;
	}

	private static MatchConfigOption BuildOption( PropertyDescription property )
	{
		var attribute = property.GetCustomAttribute<MatchConfigOptionAttribute>();
		if ( attribute is null )
			return null;

		var propertyType = property.PropertyType;
		var kind =
			propertyType == typeof( bool ) ? MatchConfigOptionKind.Bool :
			propertyType == typeof( int ) ? MatchConfigOptionKind.Int :
			propertyType == typeof( string ) ? MatchConfigOptionKind.String :
			propertyType.IsEnum ? MatchConfigOptionKind.Enum :
			throw new InvalidOperationException( $"Unsupported MatchConfig option type '{propertyType.Name}' for '{property.Name}'." );

		return new MatchConfigOption
		{
			Key = property.Name,
			Group = attribute.Group,
			Label = attribute.Label,
			Description = attribute.Description,
			CommandId = attribute.CommandId,
			IsVisible = attribute.IsVisible,
			GameCommandCheatType = attribute.GameCommandCheatType,
			RequiresRestart = attribute.RequiresRestart,
			Order = attribute.Order,
			Min = attribute.Min,
			Max = attribute.Max,
			Step = Math.Max( attribute.Step, 1 ),
			HasStandaloneValue = attribute.HasStandaloneValue,
			StandaloneValue = attribute.StandaloneValue,
			Kind = kind,
			ValueType = propertyType,
			EnumNames = propertyType.IsEnum ? Enum.GetNames( propertyType ) : Array.Empty<string>(),
			EnumDescriptions = BuildEnumDescriptions( propertyType ),
			Dependencies = property.Attributes
				.OfType<MatchConfigDependsOnAttribute>()
				.Select( dependency => new MatchConfigDependency
				{
					OptionKey = dependency.OptionKey,
					ExpectedValue = dependency.ExpectedValue,
					Comparison = dependency.Comparison
				} )
				.ToList()
		};
	}

	private static IReadOnlyDictionary<string, string> BuildEnumDescriptions( Type valueType )
	{
		if ( valueType?.IsEnum != true )
			return new Dictionary<string, string>();

		return Enum.GetNames( valueType ).ToDictionary(
			name => name,
			name => valueType.GetField( name )?
				.GetCustomAttributes( typeof( MatchConfigEnumDescriptionAttribute ), false )
				.OfType<MatchConfigEnumDescriptionAttribute>()
				.FirstOrDefault()?.Description ?? "",
			StringComparer.Ordinal );
	}

	private static void ValidateDependencies( IReadOnlyList<MatchConfigOption> builtOptions )
	{
		var optionsByKey = builtOptions.ToDictionary( option => option.Key, StringComparer.Ordinal );
		foreach ( var option in builtOptions )
		{
			foreach ( var dependency in option.Dependencies )
			{
				if ( string.IsNullOrWhiteSpace( dependency.OptionKey ) || !optionsByKey.TryGetValue( dependency.OptionKey, out var dependencyOption ) )
					throw new InvalidOperationException( $"MatchConfig option '{option.Key}' depends on unknown option '{dependency.OptionKey}'." );

				if ( string.Equals( option.Key, dependency.OptionKey, StringComparison.Ordinal ) )
					throw new InvalidOperationException( $"MatchConfig option '{option.Key}' cannot depend on itself." );

				if ( dependency.Comparison == MatchConfigDependencyComparison.LessThan && dependencyOption.Kind != MatchConfigOptionKind.Int )
					throw new InvalidOperationException( $"MatchConfig option '{option.Key}' uses a less-than dependency on non-integer option '{dependency.OptionKey}'." );

				var expectedValues = dependency.Comparison == MatchConfigDependencyComparison.OneOf
					? dependency.ExpectedValue.Split( '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
					: new[] { dependency.ExpectedValue };
				if ( expectedValues.Length == 0 || expectedValues.Any( rawValue => !TryParseDependencyValue( dependencyOption, rawValue, out _ ) ) )
					throw new InvalidOperationException( $"MatchConfig option '{option.Key}' has invalid dependency value '{dependency.ExpectedValue}' for '{dependency.OptionKey}'." );
			}
		}

		var visitStates = new Dictionary<string, int>( StringComparer.Ordinal );
		foreach ( var option in builtOptions )
			ValidateDependencyGraph( option, optionsByKey, visitStates );
	}

	private static bool TryParseDependencyValue( MatchConfigOption option, string rawValue, out object parsedValue )
	{
		parsedValue = null;
		if ( option.Kind == MatchConfigOptionKind.Int )
		{
			if ( !int.TryParse( rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue ) ||
				intValue < option.Min || intValue > option.Max )
				return false;

			parsedValue = intValue;
			return true;
		}

		if ( option.Kind == MatchConfigOptionKind.Enum )
		{
			try
			{
				parsedValue = Enum.Parse( option.ValueType, rawValue, true );
				return Enum.IsDefined( option.ValueType, parsedValue );
			}
			catch
			{
				return false;
			}
		}

		return TryParseValue( option, rawValue, out parsedValue );
	}

	private static void ValidateDependencyGraph(
		MatchConfigOption option,
		IReadOnlyDictionary<string, MatchConfigOption> optionsByKey,
		Dictionary<string, int> visitStates )
	{
		if ( visitStates.TryGetValue( option.Key, out var state ) )
		{
			if ( state == 1 )
				throw new InvalidOperationException( $"Circular MatchConfig dependency encountered at '{option.Key}'." );

			if ( state == 2 )
				return;
		}

		visitStates[option.Key] = 1;
		foreach ( var dependency in option.Dependencies )
			ValidateDependencyGraph( optionsByKey[dependency.OptionKey], optionsByKey, visitStates );
		visitStates[option.Key] = 2;
	}

	private static object ConvertOptionValue( Type valueType, object value )
	{
		if ( valueType == typeof( bool ) )
			return Convert.ToBoolean( value, CultureInfo.InvariantCulture );

		if ( valueType == typeof( int ) )
			return Convert.ToInt32( value, CultureInfo.InvariantCulture );

		if ( valueType.IsEnum )
		{
			if ( value is not null && value.GetType() == valueType )
				return value;

			return Enum.Parse( valueType, value?.ToString() ?? "", true );
		}

		if ( valueType == typeof( string ) )
			return value?.ToString() ?? "";

		return value;
	}

	private static bool TryGetOptionValue( MatchConfigOption option, MatchConfig config, out object value )
	{
		value = null;
		var property = FindCurrentProperty( option );
		if ( property is null || config is null )
			return false;

		try
		{
			value = property.GetValue( config );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Error( $"Failed to read MatchConfig option '{option.Key}': {ex.Message}" );
			return false;
		}
	}

	private static bool TrySetOptionValue( MatchConfigOption option, MatchConfig config, object value )
	{
		var property = FindCurrentProperty( option );
		if ( property is null || config is null )
			return false;

		try
		{
			property.SetValue( config, ConvertOptionValue( property.PropertyType, value ) );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Error( $"Failed to write MatchConfig option '{option.Key}': {ex.Message}" );
			return false;
		}
	}

	private static PropertyDescription FindCurrentProperty( MatchConfigOption option )
	{
		if ( option is null || string.IsNullOrWhiteSpace( option.Key ) )
			return null;

		return Game.TypeLibrary.GetType<MatchConfig>()?.Members
			.OfType<PropertyDescription>()
			.FirstOrDefault( property => string.Equals( property.Name, option.Key, StringComparison.Ordinal ) );
	}

	private static object GetOptionValueOrDefault( MatchConfigOption option, MatchConfig config )
	{
		return TryGetOptionValue( option, config, out var value ) ? value : null;
	}

}
