using Sandbox;

public sealed class MatchBootstrap
{
	public MatchConfig Config { get; set; } = new();
	public bool HasConfig { get; set; }
	public bool AutoStartGame { get; set; }
	public int StartingPlayerCount { get; set; }

	public static MatchBootstrap Current { get; private set; } = new();

	public static void PrepareLobby( MatchConfig config )
	{
		Current = new MatchBootstrap
		{
			Config = CloneConfig( config ),
			HasConfig = true,
			AutoStartGame = false,
			StartingPlayerCount = 0
		};
	}

	public static void PrepareGame( MatchConfig config, int startingPlayerCount )
	{
		Current = new MatchBootstrap
		{
			Config = CloneConfig( config ),
			HasConfig = true,
			AutoStartGame = true,
			StartingPlayerCount = startingPlayerCount
		};
	}

	public static void Clear()
	{
		Current = new MatchBootstrap();
	}

	public static MatchConfig CloneConfig( MatchConfig source )
	{
		source ??= new MatchConfig();

		return new MatchConfig
		{
			MinPlayers = source.MinPlayers,
			MaxPlayers = source.MaxPlayers,
			OnlyHostStartsGame = source.OnlyHostStartsGame,
			LandedUnownedMode = source.LandedUnownedMode,
			StartingMoney = source.StartingMoney,
			DoublesGoesAgain = source.DoublesGoesAgain,
			VacationCash = source.VacationCash,
			DontCollectRentWhileInPrison = source.DontCollectRentWhileInPrison,
			EvenBuild = source.EvenBuild,
			TurnTimeLimitSeconds = source.TurnTimeLimitSeconds
		};
	}
}
