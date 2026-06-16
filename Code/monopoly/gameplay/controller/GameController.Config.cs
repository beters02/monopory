using System;

public sealed partial class GameController : Component
{
	private void PublishGameConfigSnapshot()
	{
		if ( !Networking.IsHost )
			return;

		Config = MatchConfigSchema.Normalize( Config, Players?.Count ?? 0 );
		GameConfigSnapshot = MatchConfigSchema.Serialize( Config );
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
