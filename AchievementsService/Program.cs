using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

var builder = WebApplication.CreateBuilder( args );
var postgresConnectionString =
	builder.Configuration.GetConnectionString( "AchievementsPostgres" ) ??
	builder.Configuration["Achievements:PostgresConnectionString"] ??
	builder.Configuration["ACHIEVEMENTS_POSTGRES"];

if ( string.IsNullOrWhiteSpace( postgresConnectionString ) )
{
	builder.Services.AddSingleton<IAchievementRepository, InMemoryAchievementRepository>();
}
else
{
	builder.Services.AddSingleton( _ => new NpgsqlDataSourceBuilder( postgresConnectionString ).Build() );
	builder.Services.AddSingleton<InMemoryAchievementRepository>();
	builder.Services.AddSingleton<PostgresAchievementRepository>();
	builder.Services.AddSingleton<IAchievementRepository, FallbackAchievementRepository>();
}

builder.Services.AddHttpClient<ISteamAuthTicketVerifier, SteamWebApiAuthTicketVerifier>();
builder.Services.AddSingleton<TokenStore>();

var app = builder.Build();
if ( app.Services.GetRequiredService<IAchievementRepository>() is IInitializableRepository initializableRepository )
	await initializableRepository.InitializeAsync();

app.MapPost( "/auth/steam/session", async ( SteamSessionRequest request, IAchievementRepository repository, TokenStore tokens, ISteamAuthTicketVerifier authVerifier ) =>
{
	if ( request is null || request.SteamId == 0 || string.IsNullOrWhiteSpace( request.AuthToken ) )
		return Results.Unauthorized();

	var verification = await authVerifier.VerifyAsync( request.SteamId, request.AuthToken );
	if ( !verification.IsValid )
		return Results.Unauthorized();

	await repository.EnsurePlayerAsync( verification.SteamId, request.DisplayName ?? verification.DisplayName ?? "" );
	var token = tokens.Create( verification.SteamId );
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

public sealed record SteamSessionRequest( long SteamId, string Token, string SessionTicket, string DisplayName )
{
	public string AuthToken => !string.IsNullOrWhiteSpace( Token ) ? Token : SessionTicket;
}
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

public interface IInitializableRepository
{
	Task InitializeAsync();
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

public sealed class PostgresAchievementRepository : IAchievementRepository, IInitializableRepository
{
	private readonly NpgsqlDataSource dataSource;

	public PostgresAchievementRepository( NpgsqlDataSource dataSource )
	{
		this.dataSource = dataSource;
	}

	public async Task InitializeAsync()
	{
		await using var command = dataSource.CreateCommand( SchemaSql.Text );
		await command.ExecuteNonQueryAsync();
	}

	public async Task EnsurePlayerAsync( long steamId, string displayName )
	{
		await using var batch = dataSource.CreateBatch();
		batch.BatchCommands.Add( new NpgsqlBatchCommand(
			"""
			insert into players (steam_id, display_name, created_at, last_seen_at)
			values ($1, $2, now(), now())
			on conflict (steam_id)
			do update set display_name = excluded.display_name, last_seen_at = now();
			""" )
		{
			Parameters =
			{
				new() { Value = steamId },
				new() { Value = displayName ?? "" }
			}
		} );

		foreach ( var definition in AchievementDefinitions.All )
		{
			batch.BatchCommands.Add( new NpgsqlBatchCommand(
				"""
				insert into achievement_progress (steam_id, achievement_id, current_value, target_value)
				values ($1, $2, 0, $3)
				on conflict (steam_id, achievement_id) do nothing;
				""" )
			{
				Parameters =
				{
					new() { Value = steamId },
					new() { Value = definition.Id },
					new() { Value = Math.Max( definition.Target, 1 ) }
				}
			} );
		}

		batch.BatchCommands.Add( new NpgsqlBatchCommand(
			"""
			insert into cosmetic_selections (steam_id)
			values ($1)
			on conflict (steam_id) do nothing;
			""" )
		{
			Parameters = { new() { Value = steamId } }
		} );

		await batch.ExecuteNonQueryAsync();
	}

	public async Task<PlayerAchievementState> GetStateAsync( long steamId )
	{
		await EnsurePlayerAsync( steamId, "" );

		var progress = new List<AchievementProgressRecord>();
		await using ( var progressCommand = dataSource.CreateCommand(
			"""
			select achievement_id, current_value, target_value, unlocked_at
			from achievement_progress
			where steam_id = $1
			order by achievement_id;
			""" ) )
		{
			progressCommand.Parameters.Add( new() { Value = steamId } );
			await using var reader = await progressCommand.ExecuteReaderAsync();
			while ( await reader.ReadAsync() )
			{
				progress.Add( new AchievementProgressRecord
				{
					AchievementId = reader.GetString( 0 ),
					Current = reader.GetInt32( 1 ),
					Target = reader.GetInt32( 2 ),
					UnlockedAt = reader.IsDBNull( 3 ) ? null : new DateTimeOffset( DateTime.SpecifyKind( reader.GetDateTime( 3 ), DateTimeKind.Utc ) )
				} );
			}
		}

		var selectedPieceId = "officer_woman";
		var selectedDiceSkinId = "classic";
		await using ( var selectionCommand = dataSource.CreateCommand(
			"""
			select selected_piece_id, selected_dice_skin_id
			from cosmetic_selections
			where steam_id = $1;
			""" ) )
		{
			selectionCommand.Parameters.Add( new() { Value = steamId } );
			await using var reader = await selectionCommand.ExecuteReaderAsync();
			if ( await reader.ReadAsync() )
			{
				selectedPieceId = reader.GetString( 0 );
				selectedDiceSkinId = reader.GetString( 1 );
			}
		}

		var unlockedAchievements = progress
			.Where( achievementProgress => achievementProgress.UnlockedAt.HasValue )
			.Select( achievementProgress => achievementProgress.AchievementId )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );
		var unlockedCosmetics = CosmeticDefinitions.All
			.Where( cosmetic => string.IsNullOrWhiteSpace( cosmetic.RequiredAchievementId ) || unlockedAchievements.Contains( cosmetic.RequiredAchievementId ) )
			.Select( cosmetic => cosmetic.Id )
			.ToList();

		return new PlayerAchievementState
		{
			SteamId = steamId,
			Achievements = progress,
			UnlockedCosmeticIds = unlockedCosmetics,
			SelectedPieceId = selectedPieceId,
			SelectedDiceSkinId = selectedDiceSkinId
		};
	}

	public async Task AppendEventAsync( AchievementEventRecord achievementEvent )
	{
		if ( achievementEvent is null || achievementEvent.SteamId == 0 || string.IsNullOrWhiteSpace( achievementEvent.Type ) )
			return;

		await EnsurePlayerAsync( achievementEvent.SteamId, "" );
		await using var connection = await dataSource.OpenConnectionAsync();
		await using var transaction = await connection.BeginTransactionAsync();

		await using ( var eventCommand = new NpgsqlCommand(
			"""
			insert into achievement_events (event_id, steam_id, source_match_id, event_type, amount, event_value, payload_hash, occurred_at)
			values ($1, $2, $3, $4, $5, $6, $7, to_timestamp($8::double precision))
			on conflict (event_id) do nothing;
			""",
			connection,
			transaction ) )
		{
			eventCommand.Parameters.Add( new() { Value = achievementEvent.EventId } );
			eventCommand.Parameters.Add( new() { Value = achievementEvent.SteamId } );
			eventCommand.Parameters.Add( new() { Value = achievementEvent.SourceMatchId ?? "" } );
			eventCommand.Parameters.Add( new() { Value = achievementEvent.Type } );
			eventCommand.Parameters.Add( new() { Value = Math.Max( achievementEvent.Amount, 1 ) } );
			eventCommand.Parameters.Add( new() { Value = achievementEvent.Value ?? "" } );
			eventCommand.Parameters.Add( new() { Value = ComputeEventHash( achievementEvent ) } );
			eventCommand.Parameters.Add( new() { Value = achievementEvent.OccurredAtUnixSeconds <= 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : achievementEvent.OccurredAtUnixSeconds } );

			if ( await eventCommand.ExecuteNonQueryAsync() == 0 )
			{
				await transaction.CommitAsync();
				return;
			}
		}

		foreach ( var definition in AchievementDefinitions.All )
		{
			if ( !string.Equals( definition.CriteriaEventType, achievementEvent.Type, StringComparison.OrdinalIgnoreCase ) )
				continue;

			await using var progressCommand = new NpgsqlCommand(
				"""
				update achievement_progress
				set current_value = least(target_value, greatest(current_value, current_value + $3)),
					unlocked_at = case
						when unlocked_at is not null then unlocked_at
						when least(target_value, greatest(current_value, current_value + $3)) >= target_value then now()
						else null
					end,
					updated_at = now()
				where steam_id = $1 and achievement_id = $2;
				""",
				connection,
				transaction );
			progressCommand.Parameters.Add( new() { Value = achievementEvent.SteamId } );
			progressCommand.Parameters.Add( new() { Value = definition.Id } );
			progressCommand.Parameters.Add( new() { Value = Math.Max( achievementEvent.Amount, 1 ) } );
			await progressCommand.ExecuteNonQueryAsync();
		}

		await transaction.CommitAsync();
	}

	public async Task<bool> SetCosmeticSelectionAsync( long steamId, CosmeticSelectionRequest selection )
	{
		await EnsurePlayerAsync( steamId, "" );
		var state = await GetStateAsync( steamId );
		var pieceCosmeticId = $"piece.{selection.PieceId}";
		var diceCosmeticId = $"dice.{selection.DiceSkinId}";

		if ( !state.UnlockedCosmeticIds.Contains( pieceCosmeticId, StringComparer.OrdinalIgnoreCase ) )
			return false;

		if ( !state.UnlockedCosmeticIds.Contains( diceCosmeticId, StringComparer.OrdinalIgnoreCase ) )
			return false;

		await using var command = dataSource.CreateCommand(
			"""
			insert into cosmetic_selections (steam_id, selected_piece_id, selected_dice_skin_id, updated_at)
			values ($1, $2, $3, now())
			on conflict (steam_id)
			do update set selected_piece_id = excluded.selected_piece_id,
				selected_dice_skin_id = excluded.selected_dice_skin_id,
				updated_at = now();
			""" );
		command.Parameters.Add( new() { Value = steamId } );
		command.Parameters.Add( new() { Value = selection.PieceId } );
		command.Parameters.Add( new() { Value = selection.DiceSkinId } );
		await command.ExecuteNonQueryAsync();
		return true;
	}

	private static string ComputeEventHash( AchievementEventRecord achievementEvent )
	{
		var input = $"{achievementEvent.EventId}|{achievementEvent.SteamId}|{achievementEvent.Type}|{achievementEvent.Amount}|{achievementEvent.Value}|{achievementEvent.SourceMatchId}";
		return Convert.ToHexString( SHA256.HashData( System.Text.Encoding.UTF8.GetBytes( input ) ) );
	}
}

public sealed class FallbackAchievementRepository : IAchievementRepository, IInitializableRepository
{
	private readonly PostgresAchievementRepository postgres;
	private readonly InMemoryAchievementRepository memory;
	private readonly ILogger<FallbackAchievementRepository> logger;
	private bool useMemoryFallback;

	public FallbackAchievementRepository( PostgresAchievementRepository postgres, InMemoryAchievementRepository memory, ILogger<FallbackAchievementRepository> logger )
	{
		this.postgres = postgres;
		this.memory = memory;
		this.logger = logger;
	}

	public async Task InitializeAsync()
	{
		try
		{
			await postgres.InitializeAsync();
			logger.LogInformation( "Achievements repository initialized with Postgres." );
		}
		catch ( Exception exception ) when ( IsPostgresConnectionFailure( exception ) )
		{
			useMemoryFallback = true;
			logger.LogWarning( exception, "Postgres achievements repository is unavailable. Falling back to in-memory storage for this process." );
		}
	}

	public Task EnsurePlayerAsync( long steamId, string displayName )
	{
		return Active.EnsurePlayerAsync( steamId, displayName );
	}

	public Task<PlayerAchievementState> GetStateAsync( long steamId )
	{
		return Active.GetStateAsync( steamId );
	}

	public Task AppendEventAsync( AchievementEventRecord achievementEvent )
	{
		return Active.AppendEventAsync( achievementEvent );
	}

	public Task<bool> SetCosmeticSelectionAsync( long steamId, CosmeticSelectionRequest selection )
	{
		return Active.SetCosmeticSelectionAsync( steamId, selection );
	}

	private IAchievementRepository Active => useMemoryFallback ? memory : postgres;

	private static bool IsPostgresConnectionFailure( Exception exception )
	{
		if ( exception is NpgsqlException || exception is TimeoutException || exception is System.Net.Sockets.SocketException )
			return true;

		return exception.InnerException is not null && IsPostgresConnectionFailure( exception.InnerException );
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

public sealed record AuthTokenVerificationResult( bool IsValid, long SteamId, string DisplayName )
{
	public static AuthTokenVerificationResult Invalid { get; } = new( false, 0, "" );
}

public interface ISteamAuthTicketVerifier
{
	Task<AuthTokenVerificationResult> VerifyAsync( long claimedSteamId, string token );
}

public sealed class SteamWebApiAuthTicketVerifier : ISteamAuthTicketVerifier
{
	private readonly HttpClient http;
	private readonly IConfiguration configuration;
	private readonly IHostEnvironment environment;
	private readonly ILogger<SteamWebApiAuthTicketVerifier> logger;

	public SteamWebApiAuthTicketVerifier( HttpClient http, IConfiguration configuration, IHostEnvironment environment, ILogger<SteamWebApiAuthTicketVerifier> logger )
	{
		this.http = http;
		this.configuration = configuration;
		this.environment = environment;
		this.logger = logger;
	}

	public async Task<AuthTokenVerificationResult> VerifyAsync( long claimedSteamId, string token )
	{
		var allowDevAuth = configuration.GetValue( "Steam:AllowDevAuth", environment.IsDevelopment() );

		if ( allowDevAuth && string.Equals( token, "dev-token", StringComparison.Ordinal ) )
			return new AuthTokenVerificationResult( true, claimedSteamId, "" );

		var webApiKey =
			configuration["Steam:WebApiKey"] ??
			configuration["STEAM_WEB_API_KEY"];
		var appId =
			configuration["Steam:AppId"] ??
			configuration["STEAM_APP_ID"];

		if ( string.IsNullOrWhiteSpace( webApiKey ) || string.IsNullOrWhiteSpace( appId ) )
		{
			logger.LogWarning( "Steam ticket validation is not configured. Set Steam:WebApiKey and Steam:AppId, or STEAM_WEB_API_KEY and STEAM_APP_ID." );
			return AuthTokenVerificationResult.Invalid;
		}

		var baseUrl = configuration["Steam:WebApiBaseUrl"] ?? "https://partner.steam-api.com";
		var url = $"{baseUrl.TrimEnd( '/')}/ISteamUserAuth/AuthenticateUserTicket/v1/?" +
			$"key={Uri.EscapeDataString( webApiKey )}&" +
			$"appid={Uri.EscapeDataString( appId )}&" +
			$"ticket={Uri.EscapeDataString( token ?? "" )}";

		using var response = await http.GetAsync( url );
		if ( !response.IsSuccessStatusCode )
		{
			logger.LogWarning( "Steam ticket validation failed with HTTP {StatusCode}.", response.StatusCode );
			return AuthTokenVerificationResult.Invalid;
		}

		await using var stream = await response.Content.ReadAsStreamAsync();
		var validation = await JsonSerializer.DeserializeAsync<SteamAuthenticateUserTicketResponse>( stream, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		} );

		var parameters = validation?.Response?.Params;
		if ( parameters is null || !string.Equals( parameters.Result, "OK", StringComparison.OrdinalIgnoreCase ) )
		{
			logger.LogWarning( "Steam ticket validation returned result {Result}.", parameters?.Result ?? "<null>" );
			return AuthTokenVerificationResult.Invalid;
		}

		if ( !long.TryParse( parameters.SteamId, out var verifiedSteamId ) || verifiedSteamId != claimedSteamId )
		{
			logger.LogWarning( "Steam ticket SteamId mismatch. Claimed={ClaimedSteamId}, Validated={ValidatedSteamId}.", claimedSteamId, parameters.SteamId ?? "<null>" );
			return AuthTokenVerificationResult.Invalid;
		}

		return new AuthTokenVerificationResult( true, verifiedSteamId, "" );
	}
}

public sealed class SteamAuthenticateUserTicketResponse
{
	public SteamAuthenticateUserTicketBody Response { get; set; } = new();
}

public sealed class SteamAuthenticateUserTicketBody
{
	public SteamAuthenticateUserTicketParams Params { get; set; } = new();
	public string Error { get; set; } = "";
}

public sealed class SteamAuthenticateUserTicketParams
{
	public string Result { get; set; } = "";
	public string SteamId { get; set; } = "";
	public string OwnerSteamId { get; set; } = "";
	public bool VacBanned { get; set; }
	public bool PublisherBanned { get; set; }
}

public static class SchemaSql
{
	public const string Text =
		"""
		create table if not exists players (
			steam_id bigint primary key,
			display_name text not null default '',
			created_at timestamptz not null default now(),
			last_seen_at timestamptz not null default now()
		);

		create table if not exists achievement_progress (
			steam_id bigint not null references players(steam_id) on delete cascade,
			achievement_id text not null,
			current_value integer not null default 0 check (current_value >= 0),
			target_value integer not null check (target_value > 0),
			unlocked_at timestamptz null,
			updated_at timestamptz not null default now(),
			primary key (steam_id, achievement_id)
		);

		create table if not exists achievement_events (
			event_id text primary key,
			steam_id bigint not null references players(steam_id) on delete cascade,
			source_match_id text not null,
			event_type text not null,
			amount integer not null default 1 check (amount > 0),
			event_value text not null default '',
			payload_hash text not null default '',
			occurred_at timestamptz not null,
			received_at timestamptz not null default now()
		);

		create index if not exists ix_achievement_events_steam_id_received_at
			on achievement_events (steam_id, received_at desc);

		create table if not exists cosmetic_selections (
			steam_id bigint primary key references players(steam_id) on delete cascade,
			selected_piece_id text not null default 'officer_woman',
			selected_dice_skin_id text not null default 'classic',
			updated_at timestamptz not null default now()
		);
		""";
}
