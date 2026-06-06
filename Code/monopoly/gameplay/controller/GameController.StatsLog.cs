using System;
using System.Linq;
using Sandbox;

public sealed partial class GameController : Component
{
	private const int MinDiceFaceValue = 1;
	private const int MaxDiceFaceValue = 6;

	private void RecordPendingDiceRollForStats()
	{
		if ( PendingRollStatsRecorded )
			return;

		if ( !IsValidDieValue( LastDieA ) || !IsValidDieValue( LastDieB ) )
			return;

		StatsLogDiceFaceCounts[LastDieA] = GetDiceFaceCount( LastDieA ) + 1;
		StatsLogDiceFaceCounts[LastDieB] = GetDiceFaceCount( LastDieB ) + 1;
		PendingRollStatsRecorded = true;
	}

	public string BuildStatsLogText()
	{
		var totalDice = Enumerable
			.Range( MinDiceFaceValue, MaxDiceFaceValue - MinDiceFaceValue + 1 )
			.Sum( GetDiceFaceCount );

		if ( totalDice <= 0 )
			return "No dice values have been recorded yet.";

		var entries = Enumerable
			.Range( MinDiceFaceValue, MaxDiceFaceValue - MinDiceFaceValue + 1 )
			.Select( face => $"{face}: {GetDiceFaceCount( face )}" );

		return $"Dice values rolled ({totalDice} dice total): {string.Join( ", ", entries )}";
	}

	public void DisplayStatsLog()
	{
		if ( !Networking.IsHost )
			return;

		SendTableChatMessage( "Stats log", BuildStatsLogText() );
	}

	private int GetDiceFaceCount( int face )
	{
		if ( StatsLogDiceFaceCounts is null )
			return 0;

		return StatsLogDiceFaceCounts.TryGetValue( face, out var count ) ? Math.Max( count, 0 ) : 0;
	}
}
