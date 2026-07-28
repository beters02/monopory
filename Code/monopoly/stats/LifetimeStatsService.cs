using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class LifetimeStatsData
{
	public int SchemaVersion { get; set; } = LifetimeStatsService.CurrentSchemaVersion;
	public Dictionary<string, LifetimeMatchStats> Matches { get; set; } = new();
}

public sealed class LifetimeMatchStats
{
	public string MatchId { get; set; } = "";
	public string GameIdentifier { get; set; } = "";
	public string FirstSeenUtc { get; set; } = "";
	public string LastSeenUtc { get; set; } = "";
	public List<string> PlayerNames { get; set; } = new();
	public Dictionary<int, long> DiceFaceCounts { get; set; } = new();
	public Dictionary<int, long> DiceTotalCounts { get; set; } = new();
	public Dictionary<string, LifetimeSpaceStats> SpaceLandings { get; set; } = new();
	public Dictionary<int, bool> ProcessedEventIds { get; set; } = new();
}

public sealed class LifetimeSpaceStats
{
	public string Key { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public int SpaceIndex { get; set; }
	public long Count { get; set; }
}

public static class LifetimeStatsService
{
	public const int CurrentSchemaVersion = 1;
	private const string StatsDirectory = "stats";
	private const string StatsFile = "stats/lifetime-stats.json";
	private static LifetimeStatsData data;

	public static void RecordDiceRoll(
		string matchId,
		string gameIdentifier,
		IEnumerable<string> playerNames,
		int eventId,
		int dieA,
		int dieB )
	{
		if ( eventId < 1 || dieA is < 1 or > 6 || dieB is < 1 or > 6 )
			return;

		var match = GetOrCreateMatch( matchId, gameIdentifier, playerNames );
		if ( match is null || !TryRecordEvent( match, eventId ) )
			return;

		Increment( match.DiceFaceCounts, dieA );
		Increment( match.DiceFaceCounts, dieB );
		Increment( match.DiceTotalCounts, dieA + dieB );
		Save();
	}

	public static void RecordSpaceLanding(
		string matchId,
		string gameIdentifier,
		IEnumerable<string> playerNames,
		int eventId,
		string spaceKey,
		string displayName,
		int spaceIndex )
	{
		if ( eventId < 1 )
			return;

		var match = GetOrCreateMatch( matchId, gameIdentifier, playerNames );
		if ( match is null || !TryRecordEvent( match, eventId ) )
			return;

		spaceKey = string.IsNullOrWhiteSpace( spaceKey )
			? $"index:{spaceIndex}"
			: spaceKey.Trim();
		if ( !match.SpaceLandings.TryGetValue( spaceKey, out var space ) )
		{
			space = new LifetimeSpaceStats
			{
				Key = spaceKey,
				DisplayName = string.IsNullOrWhiteSpace( displayName )
					? $"Space {spaceIndex}"
					: displayName.Trim(),
				SpaceIndex = spaceIndex
			};
			match.SpaceLandings[spaceKey] = space;
		}

		space.Count++;
		Save();
	}

	public static CommandResult BuildDiceSummary( string matchId = "" )
	{
		var matches = ResolveMatches( matchId, out var error );
		if ( matches is null )
			return CommandResult.Fail( error );

		var faceCounts = new Dictionary<int, long>();
		var totalCounts = new Dictionary<int, long>();
		foreach ( var match in matches )
		{
			MergeCounts( faceCounts, match.DiceFaceCounts );
			MergeCounts( totalCounts, match.DiceTotalCounts );
		}

		var rollCount = totalCounts.Values.Sum();
		var title = BuildScopeTitle( matchId, matches.Count );
		var faces = string.Join( ", ", Enumerable.Range( 1, 6 )
			.Select( face => $"{face}:{GetCount( faceCounts, face )}" ) );
		var totals = string.Join( ", ", Enumerable.Range( 2, 11 )
			.Select( total => $"{total}:{GetCount( totalCounts, total )}" ) );
		return CommandResult.Success(
			$"Dice stats — {title}\nRolls: {rollCount}\nFaces: {faces}\nTotals: {totals}" );
	}

	public static CommandResult BuildSpaceSummary( string matchId = "" )
	{
		var matches = ResolveMatches( matchId, out var error );
		if ( matches is null )
			return CommandResult.Fail( error );

		var spaces = new Dictionary<string, LifetimeSpaceStats>();
		foreach ( var match in matches )
		{
			foreach ( var stored in match.SpaceLandings.Values )
			{
				if ( !spaces.TryGetValue( stored.Key, out var aggregate ) )
				{
					aggregate = new LifetimeSpaceStats
					{
						Key = stored.Key,
						DisplayName = stored.DisplayName,
						SpaceIndex = stored.SpaceIndex
					};
					spaces[stored.Key] = aggregate;
				}

				aggregate.Count += stored.Count;
			}
		}

		var totalLandings = spaces.Values.Sum( space => space.Count );
		var lines = spaces.Values
			.OrderByDescending( space => space.Count )
			.ThenBy( space => space.SpaceIndex )
			.Select( space => $"{space.DisplayName} [{space.SpaceIndex}]: {space.Count}" )
			.ToList();
		var details = lines.Count == 0 ? "No landings recorded." : string.Join( "\n", lines );
		return CommandResult.Success(
			$"Space stats — {BuildScopeTitle( matchId, matches.Count )}\nLandings: {totalLandings}\n{details}" );
	}

	public static CommandResult BuildMatchList( int limit = 20 )
	{
		limit = Math.Clamp( limit, 1, 100 );
		var matches = GetData().Matches.Values
			.OrderByDescending( match => ParseTimestamp( match.LastSeenUtc ) )
			.Take( limit )
			.ToList();
		if ( matches.Count == 0 )
			return CommandResult.Success( "No observed matches recorded." );

		var lines = matches.Select( match =>
		{
			var rolls = match.DiceTotalCounts.Values.Sum();
			var landings = match.SpaceLandings.Values.Sum( space => space.Count );
			var players = match.PlayerNames.Count == 0
				? "unknown players"
				: string.Join( ", ", match.PlayerNames );
			return $"{match.MatchId} — {players}; rolls {rolls}; landings {landings}";
		} );
		return CommandResult.Success( $"Observed matches ({matches.Count}):\n{string.Join( "\n", lines )}" );
	}

	private static LifetimeMatchStats GetOrCreateMatch(
		string matchId,
		string gameIdentifier,
		IEnumerable<string> playerNames )
	{
		if ( string.IsNullOrWhiteSpace( matchId ) )
			return null;

		matchId = matchId.Trim();
		var stats = GetData();
		var existingKey = stats.Matches.Keys.FirstOrDefault( key =>
			string.Equals( key, matchId, StringComparison.OrdinalIgnoreCase ) );
		if ( existingKey is null )
		{
			var now = DateTimeOffset.UtcNow.ToString( "O" );
			var match = new LifetimeMatchStats
			{
				MatchId = matchId,
				GameIdentifier = gameIdentifier?.Trim() ?? "",
				FirstSeenUtc = now,
				LastSeenUtc = now,
				PlayerNames = NormalizePlayerNames( playerNames )
			};
			stats.Matches[matchId] = match;
			return match;
		}

		var existing = stats.Matches[existingKey];
		existing.LastSeenUtc = DateTimeOffset.UtcNow.ToString( "O" );
		var names = NormalizePlayerNames( playerNames );
		if ( names.Count > 0 )
			existing.PlayerNames = names;
		return existing;
	}

	private static bool TryRecordEvent( LifetimeMatchStats match, int eventId )
	{
		match.ProcessedEventIds ??= new();
		if ( match.ProcessedEventIds.ContainsKey( eventId ) )
			return false;

		match.ProcessedEventIds[eventId] = true;
		match.LastSeenUtc = DateTimeOffset.UtcNow.ToString( "O" );
		return true;
	}

	private static List<LifetimeMatchStats> ResolveMatches( string matchId, out string error )
	{
		error = "";
		var stats = GetData();
		if ( string.IsNullOrWhiteSpace( matchId ) )
			return stats.Matches.Values.ToList();

		var match = stats.Matches.Values.FirstOrDefault( candidate =>
			string.Equals( candidate.MatchId, matchId.Trim(), StringComparison.OrdinalIgnoreCase ) );
		if ( match is not null )
			return new List<LifetimeMatchStats> { match };

		error = $"Unknown match ID '{matchId.Trim()}'. Run stat_matches.";
		return null;
	}

	private static LifetimeStatsData GetData()
	{
		if ( data is not null )
			return data;

		FileSystem.Data.CreateDirectory( StatsDirectory );
		if ( !FileSystem.Data.FileExists( StatsFile ) )
		{
			data = new LifetimeStatsData();
			return data;
		}

		try
		{
			data = FileSystem.Data.ReadJson<LifetimeStatsData>( StatsFile );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Could not read lifetime stats: {ex.Message}" );
		}

		data ??= new LifetimeStatsData();
		Normalize( data );
		return data;
	}

	private static void Save()
	{
		try
		{
			FileSystem.Data.CreateDirectory( StatsDirectory );
			FileSystem.Data.WriteJson( StatsFile, GetData() );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Could not write lifetime stats: {ex.Message}" );
		}
	}

	private static void Normalize( LifetimeStatsData stats )
	{
		stats.SchemaVersion = CurrentSchemaVersion;
		stats.Matches ??= new();
		foreach ( var match in stats.Matches.Values )
		{
			match.PlayerNames ??= new();
			match.DiceFaceCounts ??= new();
			match.DiceTotalCounts ??= new();
			match.SpaceLandings ??= new();
			match.ProcessedEventIds ??= new();
		}
	}

	private static List<string> NormalizePlayerNames( IEnumerable<string> playerNames )
	{
		return playerNames?
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Select( name => name.Trim() )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList() ?? new List<string>();
	}

	private static void Increment( Dictionary<int, long> counts, int value )
	{
		counts[value] = GetCount( counts, value ) + 1;
	}

	private static long GetCount( Dictionary<int, long> counts, int value )
	{
		return counts.TryGetValue( value, out var count ) ? count : 0;
	}

	private static void MergeCounts( Dictionary<int, long> target, Dictionary<int, long> source )
	{
		foreach ( var count in source )
			target[count.Key] = GetCount( target, count.Key ) + count.Value;
	}

	private static string BuildScopeTitle( string matchId, int matchCount )
	{
		return string.IsNullOrWhiteSpace( matchId )
			? $"all matches ({matchCount})"
			: matchId.Trim();
	}

	private static DateTimeOffset ParseTimestamp( string value )
	{
		return DateTimeOffset.TryParse( value, out var timestamp )
			? timestamp
			: DateTimeOffset.MinValue;
	}
}
