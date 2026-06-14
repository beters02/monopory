using Sandbox;
using System;
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

public sealed class MatchConfigOption
{
	public string Key { get; init; }
	public string Group { get; init; }
	public string Label { get; init; }
	public string Description { get; init; } = "";
	public bool IsVisible { get; init; } = true;
	public int Order { get; init; }
	public int Min { get; init; } = int.MinValue;
	public int Max { get; init; } = int.MaxValue;
	public int Step { get; init; } = 1;
	public bool HasStandaloneValue { get; init; }
	public object StandaloneValue { get; init; }
	public MatchConfigOptionKind Kind { get; init; }
	public Type ValueType { get; init; }
	public Func<MatchConfig, object> Getter { get; init; }
	public Action<MatchConfig, object> Setter { get; init; }
	public string[] EnumNames { get; init; } = Array.Empty<string>();
}

public static class MatchConfigSchema
{
	private static IReadOnlyList<MatchConfigOption> options;

	public static IReadOnlyList<MatchConfigOption> Options
	{
		get
		{
			if ( options is null || options.Count == 0 )
				options = BuildOptions();

			return options;
		}
	}

	public static MatchConfig CreateDefault()
	{
		return Normalize( new MatchConfig(), 0 );
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
		var config = CreateDefault();

		if ( string.IsNullOrWhiteSpace( snapshot ) )
			return config;

		var pairs = snapshot.Split( '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		foreach ( var pair in pairs )
		{
			var separatorIndex = pair.IndexOf( '=' );
			if ( separatorIndex <= 0 || separatorIndex >= pair.Length - 1 )
				continue;

			var key = pair[..separatorIndex];
			var rawValue = pair[(separatorIndex + 1)..];
			var option = Options.FirstOrDefault( x => string.Equals( x.Key, key, StringComparison.Ordinal ) );
			if ( option is null )
				continue;

			if ( !TryParseValue( option, rawValue, out var parsedValue ) )
				continue;

			TrySetOptionValue( option, config, parsedValue );
		}

		return Normalize( config, 0 );
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
				if ( bool.TryParse( rawValue, out var boolValue ) )
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

	private static IReadOnlyList<MatchConfigOption> BuildOptions()
	{
		var configType = Game.TypeLibrary.GetType<MatchConfig>();
		if ( configType is null )
			return Array.Empty<MatchConfigOption>();

		return configType.Members
			.OfType<PropertyDescription>()
			.Select( BuildOption )
			.Where( option => option is not null )
			.OrderBy( option => option.Order )
			.ThenBy( option => option.Label )
			.ToList();
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
			IsVisible = attribute.IsVisible,
			Order = attribute.Order,
			Min = attribute.Min,
			Max = attribute.Max,
			Step = Math.Max( attribute.Step, 1 ),
			HasStandaloneValue = attribute.HasStandaloneValue,
			StandaloneValue = attribute.StandaloneValue,
			Kind = kind,
			ValueType = propertyType,
			EnumNames = propertyType.IsEnum ? Enum.GetNames( propertyType ) : Array.Empty<string>(),
			Getter = config => property.GetValue( config ),
			Setter = ( config, value ) => property.SetValue( config, ConvertOptionValue( propertyType, value ) )
		};
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

		if ( option?.Getter is null || config is null )
			return false;

		try
		{
			value = option.Getter( config );
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
		if ( option?.Setter is null || config is null )
			return false;

		try
		{
			option.Setter( config, value );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Error( $"Failed to write MatchConfig option '{option.Key}': {ex.Message}" );
			return false;
		}
	}

	private static object GetOptionValueOrDefault( MatchConfigOption option, MatchConfig config )
	{
		return TryGetOptionValue( option, config, out var value ) ? value : null;
	}

}
