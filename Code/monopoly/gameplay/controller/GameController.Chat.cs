using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController
{
	private static readonly Regex MentionRegex = new( @"(?<!\S)@([^\s@]+)", RegexOptions.Compiled );

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
		message = NormalizeMentionSpacing( message );
		var senderName = (sender?.PlayerName ?? "").Trim();
		if ( string.IsNullOrWhiteSpace( senderName ) )
		{
			var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
			senderName = playerIndex >= 0 ? $"Player {playerIndex + 1}" : "Player";
		}

		AppendChatMessage( senderName, message );
		PlayChatMessageSounds( Rpc.Caller.SteamId, message );
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

	private void PlayChatMessageSounds( SteamId senderSteamId, string message )
	{
		var mentionedSteamIds = GetMentionedPlayers( message )
			.Select( player => (SteamId)player.OwnerId )
			.ToHashSet();

		foreach ( var connection in Connection.All )
		{
			if ( connection is null )
				continue;

			if ( connection.SteamId == senderSteamId )
			{
				PlaySoundToConnection( connection, GameAssets.Sounds.ChatSent );
				continue;
			}

			var sound = mentionedSteamIds.Contains( connection.SteamId )
				? GameAssets.Sounds.ChatMentioned
				: GameAssets.Sounds.ChatReceived;

			PlaySoundToConnection( connection, sound );
		}
	}

	private IEnumerable<PlayerState> GetMentionedPlayers( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) || Players is null || Players.Count == 0 )
			yield break;

		var mentions = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( Match match in MentionRegex.Matches( message ) )
		{
			var token = match.Groups[1].Value.TrimEnd( '.', ',', '!', '?', ':', ';', ')', ']', '}' );
			if ( !string.IsNullOrWhiteSpace( token ) )
				mentions.Add( token );
		}

		if ( mentions.Count == 0 )
			yield break;

		foreach ( var player in Players )
		{
			if ( player is null || !player.IsAssigned )
				continue;

			var playerName = (player.PlayerName ?? "").Trim();
			if ( string.IsNullOrWhiteSpace( playerName ) )
				continue;

			if ( mentions.Contains( playerName ) )
				yield return player;
		}
	}

	private string NormalizeMentionSpacing( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return message ?? "";

		var mentionedNames = GetMentionedPlayers( message )
			.Select( player => (player.PlayerName ?? "").Trim())
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );

		if ( mentionedNames.Count == 0 )
			return message;

		return MentionRegex.Replace( message, match =>
		{
			var full = match.Value;
			var name = match.Groups[1].Value.TrimEnd( '.', ',', '!', '?', ':', ';', ')', ']', '}' );
			if ( !mentionedNames.Contains( name ) )
				return full;

			var matchEnd = match.Index + match.Length;
			if ( matchEnd < message.Length && !char.IsWhiteSpace( message[matchEnd] ) )
				return $"{full} ";

			return full;
		} );
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
