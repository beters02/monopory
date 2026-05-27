using System;
using Sandbox;

public sealed partial class GameController
{
	private const int MaxChatMessages = 80;
	private const int MaxChatMessageLength = 220;

	[Rpc.Host]
	public void RequestSendChatMessage( string rawMessage )
	{
		if ( Rpc.Caller is null )
			return;

		var message = (rawMessage ?? "").Trim();
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		if ( message.Length > MaxChatMessageLength )
			message = message[..MaxChatMessageLength];

		var sender = GetPlayerForConnection( Rpc.Caller );
		var senderName = (sender?.PlayerName ?? "").Trim();
		if ( string.IsNullOrWhiteSpace( senderName ) )
		{
			var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
			senderName = playerIndex >= 0 ? $"Player {playerIndex + 1}" : "Player";
		}

		AppendChatMessage( senderName, message );
	}

	private void AppendChatMessage( string senderName, string message )
	{
		var id = Math.Max( NextChatMessageId, 1 );
		NextChatMessageId = id + 1;

		var sanitizedSender = SanitizeChatField( senderName );
		var sanitizedMessage = SanitizeChatField( message );
		ChatMessages[id] = $"{sanitizedSender}\t{sanitizedMessage}";

		while ( ChatMessages.Count > MaxChatMessages )
		{
			var oldestId = int.MaxValue;
			foreach ( var entry in ChatMessages )
			{
				if ( entry.Key < oldestId )
					oldestId = entry.Key;
			}

			if ( oldestId == int.MaxValue )
				break;

			ChatMessages.Remove( oldestId );
		}
	}

	public void SendSystemChatMessage( string message )
	{
		if ( !Networking.IsHost )
			return;

		var text = (message ?? "").Trim();
		if ( string.IsNullOrWhiteSpace( text ) )
			return;

		if ( text.Length > MaxChatMessageLength )
			text = text[..MaxChatMessageLength];

		AppendChatMessage( "System", text );
	}

	private static string SanitizeChatField( string value )
	{
		if ( string.IsNullOrEmpty( value ) )
			return "";

		return value
			.Replace( "\t", " " )
			.Replace( "\r", " " )
			.Replace( "\n", " " )
			.Trim();
	}
}
