using System;
using System.Threading.Tasks;

public sealed class NoOpSteamAchievementBridge : ISteamAchievementBridge
{
	public Task PublishUnlockedAchievementAsync( AchievementDefinition achievement )
	{
		return Task.CompletedTask;
	}

	public Task<IReadOnlySet<string>> GetUnlockedSteamAchievementApiNamesAsync()
	{
		return Task.FromResult<IReadOnlySet<string>>( new HashSet<string>() );
	}
}

public sealed class MockSteamAchievementBridge : ISteamAchievementBridge
{
	private readonly HashSet<string> unlockedApiNames = new( StringComparer.OrdinalIgnoreCase );

	public Task PublishUnlockedAchievementAsync( AchievementDefinition achievement )
	{
		if ( !string.IsNullOrWhiteSpace( achievement?.SteamApiName ) )
			unlockedApiNames.Add( achievement.SteamApiName );

		return Task.CompletedTask;
	}

	public Task<IReadOnlySet<string>> GetUnlockedSteamAchievementApiNamesAsync()
	{
		return Task.FromResult<IReadOnlySet<string>>( unlockedApiNames );
	}
}

public sealed class SteamworksAchievementBridge : ISteamAchievementBridge
{
	public Task PublishUnlockedAchievementAsync( AchievementDefinition achievement )
	{
//do if standalone
		// Backend-only achievements V1 intentionally does not publish to Steam.
		// SteamApiName stays in the catalog so SetAchievement/StoreStats can be added here later.
//do endif
		return Task.CompletedTask;
	}

	public Task<IReadOnlySet<string>> GetUnlockedSteamAchievementApiNamesAsync()
	{
		return Task.FromResult<IReadOnlySet<string>>( new HashSet<string>() );
	}
}
