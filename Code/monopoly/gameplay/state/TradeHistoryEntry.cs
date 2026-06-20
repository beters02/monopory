using System;

public sealed class TradeHistoryEntry
{
	public const string Accepted = "Accepted";
	public const string Denied = "Denied";

	public int Id { get; set; }
	public string Outcome { get; set; } = "";
	public TradeRequest Trade { get; set; }

	public string Serialize() => $"{Outcome}|{Trade?.Serialize() ?? ""}";

	public static bool TryDeserialize( int id, string value, out TradeHistoryEntry entry )
	{
		entry = null;
		if ( string.IsNullOrWhiteSpace( value ) )
			return false;

		var separator = value.IndexOf( '|' );
		if ( separator <= 0 || !TradeRequest.TryDeserialize( id, value[(separator + 1)..], out var trade ) )
			return false;

		entry = new TradeHistoryEntry { Id = id, Outcome = value[..separator], Trade = trade };
		return true;
	}
}
