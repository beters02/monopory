using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

public static class AchievementServices
{
	private static readonly LocalAuthoritativeAchievementService localService = new( new NoOpSteamAchievementBridge() );
	private static readonly QueuedAchievementService queuedService = new( localService );

	public static IAchievementService Achievements { get; private set; } = queuedService;
	public static ICosmeticUnlockService Cosmetics { get; private set; } = queuedService;

	public static void Use( IAchievementService achievements, ICosmeticUnlockService cosmetics )
	{
		queuedService.Use( achievements, cosmetics );
	}

	public static void UseBackend( string baseUrl, string bearerToken, long playerId = 0 )
	{
//do if standalone
		var backend = new HttpAchievementService( baseUrl, bearerToken, playerId );
		Use( backend, backend );
//do else
/*
		Log.Warning( "HTTP achievements backend is only available in standalone builds. Using the local achievement service." );
*/
//do endif
	}

	public static void UseLocal()
	{
		queuedService.UseLocal();
	}

	public static void Shutdown()
	{
		queuedService.UseLocal();
	}

	public static void SetReconnectHandler( Func<Task> reconnectHandler )
	{
		queuedService.SetReconnectHandler( reconnectHandler );
	}
}

public sealed class QueuedAchievementService : IAchievementService, ICosmeticUnlockService
{
	private const int MaxQueuedEvents = 256;
	private readonly LocalAuthoritativeAchievementService localService;
	private readonly Queue<AchievementEvent> queuedEvents = new();
	private IAchievementService achievements;
	private ICosmeticUnlockService cosmetics;
	private Func<Task> reconnectHandler;
	private bool backendConnected;
	private bool flushing;
	private bool reconnecting;

	public QueuedAchievementService( LocalAuthoritativeAchievementService localService )
	{
		this.localService = localService;
		achievements = localService;
		cosmetics = localService;
	}

	public void SetReconnectHandler( Func<Task> reconnectHandler )
	{
		this.reconnectHandler = reconnectHandler;
	}

	public void Use( IAchievementService achievements, ICosmeticUnlockService cosmetics )
	{
		var previousAchievements = this.achievements;
		var previousCosmetics = this.cosmetics;

		this.achievements = achievements ?? localService;
		this.cosmetics = cosmetics ?? localService;
		backendConnected = achievements is not null && cosmetics is not null && achievements != localService;
		DisposePreviousBackend( previousAchievements, previousCosmetics );

		if ( backendConnected )
		{
			Log.Info( $"Achievements backend service active. queuedEvents={queuedEvents.Count}." );
			_ = FlushQueuedEventsAsync();
		}
	}

	public void UseLocal()
	{
		var previousAchievements = achievements;
		var previousCosmetics = cosmetics;

		achievements = localService;
		cosmetics = localService;
		backendConnected = false;
		DisposePreviousBackend( previousAchievements, previousCosmetics );
	}

	public async Task<PlayerAchievementState> GetMyStateAsync()
	{
		return await achievements.GetMyStateAsync();
	}

	public async Task<PlayerAchievementState> GetPublicStateAsync( long steamId )
	{
		return await achievements.GetPublicStateAsync( steamId );
	}

	public async Task ReportEventAsync( AchievementEvent achievementEvent )
	{
		if ( achievementEvent is null )
			return;

		if ( !backendConnected )
		{
			QueueEvent( achievementEvent, "backend not connected" );
			return;
		}

		try
		{
			await achievements.ReportEventAsync( achievementEvent );
		}
		catch ( HttpRequestException exception ) when ( IsUnauthorized( exception ) )
		{
			backendConnected = false;
			QueueEvent( achievementEvent, "backend token unauthorized" );
			Log.Warning( "Achievements backend token was rejected; reconnecting before retrying queued events." );
			_ = ReconnectAndFlushAsync();
		}
	}

	public async Task<bool> CanUseCosmeticAsync( long steamId, string cosmeticId )
	{
		return await achievements.CanUseCosmeticAsync( steamId, cosmeticId );
	}

	public bool CanUseCosmetic( long steamId, string cosmeticId )
	{
		return cosmetics.CanUseCosmetic( steamId, cosmeticId );
	}

	public PlayerAchievementState GetCachedState( long steamId )
	{
		return achievements.GetCachedState( steamId );
	}

	public IReadOnlyList<CosmeticDefinition> GetAvailableCosmetics( long steamId )
	{
		return cosmetics.GetAvailableCosmetics( steamId );
	}

	private void QueueEvent( AchievementEvent achievementEvent, string reason )
	{
		if ( queuedEvents.Count >= MaxQueuedEvents )
			queuedEvents.Dequeue();

		queuedEvents.Enqueue( CloneEvent( achievementEvent ) );
		Log.Info( $"Queued achievement event {achievementEvent.Type}; reason={reason}; queuedEvents={queuedEvents.Count}." );
	}

	private async Task ReconnectAndFlushAsync()
	{
		if ( reconnecting )
			return;

		try
		{
			reconnecting = true;
			if ( reconnectHandler is not null )
				await reconnectHandler();

			if ( backendConnected )
				await FlushQueuedEventsAsync();
		}
		finally
		{
			reconnecting = false;
		}
	}

	private async Task FlushQueuedEventsAsync()
	{
		if ( flushing || !backendConnected )
			return;

		try
		{
			flushing = true;
			while ( backendConnected && queuedEvents.Count > 0 )
			{
				var achievementEvent = queuedEvents.Dequeue();
				try
				{
					await achievements.ReportEventAsync( achievementEvent );
					Log.Info( $"Flushed queued achievement event {achievementEvent.Type}; remaining={queuedEvents.Count}." );
				}
				catch ( HttpRequestException exception ) when ( IsUnauthorized( exception ) )
				{
					backendConnected = false;
					QueueEvent( achievementEvent, "backend token unauthorized during flush" );
					_ = ReconnectAndFlushAsync();
					return;
				}
				catch ( Exception exception )
				{
					QueueEvent( achievementEvent, $"flush failed: {exception.GetType().Name}" );
					return;
				}
			}
		}
		finally
		{
			flushing = false;
		}
	}

	private static bool IsUnauthorized( HttpRequestException exception )
	{
		return exception.StatusCode == HttpStatusCode.Unauthorized ||
			exception.Message.Contains( "401", StringComparison.OrdinalIgnoreCase ) ||
			exception.Message.Contains( "Unauthorized", StringComparison.OrdinalIgnoreCase );
	}

	private static AchievementEvent CloneEvent( AchievementEvent achievementEvent )
	{
		return new AchievementEvent
		{
			EventId = achievementEvent.EventId,
			SteamId = achievementEvent.SteamId,
			Type = achievementEvent.Type,
			Amount = achievementEvent.Amount,
			Value = achievementEvent.Value,
			SourceMatchId = achievementEvent.SourceMatchId,
			OccurredAtUnixSeconds = achievementEvent.OccurredAtUnixSeconds
		};
	}

	private void DisposePreviousBackend( IAchievementService previousAchievements, ICosmeticUnlockService previousCosmetics )
	{
		DisposeIfOwnedBackend( previousAchievements );

		if ( !ReferenceEquals( previousAchievements, previousCosmetics ) )
			DisposeIfOwnedBackend( previousCosmetics );
	}

	private void DisposeIfOwnedBackend( object service )
	{
		if ( service is null || ReferenceEquals( service, localService ) || ReferenceEquals( service, achievements ) || ReferenceEquals( service, cosmetics ) )
			return;

		if ( service is IDisposable disposable )
			disposable.Dispose();
	}
}
