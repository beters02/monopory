using System;

public sealed partial class GameController : Component
{
	private const string MatchConfigResetToken = "%reset";

	public bool TryRunMatchConfigCommand( MatchConfigOption option, string rawValue, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can change match config.";
			return false;
		}

		if ( option is null )
		{
			message = "Match config option is missing.";
			return false;
		}

		Config = MatchConfigSchema.Normalize( Config, Players?.Count ?? 0 );
		if ( string.IsNullOrWhiteSpace( rawValue ) )
		{
			message = MatchConfigSchema.FormatValue( option, MatchConfigSchema.GetOptionValue( option, Config ) );
			return true;
		}

		if ( option.RequiresRestart )
		{
			message = $"{option.Label} requires a match restart.";
			return false;
		}

		object nextValue;
		if ( string.Equals( rawValue, MatchConfigResetToken, StringComparison.OrdinalIgnoreCase ) )
		{
			EnsureMatchConfigDefaultSnapshot();
			var defaultConfig = MatchConfigSchema.Deserialize( MatchConfigDefaultSnapshot );
			nextValue = MatchConfigSchema.GetOptionValue( option, defaultConfig );
		}
		else if ( !MatchConfigSchema.TryParseValue( option, rawValue, out nextValue ) )
		{
			message = $"Unable to parse {option.Kind.ToString().ToLowerInvariant()} value \"{rawValue}\".";
			return false;
		}

		if ( !MatchConfigSchema.SetOptionValue( option, Config, nextValue ) )
		{
			message = $"Unable to set {option.Label}.";
			return false;
		}

		PublishGameConfigSnapshot();
		message = $"=> {MatchConfigSchema.FormatValue( option, MatchConfigSchema.GetOptionValue( option, Config ) )}";
		return true;
	}

	private void CaptureMatchConfigDefaultSnapshot()
	{
		Config = MatchConfigSchema.Normalize( Config, Players?.Count ?? 0 );
		MatchConfigDefaultSnapshot = MatchConfigSchema.Serialize( Config );
	}

	private void EnsureMatchConfigDefaultSnapshot()
	{
		if ( !string.IsNullOrWhiteSpace( MatchConfigDefaultSnapshot ) )
			return;

		CaptureMatchConfigDefaultSnapshot();
	}

	private void PublishGameConfigSnapshot()
	{
		if ( !Networking.IsHost )
			return;

		Config = MatchConfigSchema.Normalize( Config, Players?.Count ?? 0 );
		var previousSnapshot = GameConfigSnapshot;
		GameConfigSnapshot = MatchConfigSchema.Serialize( Config );
		if ( HasStarted && !string.IsNullOrWhiteSpace( previousSnapshot ) && !string.Equals( previousSnapshot, GameConfigSnapshot, StringComparison.Ordinal ) )
		{
			MatchConfigChangedAfterStart = true;
			RecordMoveHistoryEvent( "Integrity", "Config changed", "Match configuration changed after start." );
		}
		lastAppliedGameConfigSnapshot = GameConfigSnapshot;
	}

	private void ApplyGameConfigSnapshot()
	{
		if ( Networking.IsHost )
			return;

		if ( string.Equals( GameConfigSnapshot, lastAppliedGameConfigSnapshot, StringComparison.Ordinal ) )
			return;

		Config = MatchConfigSchema.Deserialize( GameConfigSnapshot );
		lastAppliedGameConfigSnapshot = GameConfigSnapshot;
		StartPrivateConfig();
	}
}
