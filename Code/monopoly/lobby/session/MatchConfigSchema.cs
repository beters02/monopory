using Sandbox;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

public enum MatchConfigOptionKind
{
	Bool,
	Int,
	Enum
}

public sealed class MatchConfigOption
{
	public PropertyInfo Property { get; init; }
	public MatchConfigOptionAttribute Metadata { get; init; }
	public MatchConfigOptionKind Kind { get; init; }
	public string[] EnumNames { get; init; } = Array.Empty<string>();

	public string Key => Property.Name;
	public string Group => Metadata.Group;
	public string Label => Metadata.Label;
	public string Description => Metadata.Description;
	public int Order => Metadata.Order;
	public int Min => Metadata.Min;
	public int Max => Metadata.Max;
	public int Step => Math.Max( Metadata.Step, 1 );
}

public static class MatchConfigSchema
{
	private static readonly IReadOnlyList<MatchConfigOption> options = BuildOptions();

	public static IReadOnlyList<MatchConfigOption> Options => options;

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

		foreach ( var option in Options )
		{
			if ( option.Kind != MatchConfigOptionKind.Int )
				continue;

			if ( !TryGetOptionValue( option, config, out var rawValueObject ) )
				continue;

			var rawValue = rawValueObject is int intValue ? intValue : 0;
			var normalizedValue = Math.Clamp( rawValue, option.Min, option.Max );
			TrySetOptionValue( option, config, normalizedValue );
		}

		config.MinPlayers = Math.Clamp( config.MinPlayers, 1, Math.Max( 1, config.MaxPlayers ) );
		config.MaxPlayers = Math.Max( config.MaxPlayers, Math.Max( config.MinPlayers, connectedPlayerCount ) );
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
					parsedValue = Enum.Parse( option.Property.PropertyType, rawValue, true );
					return true;
				}
				catch
				{
					return false;
				}

			default:
				return false;
		}
	}

	private static IReadOnlyList<MatchConfigOption> BuildOptions()
	{
		return typeof( MatchConfig )
			.GetProperties( BindingFlags.Instance | BindingFlags.Public )
			.Select( property => new
			{
				Property = property,
				Metadata = property.GetCustomAttribute<MatchConfigOptionAttribute>()
			} )
			.Where( x => x.Metadata is not null && x.Property.CanRead && x.Property.CanWrite )
			.Select( x => new MatchConfigOption
			{
				Property = x.Property,
				Metadata = x.Metadata,
				Kind = GetOptionKind( x.Property.PropertyType ),
				EnumNames = x.Property.PropertyType.IsEnum ? Enum.GetNames( x.Property.PropertyType ) : Array.Empty<string>()
			} )
			.OrderBy( option => option.Order )
			.ThenBy( option => option.Label )
			.ToList();
	}

	private static bool TryGetOptionValue( MatchConfigOption option, MatchConfig config, out object value )
	{
		value = null;

		if ( option?.Property is null || config is null )
			return false;

		try
		{
			value = option.Property.GetValue( config );
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
		if ( option?.Property is null || config is null )
			return false;

		try
		{
			option.Property.SetValue( config, value );
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

	private static MatchConfigOptionKind GetOptionKind( Type type )
	{
		if ( type == typeof( bool ) )
			return MatchConfigOptionKind.Bool;

		if ( type == typeof( int ) )
			return MatchConfigOptionKind.Int;

		if ( type.IsEnum )
			return MatchConfigOptionKind.Enum;

		throw new NotSupportedException( $"Unsupported match config property type '{type.Name}'." );
	}
}
