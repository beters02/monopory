#if STANDALONE
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public sealed class HttpAchievementService : IAchievementService, ICosmeticUnlockService, IDisposable
{
	private readonly HttpClient http;
	private readonly Dictionary<long, PlayerAchievementState> cachedStates = new();
	private readonly string bearerToken;

	public HttpAchievementService( string baseUrl, string bearerToken )
	{
		http = new HttpClient
		{
			BaseAddress = new Uri( baseUrl.TrimEnd( '/' ) + "/" )
		};

		this.bearerToken = bearerToken ?? "";
	}

	public async Task<PlayerAchievementState> GetMyStateAsync()
	{
		var state = await SendAsync<PlayerAchievementState>( HttpMethod.Get, "me/achievements" );
		CacheState( state );
		return state;
	}

	public async Task<PlayerAchievementState> GetPublicStateAsync( long steamId )
	{
		var state = await SendAsync<PlayerAchievementState>( HttpMethod.Get, $"players/{steamId}/achievements", false );
		CacheState( state );
		return state;
	}

	public async Task ReportEventAsync( AchievementEvent achievementEvent )
	{
		var state = await SendAsync<PlayerAchievementState>( HttpMethod.Post, "me/achievement-events", true, achievementEvent );
		CacheState( state );
	}

	public async Task<bool> CanUseCosmeticAsync( long steamId, string cosmeticId )
	{
		var state = GetCachedState( steamId );
		if ( state.SteamId == 0 || state.SteamId != steamId )
			state = await GetPublicStateAsync( steamId );

		return StateCanUseCosmetic( state, cosmeticId );
	}

	public bool CanUseCosmetic( long steamId, string cosmeticId )
	{
		return StateCanUseCosmetic( GetCachedState( steamId ), cosmeticId );
	}

	public IReadOnlyList<CosmeticDefinition> GetAvailableCosmetics( long steamId )
	{
		return CosmeticCatalog.All.Where( cosmetic => CanUseCosmetic( steamId, cosmetic.Id ) ).ToList();
	}

	public PlayerAchievementState GetCachedState( long steamId )
	{
		if ( steamId != 0 && cachedStates.TryGetValue( steamId, out var state ) )
			return state;

		return new PlayerAchievementState
		{
			SteamId = steamId,
			Achievements = AchievementCatalog.All.Select( definition => new AchievementProgress
			{
				AchievementId = definition.Id,
				Current = 0,
				Target = Math.Max( definition.Criteria?.Target ?? 1, 1 )
			} ).ToList(),
			UnlockedCosmeticIds = CosmeticCatalog.All
				.Where( cosmetic => string.IsNullOrWhiteSpace( cosmetic.RequiredAchievementId ) )
				.Select( cosmetic => cosmetic.Id )
				.ToList()
		};
	}

	public void Dispose()
	{
		http?.Dispose();
	}

	private async Task<T> SendAsync<T>( HttpMethod method, string path, bool includeAuth = true, object body = null )
	{
		using var request = new HttpRequestMessage( method, path );
		if ( includeAuth && !string.IsNullOrWhiteSpace( bearerToken ) )
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( "Bearer", bearerToken );

		if ( body is not null )
			request.Content = JsonContent.Create( body );

		using var response = await http.SendAsync( request );
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<T>();
	}

	private void CacheState( PlayerAchievementState state )
	{
		if ( state is null || state.SteamId == 0 )
			return;

		cachedStates[state.SteamId] = state;
	}

	private static bool StateCanUseCosmetic( PlayerAchievementState state, string cosmeticId )
	{
		var cosmetic = CosmeticCatalog.GetById( cosmeticId );
		if ( cosmetic is null )
			return false;

		if ( string.IsNullOrWhiteSpace( cosmetic.RequiredAchievementId ) )
			return true;

		return state?.UnlockedCosmeticIds?.Contains( cosmetic.Id, StringComparer.OrdinalIgnoreCase ) == true;
	}
}
#endif
