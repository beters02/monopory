using System;
using System.Threading.Tasks;

public sealed partial class GameController : Component
{
	private string CurrentAchievementMatchId =>
		GameStartedAt > 0f
			? $"{GameStartedAt:0.000}:{StartingPlayerCount}"
			: "lobby";

	private void ReportAchievementEvent( PlayerState player, string eventType, int amount = 1, string value = "" )
	{
		if ( !Networking.IsHost || player is null || !player.IsAssigned || player.OwnerId == 0 )
			return;

		ReceiveAchievementEvent(
			player.OwnerId,
			eventType,
			Math.Max( amount, 1 ),
			value ?? "",
			BuildAchievementEventId( player, eventType ),
			CurrentAchievementMatchId,
			GetUnixNow()
		);
	}

	[Rpc.Broadcast]
	private void ReceiveAchievementEvent( long steamId, string eventType, int amount, string value, string eventId, string sourceMatchId, long occurredAtUnixSeconds )
	{
		var localSteamId = GetLocalSteamId();
		if ( !localSteamId.HasValue || steamId == 0 || steamId != localSteamId.Value )
			return;

		_ = ReportAchievementEventAsync( steamId, eventType, amount, value, eventId, sourceMatchId, occurredAtUnixSeconds );
	}

	private async Task ReportAchievementEventAsync( long steamId, string eventType, int amount, string value, string eventId, string sourceMatchId, long occurredAtUnixSeconds )
	{
		try
		{
			await AchievementServices.Achievements.ReportEventAsync( new AchievementEvent
			{
				EventId = eventId,
				SteamId = steamId,
				Type = eventType,
				Amount = Math.Max( amount, 1 ),
				Value = value ?? "",
				SourceMatchId = sourceMatchId ?? "",
				OccurredAtUnixSeconds = occurredAtUnixSeconds
			} );
		}
		catch ( Exception exception )
		{
			var detail = exception.InnerException is null
				? exception.Message
				: $"{exception.Message} Inner={exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
			Log.Warning( $"Failed to report achievement event {eventType}: {exception.GetType().Name}: {detail}" );
		}
	}

	private static long GetUnixNow()
	{
		return Math.Max( (long)Time.Now, 1 );
	}

	private static string BuildAchievementEventId( PlayerState player, string eventType )
	{
		var steamId = player?.OwnerId ?? 0;
		return $"{steamId}:{eventType}:{Time.Now:0.000}:{Game.Random.Int( 0, 1000000 )}";
	}

	private void ReportMatchStartedAchievements()
	{
		foreach ( var player in GetAssignedPlayers() )
			ReportAchievementEvent( player, AchievementEventTypes.MatchStarted );
	}

	private void ReportMatchCompletedAchievements()
	{
		foreach ( var player in GetAssignedPlayers() )
			ReportAchievementEvent( player, AchievementEventTypes.MatchCompleted );
	}

	private void ReportWinnerAchievements()
	{
		if ( Winner is not null )
			ReportAchievementEvent( Winner, AchievementEventTypes.MatchWon );
	}

	private void ReportDiceRollAchievements( PlayerState player, bool rolledDoubles )
	{
		if ( player is null )
			return;

		if ( rolledDoubles )
			ReportAchievementEvent( player, AchievementEventTypes.RolledDoubles );

		if ( LastDieA == 1 && LastDieB == 1 )
			ReportAchievementEvent( player, AchievementEventTypes.RolledSnakeEyes );
	}

	private void ReportPropertyAcquiredAchievements( int playerIndex, SpaceDef def )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || def is null )
			return;

		ReportAchievementEvent( player, AchievementEventTypes.PropertyAcquired, 1, def.Index.ToString() );
	}

	private void ReportNewlyOwnedSetAchievements( HashSet<string> previousKeys, params int[] playerIndexes )
	{
		if ( previousKeys is null )
			return;

		foreach ( var playerIndex in playerIndexes.Distinct().Where( index => index >= 0 ) )
		{
			var player = Players.ElementAtOrDefault( playerIndex );
			if ( player is null || !player.IsAssigned || player.IsBankrupt )
				continue;

			foreach ( var setKey in GetOwnedSetKeysForPlayer( playerIndex ) )
			{
				if ( previousKeys.Contains( setKey ) )
					continue;

				ReportAchievementEvent( player, AchievementEventTypes.OwnedSetCompleted, 1, setKey );
			}
		}
	}
}
