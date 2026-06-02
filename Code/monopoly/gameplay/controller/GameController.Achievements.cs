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

		_ = ReportAchievementEventAsync( player, eventType, amount, value );
	}

	private async Task ReportAchievementEventAsync( PlayerState player, string eventType, int amount, string value )
	{
		try
		{
			await AchievementServices.Achievements.ReportEventAsync( new AchievementEvent
			{
				EventId = BuildAchievementEventId( player, eventType ),
				SteamId = player.OwnerId,
				Type = eventType,
				Amount = Math.Max( amount, 1 ),
				Value = value ?? "",
				SourceMatchId = CurrentAchievementMatchId,
				OccurredAtUnixSeconds = GetUnixNow()
			} );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to report achievement event {eventType}: {exception.Message}" );
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
