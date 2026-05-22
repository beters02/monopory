using Sandbox;

public sealed class MatchBootstrap
{
	public MatchConfig Config { get; set; } = new();
	public bool HasConfig { get; set; }
	public bool AutoStartGame { get; set; }
	public int StartingPlayerCount { get; set; }
	public List<LobbyPlayer> StartingPlayers { get; set; } = new();

	public static MatchBootstrap Current { get; private set; } = new();

	public static void PrepareLobby( MatchConfig config )
	{
		Current = new MatchBootstrap
		{
			Config = CloneConfig( config ),
			HasConfig = true,
			AutoStartGame = false,
			StartingPlayerCount = 0,
			StartingPlayers = new()
		};
	}

	public static void PrepareGame( MatchConfig config, IReadOnlyList<LobbyPlayer> startingPlayers )
	{
		var players = ClonePlayers( startingPlayers );

		Current = new MatchBootstrap
		{
			Config = CloneConfig( config ),
			HasConfig = true,
			AutoStartGame = true,
			StartingPlayerCount = players.Count,
			StartingPlayers = players
		};
	}

	public static void Clear()
	{
		Current = new MatchBootstrap();
	}

	public static MatchConfig CloneConfig( MatchConfig source )
	{
		return MatchConfigSchema.Clone( source );
	}

	private static List<LobbyPlayer> ClonePlayers( IReadOnlyList<LobbyPlayer> players )
	{
		if ( players is null )
			return new();

		return players
			.Where( player => player is not null && player.OwnerId != 0 )
			.Select( player => new LobbyPlayer
			{
				OwnerId = player.OwnerId,
				Name = player.Name,
				IsReady = player.IsReady,
				IsLocal = player.IsLocal
			} )
			.ToList();
	}
}
