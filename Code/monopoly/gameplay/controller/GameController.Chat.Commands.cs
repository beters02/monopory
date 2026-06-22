using System;
using System.Collections.Generic;
using Sandbox;

public sealed partial class GameController : Component
{
	private bool TryHandleChatCommand( string message )
	{
		return ChatCommandManager.TryExecute( this, Rpc.Caller, message );
	}

	internal void ClearChatForPlayer( Connection caller )
	{
		if ( caller is null )
			return;

		using ( Rpc.FilterInclude( caller ) )
			ClearChatLocal( NextChatMessageId - 1 );
	}

	internal void SendPrivateChatMessage( Connection caller, string targetPlayerName, string privateMessage )
	{
		if ( caller is null )
			return;

		if ( string.IsNullOrWhiteSpace( targetPlayerName ) || string.IsNullOrWhiteSpace( privateMessage ) )
		{
			SendSystemChatMessageToConnection( caller, "Usage: /msg {player} {message}" );
			return;
		}

		var sender = GetPlayerForConnection( caller );
		var receiver = ResolvePlayerReference( targetPlayerName, caller );
		if ( sender is null || receiver is null )
		{
			SendSystemChatMessageToConnection( caller, $"Could not send private message to {targetPlayerName}." );
			return;
		}

		privateMessage = NormalizeMentionSpacing( privateMessage.Trim() );
		var receiverConnection = GetConnectionForPlayer( receiver );
		if ( receiverConnection is null )
		{
			SendSystemChatMessageToConnection( caller, $"{receiver.PlayerName} is not connected." );
			return;
		}

		using ( Rpc.FilterInclude( caller ) )
			AppendLocalChatMessage( "Whisper", $"To {receiver.PlayerName}: {privateMessage}" );

		using ( Rpc.FilterInclude( receiverConnection ) )
			AppendLocalChatMessage( "Whisper", $"From {sender.PlayerName}: {privateMessage}" );

		PlaySoundToConnection( caller, GameAssets.Sounds.ChatSent );
		PlaySoundToConnection( receiverConnection, GameAssets.Sounds.ChatMentioned );
	}

	internal void SendPrivateChatMessageFromArguments( Connection caller, string arguments )
	{
		if ( string.IsNullOrWhiteSpace( arguments ) )
		{
			SendSystemChatMessageToConnection( caller, "Usage: /msg {player} {message}" );
			return;
		}

		if ( TrySplitPrivateMessageArguments( arguments, out var targetPlayerName, out var privateMessage ) )
		{
			SendPrivateChatMessage( caller, targetPlayerName, privateMessage );
			return;
		}

		SendSystemChatMessageToConnection( caller, "Usage: /msg {player} {message}" );
	}

	private bool TrySplitPrivateMessageArguments( string arguments, out string targetPlayerName, out string privateMessage )
	{
		targetPlayerName = "";
		privateMessage = "";

		var text = (arguments ?? "").Trim();
		if ( string.IsNullOrWhiteSpace( text ) )
			return false;

		var playerNames = Players
			.Where( player => player is not null && player.IsAssigned )
			.Select( player => (player.PlayerName ?? "").Trim() )
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.OrderByDescending( name => name.Length );

		foreach ( var playerName in playerNames )
		{
			if ( text.Length <= playerName.Length )
				continue;

			if ( !text.StartsWith( playerName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( !char.IsWhiteSpace( text[playerName.Length] ) )
				continue;

			targetPlayerName = playerName;
			privateMessage = text[playerName.Length..].Trim();
			return !string.IsNullOrWhiteSpace( privateMessage );
		}

		var parts = text.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		if ( parts.Length < 2 )
			return false;

		targetPlayerName = parts[0];
		privateMessage = parts[1];
		return true;
	}
}

[AttributeUsage( AttributeTargets.Method )]
public sealed class ChatCmdAttribute : Attribute
{
	public string Name { get; }
	public string Usage { get; }
	public string Description { get; }

	public ChatCmdAttribute( string name, string usage = "", string description = "" )
	{
		Name = name ?? "";
		Usage = usage ?? "";
		Description = description ?? "";
	}
}

public sealed class ChatCommand
{
	public string Name { get; init; }
	public MethodDescription Method { get; init; }
	public ChatCmdAttribute Attribute { get; init; }

	public string Usage =>
		string.IsNullOrWhiteSpace( Attribute?.Usage ) ? $"/{Name}" : Attribute.Usage;

	public ChatCommand( string name, MethodDescription method, ChatCmdAttribute attribute )
	{
		Name = name;
		Method = method;
		Attribute = attribute;
	}
}

public static class ChatCommands
{
	private static readonly Dictionary<string, ChatCommand> Commands = new( StringComparer.OrdinalIgnoreCase );
	private static bool registered;

	public static IReadOnlyDictionary<string, ChatCommand> All
	{
		get
		{
			EnsureRegistered();
			return Commands;
		}
	}

	private static void EnsureRegistered()
	{
		if ( registered )
			return;

		registered = true;
		foreach ( var (method, attribute) in Game.TypeLibrary.GetMethodsWithAttribute<ChatCmdAttribute>() )
		{
			var name = attribute.Name;
			if ( string.IsNullOrWhiteSpace( name ) )
				name = method.Name;

			name = ChatCommandManager.NormalizeCommandName( name );
			Commands[name] = new ChatCommand( name, method, attribute );
		}
	}
}

public static class ChatCommandManager
{
	public static bool TryExecute( GameController game, Connection caller, string rawMessage )
	{
		if ( game is null || caller is null )
			return false;

		if ( string.IsNullOrWhiteSpace( rawMessage ) || !rawMessage.StartsWith( "/", StringComparison.Ordinal ) )
			return false;

		var parts = rawMessage[1..].Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		if ( parts.Length == 0 )
			return false;

		var commandName = NormalizeCommandName( parts[0] );
		var arguments = parts.Length > 1 ? parts[1] : "";
		if ( !ChatCommands.All.TryGetValue( commandName, out var command ) )
		{
			GameController.SendSystemChatMessageToConnection( caller, $"Unknown chat command /{parts[0]}. Use /help." );
			return true;
		}

		try
		{
			command.Method.Invoke( null, new object[] { game, caller, arguments } );
		}
		catch ( Exception exception )
		{
			Log.Error( $"Chat command /{commandName} threw: {exception.Message}" );
			GameController.SendSystemChatMessageToConnection( caller, $"Chat command /{commandName} failed." );
		}

		return true;
	}

	public static string NormalizeCommandName( string commandName )
	{
		return (commandName ?? "").Trim().TrimStart( '/' ).ToLowerInvariant();
	}

	public static string BuildHelpText()
	{
		var usages = ChatCommands.All.Values
			.OrderBy( command => command.Name )
			.Select( command => command.Usage );

		return $"Chat commands: {string.Join( ", ", usages )}";
	}
}

public static class HelpChatCommand
{
	public const string Name = "help";

	[ChatCmd( Name, "/help", "Shows chat commands." )]
	public static void Execute( GameController game, Connection caller, string arguments )
	{
		GameController.SendSystemChatMessageToConnection( caller, ChatCommandManager.BuildHelpText() );
	}
}

public static class ClearChatCommand
{
	public const string Name = "clear";

	[ChatCmd( Name, "/clear", "Clears your local chat view." )]
	public static void Execute( GameController game, Connection caller, string arguments )
	{
		game?.ClearChatForPlayer( caller );
	}
}

public static class PrivateMessageChatCommand
{
	public const string Name = "msg";

	[ChatCmd( Name, "/msg {player} {message}", "Sends a private message." )]
	public static void Execute( GameController game, Connection caller, string arguments )
	{
		game?.SendPrivateChatMessageFromArguments( caller, arguments );
	}
}
