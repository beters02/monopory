using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder( args );
builder.Services.AddSingleton<IAchievementRepository, InMemoryAchievementRepository>();
builder.Services.AddSingleton<TokenStore>();

var app = builder.Build();

app.MapPost( "/auth/steam/session", async ( SteamSessionRequest request, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( request is null || request.SteamId == 0 || string.IsNullOrWhiteSpace( request.SessionTicket ) )
		return Results.Unauthorized();

	// Production implementation should verify this ticket with Steam's authentication API
	// before issuing the service token. This keeps the API shape stable while Steam setup is pending.
	await repository.EnsurePlayerAsync( request.SteamId, request.DisplayName ?? "" );
	var token = tokens.Create( request.SteamId );
	return Results.Ok( new SteamSessionResponse( token, DateTimeOffset.UtcNow.AddHours( 2 ) ) );
} );

app.MapGet( "/players/{steamId:long}/achievements", async ( long steamId, IAchievementRepository repository ) =>
{
	var state = await repository.GetStateAsync( steamId );
	return Results.Ok( state.AsPublic() );
} );

app.MapGet( "/me/achievements", async ( HttpRequest request, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetSteamId( request, tokens, out var steamId ) )
		return Results.Unauthorized();

	return Results.Ok( await repository.GetStateAsync( steamId ) );
} );

app.MapPost( "/me/achievement-events", async ( HttpRequest request, AchievementEventRecord achievementEvent, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetSteamId( request, tokens, out var steamId ) )
		return Results.Unauthorized();

	if ( achievementEvent is null || achievementEvent.SteamId != steamId || string.IsNullOrWhiteSpace( achievementEvent.Type ) )
		return Results.BadRequest();

	await repository.AppendEventAsync( achievementEvent );
	return Results.Ok( await repository.GetStateAsync( steamId ) );
} );

app.MapGet( "/me/unlocks", async ( HttpRequest request, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetSteamId( request, tokens, out var steamId ) )
		return Results.Unauthorized();

	var state = await repository.GetStateAsync( steamId );
	return Results.Ok( state.UnlockedCosmeticIds );
} );

app.MapPost( "/me/cosmetics/selection", async ( HttpRequest request, CosmeticSelectionRequest selection, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetSteamId( request, tokens, out var steamId ) )
		return Results.Unauthorized();

	var result = await repository.SetCosmeticSelectionAsync( steamId, selection ?? new() );
	return result ? Results.Ok( await repository.GetStateAsync( steamId ) ) : Results.BadRequest();
} );

app.Run();

static bool TryGetSteamId( HttpRequest request, TokenStore tokens, out long steamId )
{
	steamId = 0;
	var auth = request.Headers.Authorization.ToString();
	if ( string.IsNullOrWhiteSpace( auth ) || !auth.StartsWith( "Bearer ", StringComparison.OrdinalIgnoreCase ) )
		return false;

	return tokens.TryResolve( auth["Bearer ".Length..].Trim(), out steamId );
}

public sealed record SteamSessionRequest( long SteamId, string SessionTicket, string DisplayName );
public sealed record SteamSessionResponse( string AccessToken, DateTimeOffset ExpiresAt );

public sealed class AchievementDefinition
{
	public string Id { get; init; } = "";
	public string SteamApiName { get; init; } = "";
	public string Title { get; init; } = "";
	public string Description { get; init; } = "";
	public string CriteriaEventType { get; init; } = "";
	public int Target { get; init; } = 1;
	public string[] RewardIds { get; init; } = [];
}

public sealed class AchievementProgressRecord
{
	public string AchievementId { get; init; } = "";
	public int Current { get; init; }
	public int Target { get; init; }
	public DateTimeOffset? UnlockedAt { get; init; }
}

public sealed class AchievementEventRecord
{
	public string EventId { get; init; } = Guid.NewGuid().ToString( "N" );
	public string SourceMatchId { get; init; } = "";
	public long SteamId { get; init; }
	public string Type { get; init; } = "";
	public int Amount { get; init; } = 1;
	public string Value { get; init; } = "";
	public long OccurredAtUnixSeconds { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public sealed class PlayerAchievementState
{
	public long SteamId { get; init; }
	public IReadOnlyList<AchievementProgressRecord> Achievements { get; init; } = [];
	public IReadOnlyList<string> UnlockedCosmeticIds { get; init; } = [];
	public string SelectedPieceId { get; init; } = "officer_woman";
	public string SelectedDiceSkinId { get; init; } = "classic";

	public PlayerAchievementState AsPublic()
	{
		return new PlayerAchievementState
		{
			SteamId = SteamId,
			Achievements = Achievements,
			UnlockedCosmeticIds = UnlockedCosmeticIds,
			SelectedPieceId = SelectedPieceId,
			SelectedDiceSkinId = SelectedDiceSkinId
		};
	}
}

public sealed class CosmeticSelectionRequest
{
	public string PieceId { get; init; } = "officer_woman";
	public string DiceSkinId { get; init; } = "classic";
}

public interface IAchievementRepository
{
	Task EnsurePlayerAsync( long steamId, string displayName );
	Task<PlayerAchievementState> GetStateAsync( long steamId );
	Task AppendEventAsync( AchievementEventRecord achievementEvent );
	Task<bool> SetCosmeticSelectionAsync( long steamId, CosmeticSelectionRequest selection );
}

public sealed class InMemoryAchievementRepository : IAchievementRepository
{
	private readonly Dictionary<long, PlayerStore> players = new();
	private readonly HashSet<string> eventIds = new( StringComparer.OrdinalIgnoreCase );

	public Task EnsurePlayerAsync( long steamId, string displayName )
	{
		GetOrCreatePlayer( steamId ).DisplayName = displayName ?? "";
		return Task.CompletedTask;
	}

	public Task<PlayerAchievementState> GetStateAsync( long steamId )
	{
		var player = GetOrCreatePlayer( steamId );
		return Task.FromResult( player.ToState() );
	}

	public Task AppendEventAsync( AchievementEventRecord achievementEvent )
	{
		if ( achievementEvent is null || achievementEvent.SteamId == 0 || string.IsNullOrWhiteSpace( achievementEvent.Type ) )
			return Task.CompletedTask;

		lock ( players )
		{
			if ( !eventIds.Add( achievementEvent.EventId ) )
				return Task.CompletedTask;

			var player = GetOrCreatePlayer( achievementEvent.SteamId );
			player.Events.Add( achievementEvent );
			foreach ( var definition in AchievementDefinitions.All )
			{
				if ( !string.Equals( definition.CriteriaEventType, achievementEvent.Type, StringComparison.OrdinalIgnoreCase ) )
					continue;

				var progress = player.Progress[definition.Id];
				if ( progress.UnlockedAt.HasValue )
					continue;

				var next = Math.Min( progress.Current + Math.Max( achievementEvent.Amount, 1 ), Math.Max( definition.Target, 1 ) );
				player.Progress[definition.Id] = progress with
				{
					Current = Math.Max( progress.Current, next ),
					UnlockedAt = next >= definition.Target ? DateTimeOffset.UtcNow : progress.UnlockedAt
				};
			}
		}

		return Task.CompletedTask;
	}

	public Task<bool> SetCosmeticSelectionAsync( long steamId, CosmeticSelectionRequest selection )
	{
		var player = GetOrCreatePlayer( steamId );
		var state = player.ToState();
		var pieceCosmeticId = $"piece.{selection.PieceId}";
		var diceCosmeticId = $"dice.{selection.DiceSkinId}";

		if ( !state.UnlockedCosmeticIds.Contains( pieceCosmeticId, StringComparer.OrdinalIgnoreCase ) )
			return Task.FromResult( false );

		if ( !state.UnlockedCosmeticIds.Contains( diceCosmeticId, StringComparer.OrdinalIgnoreCase ) )
			return Task.FromResult( false );

		player.SelectedPieceId = selection.PieceId;
		player.SelectedDiceSkinId = selection.DiceSkinId;
		return Task.FromResult( true );
	}

	private PlayerStore GetOrCreatePlayer( long steamId )
	{
		lock ( players )
		{
			if ( players.TryGetValue( steamId, out var player ) )
				return player;

			player = new PlayerStore( steamId );
			players[steamId] = player;
			return player;
		}
	}
}

public sealed class PlayerStore
{
	public long SteamId { get; }
	public string DisplayName { get; set; } = "";
	public string SelectedPieceId { get; set; } = "officer_woman";
	public string SelectedDiceSkinId { get; set; } = "classic";
	public List<AchievementEventRecord> Events { get; } = [];
	public Dictionary<string, ProgressValue> Progress { get; } = AchievementDefinitions.All.ToDictionary(
		definition => definition.Id,
		definition => new ProgressValue( 0, Math.Max( definition.Target, 1 ), null ),
		StringComparer.OrdinalIgnoreCase
	);

	public PlayerStore( long steamId )
	{
		SteamId = steamId;
	}

	public PlayerAchievementState ToState()
	{
		var unlockedAchievements = Progress
			.Where( entry => entry.Value.UnlockedAt.HasValue )
			.Select( entry => entry.Key )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );

		var unlockedCosmetics = CosmeticDefinitions.All
			.Where( cosmetic => string.IsNullOrWhiteSpace( cosmetic.RequiredAchievementId ) || unlockedAchievements.Contains( cosmetic.RequiredAchievementId ) )
			.Select( cosmetic => cosmetic.Id )
			.ToList();

		return new PlayerAchievementState
		{
			SteamId = SteamId,
			Achievements = Progress.Select( entry => new AchievementProgressRecord
			{
				AchievementId = entry.Key,
				Current = entry.Value.Current,
				Target = entry.Value.Target,
				UnlockedAt = entry.Value.UnlockedAt
			} ).ToList(),
			UnlockedCosmeticIds = unlockedCosmetics,
			SelectedPieceId = SelectedPieceId,
			SelectedDiceSkinId = SelectedDiceSkinId
		};
	}
}

public sealed record ProgressValue( int Current, int Target, DateTimeOffset? UnlockedAt );
public sealed record CosmeticDefinition( string Id, string RequiredAchievementId );

public static class AchievementDefinitions
{
	public static readonly IReadOnlyList<AchievementDefinition> All =
	[
		new() { Id = "first_win", SteamApiName = "ACH_FIRST_WIN", Title = "First Victory", Description = "Win your first match.", CriteriaEventType = "match_won", Target = 1, RewardIds = ["piece.detective_man"] },
		new() { Id = "property_collector", SteamApiName = "ACH_PROPERTY_COLLECTOR", Title = "Property Collector", Description = "Acquire 10 properties across matches.", CriteriaEventType = "property_acquired", Target = 10, RewardIds = ["dice.gold"] },
		new() { Id = "doubles_trouble", SteamApiName = "ACH_DOUBLES_TROUBLE", Title = "Doubles Trouble", Description = "Roll doubles 10 times.", CriteriaEventType = "rolled_doubles", Target = 10 },
		new() { Id = "snake_eyes", SteamApiName = "ACH_SNAKE_EYES", Title = "Snake Eyes", Description = "Roll double ones.", CriteriaEventType = "rolled_snake_eyes", Target = 1, RewardIds = ["dice.midnight"] },
		new() { Id = "jailbird", SteamApiName = "ACH_JAILBIRD", Title = "Jailbird", Description = "Get sent to jail 5 times.", CriteriaEventType = "sent_to_jail", Target = 5 }
	];
}

public static class CosmeticDefinitions
{
	public static readonly IReadOnlyList<CosmeticDefinition> All =
	[
		new( "piece.officer_woman", "" ),
		new( "piece.detective_man", "first_win" ),
		new( "dice.classic", "" ),
		new( "dice.gold", "property_collector" ),
		new( "dice.midnight", "snake_eyes" )
	];
}

public sealed class TokenStore
{
	private readonly Dictionary<string, (long SteamId, DateTimeOffset ExpiresAt)> tokens = new();

	public string Create( long steamId )
	{
		var bytes = RandomNumberGenerator.GetBytes( 32 );
		var token = Convert.ToBase64String( bytes );
		tokens[token] = (steamId, DateTimeOffset.UtcNow.AddHours( 2 ));
		return token;
	}

	public bool TryResolve( string token, out long steamId )
	{
		steamId = 0;
		if ( string.IsNullOrWhiteSpace( token ) || !tokens.TryGetValue( token, out var session ) )
			return false;

		if ( session.ExpiresAt < DateTimeOffset.UtcNow )
		{
			tokens.Remove( token );
			return false;
		}

		steamId = session.SteamId;
		return true;
	}
}
