using System;

public sealed partial class LobbyController
{
	private void ApplyHostedConfig()
	{
		var bootstrap = MatchBootstrap.Current;
		if ( bootstrap?.HasConfig == true )
			Config = MatchBootstrap.CloneConfig( bootstrap.Config );

		HostedMinPlayers = Math.Max( Config?.MinPlayers ?? 1, 1 );
		HostedMaxPlayers = Math.Max( Config?.MaxPlayers ?? HostedMinPlayers, HostedMinPlayers );
		HostedOnlyHostStartsGame = Config?.OnlyHostStartsGame ?? true;
	}

	private MatchConfig GetGameConfig()
	{
		var config = MatchBootstrap.CloneConfig( Config );
		config.MinPlayers = MinPlayers;
		config.MaxPlayers = MaxPlayers;
		config.OnlyHostStartsGame = OnlyHostStartsGame;
		return config;
	}
}
