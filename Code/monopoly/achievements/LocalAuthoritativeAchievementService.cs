using System;
using System.Threading.Tasks;

public sealed class LocalAuthoritativeAchievementService : IAchievementService, ICosmeticUnlockService
{
	private readonly Dictionary<long, Dictionary<string, AchievementProgress>> progressBySteamId = new();
	private readonly HashSet<string> processedEventIds = new( StringComparer.OrdinalIgnoreCase );
	private readonly HashSet<long> loadedSteamStateForSteamIds = new();
	private readonly ISteamAchievementBridge steamBridge;

	public LocalAuthoritativeAchievementService( ISteamAchievementBridge steamBridge = null )
	{
		this.steamBridge = steamBridge ?? new NoOpSteamAchievementBridge();
	}

	public async Task<PlayerAchievementState> GetMyStateAsync()
	{
		var steamId = GetLocalSteamId() ?? 0;
		if ( steamId != 0 )
			await LoadSteamStateAsync( steamId );

		return GetCachedState( steamId );
	}

	public Task<PlayerAchievementState> GetPublicStateAsync( long steamId )
	{
		var state = GetCachedState( steamId );
		var publicProgress = state.Achievements
			.Where( progress =>
			{
				var definition = AchievementCatalog.GetById( progress.AchievementId );
				return definition is not null && (definition.Visibility == AchievementVisibility.Public || progress.IsUnlocked);
			} )
			.ToList();

		return Task.FromResult( new PlayerAchievementState
		{
			SteamId = state.SteamId,
			Achievements = publicProgress,
			UnlockedCosmeticIds = state.UnlockedCosmeticIds,
			SelectedPieceId = state.SelectedPieceId,
			SelectedDiceSkinId = state.SelectedDiceSkinId
		} );
	}

	public Task ReportEventAsync( AchievementEvent achievementEvent )
	{
		if ( achievementEvent is null || achievementEvent.SteamId == 0 || string.IsNullOrWhiteSpace( achievementEvent.Type ) )
			return Task.CompletedTask;

		if ( !processedEventIds.Add( achievementEvent.EventId ) )
			return Task.CompletedTask;

		var progress = GetOrCreateProgress( achievementEvent.SteamId );
		foreach ( var definition in AchievementCatalog.All )
		{
			if ( definition?.Criteria is null )
				continue;

			if ( !string.Equals( definition.Criteria.EventType, achievementEvent.Type, StringComparison.OrdinalIgnoreCase ) )
				continue;

			var achievementProgress = progress[definition.Id];
			if ( achievementProgress.IsUnlocked )
				continue;

			var previous = achievementProgress.Current;
			achievementProgress.Current = Math.Clamp(
				CalculateNextProgress( definition, achievementProgress.Current, achievementEvent ),
				0,
				Math.Max( definition.Criteria.Target, 1 )
			);

			if ( achievementProgress.Current < previous )
				achievementProgress.Current = previous;

			if ( achievementProgress.Current >= achievementProgress.Target )
			{
				achievementProgress.UnlockedAt = DateTimeOffset.UtcNow;
				_ = steamBridge.PublishUnlockedAchievementAsync( definition );
			}
		}

		return Task.CompletedTask;
	}

	public Task<bool> CanUseCosmeticAsync( long steamId, string cosmeticId )
	{
		return Task.FromResult( CanUseCosmetic( steamId, cosmeticId ) );
	}

	public bool CanUseCosmetic( long steamId, string cosmeticId )
	{
		var cosmetic = CosmeticCatalog.GetById( cosmeticId );
		if ( cosmetic is null )
			return false;

		if ( string.IsNullOrWhiteSpace( cosmetic.RequiredAchievementId ) )
			return true;

		if ( steamId == 0 )
			return false;

		var progress = GetOrCreateProgress( steamId );
		return progress.TryGetValue( cosmetic.RequiredAchievementId, out var achievementProgress ) && achievementProgress.IsUnlocked;
	}

	public IReadOnlyList<CosmeticDefinition> GetAvailableCosmetics( long steamId )
	{
		return CosmeticCatalog.All.Where( cosmetic => CanUseCosmetic( steamId, cosmetic.Id ) ).ToList();
	}

	public PlayerAchievementState GetCachedState( long steamId )
	{
		var progress = GetOrCreateProgress( steamId );
		var unlockedCosmeticIds = CosmeticCatalog.All
			.Where( cosmetic => CanUseCosmetic( steamId, cosmetic.Id ) )
			.Select( cosmetic => cosmetic.Id )
			.ToList();

		return new PlayerAchievementState
		{
			SteamId = steamId,
			Achievements = progress.Values
				.Select( CloneProgress )
				.OrderBy( achievementProgress => achievementProgress.AchievementId )
				.ToList(),
			UnlockedCosmeticIds = unlockedCosmeticIds,
			SelectedPieceId = PieceCatalog.DefaultPieceId,
			SelectedDiceSkinId = DiceSkinCatalog.DefaultDiceSkinId
		};
	}

	private Dictionary<string, AchievementProgress> GetOrCreateProgress( long steamId )
	{
		if ( !progressBySteamId.TryGetValue( steamId, out var progress ) )
		{
			progress = AchievementCatalog.All.ToDictionary(
				definition => definition.Id,
				definition => new AchievementProgress
				{
					AchievementId = definition.Id,
					Target = Math.Max( definition.Criteria?.Target ?? 1, 1 )
				},
				StringComparer.OrdinalIgnoreCase
			);

			progressBySteamId[steamId] = progress;
		}

		return progress;
	}

	private async Task LoadSteamStateAsync( long steamId )
	{
		if ( !loadedSteamStateForSteamIds.Add( steamId ) )
			return;

		var unlockedApiNames = await steamBridge.GetUnlockedSteamAchievementApiNamesAsync();
		if ( unlockedApiNames is null || unlockedApiNames.Count == 0 )
			return;

		var progress = GetOrCreateProgress( steamId );
		foreach ( var definition in AchievementCatalog.All )
		{
			if ( string.IsNullOrWhiteSpace( definition.SteamApiName ) || !unlockedApiNames.Contains( definition.SteamApiName ) )
				continue;

			var achievementProgress = progress[definition.Id];
			achievementProgress.Current = Math.Max( achievementProgress.Current, achievementProgress.Target );
			achievementProgress.UnlockedAt ??= DateTimeOffset.UtcNow;
		}
	}

	private static int CalculateNextProgress( AchievementDefinition definition, int current, AchievementEvent achievementEvent )
	{
		return definition.Criteria.Kind switch
		{
			AchievementCriteriaKind.EventValueAtLeast when string.Equals( achievementEvent.Value, definition.Criteria.RequiredValue, StringComparison.OrdinalIgnoreCase ) => definition.Criteria.Target,
			AchievementCriteriaKind.EventValueAtLeast => current,
			_ => current + Math.Max( achievementEvent.Amount, 1 )
		};
	}

	private static AchievementProgress CloneProgress( AchievementProgress progress )
	{
		return new AchievementProgress
		{
			AchievementId = progress.AchievementId,
			Current = progress.Current,
			Target = progress.Target,
			UnlockedAt = progress.UnlockedAt
		};
	}

	private static long? GetLocalSteamId()
	{
		try
		{
			return Connection.Local?.SteamId;
		}
		catch
		{
			return null;
		}
	}
}
