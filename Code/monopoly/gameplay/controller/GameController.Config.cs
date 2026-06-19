using System;

public sealed partial class GameController : Component
{
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
