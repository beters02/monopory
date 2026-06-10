using Sandbox;
using System;
using System.Globalization;
using System.Linq;

public enum MatchConfigOptionKind
{
	Bool,
	Int,
	Enum
}

public sealed class MatchConfigOption
{
	public string Key { get; init; }
	public string Group { get; init; }
	public string Label { get; init; }
	public string Description { get; init; } = "";
	public int Order { get; init; }
	public int Min { get; init; } = int.MinValue;
	public int Max { get; init; } = int.MaxValue;
	public int Step { get; init; } = 1;
	public MatchConfigOptionKind Kind { get; init; }
	public Type ValueType { get; init; }
	public Func<MatchConfig, object> Getter { get; init; }
	public Action<MatchConfig, object> Setter { get; init; }
	public string[] EnumNames { get; init; } = Array.Empty<string>();
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
					parsedValue = Enum.Parse( option.ValueType, rawValue, true );
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
		return new[]
		{
			IntOption( "MinPlayers", "Lobby", "Min Players", "Minimum ready players required before the host can start.", 0, 1, 6, 1, config => config.MinPlayers, ( config, value ) => config.MinPlayers = value ),
			IntOption( "MaxPlayers", "Lobby", "Max Players", "Maximum seats allowed in the hosted lobby.", 1, 1, 6, 1, config => config.MaxPlayers, ( config, value ) => config.MaxPlayers = value ),
			BoolOption( "OnlyHostStartsGame", "Lobby", "Only Host Starts Game", "If enabled, only the host can launch the match.", 2, config => config.OnlyHostStartsGame, ( config, value ) => config.OnlyHostStartsGame = value ),
			IntOption( "AbandonTimeoutSeconds", "Lobby", "Abandon Timeout Seconds", "How long disconnected players can rejoin before they are abandoned and removed.", 3, 15, 1800, 15, config => config.AbandonTimeoutSeconds, ( config, value ) => config.AbandonTimeoutSeconds = value ),
			BoolOption( "AutosaveEnabled", "Lobby", "Autosave Enabled", "Automatically saves the match at stable recovery points.", 4, config => config.AutosaveEnabled, ( config, value ) => config.AutosaveEnabled = value ),
			BoolOption( "AutosaveOnStableActions", "Lobby", "Autosave On Stable Actions", "Autosaves after safe turn, trade, property, and bankruptcy transitions.", 5, config => config.AutosaveOnStableActions, ( config, value ) => config.AutosaveOnStableActions = value ),
			EnumOption( "LandedUnownedCanAffordMode", "Property Rules", "Affordable Unowned Landing", "What happens when a player can afford an unowned property.", 10, config => config.LandedUnownedCanAffordMode, ( config, value ) => config.LandedUnownedCanAffordMode = value ),
			EnumOption( "LandedUnownedCantAffordMode", "Property Rules", "Unaffordable Unowned Landing", "What happens when a player cannot afford an unowned property.", 11, config => config.LandedUnownedCantAffordMode, ( config, value ) => config.LandedUnownedCantAffordMode = value ),
			BoolOption( "CanSkipUnowned", "Property Rules", "Can Skip Unowned", "Allows players to ignore an unowned property instead of buying or auctioning it.", 12, config => config.CanSkipUnowned, ( config, value ) => config.CanSkipUnowned = value ),
			IntOption( "StartingMoney", "Economy", "Starting Money", "Cash each player begins the game with.", 20, 0, 10000, 100, config => config.StartingMoney, ( config, value ) => config.StartingMoney = value ),
			IntOption( "LandOnGoMoney", "Economy", "Land On GO Additional Money", "Additional bonus paid on top of Pass GO Money when a move ends on GO.", 21, 0, 5000, 50, config => config.LandOnGoMoney, ( config, value ) => config.LandOnGoMoney = value ),
			IntOption( "PassGoMoney", "Economy", "Pass GO Money", "Bonus for passing GO during movement.", 22, 0, 5000, 50, config => config.PassGoMoney, ( config, value ) => config.PassGoMoney = value ),
			IntOption( "SnakeEyesBonusMoney", "Economy", "Snake Eyes Bonus Money", "Bonus awarded when a player rolls snake eyes.", 23, 0, 5000, 50, config => config.SnakeEyesBonusMoney, ( config, value ) => config.SnakeEyesBonusMoney = value ),
			EnumOption( "PlayerBankruptedPlayerMode", "Economy", "Player Bankrupted Player Mode", "What happens to properties when one player bankrupts another.", 24, config => config.PlayerBankruptedPlayerMode, ( config, value ) => config.PlayerBankruptedPlayerMode = value ),
			BoolOption( "DoublesGoesAgain", "Turn Rules", "Doubles Goes Again", "Lets players take another turn after rolling doubles.", 30, config => config.DoublesGoesAgain, ( config, value ) => config.DoublesGoesAgain = value ),
			BoolOption( "DoublesGoAgainOutOfVacationCashBreak", "Turn Rules", "Doubles Go Again Out Of Vacation Cash Break", "If Doubles Goes Again and Vacation Cash are enabled, a player can get another turn from doubles after their Vacation Cash skipped turn.", 31, config => config.DoublesGoAgainOutOfVacationCashBreak, ( config, value ) => config.DoublesGoAgainOutOfVacationCashBreak = value ),
			BoolOption( "DoublesGoAgainOutOfJail", "Turn Rules", "Doubles Go Again Out Of Jail", "If Doubles Goes Again is enabled, a player can get another turn from doubles on a roll made after leaving jail.", 32, config => config.DoublesGoAgainOutOfJail, ( config, value ) => config.DoublesGoAgainOutOfJail = value ),
			BoolOption( "RandomizeTurnOrder", "Turn Rules", "Randomize Turn Order", "Shuffles the starting player order at match start.", 33, config => config.RandomizeTurnOrder, ( config, value ) => config.RandomizeTurnOrder = value ),
			BoolOption( "ForceJailFineAfterFailedDoubles", "Turn Rules", "Force Jail Fine After Failed Doubles", "After the final failed jail roll, automatically pay the fine to leave jail.", 34, config => config.ForceJailFineAfterFailedDoubles, ( config, value ) => config.ForceJailFineAfterFailedDoubles = value ),
			IntOption( "TurnTimeLimitSeconds", "Turn Rules", "Turn Time Limit Seconds", "How long each turn can last before timeout handling kicks in.", 35, 15, 900, 15, config => config.TurnTimeLimitSeconds, ( config, value ) => config.TurnTimeLimitSeconds = value ),
			IntOption( "InstantMoveButtonUnlockMinutes", "Turn Rules", "Instant Move Button Unlock Minutes", "Elapsed match minutes before the finish-movement button can appear. 0 allows it immediately.", 36, 0, 240, 5, config => config.InstantMoveButtonUnlockMinutes, ( config, value ) => config.InstantMoveButtonUnlockMinutes = value ),
			BoolOption( "VacationCash", "Board Rules", "Vacation Cash", "Awards pooled cash when landing on Free Parking, if enabled.", 40, config => config.VacationCash, ( config, value ) => config.VacationCash = value ),
			BoolOption( "DontCollectRentWhileInPrison", "Board Rules", "No Rent While In Prison", "Prevents jailed players from collecting rent.", 41, config => config.DontCollectRentWhileInPrison, ( config, value ) => config.DontCollectRentWhileInPrison = value ),
			BoolOption( "EvenBuild", "Board Rules", "Even Build", "Requires houses to be built evenly across a color set.", 42, config => config.EvenBuild, ( config, value ) => config.EvenBuild = value )
		}
			.OrderBy( option => option.Order )
			.ThenBy( option => option.Label )
			.ToList();
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

	private static MatchConfigOption BoolOption( string key, string group, string label, string description, int order, Func<MatchConfig, bool> getter, Action<MatchConfig, bool> setter )
	{
		return new MatchConfigOption
		{
			Key = key,
			Group = group,
			Label = label,
			Description = description,
			Order = order,
			Kind = MatchConfigOptionKind.Bool,
			ValueType = typeof( bool ),
			Getter = config => getter( config ),
			Setter = ( config, value ) => setter( config, Convert.ToBoolean( value, CultureInfo.InvariantCulture ) )
		};
	}

	private static MatchConfigOption IntOption( string key, string group, string label, string description, int order, int min, int max, int step, Func<MatchConfig, int> getter, Action<MatchConfig, int> setter )
	{
		return new MatchConfigOption
		{
			Key = key,
			Group = group,
			Label = label,
			Description = description,
			Order = order,
			Min = min,
			Max = max,
			Step = Math.Max( step, 1 ),
			Kind = MatchConfigOptionKind.Int,
			ValueType = typeof( int ),
			Getter = config => getter( config ),
			Setter = ( config, value ) => setter( config, Convert.ToInt32( value, CultureInfo.InvariantCulture ) )
		};
	}

	private static MatchConfigOption EnumOption<TEnum>( string key, string group, string label, string description, int order, Func<MatchConfig, TEnum> getter, Action<MatchConfig, TEnum> setter ) where TEnum : struct, Enum
	{
		return new MatchConfigOption
		{
			Key = key,
			Group = group,
			Label = label,
			Description = description,
			Order = order,
			Kind = MatchConfigOptionKind.Enum,
			ValueType = typeof( TEnum ),
			EnumNames = Enum.GetNames<TEnum>(),
			Getter = config => getter( config ),
			Setter = ( config, value ) =>
			{
				if ( value is TEnum typedValue )
				{
					setter( config, typedValue );
					return;
				}

				if ( Enum.TryParse<TEnum>( value?.ToString() ?? "", true, out var parsedValue ) )
					setter( config, parsedValue );
			}
		};
	}

}
