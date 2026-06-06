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
	builder.Services.AddSingleton<IDeviceAuthRepository, InMemoryDeviceAuthRepository>();
}
else
{
	builder.Services.AddSingleton( _ => new NpgsqlDataSourceBuilder( postgresConnectionString ).Build() );
	builder.Services.AddSingleton<InMemoryAchievementRepository>();
	builder.Services.AddSingleton<InMemoryDeviceAuthRepository>();
	builder.Services.AddSingleton<PostgresAchievementRepository>();
	builder.Services.AddSingleton<PostgresDeviceAuthRepository>();
	builder.Services.AddSingleton<IAchievementRepository, FallbackAchievementRepository>();
	builder.Services.AddSingleton<IDeviceAuthRepository, FallbackDeviceAuthRepository>();
}

builder.Services.AddSingleton<TokenStore>();
builder.Services.AddHttpClient<ISboxAuthTokenVerifier, FacepunchSboxAuthTokenVerifier>();
builder.Services.AddHttpClient<ISteamworksAuthTicketVerifier, SteamworksWebApiAuthTicketVerifier>();

var app = builder.Build();
if ( app.Services.GetRequiredService<IAchievementRepository>() is IInitializableRepository achievementRepository )
	await achievementRepository.InitializeAsync();
if ( app.Services.GetRequiredService<IDeviceAuthRepository>() is IInitializableRepository deviceRepository )
	await deviceRepository.InitializeAsync();

app.MapPost( "/auth/device/session", async ( DeviceSessionRequest request, IAchievementRepository repository, IDeviceAuthRepository devices, TokenStore tokens ) =>
{
	if ( request is null || string.IsNullOrWhiteSpace( request.DeviceId ) || string.IsNullOrWhiteSpace( request.Secret ) )
		return Results.BadRequest();

	var result = await devices.AuthenticateAsync( request.DeviceId, request.Secret );
	if ( !result.IsValid )
		return Results.Json( new AuthFailureResponse( result.FailureReason ), statusCode: StatusCodes.Status401Unauthorized );

	await repository.EnsurePlayerAsync( result.PlayerId, request.DisplayName ?? "" );
	var token = tokens.Create( result.PlayerId );
	return Results.Ok( new DeviceSessionResponse( token, DateTimeOffset.UtcNow.AddHours( 2 ), result.PlayerId, request.DeviceId ) );
} );

app.MapPost( "/auth/sbox/session", async ( SboxSessionRequest request, IAchievementRepository repository, TokenStore tokens, ISboxAuthTokenVerifier authVerifier ) =>
{
	if ( request is null || request.SteamId == 0 || string.IsNullOrWhiteSpace( request.Token ) )
		return Results.BadRequest();

	var result = await authVerifier.VerifyAsync( request.SteamId, request.Token );
	if ( !result.IsValid )
		return Results.Json( new AuthFailureResponse( result.FailureReason ), statusCode: StatusCodes.Status401Unauthorized );

	await repository.EnsurePlayerAsync( result.SteamId, request.DisplayName ?? "" );
	var token = tokens.Create( result.SteamId );
	return Results.Ok( new SboxSessionResponse( token, DateTimeOffset.UtcNow.AddHours( 2 ), result.SteamId ) );
} );

app.MapPost( "/auth/steamworks/session", async ( SteamworksSessionRequest request, IAchievementRepository repository, TokenStore tokens, ISteamworksAuthTicketVerifier authVerifier ) =>
{
	if ( request is null || request.SteamId == 0 || string.IsNullOrWhiteSpace( request.Ticket ) )
		return Results.BadRequest();

	var result = await authVerifier.VerifyAsync( request.SteamId, request.Ticket );
	if ( !result.IsValid )
		return Results.Json( new AuthFailureResponse( result.FailureReason ), statusCode: StatusCodes.Status401Unauthorized );

	await repository.EnsurePlayerAsync( result.SteamId, request.DisplayName ?? "" );
	var token = tokens.Create( result.SteamId );
	return Results.Ok( new SteamworksSessionResponse( token, DateTimeOffset.UtcNow.AddHours( 2 ), result.SteamId ) );
} );

app.MapPost( "/auth/forkbox/session", async ( ForkboxSessionRequest request, IAchievementRepository repository, TokenStore tokens, ISteamworksAuthTicketVerifier authVerifier ) =>
{
	if ( request is null || request.SteamId == 0 || string.IsNullOrWhiteSpace( request.Token ) )
		return Results.BadRequest();

	var result = await authVerifier.VerifyAsync( request.SteamId, request.Token );
	if ( !result.IsValid )
		return Results.Unauthorized();

	await repository.EnsurePlayerAsync( result.SteamId, request.DisplayName ?? "" );
	var token = tokens.Create( result.SteamId );
	return Results.Ok( new ForkboxSessionResponse( token, DateTimeOffset.UtcNow.AddHours( 2 ), result.SteamId ) );
} );

app.MapGet( "/players/{steamId:long}/achievements", async ( long steamId, IAchievementRepository repository ) =>
{
	var state = await repository.GetStateAsync( steamId );
	return Results.Ok( state.AsPublic() );
} );

app.MapGet( "/me/achievements", async ( HttpRequest request, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetPlayerId( request, tokens, out var playerId ) )
		return Results.Unauthorized();

	return Results.Ok( await repository.GetStateAsync( playerId ) );
} );

app.MapPost( "/me/achievement-events", async ( HttpRequest request, AchievementEventRecord achievementEvent, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetPlayerId( request, tokens, out var playerId ) )
		return Results.Unauthorized();

	if ( achievementEvent is null || string.IsNullOrWhiteSpace( achievementEvent.Type ) )
		return Results.BadRequest();

	await repository.AppendEventAsync( new AchievementEventRecord
	{
		EventId = achievementEvent.EventId,
		SteamId = playerId,
		Type = achievementEvent.Type,
		Amount = achievementEvent.Amount,
		Value = achievementEvent.Value,
		SourceMatchId = achievementEvent.SourceMatchId,
		OccurredAtUnixSeconds = achievementEvent.OccurredAtUnixSeconds
	} );
	return Results.Ok( await repository.GetStateAsync( playerId ) );
} );

app.MapGet( "/me/unlocks", async ( HttpRequest request, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetPlayerId( request, tokens, out var playerId ) )
		return Results.Unauthorized();

	var state = await repository.GetStateAsync( playerId );
	return Results.Ok( state.UnlockedCosmeticIds );
} );

app.MapPost( "/me/cosmetics/selection", async ( HttpRequest request, CosmeticSelectionRequest selection, IAchievementRepository repository, TokenStore tokens ) =>
{
	if ( !TryGetPlayerId( request, tokens, out var playerId ) )
		return Results.Unauthorized();

	var result = await repository.SetCosmeticSelectionAsync( playerId, selection ?? new() );
	return result ? Results.Ok( await repository.GetStateAsync( playerId ) ) : Results.BadRequest();
} );

app.Run();

static bool TryGetPlayerId( HttpRequest request, TokenStore tokens, out long playerId )
{
	playerId = 0;
	var auth = request.Headers.Authorization.ToString();
	if ( string.IsNullOrWhiteSpace( auth ) || !auth.StartsWith( "Bearer ", StringComparison.OrdinalIgnoreCase ) )
		return false;

	return tokens.TryResolve( auth["Bearer ".Length..].Trim(), out playerId );
}

public sealed record AuthFailureResponse( string Reason );
public sealed record DeviceSessionRequest( string DeviceId, string Secret, string DisplayName );
public sealed record DeviceSessionResponse( string AccessToken, DateTimeOffset ExpiresAt, long PlayerId, string DeviceId );
public sealed record SboxSessionRequest( long SteamId, string Token, string DisplayName );
public sealed record SboxSessionResponse( string AccessToken, DateTimeOffset ExpiresAt, long PlayerId );
public sealed record SteamworksSessionRequest( long SteamId, string Ticket, string DisplayName );
public sealed record SteamworksSessionResponse( string AccessToken, DateTimeOffset ExpiresAt, long PlayerId );
public sealed record ForkboxSessionRequest( long SteamId, string Token, string DisplayName );
public sealed record ForkboxSessionResponse( string AccessToken, DateTimeOffset ExpiresAt, long PlayerId );

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
		logger.LogInformation("Starting achievements server");
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

	public static bool IsPostgresConnectionFailure( Exception exception )
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

public sealed record SboxAuthResult( bool IsValid, long SteamId, string FailureReason = "" )
{
	public static SboxAuthResult Failed( string reason )
	{
		return new SboxAuthResult( false, 0, reason ?? "" );
	}
}

public interface ISboxAuthTokenVerifier
{
	Task<SboxAuthResult> VerifyAsync( long claimedSteamId, string token );
}

public sealed class FacepunchSboxAuthTokenVerifier : ISboxAuthTokenVerifier
{
	private readonly HttpClient http;
	private readonly ILogger<FacepunchSboxAuthTokenVerifier> logger;

	public FacepunchSboxAuthTokenVerifier( HttpClient http, ILogger<FacepunchSboxAuthTokenVerifier> logger )
	{
		this.http = http;
		this.logger = logger;
	}

	public async Task<SboxAuthResult> VerifyAsync( long claimedSteamId, string token )
	{
		var payload = new Dictionary<string, object>
		{
			["steamid"] = claimedSteamId,
			["token"] = token ?? ""
		};

		using var content = new StringContent( JsonSerializer.Serialize( payload ), Encoding.UTF8, "application/json" );
		using var response = await http.PostAsync( "https://services.facepunch.com/sbox/auth/token", content );
		var responseText = await response.Content.ReadAsStringAsync();
		if ( !response.IsSuccessStatusCode )
		{
			logger.LogWarning( "s&box auth token validation failed with HTTP {StatusCode}: {Body}", response.StatusCode, responseText );
			return SboxAuthResult.Failed( $"sbox_http_{(int)response.StatusCode}" );
		}

		var validation = JsonSerializer.Deserialize<SboxAuthTokenResponse>( responseText, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		} );

		if ( validation is null || !string.Equals( validation.Status, "ok", StringComparison.OrdinalIgnoreCase ) )
		{
			logger.LogWarning( "s&box auth token validation returned status {Status}: {Body}", validation?.Status ?? "<null>", responseText );
			return SboxAuthResult.Failed( $"sbox_status_{validation?.Status ?? "null"}" );
		}

		if ( validation.SteamId != claimedSteamId )
		{
			logger.LogWarning( "s&box auth token SteamId mismatch. Claimed={ClaimedSteamId}, Validated={ValidatedSteamId}.", claimedSteamId, validation.SteamId );
			return SboxAuthResult.Failed( "sbox_steam_id_mismatch" );
		}

		return new SboxAuthResult( true, validation.SteamId );
	}
}

public sealed class SboxAuthTokenResponse
{
	public long SteamId { get; set; }
	public string Status { get; set; } = "";
}

public sealed record SteamworksAuthResult( bool IsValid, long SteamId, string FailureReason = "" )
{
	public static SteamworksAuthResult Failed( string reason )
	{
		return new SteamworksAuthResult( false, 0, reason ?? "" );
	}
}

public interface ISteamworksAuthTicketVerifier
{
	Task<SteamworksAuthResult> VerifyAsync( long claimedSteamId, string ticket );
}

public sealed class SteamworksWebApiAuthTicketVerifier : ISteamworksAuthTicketVerifier
{
	private readonly HttpClient http;
	private readonly IConfiguration configuration;
	private readonly ILogger<SteamworksWebApiAuthTicketVerifier> logger;

	public SteamworksWebApiAuthTicketVerifier( HttpClient http, IConfiguration configuration, ILogger<SteamworksWebApiAuthTicketVerifier> logger )
	{
		this.http = http;
		this.configuration = configuration;
		this.logger = logger;
	}

	public async Task<SteamworksAuthResult> VerifyAsync( long claimedSteamId, string ticket )
	{
		var webApiKey =
			configuration["Steam:WebApiKey"] ??
			configuration["STEAM_WEB_API_KEY"];
		var appIds = GetTicketAppIds();

		if ( string.IsNullOrWhiteSpace( webApiKey ) )
		{
			logger.LogWarning( "Steamworks ticket validation is not configured. Set Steam:WebApiKey or STEAM_WEB_API_KEY." );
			return SteamworksAuthResult.Failed( "steam_web_api_key_missing" );
		}

		var lastFailure = "";
		foreach ( var appId in appIds )
		{
			var result = await VerifyForAppIdAsync( claimedSteamId, ticket, webApiKey, appId );
			if ( result.IsValid )
				return result;

			lastFailure = result.FailureReason;
		}

		return SteamworksAuthResult.Failed( lastFailure );
	}

	private async Task<SteamworksAuthResult> VerifyForAppIdAsync( long claimedSteamId, string ticket, string webApiKey, string appId )
	{
		var baseUrl = configuration["Steam:WebApiBaseUrl"] ??
			configuration["STEAM_WEB_API_BASE_URL"] ??
			"https://api.steampowered.com";
		var url = $"{baseUrl.TrimEnd( '/')}/ISteamUserAuth/AuthenticateUserTicket/v1/?" +
			$"key={Uri.EscapeDataString( webApiKey )}&" +
			$"appid={Uri.EscapeDataString( appId )}&" +
			$"ticket={Uri.EscapeDataString( ticket ?? "" )}";

		using var response = await http.GetAsync( url );
		var responseText = await response.Content.ReadAsStringAsync();
		if ( !response.IsSuccessStatusCode )
		{
			logger.LogWarning( "Steamworks ticket validation failed for AppId={AppId} with HTTP {StatusCode}: {Body}", appId, response.StatusCode, responseText );
			return SteamworksAuthResult.Failed( $"steamworks_http_{(int)response.StatusCode}_appid_{appId}" );
		}

		var validation = JsonSerializer.Deserialize<SteamAuthenticateUserTicketResponse>( responseText, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		} );

		var parameters = validation?.Response?.Params;
		if ( parameters is null || !string.Equals( parameters.Result, "OK", StringComparison.OrdinalIgnoreCase ) )
		{
			logger.LogWarning( "Steamworks ticket validation failed for AppId={AppId}. Result={Result} Error={Error} Body={Body}", appId, parameters?.Result ?? "<null>", SteamAuthenticateUserTicketErrors.Get( validation ), responseText );
			return SteamworksAuthResult.Failed( $"steamworks_result_{parameters?.Result ?? "null"}_appid_{appId}" );
		}

		if ( !long.TryParse( parameters.SteamId, out var verifiedSteamId ) || verifiedSteamId != claimedSteamId )
		{
			logger.LogWarning( "Steamworks ticket SteamId mismatch for AppId={AppId}. Claimed={ClaimedSteamId}, Validated={ValidatedSteamId}.", appId, claimedSteamId, parameters.SteamId ?? "<null>" );
			return SteamworksAuthResult.Failed( $"steamworks_steam_id_mismatch_appid_{appId}" );
		}

		logger.LogInformation( "Steamworks ticket validated for SteamId={SteamId} AppId={AppId}.", verifiedSteamId, appId );
		return new SteamworksAuthResult( true, verifiedSteamId );
	}

	private IReadOnlyList<string> GetTicketAppIds()
	{
		var configured =
			configuration["Steam:TicketAppIds"] ??
			configuration["STEAM_TICKET_APP_IDS"];
		var primary =
			configuration["Steam:AppId"] ??
			configuration["STEAM_APP_ID"] ??
			"4745160";

		var values = new List<string>();
		if ( !string.IsNullOrWhiteSpace( configured ) )
			values.AddRange( configured.Split( [',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) );

		if ( !string.IsNullOrWhiteSpace( primary ) )
			values.Add( primary );

		return values
			.Where( value => !string.IsNullOrWhiteSpace( value ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();
	}
}

public sealed class SteamAuthenticateUserTicketResponse
{
	public SteamAuthenticateUserTicketBody Response { get; set; } = new();
}

public sealed class SteamAuthenticateUserTicketBody
{
	public SteamAuthenticateUserTicketParams Params { get; set; } = new();
	public JsonElement Error { get; set; }
}

public static class SteamAuthenticateUserTicketErrors
{
	public static string Get( SteamAuthenticateUserTicketResponse validation )
	{
		var error = validation?.Response?.Error;
		if ( error is null || error.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null )
			return "";

		return error.Value.ValueKind == JsonValueKind.String
			? error.Value.GetString() ?? ""
			: error.Value.GetRawText();
	}
}

public sealed class SteamAuthenticateUserTicketParams
{
	public string Result { get; set; } = "";
	public string SteamId { get; set; } = "";
	public string OwnerSteamId { get; set; } = "";
	public bool VacBanned { get; set; }
	public bool PublisherBanned { get; set; }
}

public sealed record DeviceAuthResult( bool IsValid, long PlayerId, string FailureReason = "" )
{
	public static DeviceAuthResult Failed( string reason )
	{
		return new DeviceAuthResult( false, 0, reason ?? "" );
	}
}

public interface IDeviceAuthRepository
{
	Task<DeviceAuthResult> AuthenticateAsync( string deviceId, string secret );
}

public sealed class InMemoryDeviceAuthRepository : IDeviceAuthRepository
{
	private readonly Dictionary<string, DeviceCredential> credentials = new( StringComparer.OrdinalIgnoreCase );

	public Task<DeviceAuthResult> AuthenticateAsync( string deviceId, string secret )
	{
		if ( !DeviceCredentialRules.IsValid( deviceId, secret ) )
			return Task.FromResult( DeviceAuthResult.Failed( "invalid_device_credentials" ) );

		var normalizedDeviceId = DeviceCredentialRules.NormalizeDeviceId( deviceId );
		var secretHash = DeviceCredentialRules.HashSecret( normalizedDeviceId, secret );
		if ( credentials.TryGetValue( normalizedDeviceId, out var existing ) )
		{
			return Task.FromResult( string.Equals( existing.SecretHash, secretHash, StringComparison.Ordinal )
				? new DeviceAuthResult( true, existing.PlayerId )
				: DeviceAuthResult.Failed( "device_secret_mismatch" ) );
		}

		var credential = new DeviceCredential( normalizedDeviceId, DeviceCredentialRules.CreatePlayerId( normalizedDeviceId ), secretHash );
		credentials[normalizedDeviceId] = credential;
		return Task.FromResult( new DeviceAuthResult( true, credential.PlayerId ) );
	}
}

public sealed class PostgresDeviceAuthRepository : IDeviceAuthRepository, IInitializableRepository
{
	private readonly NpgsqlDataSource dataSource;

	public PostgresDeviceAuthRepository( NpgsqlDataSource dataSource )
	{
		this.dataSource = dataSource;
	}

	public async Task InitializeAsync()
	{
		await using var command = dataSource.CreateCommand(
			"""
			create table if not exists device_credentials (
				device_id text primary key,
				player_id bigint not null unique,
				secret_hash text not null,
				created_at timestamptz not null default now(),
				last_seen_at timestamptz not null default now()
			);
			""" );
		await command.ExecuteNonQueryAsync();
	}

	public async Task<DeviceAuthResult> AuthenticateAsync( string deviceId, string secret )
	{
		if ( !DeviceCredentialRules.IsValid( deviceId, secret ) )
			return DeviceAuthResult.Failed( "invalid_device_credentials" );

		var normalizedDeviceId = DeviceCredentialRules.NormalizeDeviceId( deviceId );
		var secretHash = DeviceCredentialRules.HashSecret( normalizedDeviceId, secret );
		var playerId = DeviceCredentialRules.CreatePlayerId( normalizedDeviceId );

		await using var command = dataSource.CreateCommand(
			"""
			insert into device_credentials (device_id, player_id, secret_hash, created_at, last_seen_at)
			values ($1, $2, $3, now(), now())
			on conflict (device_id)
			do update set last_seen_at = now()
			returning player_id, secret_hash;
			""" );
		command.Parameters.Add( new() { Value = normalizedDeviceId } );
		command.Parameters.Add( new() { Value = playerId } );
		command.Parameters.Add( new() { Value = secretHash } );

		await using var reader = await command.ExecuteReaderAsync();
		if ( !await reader.ReadAsync() )
			return DeviceAuthResult.Failed( "device_auth_failed" );

		var storedPlayerId = reader.GetInt64( 0 );
		var storedSecretHash = reader.GetString( 1 );
		return string.Equals( storedSecretHash, secretHash, StringComparison.Ordinal )
			? new DeviceAuthResult( true, storedPlayerId )
			: DeviceAuthResult.Failed( "device_secret_mismatch" );
	}
}

public sealed class FallbackDeviceAuthRepository : IDeviceAuthRepository, IInitializableRepository
{
	private readonly PostgresDeviceAuthRepository postgres;
	private readonly InMemoryDeviceAuthRepository memory;
	private readonly ILogger<FallbackDeviceAuthRepository> logger;
	private bool useMemoryFallback;

	public FallbackDeviceAuthRepository( PostgresDeviceAuthRepository postgres, InMemoryDeviceAuthRepository memory, ILogger<FallbackDeviceAuthRepository> logger )
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
			logger.LogInformation( "Device auth repository initialized with Postgres." );
		}
		catch ( Exception exception ) when ( FallbackAchievementRepository.IsPostgresConnectionFailure( exception ) )
		{
			useMemoryFallback = true;
			logger.LogWarning( exception, "Postgres device auth repository is unavailable. Falling back to in-memory auth for this process." );
		}
	}

	public Task<DeviceAuthResult> AuthenticateAsync( string deviceId, string secret )
	{
		return useMemoryFallback
			? memory.AuthenticateAsync( deviceId, secret )
			: postgres.AuthenticateAsync( deviceId, secret );
	}
}

public sealed record DeviceCredential( string DeviceId, long PlayerId, string SecretHash );

public static class DeviceCredentialRules
{
	public static bool IsValid( string deviceId, string secret )
	{
		return Guid.TryParse( deviceId, out _ ) && !string.IsNullOrWhiteSpace( secret ) && secret.Length >= 32;
	}

	public static string NormalizeDeviceId( string deviceId )
	{
		return Guid.Parse( deviceId ).ToString( "D" );
	}

	public static long CreatePlayerId( string deviceId )
	{
		var hash = SHA256.HashData( Encoding.UTF8.GetBytes( "rentrush-device-player:" + NormalizeDeviceId( deviceId ) ) );
		var value = BitConverter.ToInt64( hash, 0 ) & long.MaxValue;
		return value == 0 ? 1 : value;
	}

	public static string HashSecret( string deviceId, string secret )
	{
		var input = $"{NormalizeDeviceId( deviceId )}|{secret}";
		return Convert.ToHexString( SHA256.HashData( Encoding.UTF8.GetBytes( input ) ) );
	}
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

		create table if not exists device_credentials (
			device_id text primary key,
			player_id bigint not null unique,
			secret_hash text not null,
			created_at timestamptz not null default now(),
			last_seen_at timestamptz not null default now()
		);
		""";
}
