using System;

public sealed class TradeHistoryEntry
{
	public const string Accepted = "Accepted";
	public const string Denied = "Denied";

	public int Id { get; set; }
	public string Outcome { get; set; } = "";
	public TradeRequest Trade { get; set; }

	public string Serialize() => $"{Outcome}|{Trade?.Id ?? 0}|{Trade?.Serialize() ?? ""}";

	public static bool TryDeserialize( int id, string value, out TradeHistoryEntry entry )
	{
		entry = null;
		if ( string.IsNullOrWhiteSpace( value ) )
			return false;

		var separator = value.IndexOf( '|' );
		if ( separator <= 0 )
			return false;

		var payload = value[(separator + 1)..];
		var tradeId = id;
		var tradeIdSeparator = payload.IndexOf( '|' );
		if ( payload.Split( '|' ).Length == 9 && tradeIdSeparator > 0 && int.TryParse( payload[..tradeIdSeparator], out var parsedTradeId ) )
		{
			tradeId = parsedTradeId;
			payload = payload[(tradeIdSeparator + 1)..];
		}

		if ( !TradeRequest.TryDeserialize( tradeId, payload, out var trade ) )
			return false;

		entry = new TradeHistoryEntry { Id = id, Outcome = value[..separator], Trade = trade };
		return true;
	}
}
