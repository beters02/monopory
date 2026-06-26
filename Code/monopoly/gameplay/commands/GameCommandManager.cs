using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.ui.components;

[AttributeUsage( AttributeTargets.Method )]
public sealed class CheatCmdAttribute : Attribute {}
[AttributeUsage( AttributeTargets.Method )]
public sealed class HostCheatCmdAttribute : Attribute {}
[AttributeUsage( AttributeTargets.Method )]
public sealed class HostCmdAttribute : Attribute {}
[AttributeUsage( AttributeTargets.Method )]
public sealed class StandaloneCmdAttribute : Attribute {}
[AttributeUsage( AttributeTargets.Method )]
public sealed class SourceOverwriteCmdAttribute : Attribute
{
	public string Name { get; set; }

	public SourceOverwriteCmdAttribute( string name )
	{
		Name = name;
	}
}

public sealed class CommandResult
{
	public bool Ok { get; }
	public string Message { get; }

	private CommandResult( bool ok, string message = "" )
	{
		Ok = ok;
		Message = message ?? "";
	}

	public static CommandResult Success( string message = "" ) => new( true, message );
	public static CommandResult Fail( string message ) => new( false, message );
	public static CommandResult FailCheats() => new( false, "Cheats must be enabled to use this command." );
	public static CommandResult FailHost() => new( false, "Must be host to use this command." );
	public static CommandResult FailCheatsOrHost() => new( false, "Must be host or have cheats enabled to use this command." );
	public static CommandResult FailStandalone() => new( false, "This command is only allowed in standalone." );
}

public enum GameCommandCheatType
{
	None,
	Cheats,
	HostOrCheats,
	Host
}

public enum GameCommandAppType
{
	None,
	Standalone
}

public sealed class GameCommand
{
	public string Name { get; init; }
	public MethodDescription Method { get; init; }
	public ConCmdAttribute Attribute { get; init; }
	public SourceOverwriteCmdAttribute SourceAttribute { get; init; }
	public MatchConfigOption MatchConfigOption { get; init; }
	public GameCommandCheatType CheatType { get; init; }
	public GameCommandAppType AppType { get; init; }
	public bool IsSourceOverwrite { get; init; }
	public bool IsMatchConfigCommand { get; init; }

	public GameCommand( string name, MethodDescription method, ConCmdAttribute attribute, GameCommandCheatType cheatType, GameCommandAppType appType )
	{
		Name = name;
		Method = method;
		Attribute = attribute;
		CheatType = cheatType;
		AppType = appType;
		IsSourceOverwrite = false;
	}

	public GameCommand( string name, MethodDescription method, SourceOverwriteCmdAttribute attribute, GameCommandCheatType cheatType, GameCommandAppType appType )
	{
		Name = name;
		Method = method;
		SourceAttribute = attribute;
		CheatType = cheatType;
		AppType = appType;
		IsSourceOverwrite = true;
	}

	public GameCommand( string name, MatchConfigOption option )
	{
		Name = name;
		MatchConfigOption = option;
		CheatType = option?.GameCommandCheatType ?? GameCommandCheatType.Host;
		AppType = GameCommandAppType.None;
		IsSourceOverwrite = true;
		IsMatchConfigCommand = true;
	}
}

public static class GameCommands
{
	private static readonly Dictionary<string, GameCommand> Commands = new();
	private static bool registered;

	public static IReadOnlyDictionary<string, GameCommand> All
	{
		get
		{
			EnsureRegistered();
			return Commands;
		}
	}

	private class MethodHelperObject
	{
		public ConCmdAttribute conCmdAttribute;
		public SourceOverwriteCmdAttribute sourceOverwriteCmdAttribute;
		public MethodDescription method;
	}

	private static void EnsureRegistered()
	{
		if ( registered )
			return;

		registered = true;
		

		List<MethodHelperObject> methodsToGoOver = new();

		foreach ( var (_method, attribute) in Game.TypeLibrary.GetMethodsWithAttribute<ConCmdAttribute>() )
		{
			MethodHelperObject obj = new()
			{
				method = _method,
				conCmdAttribute = attribute
			};

			methodsToGoOver.Add(obj);
		}

		foreach ( var (_method, attribute ) in Game.TypeLibrary.GetMethodsWithAttribute<SourceOverwriteCmdAttribute>())
		{
			MethodHelperObject obj = new()
			{
				method = _method,
				sourceOverwriteCmdAttribute = attribute
			};

			methodsToGoOver.Add(obj);
		}

		foreach ( var obj in methodsToGoOver )
		{
			var method = obj.method;

			var name = obj.conCmdAttribute is not null ? obj.conCmdAttribute.Name : obj.sourceOverwriteCmdAttribute.Name;
			if ( string.IsNullOrWhiteSpace( name ) )
				name = method.Name;

			GameCommandCheatType cheatType = GameCommandCheatType.None;
			
			if ( method.GetCustomAttribute<CheatCmdAttribute>() is not null )
				cheatType = GameCommandCheatType.Cheats;
			else if ( method.GetCustomAttribute<HostCheatCmdAttribute>() is not null )
				cheatType = GameCommandCheatType.HostOrCheats;
			else if ( method.GetCustomAttribute<HostCmdAttribute>() is not null )
				cheatType = GameCommandCheatType.Host;

			GameCommandAppType appType = GameCommandAppType.None;

			if ( method.GetCustomAttribute<StandaloneCmdAttribute>() is not null )
				appType = GameCommandAppType.Standalone;

			if (obj.sourceOverwriteCmdAttribute is not null)
				Commands[name] = new GameCommand( name, method, obj.sourceOverwriteCmdAttribute, cheatType, appType );
			else
				Commands[name] = new GameCommand( name, method, obj.conCmdAttribute, cheatType, appType );
			
		}

		foreach ( var option in MatchConfigSchema.Options )
		{
			if ( option is null || string.IsNullOrWhiteSpace( option.CommandId ) )
				continue;

			var name = $"mn_{option.CommandId.Trim()}";
			Commands[name] = new GameCommand( name, option );
		}
	}
}

public sealed class GameCommandManager : Component
{
	private bool firstRun = true;
	private IReadOnlyDictionary<string, GameCommand> _commands = GameCommands.All;
	private static IReadOnlyDictionary<string, GameCommand> Commands;

	protected override void OnAwake()
	{
		base.OnAwake();
		Commands = _commands;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( firstRun )
			firstRun = false;
	}

	internal static string JoinPlayerName( string playerName, string[] playerNameTail )
	{
		if ( playerNameTail is null || playerNameTail.Length == 0 )
			return playerName;

		return string.Join( " ", new[] { playerName }.Concat( playerNameTail ) );
	}

	internal static void ExecuteCommand( string commandName, Func<CommandResult> callback )
	{
		try
		{
			LogCommandResult( commandName, callback() );
		}
		catch ( Exception exception )
		{
			var message = $"{commandName} command threw: {exception.Message}";
			WriteStandaloneConsoleLine( message, "err" );
		}
	}

	internal static void RunCommand(string commandName, Func<CommandResult> callback, Connection caller = null )
	{

		var commands = Commands ?? GameCommands.All;
		if ( caller == null || !commands.TryGetValue( commandName, out GameCommand value ) )
		{
			ExecuteCommand( commandName, callback );
			return;
		}

		if ( value.AppType == GameCommandAppType.Standalone )
			if ( !CanUseStandaloneCommand() )
			{
				LogCommandResult( commandName, CommandResult.FailStandalone() );
				return;
			}

		if ( value.CheatType == GameCommandCheatType.Cheats )
			if ( !CanUseCheatCommand( caller ) )
			{
				LogCommandResult( commandName, CommandResult.FailCheats());
				return;
			}
		else if ( value.CheatType == GameCommandCheatType.HostOrCheats )
			if ( !CanUseHostCheatCommand( caller ) )
			{
				LogCommandResult( commandName, CommandResult.FailCheats());
				return;
			}
		else if ( value.CheatType == GameCommandCheatType.Host )
			if ( !CanUseHostCommand( caller ))
			{
				LogCommandResult( commandName, CommandResult.FailHost());
				return;
			}

		CommandResult result;
		try
		{
			result = callback();
			LogCommandResult( commandName, result );
		}
		catch ( Exception exception )
		{
			var message = $"{commandName} command threw: {exception.Message}";
			WriteStandaloneConsoleLine( message, "error" );
			return;
		}

		if ( result?.Ok == true )
		{
			var commandType = GetCommandTypeLabel( value );
			GameController.Instance?.RecordCommand( caller, commandName, commandType );
			if ( value.CheatType == GameCommandCheatType.Cheats )
				GameController.Instance?.RecordAdminCommand( caller, commandName, "match" );
		}
	}

	public static void RunCommandFromName( Connection caller, string commandName, params string[] args )
	{
		var commands = Commands ?? GameCommands.All;
		if ( !commands.TryGetValue( commandName, out var command ) )
		{
			LogCommandResult(commandName, CommandResult.Fail($"Could not find command {commandName}"));
			return;
		}

		if ( command.IsMatchConfigCommand )
		{
			RunCommand( commandName, () => RunMatchConfigCommand( command, args ), caller );
			return;
		}

		var useArgs = new object[1 + (args?.Length ?? 0)];
		useArgs[0] = caller;

		for ( var i = 0; i < (args?.Length ?? 0); i++ )
			useArgs[i + 1] = args[i];
		
		command.Method.Invoke( null, useArgs );
	}

	private static CommandResult RunMatchConfigCommand( GameCommand command, string[] args )
	{
		var option = command?.MatchConfigOption;
		if ( option is null )
			return CommandResult.Fail( "Match config option is missing." );

		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game controller." );

		var rawValue = args is null || args.Length == 0 ? null : string.Join( " ", args );
		return game.TryRunMatchConfigCommand( option, rawValue, out var message )
			? CommandResult.Success( message )
			: CommandResult.Fail( message );
	}

	private static string GetCommandTypeLabel( GameCommand command )
	{
		if ( command is null )
			return "Unrestricted";

		return command.CheatType switch
		{
			GameCommandCheatType.Cheats => "CheatCmd",
			GameCommandCheatType.HostOrCheats => "HostCheatCmd",
			GameCommandCheatType.Host => "HostCmd",
			_ => "Unrestricted"
		};
	}

	internal static bool CanUseCheatCommand( Connection caller )
	{
		return MonopolyApp.IsCheatsEnabled();
	}

	internal static bool CanUseHostCheatCommand( Connection caller )
	{
		if ( Networking.IsHost && (caller is null || caller == Connection.Local) )
			return true;

		if ( GameController.Instance?.IsEffectiveHostCaller( caller ) == true )
			return true;

		return MonopolyApp.IsCheatsEnabled();
	}

	internal static bool CanUseHostCommand( Connection caller )
	{
		return Networking.IsHost && (caller is null || caller == Connection.Local);
	}

	internal static bool CanUseStandaloneCommand()
	{
		return MonopolyApp.IsStandalone();
	}

	internal static bool HasUnresolvedPendingBuyDecision( GameController game, PlayerState player )
	{
		return game is not null &&
			player is not null &&
			game.Phase == GamePhase.WaitingForBuyDecision &&
			game.CurrentPlayer == player &&
			game.PendingPurchaseSpaceIndex >= 0;
	}

	internal static string GetPendingBuyDecisionMessage( GameController game )
	{
		var pendingDef = game?.Board?.GetSpaceDef( game.PendingPurchaseSpaceIndex );
		var pendingName = pendingDef?.DisplayName ?? "the pending property";

		return $"Resolve the pending property decision for {pendingName} before buying other properties for that player.";
	}

	private static void LogCommandResult( string commandName, CommandResult result )
	{

		if ( result is null )
		{
			var message = $"{commandName} command failed: no command result.";
			WriteStandaloneConsoleLine( message, "wrn" );
			return;
		}

		if ( !result.Ok )
		{
			var message = $"{commandName} command failed: {result.Message}";
			WriteStandaloneConsoleLine( message, "wrn" );
			return;
		}

		if ( string.IsNullOrWhiteSpace( result.Message ) )
		{
			//var message = $"{commandName} command succeeded.";
			var message = result.Message;
			WriteStandaloneConsoleLine( message, "msg" );
			return;
		}

		var successMessage = $"{commandName}: {result.Message}";
		WriteStandaloneConsoleLine( successMessage, "msg" );
	}

	private static void WriteStandaloneConsoleLine( string message, string kind ) =>
		Sandbox.ui.components.StandaloneConsole.WriteLine( message, kind );

	/*private void HandlePreviouslyExistingCommands()
	{
		var cheatsEnabled = MonopolyApp.CheatsEnabled;
		if ( cheatsEnabled != lastCheatsEnabled )
		{
			var oldValue = lastCheatsEnabled;
			lastCheatsEnabled = cheatsEnabled;
			OnSvCheatsChanged( oldValue, cheatsEnabled, firstRun );
		}
	}

	private void OnSvCheatsChanged( bool oldValue, bool newValue, bool wasFirstRun )
	{
		OnSvCheatsChangedStatic( oldValue, newValue, firstRun );
	}*/

	public static void OnSvCheatsChangedStatic( bool oldValue, bool newValue, bool wasFirstRun = false )
	{
		if ( wasFirstRun )
			return;

		if ( !SceneSystemService.IsGameSceneActiveScene(GameScene.Game) )
			return;

		GameController.Instance?.SendTableChatMessage(
			"Server cheats changed",
			$"sv_cheats is now {(newValue ? "enabled" : "disabled")}."
		);

		GameController.Instance?.SendGlobalPopupToAll("Server Cheats Changed", $"sv_cheats is now {(newValue ? "enabled" : "disabled")}.");
	}

	public static bool TryParseBool(string value, out bool parsed)
	{
		parsed = false;

		if ( value is null )
			return false;
		
		if ( bool.TryParse( value, out bool boolValue ) )
		{
			parsed = boolValue;
			return true;
		}

		if ( int.TryParse( value, out int intValue ) )
		{
			if ( intValue != 1 && intValue != 0 )
				return false;

			parsed = intValue == 1;
			return true;
		}

		return false;
	}

	public static bool IsCommandSourceOverwrite( string command )
	{
		var commands = Commands ?? GameCommands.All;
		return commands.TryGetValue( command, out var gameCommand ) && gameCommand.IsSourceOverwrite;
	}
}

public static class SvCheatsCommand
{
	public const string Name = "sv_cheats";
	public const string Alias = "mn_cheats";

	[ConCmd ( Alias )]
	[SourceOverwriteCmd( Name )]
	public static void Execute( Connection connection, string value = "_" )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			bool oldValue = MonopolyApp.IsCheatsEnabled();

			if ( value is null || value == "_" )
				return CommandResult.Success($"sv_cheats {oldValue}");

			if ( !GameCommandManager.CanUseHostCommand( connection ) )
				return CommandResult.FailHost();

			var couldParse = GameCommandManager.TryParseBool( value, out bool newValue );
			if ( !couldParse )
				return CommandResult.Fail( $"Unable to parse value {value}" );

			MonopolyApp.SetCheatsEnabled( newValue );
			GameCommandManager.OnSvCheatsChangedStatic( oldValue, newValue );
			return CommandResult.Success( $"sv_cheats => {value}" );
		} );
	}
}

public static class RollPhysicalDiceCommand
{
	public const string Name = "roll_physical_dice";

	[HostCheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( game.CurrentPlayer != player )
				return CommandResult.Fail( "It is not that player's turn." );

			_ = game.RollDiceAsync();
			return CommandResult.Success();
		}, connection );
	}
}

public static class HelpCommand
{
	public const string Name = "help";

	[ConCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var names = GameCommands.All.Keys.OrderBy( name => name ).ToList();
			return CommandResult.Success( $"Available commands: {string.Join( ", ", names )}" );
		}, connection );
	}
}

public static class EndTurnCommand
{
	public const string Name = "end_turn";

	[HostCheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( game.CurrentPlayer != player )
				return CommandResult.Fail( "It is not that player's turn." );

			game.EndTurn();
			return CommandResult.Success();
		}, connection );
	}
}

public static class FinishMoveCommand
{
	public const string Name = "finish_move";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( playerName != "self" || MonopolyApp.GetConnectionForPlayer( player ) != Connection.Local)
				if ( !GameCommandManager.CanUseHostCommand( Connection.Local ) )
					return CommandResult.FailCheatsOrHost();

			if ( game.CurrentPlayer != player )
				return CommandResult.Fail( "It is not that player's turn." );

			if ( !game.CanFinishActiveMovement( player ) )
				return CommandResult.Fail( "That player cannot finish movement right now." );

			if ( Networking.IsHost )
				game.FinishActiveMovement( player );
			else
				game.RequestFinishActiveMovement();

			return CommandResult.Success( "Finished active movement." );
		}, connection );
	}
}

public static class SetCameraModeCommand
{
	public const string Name = "set_camera_mode";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string mode = "default" )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if ( !Enum.TryParse<BoardCameraMode>( mode, true, out var cameraMode ) )
				return CommandResult.Fail( $"Unknown camera mode \"{mode}\". Use Default, Board, FreeCam, or Token." );

			var camera = GameCamera.Instance;
			if ( camera is null )
				return CommandResult.Fail( "No active game camera." );

			camera.SetMode( cameraMode );
			return CommandResult.Success( $"Camera mode => {cameraMode}." );
		}, connection );
	}
}

public static class PlayTurnReminderCommand
{
	public const string Name = "play_turn_reminder";

	[HostCheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			return game.TryPlayTurnReminder( player )
				? CommandResult.Success( $"Played turn reminder for {player.PlayerName}." )
				: CommandResult.Fail( $"Could not play turn reminder for {player.PlayerName}." );
		}, connection );
	}
}

public static class BuyPropertyCommand
{
	public const string Name = "buy_property";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, int propertyIndex, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( game.CanBuyPendingProperty( player, propertyIndex ) )
				return game.TryBuyPendingPropertyForPlayer( player, out var pendingMessage )
					? CommandResult.Success( pendingMessage )
					: CommandResult.Fail( pendingMessage );

			if ( GameCommandManager.HasUnresolvedPendingBuyDecision( game, player ) )
				return CommandResult.Fail( GameCommandManager.GetPendingBuyDecisionMessage( game ) );

			return game.TryBuyPropertyForPlayer( player, propertyIndex, true, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		}, connection );
	}
}

public static class BuyPropertySetCommand
{
	public const string Name = "buy_property_set";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string propertySet, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if ( !ColorGroups.TryParse( propertySet, out var colorGroup ) || colorGroup == ColorGroup.None )
				return CommandResult.Fail( $"Property set \"{propertySet}\" does not exist." );

			var game = GameController.Instance;
			var board = Board.Instance;
			if ( game is null || board?.SpaceDefs is null )
				return CommandResult.Fail( "No active board." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( GameCommandManager.HasUnresolvedPendingBuyDecision( game, player ) )
				return CommandResult.Fail( GameCommandManager.GetPendingBuyDecisionMessage( game ) );

			var properties = board.SpaceDefs
				.Where( def => def is not null && def.ColorGroup == colorGroup )
				.ToList();

			return game.TryBuyPropertySetForPlayer( player, properties, true, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		}, connection );
	}
}

public static class RollDiceCommand
{
	public const string Name = "roll_dice";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, int amount = -1, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( game.CurrentPlayer != player )
				return CommandResult.Fail( "It is not that player's turn." );

			var isForcedRoll = amount >= 0;
			var isCallerRollingSelf = resolvedPlayerName.Equals( "self", StringComparison.OrdinalIgnoreCase ) ||
				resolvedPlayerName.Equals( "me", StringComparison.OrdinalIgnoreCase );

			if ( (isForcedRoll || !isCallerRollingSelf) && !GameCommandManager.CanUseHostCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

			if ( Networking.IsHost )
				_ = game.RollDiceAsync( amount );
			else
				game.RequestRollDice( amount );

			return CommandResult.Success();
		}, connection );
	}
}

public static class BankruptPlayerCommand
{
	public const string Name = "bankrupt_player";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			return game.TryBankruptPlayerForCheat( player, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		}, connection );
	}
}

public static class RollTwoDiceCommand
{
	public const string Name = "roll_two_dice";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, int dieA, int dieB, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if ( dieA is < 1 or > 6 || dieB is < 1 or > 6 )
				return CommandResult.Fail( "Dice values must be between 1 and 6." );

			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( game.CurrentPlayer != player )
				return CommandResult.Fail( "It is not that player's turn." );

			if ( Networking.IsHost )
				_ = game.RollTwoDiceAsync( dieA, dieB );
			else
				game.RequestRollTwoDice( dieA, dieB );

			return CommandResult.Success();
		}, connection );
	}
}

public static class DisplayStatsLogCommand
{
	public const string Name = "display_stats_log";

	[ConCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			if ( Networking.IsHost )
				game.DisplayStatsLog();
			else
				game.RequestDisplayStatsLog();

			return CommandResult.Success( game.BuildStatsLogText() );
		}, connection );
	}
}

public static class DebugStatsModalCommand
{
	public const string Name = "debug_stats_modal";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			return game.TryApplyDebugStatsModalData( out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		}, connection );
	}
}

public static class ViewDeckCommand
{
	public const string Name = "view_deck";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string deckName = "chance" )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			if ( !game.TryParseCardDeck( deckName, out var deck ) )
				return CommandResult.Fail( "Unknown deck. Use chance or chest." );

			return CommandResult.Success( game.BuildDeckListText( deck ) );
		}, connection );
	}
}

public static class DisplayHiddenUiCommand
{
	public const string Name = "display_hidden_ui";

	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, bool visible = true )
	{
		GameCommandManager.RunCommand( Name, () => SetHiddenUiVisible( connection, visible ), connection );
	}

	internal static CommandResult SetHiddenUiVisible( Connection connection, bool visible )
	{
		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		if ( !Networking.IsHost )
			return CommandResult.Fail( "Only the host can change hidden UI visibility." );

		game.SetForceHiddenUiVisible( visible );
		return CommandResult.Success( visible ? "Hidden UI debug visibility enabled." : "Hidden UI debug visibility disabled." );
	}
}

public static class DisplayAllUiCommand
{
	public const string Name = "display_all_ui";

	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, bool visible = true )
	{
		GameCommandManager.RunCommand( Name, () => DisplayHiddenUiCommand.SetHiddenUiVisible( connection, visible ), connection );
	}
}

public static class DisplayMakeUiVisibleCommand
{
	public const string Name = "display_make_ui_visible";

	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, bool visible = true )
	{
		GameCommandManager.RunCommand( Name, () => DisplayHiddenUiCommand.SetHiddenUiVisible( connection, visible ), connection );
	}
}

public static class ForceReadyUpCommand
{
	public const string Name = "force_ready_up";

	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var lobby = LobbyController.Instance;
			if ( lobby is not null )
			{
				return lobby.TryForceReadyUp( out var lobbyMessage )
					? CommandResult.Success( lobbyMessage )
					: CommandResult.Fail( lobbyMessage );
			}

			var game = GameController.Instance;
			if ( game is not null )
			{
				return game.TryForceReadyUp( out var gameMessage )
					? CommandResult.Success( gameMessage )
					: CommandResult.Fail( gameMessage );
			}

			return CommandResult.Fail( "No active lobby." );
		}, connection );
	}
}

public static class ChangeMoneyCommand
{
	public const string Name = "change_money";

	[CheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, int amount, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			return game.TryChangeMoneyForPlayer( player, amount, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		}, connection );
	}
}

public static class ChangeVacationCashCommand
{
	public const string Name = "change_vacation_cash";

	[ConCmd( Name )]
	public static void Execute( Connection connection, int amount )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to change vacation cash." );

			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			return game.TryChangeVacationCash( amount, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		}, connection );
	}
}

public static class ForceEndGameWinCommand
{
	public const string Name = "force_end_game_win";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to force-end the game." );

			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			var player = game.ResolvePlayerReference( resolvedPlayerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			return game.TryForceEndGameWin( player, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		}, connection );
	}
}

public static class JailPlayerCommand
{
	public const string Name = "jail_player";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self" )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			var player = game.ResolvePlayerReference( playerName, connection );
			if ( player is null )
				return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

			if ( game.CurrentPlayer != player )
				return CommandResult.Fail( "It is not that player's turn." );

			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

			if ( Networking.IsHost )
				game.SendPlayerToJail( player );
			else
				game.RequestSendPlayerToJail( player );

			return CommandResult.Success();
		}, connection );
	}
}

public static class GetHostId
{
	public const string Name = "get_host";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string networkingHost = "false" )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if ( !GameCommandManager.TryParseBool( networkingHost, out bool shouldDisplayNetworkingHost ) )
				StandaloneConsole.WriteLine($"Could not parse bool value {networkingHost}. Continuing with false.", StandaloneConsole.EntryKind.Warning);
			
			long id = MonopolyApp.CurrentHostOwnerId;
			string idString = Networking.IsHost ? id.ToString() : "Only the host can see player's steam ids.";
			string playerName = MonopolyApp.GetPlayerDisplayName(id)?? "Unable to find player name.";

			return CommandResult.Success($"Player Name: {playerName} - SteamId: {idString}.");
		}, connection );
	}
}

public static class TryGetSteamLobbySocket
{
	public const string Name = "try_get_steam_lobby_socket";

	[StandaloneCmd]
	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			bool gotSocket = MonopolyApp.TryGetLobbySocket(out object socket, out string msg);
			if ( gotSocket )
				return CommandResult.Success(msg);

			return CommandResult.Fail(msg);
		}, connection );
	}
}

public static class TryTransferSteamLobbyHost
{
	public const string Name = "try_transfer_lobby_host";

	[StandaloneCmd]
	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string unresolvedPlayerName = "self", params string[] unresolvedPlayerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if (unresolvedPlayerName == "self")
				unresolvedPlayerName = Connection.Local.Name;

			var joinedPlayerName = GameCommandManager.JoinPlayerName( unresolvedPlayerName, unresolvedPlayerNameTail );
			bool couldResolvePlayerName = MonopolyApp.TryGetPlayerFullNameFromString( joinedPlayerName, out string playerName );
			if ( !couldResolvePlayerName )
				return CommandResult.Fail($"Could not find player {joinedPlayerName}");

			bool couldResolvePlayer = MonopolyApp.TryResolvePlayerReferenceSmart( playerName, out PlayerState gamePlayer, out LobbyPlayer lobbyPlayer );
			if ( !couldResolvePlayer )
				return CommandResult.Fail($"Unknown failure - could not find player in GameRef.Players or LobbyRef.Players");
			
			var steamId = gamePlayer is not null ? gamePlayer.SteamId : (long)lobbyPlayer.SteamId;

			bool transferred = MonopolyApp.TryTransferSteamLobbyHost( steamId.ToString(), out string msg );
			if ( !transferred )
				return CommandResult.Fail(msg);

			return CommandResult.Success(msg);
		}, connection );
	}
}

public static class DebugSteamId
{
	public const string Name = "debug_steam_id";

	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string unresolvedPlayerName = "self", params string[] unresolvedPlayerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			if (unresolvedPlayerName == "self")
				unresolvedPlayerName = Connection.Local.Name;

			var joinedPlayerName = GameCommandManager.JoinPlayerName( unresolvedPlayerName, unresolvedPlayerNameTail );
			bool couldResolvePlayerName = MonopolyApp.TryGetPlayerFullNameFromString( joinedPlayerName, out string playerName );
			if ( !couldResolvePlayerName )
				return CommandResult.Fail($"Could not find player {joinedPlayerName}");

			bool couldResolvePlayer = MonopolyApp.TryResolvePlayerReferenceSmart( playerName, out PlayerState gamePlayer, out LobbyPlayer lobbyPlayer );
			if ( !couldResolvePlayer )
				return CommandResult.Fail($"Unknown failure - could not find player in GameRef.Players or LobbyRef.Players");
			
			var steamId = gamePlayer is not null ? gamePlayer.SteamId : (long)lobbyPlayer.SteamId;

			return CommandResult.Success(steamId.ToString());
		}, connection );
	}
}

public static class Exit
{
	public const string Name = "exit";

	[SourceOverwriteCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			Game.Close();
			return CommandResult.Success();
		}, connection );
	}
}

public static class Quit
{
	public const string Name = "quit";

	[SourceOverwriteCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			Game.Close();
			return CommandResult.Success();
		}, connection );
	}
}

public static class GetActiveScene
{
	public const string Name = "get_active_scene";

	[HostCheatCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			GameScene scene = SceneSystemService.GetActiveGameScene();
			if ( scene is null )
				return CommandResult.Fail("Could not get active game scene (scene returned null)");
			return CommandResult.Success($"Current active scene: {scene.Name}");
		}, connection );
	}
}

public static class KickPlayerCommand
{
	public const string Name = "kick_player";

	[HostCmd]
	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", string forceAbandon = "false", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var resolvedPlayerName = ResolveKickPlayerName( playerName, forceAbandon, playerNameTail, out var shouldForceAbandon, out var parseError );
			if ( parseError is not null )
				return CommandResult.Fail( parseError );

			if ( MonopolyApp.TryResolvePlayerReferenceSmart( resolvedPlayerName, out var gamePlayer, out var lobbyPlayer ) )
			{
				if ( gamePlayer is not null )
				{
					var game = GameController.Instance;
					if ( game is null )
						return CommandResult.Fail( "No active game controller." );

					return game.TryKickPlayer( gamePlayer, connection, shouldForceAbandon, out var gameMessage )
						? CommandResult.Success( gameMessage )
						: CommandResult.Fail( gameMessage );
				}

				var lobby = LobbyController.Instance;
				if ( lobby is null )
					return CommandResult.Fail( "No active lobby." );

				return lobby.TryKickPlayer( lobbyPlayer.OwnerId, connection, shouldForceAbandon, out var lobbyMessage )
					? CommandResult.Success( lobbyMessage )
					: CommandResult.Fail( lobbyMessage );
			}

			return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );
		}, connection );
	}

	private static string ResolveKickPlayerName( string playerName, string forceAbandon, string[] playerNameTail, out bool shouldForceAbandon, out string parseError )
	{
		shouldForceAbandon = false;
		parseError = null;

		if ( GameCommandManager.TryParseBool( forceAbandon, out shouldForceAbandon ) )
			return GameCommandManager.JoinPlayerName( playerName, playerNameTail );

		var nameParts = new List<string> { forceAbandon };
		if ( playerNameTail is not null )
			nameParts.AddRange( playerNameTail );

		if ( nameParts.Count > 0 && GameCommandManager.TryParseBool( nameParts[^1], out shouldForceAbandon ) )
		{
			nameParts.RemoveAt( nameParts.Count - 1 );
			return GameCommandManager.JoinPlayerName( playerName, nameParts.ToArray() );
		}

		var fullName = GameCommandManager.JoinPlayerName( playerName, nameParts.ToArray() );
		if ( MonopolyApp.TryResolvePlayerReferenceSmart( fullName, out _, out _ ) )
			return fullName;

		parseError = $"Unable to parse forceAbandon value \"{forceAbandon}\".";
		return fullName;
	}
}

public static class PingPlayerCommand
{
	public const string Name = "ping_player";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var resolvedPlayerName = GameCommandManager.JoinPlayerName( playerName, playerNameTail );
			if ( !MonopolyApp.TryResolvePlayerReferenceSmart( resolvedPlayerName, out var gamePlayer, out var lobbyPlayer ) )
				return CommandResult.Fail( $"Could not find player \"{resolvedPlayerName}\"." );

			if ( gamePlayer is not null )
			{
				var game = GameController.Instance;
				if ( game is null )
					return CommandResult.Fail( "No active game controller." );

				return game.TryPingPlayer( gamePlayer, out var message )
					? CommandResult.Success( message )
					: CommandResult.Fail( message );
			}

			var gameController = GameController.Instance;
			if ( gameController is null )
				return CommandResult.Fail( "Ping is only available during an active match." );

			var matchedPlayer = gameController.Players.FirstOrDefault( player =>
				player is not null && player.IsAssigned && player.OwnerId == lobbyPlayer.OwnerId );

			if ( matchedPlayer is null )
				return CommandResult.Fail( $"Could not find active match player for \"{resolvedPlayerName}\"." );

			return gameController.TryPingPlayer( matchedPlayer, out var gameMessage )
				? CommandResult.Success( gameMessage )
				: CommandResult.Fail( gameMessage );
		}, connection );
	}
}

public static class GamePhaseCommand
{
	public const string Name = "game_phase";

	[ConCmd( Name )]
	public static void Execute( Connection connection )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			return CommandResult.Success( game.Phase.ToString() );
		}, connection );
	}
}
