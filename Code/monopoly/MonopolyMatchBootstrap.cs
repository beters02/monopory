using Sandbox;

public sealed class MonopolyMatchBootstrap
{
	public MonopolyGameConfig Config { get; set; } = new();
	public bool HasConfig { get; set; }
	public bool AutoStartGame { get; set; }
	public int StartingPlayerCount { get; set; }

	public static MonopolyMatchBootstrap Current { get; private set; } = new();

	public static void PrepareLobby( MonopolyGameConfig config )
	{
		Current = new MonopolyMatchBootstrap
		{
			Config = CloneConfig( config ),
			HasConfig = true,
			AutoStartGame = false,
			StartingPlayerCount = 0
		};
	}

	public static void PrepareGame( MonopolyGameConfig config, int startingPlayerCount )
	{
		Current = new MonopolyMatchBootstrap
		{
			Config = CloneConfig( config ),
			HasConfig = true,
			AutoStartGame = true,
			StartingPlayerCount = startingPlayerCount
		};
	}

	public static void Clear()
	{
		Current = new MonopolyMatchBootstrap();
	}

	public static MonopolyGameConfig CloneConfig( MonopolyGameConfig source )
	{
		source ??= new MonopolyGameConfig();

		return new MonopolyGameConfig
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
