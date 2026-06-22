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
#if STANDALONE
		// Intentionally left behind the bridge until Steam achievements are created in Steamworks.
		// The internal achievement id already maps to SteamApiName for a future SetAchievement/StoreStats call.
#endif
		return Task.CompletedTask;
	}

	public Task<IReadOnlySet<string>> GetUnlockedSteamAchievementApiNamesAsync()
	{
		return Task.FromResult<IReadOnlySet<string>>( new HashSet<string>() );
	}
}
