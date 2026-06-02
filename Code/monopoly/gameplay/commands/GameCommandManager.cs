using System;
using Sandbox;

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

public sealed class GameCommandManager : Component
{
	public static CommandResult BuyProperty( Connection caller, int propertyIndex, string playerName = "self" )
	{
		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( game.CanBuyPendingProperty( player, propertyIndex ) )
			return game.TryBuyPendingPropertyForPlayer( player, out var pendingMessage )
				? CommandResult.Success( pendingMessage )
				: CommandResult.Fail( pendingMessage );

		if ( HasUnresolvedPendingBuyDecision( game, player ) )
			return CommandResult.Fail( GetPendingBuyDecisionMessage( game ) );

		if ( !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to buy arbitrary properties." );

		return game.TryBuyPropertyForPlayer( player, propertyIndex, true, out var message )
			? CommandResult.Success( message )
			: CommandResult.Fail( message );
	}

	public static CommandResult BuyPropertySet( Connection caller, string propertySet, string playerName = "self" )
	{
		if ( !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to buy property sets." );

		if ( !ColorGroups.TryParse( propertySet, out var colorGroup ) || colorGroup == ColorGroup.None )
			return CommandResult.Fail( $"Property set \"{propertySet}\" does not exist." );

		var game = GameController.Instance;
		var board = Board.Instance;
		if ( game is null || board?.SpaceDefs is null )
			return CommandResult.Fail( "No active board." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( HasUnresolvedPendingBuyDecision( game, player ) )
			return CommandResult.Fail( GetPendingBuyDecisionMessage( game ) );

		var properties = board.SpaceDefs
			.Where( def => def is not null && def.ColorGroup == colorGroup )
			.ToList();

		return game.TryBuyPropertySetForPlayer( player, properties, true, out var message )
			? CommandResult.Success( message )
			: CommandResult.Fail( message );
	}

	public static CommandResult RollDice( Connection caller, int amount = -1, string playerName = "self" )
	{
		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( game.CurrentPlayer != player )
			return CommandResult.Fail( "It is not that player's turn." );

		var isForcedRoll = amount >= 0;
		var isCallerRollingSelf = playerName.Equals( "self", StringComparison.OrdinalIgnoreCase ) ||
			playerName.Equals( "me", StringComparison.OrdinalIgnoreCase );

		if ( (isForcedRoll || !isCallerRollingSelf) && !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

		if ( Networking.IsHost )
			_ = game.RollDiceAsync( amount );
		else
			game.RequestRollDice( amount );

		return CommandResult.Success();
	}

	public static CommandResult RollTwoDice( Connection caller, int dieA, int dieB, string playerName = "self" )
	{
		if ( dieA is < 1 or > 6 || dieB is < 1 or > 6 )
			return CommandResult.Fail( "Dice values must be between 1 and 6." );

		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( game.CurrentPlayer != player )
			return CommandResult.Fail( "It is not that player's turn." );

		if ( !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

		if ( Networking.IsHost )
			_ = game.RollTwoDiceAsync( dieA, dieB );
		else
			game.RequestRollTwoDice( dieA, dieB );

		return CommandResult.Success();
	}

	public static CommandResult SendToJail( Connection caller, string playerName = "self" )
	{
		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( game.CurrentPlayer != player )
			return CommandResult.Fail( "It is not that player's turn." );

		if ( !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

		if ( Networking.IsHost )
			game.SendPlayerToJail( player );
		else
			game.RequestSendPlayerToJail( player );

		return CommandResult.Success();
	}

	public static CommandResult ChangeMoney( Connection caller, int amount, string playerName = "self" )
	{
		if ( !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to change player money." );

		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

		return game.TryChangeMoneyForPlayer( player, amount, out var message )
			? CommandResult.Success( message )
			: CommandResult.Fail( message );
	}

	public static CommandResult ChangeVacationCash( Connection caller, int amount )
	{
		if ( !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to change vacation cash." );

		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		return game.TryChangeVacationCash( amount, out var message )
			? CommandResult.Success( message )
			: CommandResult.Fail( message );
	}

	public static CommandResult ForceEndGameWin( Connection caller, string playerName = "self" )
	{
		if ( !CanUseCheatCommand( caller ) )
			return CommandResult.Fail( "sv_cheats must be enabled to force-end the game." );

		var game = GameController.Instance;
		if ( game is null )
			return CommandResult.Fail( "No active game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return CommandResult.Fail( $"Could not find player \"{playerName}\"." );

		return game.TryForceEndGameWin( player, out var message )
			? CommandResult.Success( message )
			: CommandResult.Fail( message );
	}

	private static bool CanUseCheatCommand( Connection caller )
	{
		if ( Networking.IsHost && (caller is null || caller == Connection.Local) )
			return true;

		return Game.CheatsEnabled;
	}

	private static bool HasUnresolvedPendingBuyDecision( GameController game, PlayerState player )
	{
		return game is not null &&
			player is not null &&
			game.Phase == GamePhase.WaitingForBuyDecision &&
			game.CurrentPlayer == player &&
			game.PendingPurchaseSpaceIndex >= 0;
	}

	private static string GetPendingBuyDecisionMessage( GameController game )
	{
		var pendingDef = game?.Board?.GetSpaceDef( game.PendingPurchaseSpaceIndex );
		var pendingName = pendingDef?.DisplayName ?? "the pending property";

		return $"Resolve the pending property decision for {pendingName} before buying other properties for that player.";
	}
}
