using Sandbox;

public static class StatDiceRollCommand
{
	public const string Name = "stat_diceroll";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string matchId = "" )
	{
		GameCommandManager.RunCommand(
			Name,
			() => LifetimeStatsService.BuildDiceSummary( matchId ),
			connection );
	}
}

public static class StatSpaceLandCommand
{
	public const string Name = "stat_spaceland";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string matchId = "" )
	{
		GameCommandManager.RunCommand(
			Name,
			() => LifetimeStatsService.BuildSpaceSummary( matchId ),
			connection );
	}
}

public static class StatMatchesCommand
{
	public const string Name = "stat_matches";

	[ConCmd( Name )]
	public static void Execute( Connection connection, int limit = 20 )
	{
		GameCommandManager.RunCommand(
			Name,
			() => LifetimeStatsService.BuildMatchList( limit ),
			connection );
	}
}
