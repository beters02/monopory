using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sandbox;

#pragma warning disable CA2255

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
}

public interface IGameCommand
{
	string Name { get; }
	Delegate Callback { get; }
}

public sealed class GameCommand<TCallback> : IGameCommand
	where TCallback : Delegate
{
	public string Name { get; }
	public TCallback TypedCallback { get; }
	public Delegate Callback => TypedCallback;

	public GameCommand( string name, TCallback callback )
	{
		Name = name;
		TypedCallback = callback;
	}
}

public static class GameCommands
{
	private static readonly Dictionary<string, IGameCommand> Commands = new();

	public static IReadOnlyDictionary<string, IGameCommand> All => Commands;

	public static GameCommand<TCallback> Register<TCallback>( GameCommand<TCallback> command )
		where TCallback : Delegate
	{
		Commands[command.Name] = command;
		return command;
	}
}

public sealed class GameCommandManager : Component
{
	private bool lastCheatsEnabled;
	private bool firstRun = true;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		HandlePreviouslyExistingCommands();

		if ( firstRun )
			firstRun = false;
	}

	internal static string JoinPlayerName( string playerName, string[] playerNameTail )
	{
		if ( playerNameTail is null || playerNameTail.Length == 0 )
			return playerName;

		return string.Join( " ", new[] { playerName }.Concat( playerNameTail ) );
	}

	internal static void RunCommand( IGameCommand command, Func<CommandResult> callback )
	{
		try
		{
			LogCommandResult( command, callback() );
		}
		catch ( Exception exception )
		{
			var message = $"{command.Name} command threw: {exception.Message}";
			Log.Error( message );
			WriteStandaloneConsoleLine( message, "err" );
		}
	}

	internal static bool CanUseCheatCommand( Connection caller )
	{
		if ( Networking.IsHost && (caller is null || caller == Connection.Local) )
			return true;

		return Game.CheatsEnabled;
	}

	internal static bool CanUseHostCheatCommand( Connection caller )
	{
		return Networking.IsHost &&
			(caller is null || caller == Connection.Local) &&
			Game.CheatsEnabled;
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

	private static void LogCommandResult( IGameCommand command, CommandResult result )
	{
		if ( result is null )
		{
			var message = $"{command.Name} command failed: no command result.";
			Log.Warning( message );
			WriteStandaloneConsoleLine( message, "wrn" );
			return;
		}

		if ( !result.Ok )
		{
			var message = $"{command.Name} command failed: {result.Message}";
			Log.Warning( message );
			WriteStandaloneConsoleLine( message, "wrn" );
			return;
		}

		if ( string.IsNullOrWhiteSpace( result.Message ) )
		{
			var message = $"{command.Name} command succeeded.";
			Log.Info( message );
			WriteStandaloneConsoleLine( message, "msg" );
			return;
		}

		var successMessage = $"{command.Name}: {result.Message}";
		Log.Info( successMessage );
		WriteStandaloneConsoleLine( successMessage, "msg" );
	}

	private static void WriteStandaloneConsoleLine( string message, string kind )
	{
		if ( MonopolyApp.IsStandalone )
			return;

		Sandbox.ui.components.StandaloneConsole.WriteLine( message, kind );
	}

	private void HandlePreviouslyExistingCommands()
	{
		var cheatsEnabled = Game.CheatsEnabled;
		if ( cheatsEnabled != lastCheatsEnabled )
		{
			var oldValue = lastCheatsEnabled;
			lastCheatsEnabled = cheatsEnabled;
			OnSvCheatsChanged( oldValue, cheatsEnabled, firstRun );
		}
	}

	private void OnSvCheatsChanged( bool oldValue, bool newValue, bool wasFirstRun )
	{
		Log.Info( $"sv_cheats changed: {oldValue} -> {newValue}" );

		if ( wasFirstRun )
			return;

		GameController.Instance?.SendTableChatMessage(
			"Server cheats changed",
			$"sv_cheats is now {(newValue ? "enabled" : "disabled")}."
		);
	}
}

public static class RollPhysicalDiceCommand
{
	public const string Name = "roll_physical_dice";
	public static readonly GameCommand<Action<Connection, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
		{
			if ( !GameCommandManager.CanUseHostCheatCommand( connection ) )
				return CommandResult.Fail( "roll_physical_dice can only be used by the host with sv_cheats enabled." );

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
		} );
	}
}

public static class EndTurnCommand
{
	public const string Name = "end_turn";
	public static readonly GameCommand<Action<Connection, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
		{
			if ( !GameCommandManager.CanUseHostCheatCommand( connection ) )
				return CommandResult.Fail( "end_turn can only be used by the host with sv_cheats enabled." );

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
		} );
	}
}

public static class BuyPropertyCommand
{
	public const string Name = "buy_property";
	public static readonly GameCommand<Action<Connection, int, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, int propertyIndex, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
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

			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to buy arbitrary properties." );

			return game.TryBuyPropertyForPlayer( player, propertyIndex, true, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		} );
	}
}

public static class BuyPropertySetCommand
{
	public const string Name = "buy_property_set";
	public static readonly GameCommand<Action<Connection, string, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, string propertySet, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
		{
			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to buy property sets." );

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
		} );
	}
}

public static class RollDiceCommand
{
	public const string Name = "roll_dice";
	public static readonly GameCommand<Action<Connection, int, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, int amount = -1, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
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

			if ( (isForcedRoll || !isCallerRollingSelf) && !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

			if ( Networking.IsHost )
				_ = game.RollDiceAsync( amount );
			else
				game.RequestRollDice( amount );

			return CommandResult.Success();
		} );
	}
}

public static class RollTwoDiceCommand
{
	public const string Name = "roll_two_dice";
	public static readonly GameCommand<Action<Connection, int, int, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, int dieA, int dieB, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
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

			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

			if ( Networking.IsHost )
				_ = game.RollTwoDiceAsync( dieA, dieB );
			else
				game.RequestRollTwoDice( dieA, dieB );

			return CommandResult.Success();
		} );
	}
}

public static class ChangeMoneyCommand
{
	public const string Name = "change_money";
	public static readonly GameCommand<Action<Connection, int, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, int amount, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
		{
			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to change player money." );

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
		} );
	}
}

public static class ChangeVacationCashCommand
{
	public const string Name = "change_vacation_cash";
	public static readonly GameCommand<Action<Connection, int>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, int amount )
	{
		GameCommandManager.RunCommand( Command, () =>
		{
			if ( !GameCommandManager.CanUseCheatCommand( connection ) )
				return CommandResult.Fail( "sv_cheats must be enabled to change vacation cash." );

			var game = GameController.Instance;
			if ( game is null )
				return CommandResult.Fail( "No active game." );

			return game.TryChangeVacationCash( amount, out var message )
				? CommandResult.Success( message )
				: CommandResult.Fail( message );
		} );
	}
}

public static class ForceEndGameWinCommand
{
	public const string Name = "force_end_game_win";
	public static readonly GameCommand<Action<Connection, string, string[]>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		GameCommandManager.RunCommand( Command, () =>
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
		} );
	}
}

public static class JailPlayerCommand
{
	public const string Name = "jail_player";
	public static readonly GameCommand<Action<Connection, string>> Command = new( Name, Execute );

	[ModuleInitializer]
	public static void Register()
	{
		GameCommands.Register( Command );
	}

	[ConCmd( Name )]
	public static void Execute( Connection connection, string playerName = "self" )
	{
		GameCommandManager.RunCommand( Command, () =>
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
		} );
	}
}
