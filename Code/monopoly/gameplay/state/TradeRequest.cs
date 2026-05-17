using System;
using Sandbox;

public sealed class TradeRequest
{
	public int Id { get; set; }
	public int SenderPlayerIndex { get; set; }
	public int ReceiverPlayerIndex { get; set; }
	public int SenderMoney { get; set; }
	public int ReceiverMoney { get; set; }
	public List<int> SenderPropertyIndexes { get; set; } = new();
	public List<int> ReceiverPropertyIndexes { get; set; } = new();

	public bool InvolvesPlayer( int playerIndex )
	{
		return SenderPlayerIndex == playerIndex || ReceiverPlayerIndex == playerIndex;
	}

	public bool IsEmpty =>
		SenderMoney <= 0 &&
		ReceiverMoney <= 0 &&
		SenderPropertyIndexes.Count == 0 &&
		ReceiverPropertyIndexes.Count == 0;

	public string Serialize()
	{
		return string.Join( "|",
			SenderPlayerIndex,
			ReceiverPlayerIndex,
			Math.Max( SenderMoney, 0 ),
			Math.Max( ReceiverMoney, 0 ),
			string.Join( ",", SenderPropertyIndexes.Distinct().OrderBy( x => x ) ),
			string.Join( ",", ReceiverPropertyIndexes.Distinct().OrderBy( x => x ) )
		);
	}

	public static bool TryDeserialize( int id, string value, out TradeRequest request )
	{
		request = null;

		if ( string.IsNullOrWhiteSpace( value ) )
			return false;

		var parts = value.Split( '|' );
		if ( parts.Length != 6 )
			return false;

		if ( !int.TryParse( parts[0], out var senderIndex ) ||
			!int.TryParse( parts[1], out var receiverIndex ) ||
			!int.TryParse( parts[2], out var senderMoney ) ||
			!int.TryParse( parts[3], out var receiverMoney ) )
			return false;

		request = new TradeRequest
		{
			Id = id,
			SenderPlayerIndex = senderIndex,
			ReceiverPlayerIndex = receiverIndex,
			SenderMoney = Math.Max( senderMoney, 0 ),
			ReceiverMoney = Math.Max( receiverMoney, 0 ),
			SenderPropertyIndexes = ParsePropertyIndexes( parts[4] ),
			ReceiverPropertyIndexes = ParsePropertyIndexes( parts[5] )
		};

		return true;
	}

	private static List<int> ParsePropertyIndexes( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return new();

		return value.Split( ',', StringSplitOptions.RemoveEmptyEntries )
			.Select( part => int.TryParse( part, out var index ) ? index : -1 )
			.Where( index => index >= 0 )
			.Distinct()
			.OrderBy( index => index )
			.ToList();
	}
}
