using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

public sealed partial class GameController
{
	private static readonly char[] MentionTrailingCharacters = ['.', ',', '!', '?', ':', ';', ')', ']', '}'];

	[Rpc.Host]
	public void RequestSendChatMessage( string rawMessage )
	{
		if ( Rpc.Caller is null )
			return;

		var message = (rawMessage ?? "").Replace( '\u00A0', ' ' ).Trim();
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		if ( message.Length > MaxChatMessageLength )
			message = message[..MaxChatMessageLength];

		if ( TryHandleChatCommand( message ) )
			return;

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

	private bool TryHandleChatCommand( string message )
	{
		if ( string.Equals( message, DisplayStatsLogCommand.Name, StringComparison.OrdinalIgnoreCase ) )
		{
			DisplayStatsLog();
			return true;
		}

		return false;
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

		var mentions = new HashSet<PlayerState>();
		foreach ( var mention in FindPlayerMentions( message ) )
		{
			if ( mentions.Add( mention.Player ) )
				yield return mention.Player;
		}
	}

	private string NormalizeMentionSpacing( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return message ?? "";

		var mentions = FindPlayerMentions( message, true, true ).ToList();
		if ( mentions.Count == 0 )
			return message;

		var normalized = message;
		for ( var i = mentions.Count - 1; i >= 0; i-- )
		{
			var mention = mentions[i];
			if ( mention.End >= normalized.Length || char.IsWhiteSpace( normalized[mention.End] ) )
				continue;

			normalized = normalized.Insert( mention.End, " " );
		}

		return normalized;
	}

	private IEnumerable<PlayerMention> FindPlayerMentions( string message, bool allowMissingTrailingSpace = false, bool forceMentionBoundary = false )
	{
		if ( string.IsNullOrWhiteSpace( message ) || Players is null )
			yield break;

		var mentionablePlayers = Players
			.Where( player => player is not null && player.IsAssigned )
			.Select( player => new
			{
				Player = player,
				Name = (player.PlayerName ?? "").Trim()
			} )
			.Where( entry => !string.IsNullOrWhiteSpace( entry.Name ) )
			.OrderByDescending( entry => entry.Name.Length )
			.ToList();

		if ( mentionablePlayers.Count == 0 )
			yield break;

		for ( var index = 0; index < message.Length; index++ )
		{
			if ( message[index] != '@' || (index > 0 && !char.IsWhiteSpace( message[index - 1] )) )
				continue;

			var entriesToSearch = forceMentionBoundary
				? mentionablePlayers
					.OrderByDescending( entry => entry.Name.Contains( ' ' ) )
					.ThenBy( entry => entry.Name.Length )
					.ToList()
				: mentionablePlayers;

			foreach ( var entry in entriesToSearch )
			{
				var nameStart = index + 1;
				if ( nameStart + entry.Name.Length > message.Length )
					continue;

				if ( !message.AsSpan( nameStart, entry.Name.Length ).Equals( entry.Name, StringComparison.OrdinalIgnoreCase ) )
					continue;

				var nameEnd = nameStart + entry.Name.Length;
				var end = nameEnd;
				if ( end < message.Length && MentionTrailingCharacters.Contains( message[end] ) )
					end++;

				if ( end < message.Length && !char.IsWhiteSpace( message[end] ) && !allowMissingTrailingSpace )
					continue;

				yield return new PlayerMention( index, end, entry.Player );
				index = end - 1;
				break;
			}
		}
	}

	private readonly record struct PlayerMention( int Start, int End, PlayerState Player );

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
