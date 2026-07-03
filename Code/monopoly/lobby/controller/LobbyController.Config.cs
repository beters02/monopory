using System;

public sealed partial class LobbyController
{
	private void ApplyHostedConfig()
	{
		var bootstrap = MatchBootstrap.Current;
		if ( bootstrap?.HasConfig == true && bootstrap.AutoStartGame != true && bootstrap.HasLoadedGame != true )
			Config = MatchConfigSchema.Clone( bootstrap.Config );
		else
			Config ??= MatchConfigSchema.CreateDefault();

		Config = MatchConfigSchema.Normalize( Config, Players?.Count ?? 0 );

		if ( Networking.IsHost )
		{
			HostedConfigSnapshot = MatchConfigSchema.Serialize( Config );
			lastAppliedHostedConfigSnapshot = HostedConfigSnapshot;
		}
		else
		{
			ApplyHostedConfigSnapshot();
		}
	}

	private MatchConfig GetGameConfig()
	{
		return MatchConfigSchema.Normalize( MatchConfigSchema.Clone( Config ), Players?.Count ?? 0 );
	}

	public MatchConfig GetEditableConfig()
	{
		ApplyHostedConfigSnapshot();
		return MatchConfigSchema.Clone( Config );
	}

	public bool TrySaveConfig( MatchConfig proposedConfig, out string errorMessage )
	{
		errorMessage = "";

		if ( !Networking.IsHost || !IsLocalEffectiveHost )
		{
			errorMessage = "Only the host can change lobby game settings.";
			return false;
		}

		var normalizedConfig = MatchConfigSchema.Normalize( MatchConfigSchema.Clone( proposedConfig ), Players?.Count ?? 0 );
		if ( normalizedConfig.MaxPlayers < (Players?.Count ?? 0) )
		{
			errorMessage = "Max players cannot be lower than the number of players already in the lobby.";
			return false;
		}

		Config = normalizedConfig;
		HostedConfigSnapshot = MatchConfigSchema.Serialize( Config );
		lastAppliedHostedConfigSnapshot = HostedConfigSnapshot;
		MatchBootstrap.PrepareLobby( Config );
		return true;
	}

	private void ApplyHostedConfigSnapshot()
	{
		if ( Networking.IsHost )
			return;

		if ( string.Equals( HostedConfigSnapshot, lastAppliedHostedConfigSnapshot, StringComparison.Ordinal ) )
			return;

		Config = MatchConfigSchema.Deserialize( HostedConfigSnapshot );
		lastAppliedHostedConfigSnapshot = HostedConfigSnapshot;
	}
}
