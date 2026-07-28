using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameController
{
	private void BroadcastLifetimeDiceRoll( int dieA, int dieB )
	{
		if ( !Networking.IsHost )
			return;

		var eventId = NextLifetimeStatsEventId++;
		RecordLifetimeDiceRollLocal(
			BuildLifetimeStatsMatchId(),
			CurrentGameIdentifier ?? "",
			BuildLifetimeStatsPlayerNames(),
			eventId,
			dieA,
			dieB );
	}

	private void BroadcastLifetimeSpaceLanding( SpaceDef space )
	{
		if ( !Networking.IsHost || space is null )
			return;

		var eventId = NextLifetimeStatsEventId++;
		RecordLifetimeSpaceLandingLocal(
			BuildLifetimeStatsMatchId(),
			CurrentGameIdentifier ?? "",
			BuildLifetimeStatsPlayerNames(),
			eventId,
			space.Key ?? "",
			space.DisplayName ?? "",
			space.Index );
	}

	[Rpc.Broadcast]
	private void RecordLifetimeDiceRollLocal(
		string matchId,
		string gameIdentifier,
		string playerNames,
		int eventId,
		int dieA,
		int dieB )
	{
		LifetimeStatsService.RecordDiceRoll(
			matchId,
			gameIdentifier,
			ParseLifetimeStatsPlayerNames( playerNames ),
			eventId,
			dieA,
			dieB );
	}

	[Rpc.Broadcast]
	private void RecordLifetimeSpaceLandingLocal(
		string matchId,
		string gameIdentifier,
		string playerNames,
		int eventId,
		string spaceKey,
		string displayName,
		int spaceIndex )
	{
		LifetimeStatsService.RecordSpaceLanding(
			matchId,
			gameIdentifier,
			ParseLifetimeStatsPlayerNames( playerNames ),
			eventId,
			spaceKey,
			displayName,
			spaceIndex );
	}

	private string BuildLifetimeStatsMatchId()
	{
		var gameIdentifier = string.IsNullOrWhiteSpace( CurrentGameIdentifier )
			? "match"
			: CurrentGameIdentifier.Trim();
		var commitment = DiceCommitmentHash ?? "";
		var commitmentPrefix = commitment[..Math.Min( commitment.Length, 8 )];
		return string.IsNullOrWhiteSpace( commitmentPrefix )
			? gameIdentifier
			: $"{gameIdentifier}-{commitmentPrefix}";
	}

	private string BuildLifetimeStatsPlayerNames()
	{
		return string.Join( "\n", GetLobbyPlayers()
			.Select( player => player.PlayerName )
			.Where( name => !string.IsNullOrWhiteSpace( name ) ) );
	}

	private static IEnumerable<string> ParseLifetimeStatsPlayerNames( string playerNames )
	{
		return (playerNames ?? "")
			.Split( '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
	}
}
