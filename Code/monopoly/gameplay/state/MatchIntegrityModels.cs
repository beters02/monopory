using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class DiceHistoryEntry
{
	public int RollIndex { get; set; }
	public int TurnNumber { get; set; }
	public int PlayerIndex { get; set; }
	public string PlayerName { get; set; } = "";
	public int DieA { get; set; }
	public int DieB { get; set; }
	public int Total { get; set; }
	public bool IsJailAttempt { get; set; }
	public bool IsForced { get; set; }
	public string Context { get; set; } = "";
}

public sealed class AdminHistoryEntry
{
	public int Id { get; set; }
	public int TurnNumber { get; set; }
	public string AdminName { get; set; } = "";
	public string Command { get; set; } = "";
	public string Target { get; set; } = "";
	public string Message { get; set; } = "";
}

public sealed class MoveHistoryPlayerSnapshot
{
	public int PlayerIndex { get; set; }
	public string PlayerName { get; set; } = "";
	public int Money { get; set; }
	public int SpaceIndex { get; set; }
	public bool IsInJail { get; set; }
	public bool IsBankrupt { get; set; }
	public int PropertyCount { get; set; }
	public bool IsTurnHolder { get; set; }
}

public sealed class MoveHistoryMoneyDelta
{
	public int PlayerIndex { get; set; }
	public string PlayerName { get; set; } = "";
	public int BeforeMoney { get; set; }
	public int AfterMoney { get; set; }
	public int Delta { get; set; }
	public string Reason { get; set; } = "";
	public int EventId { get; set; }
}

public sealed class MoveHistoryEvent
{
	public int Id { get; set; }
	public string Kind { get; set; } = "";
	public string Title { get; set; } = "";
	public string Message { get; set; } = "";
}

public sealed class MoveHistoryTurn
{
	public int TurnNumber { get; set; }
	public int PlayerIndex { get; set; }
	public string PlayerName { get; set; } = "";
	public bool IsFinalized { get; set; }
	public List<MoveHistoryPlayerSnapshot> StartPlayers { get; set; } = new();
	public List<MoveHistoryPlayerSnapshot> EndPlayers { get; set; } = new();
	public List<MoveHistoryEvent> Events { get; set; } = new();
	public List<MoveHistoryMoneyDelta> MoneyDeltas { get; set; } = new();
}

public static class MatchIntegrityJson
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public static string Serialize<T>( T value ) => JsonSerializer.Serialize( value, Options );

	public static T Deserialize<T>( string json ) where T : new()
	{
		if ( string.IsNullOrWhiteSpace( json ) )
			return new T();

		try
		{
			return JsonSerializer.Deserialize<T>( json, Options ) ?? new T();
		}
		catch
		{
			return new T();
		}
	}
}
