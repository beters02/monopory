using Sandbox;

public sealed class MonopolyMatchBootstrap
{
	public MonopolyGameConfig Config { get; set; } = new();
	public bool AutoStartGame { get; set; }
	public int StartingPlayerCount { get; set; }

	public static MonopolyMatchBootstrap Current { get; private set; } = new();

	public static void PrepareGame( MonopolyGameConfig config, int startingPlayerCount )
	{
		Current = new MonopolyMatchBootstrap
		{
			Config = CloneConfig( config ),
			AutoStartGame = true,
			StartingPlayerCount = startingPlayerCount
		};
	}

	public static void Clear()
	{
		Current = new MonopolyMatchBootstrap();
	}

	private static MonopolyGameConfig CloneConfig( MonopolyGameConfig source )
	{
		source ??= new MonopolyGameConfig();

		return new MonopolyGameConfig
		{
			MinPlayers = source.MinPlayers,
			MaxPlayers = source.MaxPlayers,
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
