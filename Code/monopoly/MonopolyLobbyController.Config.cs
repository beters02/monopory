using System;

public sealed partial class MonopolyLobbyController
{
	private void ApplyHostedConfig()
	{
		var bootstrap = MonopolyMatchBootstrap.Current;
		if ( bootstrap?.HasConfig == true )
			Config = MonopolyMatchBootstrap.CloneConfig( bootstrap.Config );

		HostedMinPlayers = Math.Max( Config?.MinPlayers ?? 1, 1 );
		HostedMaxPlayers = Math.Max( Config?.MaxPlayers ?? HostedMinPlayers, HostedMinPlayers );
		HostedOnlyHostStartsGame = Config?.OnlyHostStartsGame ?? true;
	}

	private MonopolyGameConfig GetGameConfig()
	{
		var config = MonopolyMatchBootstrap.CloneConfig( Config );
		config.MinPlayers = MinPlayers;
		config.MaxPlayers = MaxPlayers;
		config.OnlyHostStartsGame = OnlyHostStartsGame;
		return config;
	}
}
