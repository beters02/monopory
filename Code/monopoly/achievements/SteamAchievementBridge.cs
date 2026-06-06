using System;
using System.Threading.Tasks;
using Forkbox.Steamworks;

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
		if ( achievement is null || string.IsNullOrWhiteSpace( achievement.SteamApiName ) )
			return Task.CompletedTask;

//do if standalone
		try
		{
			if ( !SteamUserStats.TryGetAchievement( achievement.SteamApiName, out var steamAchievement ) )
			{
				Log.Warning( $"Steam achievement API name was not found: {achievement.SteamApiName}" );
				return Task.CompletedTask;
			}

			if ( steamAchievement.State )
				return Task.CompletedTask;

			if ( !SteamUserStats.TriggerAchievement( achievement.SteamApiName ) )
			{
				Log.Warning( $"Steam achievement trigger failed for {achievement.SteamApiName}." );
				return Task.CompletedTask;
			}

			if ( SteamUserStats.StoreStats() )
			{
				Log.Info( $"Published Steam achievement unlock: {achievement.SteamApiName}" );
			}
			else
			{
				Log.Warning( $"Steam achievement StoreStats failed for {achievement.SteamApiName}." );
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to publish Steam achievement {achievement.SteamApiName}: {exception.Message}" );
		}
//do endif
		return Task.CompletedTask;
	}

	public Task<IReadOnlySet<string>> GetUnlockedSteamAchievementApiNamesAsync()
	{
		var unlockedApiNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

//do if standalone
		try
		{
			foreach ( var achievement in SteamUserStats.Achievements )
			{
				if ( achievement.State && !string.IsNullOrWhiteSpace( achievement.Identifier ) )
					unlockedApiNames.Add( achievement.Identifier );
			}
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to read Steam achievement state: {exception.Message}" );
		}
//do endif
		return Task.FromResult<IReadOnlySet<string>>( unlockedApiNames );
	}
}
