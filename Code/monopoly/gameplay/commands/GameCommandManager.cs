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
	public GameCommandCheatType CheatType { get; init; }
	public GameCommandAppType AppType { get; init; }

	public GameCommand( string name, MethodDescription method, ConCmdAttribute attribute, GameCommandCheatType cheatType, GameCommandAppType appType )
	{
		Name = name;
		Method = method;
		Attribute = attribute;
		CheatType = cheatType;
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

	private static void EnsureRegistered()
	{
		if ( registered )
			return;

		registered = true;

		foreach ( var (method, attribute) in Game.TypeLibrary.GetMethodsWithAttribute<ConCmdAttribute>() )
		{
			var name = attribute.Name;
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

			Commands[name] = new GameCommand( name, method, attribute, cheatType, appType );
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

		//HandlePreviouslyExistingCommands();

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
			Log.Error( message );
			WriteStandaloneConsoleLine( message, "err" );
		}
	}

	internal static void RunCommand(string commandName, Func<CommandResult> callback, Connection caller = null )
	{

		if ( caller == null || !Commands.TryGetValue( commandName, out GameCommand value ) )
		{
			ExecuteCommand( commandName, callback );
			return;
		}

		Log.Info("Found command.");
		Log.Info(value.Name);
		Log.Info(value.CheatType);

		if ( value.AppType == GameCommandAppType.Standalone )
			if ( !CanUseStandaloneCommand() )
			{
				LogCommandResult( commandName, CommandResult.FailStandalone() );
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

		ExecuteCommand( commandName, callback );
	}

	internal static bool CanUseCheatCommand( Connection caller )
	{
		return MonopolyApp.CheatsEnabled;
	}

	internal static bool CanUseHostCheatCommand( Connection caller )
	{
		if ( Networking.IsHost && (caller is null || caller == Connection.Local) )
			return true;

		if ( GameController.Instance?.IsEffectiveHostCaller( caller ) == true )
			return true;

		return MonopolyApp.CheatsEnabled;
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
			Log.Warning( message );
			WriteStandaloneConsoleLine( message, "wrn" );
			return;
		}

		if ( !result.Ok )
		{
			var message = $"{commandName} command failed: {result.Message}";
			Log.Warning( message );
			WriteStandaloneConsoleLine( message, "wrn" );
			return;
		}

		if ( string.IsNullOrWhiteSpace( result.Message ) )
		{
			var message = $"{commandName} command succeeded.";
			Log.Info( message );
			WriteStandaloneConsoleLine( message, "msg" );
			return;
		}

		var successMessage = $"{commandName}: {result.Message}";
		Log.Info( successMessage );
		WriteStandaloneConsoleLine( successMessage, "msg" );
	}

	private static void WriteStandaloneConsoleLine( string message, string kind )
	{
		if ( MonopolyApp.IsStandalone() )
			return;

		Sandbox.ui.components.StandaloneConsole.WriteLine( message, kind );
	}

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
		Log.Info( $"mn_cheats changed: {oldValue} -> {newValue}" );

		if ( wasFirstRun )
			return;

		GameController.Instance?.SendTableChatMessage(
			"Server cheats changed",
			$"mn_cheats is now {(newValue ? "enabled" : "disabled")}."
		);
		GameController.Instance?.SendGlobalPopupToAll("Server Cheats Changed", $"mn_cheats is now {(newValue ? "enabled" : "disabled")}.");
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
}

public static class SvCheatsCommand
{
	public const string Name = "mn_cheats";

	[ConCmd( Name )]
	public static void Execute( Connection connection, string value = "_" )
	{
		GameCommandManager.RunCommand( Name, () =>
		{
			bool oldValue = MonopolyApp.CheatsEnabled;

			if ( value is null || value == "_" )
				return CommandResult.Success($"mn_cheats {oldValue}");

			if ( !GameCommandManager.CanUseHostCommand( connection ) )
				return CommandResult.FailHost();

			var couldParse = GameCommandManager.TryParseBool( value, out bool parsed );
			if ( !couldParse )
				return CommandResult.Fail( $"Unable to parse value {value}" );

			bool newValue = parsed;
			MonopolyApp.SetCheatsEnabled( newValue );
			GameCommandManager.OnSvCheatsChangedStatic( oldValue, newValue );
			return CommandResult.Success( $"mn_cheats => {value}" );
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
			var game = GameController.Instance;
			if ( game is not null )
			{
				return game.TryForceReadyUp( out var gameMessage )
					? CommandResult.Success( gameMessage )
					: CommandResult.Fail( gameMessage );
			}

			var lobby = LobbyController.Instance;
			if ( lobby is null )
				return CommandResult.Fail( "No active lobby." );

			return lobby.TryForceReadyUp( out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
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
				StandaloneConsole.WriteLine($"Could not parse bool value {networkingHost}. Continuing with false.", StandaloneConsole.EWarning);
			
			long id = shouldDisplayNetworkingHost ?  Connection.Host.SteamId : MonopolyApp.EffectiveHostOwnerId;
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
	public static void Execute( Connection connection, string networkingHost = "false" )
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