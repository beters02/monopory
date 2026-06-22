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
	private readonly long authenticatedPlayerId;

	public HttpAchievementService( string baseUrl, string bearerToken, long authenticatedPlayerId = 0 )
	{
		http = new HttpClient
		{
			BaseAddress = new Uri( baseUrl.TrimEnd( '/' ) + "/" )
		};

		this.bearerToken = bearerToken ?? "";
		this.authenticatedPlayerId = authenticatedPlayerId;
	}

	public static async Task<DeviceSessionResponse> AuthenticateDeviceAsync( string baseUrl, string deviceId, string secret, string displayName )
	{
		using var client = new HttpClient
		{
			BaseAddress = new Uri( baseUrl.TrimEnd( '/' ) + "/" )
		};

		using var response = await client.PostAsJsonAsync( "auth/device/session", new DeviceSessionRequest
		{
			DeviceId = deviceId ?? "",
			Secret = secret ?? "",
			DisplayName = displayName ?? ""
		} );
		await EnsureAuthSuccessAsync( response, "device" );

		return await response.Content.ReadFromJsonAsync<DeviceSessionResponse>();
	}

	public static async Task<SboxSessionResponse> AuthenticateSboxAsync( string baseUrl, long steamId, string token, string displayName )
	{
		using var client = new HttpClient
		{
			BaseAddress = new Uri( baseUrl.TrimEnd( '/' ) + "/" )
		};

		using var response = await client.PostAsJsonAsync( "auth/sbox/session", new SboxSessionRequest
		{
			SteamId = steamId,
			Token = token ?? "",
			DisplayName = displayName ?? ""
		} );
		await EnsureAuthSuccessAsync( response, "sbox" );

		return await response.Content.ReadFromJsonAsync<SboxSessionResponse>();
	}

	public static async Task<SteamworksSessionResponse> AuthenticateSteamworksAsync( string baseUrl, long steamId, string ticket, string displayName )
	{
		using var client = new HttpClient
		{
			BaseAddress = new Uri( baseUrl.TrimEnd( '/' ) + "/" )
		};

		using var response = await client.PostAsJsonAsync( "auth/steamworks/session", new SteamworksSessionRequest
		{
			SteamId = steamId,
			Ticket = ticket ?? "",
			DisplayName = displayName ?? ""
		} );
		await EnsureAuthSuccessAsync( response, "steamworks" );

		return await response.Content.ReadFromJsonAsync<SteamworksSessionResponse>();
	}

	private static async Task EnsureAuthSuccessAsync( HttpResponseMessage response, string authKind )
	{
		if ( response.IsSuccessStatusCode )
			return;

		var body = await response.Content.ReadAsStringAsync();
		throw new HttpRequestException( $"Achievements {authKind} auth failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}" );
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
		var state = await SendAsync<PlayerAchievementState>( HttpMethod.Post, "me/achievement-events", true, RewriteEventForAuthenticatedPlayer( achievementEvent ) );
		CacheState( state );
	}

	public async Task<bool> CanUseCosmeticAsync( long steamId, string cosmeticId )
	{
		var playerId = ResolvePlayerId( steamId );
		var state = GetCachedState( playerId );
		if ( state.SteamId == 0 || state.SteamId != playerId )
			state = authenticatedPlayerId != 0 ? await GetMyStateAsync() : await GetPublicStateAsync( playerId );

		return StateCanUseCosmetic( state, cosmeticId );
	}

	public bool CanUseCosmetic( long steamId, string cosmeticId )
	{
		return StateCanUseCosmetic( GetCachedState( ResolvePlayerId( steamId ) ), cosmeticId );
	}

	public IReadOnlyList<CosmeticDefinition> GetAvailableCosmetics( long steamId )
	{
		return CosmeticCatalog.All.Where( cosmetic => CanUseCosmetic( steamId, cosmetic.Id ) ).ToList();
	}

	public PlayerAchievementState GetCachedState( long steamId )
	{
		steamId = ResolvePlayerId( steamId );
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

	private long ResolvePlayerId( long requestedPlayerId )
	{
		return authenticatedPlayerId != 0 ? authenticatedPlayerId : requestedPlayerId;
	}

	private AchievementEvent RewriteEventForAuthenticatedPlayer( AchievementEvent achievementEvent )
	{
		if ( achievementEvent is null || authenticatedPlayerId == 0 )
			return achievementEvent;

		return new AchievementEvent
		{
			EventId = achievementEvent.EventId,
			SteamId = authenticatedPlayerId,
			Type = achievementEvent.Type,
			Amount = achievementEvent.Amount,
			Value = achievementEvent.Value,
			SourceMatchId = achievementEvent.SourceMatchId,
			OccurredAtUnixSeconds = achievementEvent.OccurredAtUnixSeconds
		};
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
