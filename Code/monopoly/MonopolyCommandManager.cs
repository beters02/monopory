using System;
using Sandbox;

public sealed class MonopolyCommandResult
{
	public bool Ok { get; }
	public string Message { get; }

	private MonopolyCommandResult( bool ok, string message = "" )
	{
		Ok = ok;
		Message = message ?? "";
	}

	public static MonopolyCommandResult Success( string message = "" ) => new( true, message );
	public static MonopolyCommandResult Fail( string message ) => new( false, message );
}

public sealed class MonopolyCommandManager : Component
{
	public static MonopolyCommandResult BuyProperty( Connection caller, int propertyIndex, string playerName = "self" )
	{
		var game = MonopolyGame.Instance;
		if ( game is null )
			return MonopolyCommandResult.Fail( "No active Monopoly game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return MonopolyCommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( game.CanBuyPendingProperty( player, propertyIndex ) )
			return game.TryBuyPendingPropertyForPlayer( player, out var pendingMessage )
				? MonopolyCommandResult.Success( pendingMessage )
				: MonopolyCommandResult.Fail( pendingMessage );

		if ( HasUnresolvedPendingBuyDecision( game, player ) )
			return MonopolyCommandResult.Fail( GetPendingBuyDecisionMessage( game ) );

		if ( !CanUseCheatCommand( caller ) )
			return MonopolyCommandResult.Fail( "sv_cheats must be enabled to buy arbitrary properties." );

		return game.TryBuyPropertyForPlayer( player, propertyIndex, true, out var message )
			? MonopolyCommandResult.Success( message )
			: MonopolyCommandResult.Fail( message );
	}

	public static MonopolyCommandResult BuyPropertySet( Connection caller, string propertySet, string playerName = "self" )
	{
		if ( !CanUseCheatCommand( caller ) )
			return MonopolyCommandResult.Fail( "sv_cheats must be enabled to buy property sets." );

		if ( !MonopolyColorGroups.TryParse( propertySet, out var colorGroup ) || colorGroup == MonopolyColorGroup.None )
			return MonopolyCommandResult.Fail( $"Property set \"{propertySet}\" does not exist." );

		var game = MonopolyGame.Instance;
		var board = MonopolyBoard.Instance;
		if ( game is null || board?.SpaceDefs is null )
			return MonopolyCommandResult.Fail( "No active Monopoly board." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return MonopolyCommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( HasUnresolvedPendingBuyDecision( game, player ) )
			return MonopolyCommandResult.Fail( GetPendingBuyDecisionMessage( game ) );

		var properties = board.SpaceDefs
			.Where( def => def is not null && def.ColorGroup == colorGroup )
			.ToList();

		return game.TryBuyPropertySetForPlayer( player, properties, true, out var message )
			? MonopolyCommandResult.Success( message )
			: MonopolyCommandResult.Fail( message );
	}

	public static MonopolyCommandResult RollDice( Connection caller, int amount = -1, string playerName = "self" )
	{
		var game = MonopolyGame.Instance;
		if ( game is null )
			return MonopolyCommandResult.Fail( "No active Monopoly game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return MonopolyCommandResult.Fail( $"Could not find player \"{playerName}\"." );

		if ( game.CurrentPlayer != player )
			return MonopolyCommandResult.Fail( "It is not that player's turn." );

		var isForcedRoll = amount >= 0;
		var isCallerRollingSelf = playerName.Equals( "self", StringComparison.OrdinalIgnoreCase ) ||
			playerName.Equals( "me", StringComparison.OrdinalIgnoreCase );

		if ( (isForcedRoll || !isCallerRollingSelf) && !CanUseCheatCommand( caller ) )
			return MonopolyCommandResult.Fail( "sv_cheats must be enabled to force rolls or roll for another player." );

		if ( Networking.IsHost )
			_ = game.RollDiceAsync( amount );
		else
			game.RequestRollDice( amount );

		return MonopolyCommandResult.Success();
	}

	public static MonopolyCommandResult ChangeMoney( Connection caller, int amount, string playerName = "self" )
	{
		if ( !CanUseCheatCommand( caller ) )
			return MonopolyCommandResult.Fail( "sv_cheats must be enabled to change player money." );

		var game = MonopolyGame.Instance;
		if ( game is null )
			return MonopolyCommandResult.Fail( "No active Monopoly game." );

		var player = game.ResolvePlayerReference( playerName, caller );
		if ( player is null )
			return MonopolyCommandResult.Fail( $"Could not find player \"{playerName}\"." );

		return game.TryChangeMoneyForPlayer( player, amount, out var message )
			? MonopolyCommandResult.Success( message )
			: MonopolyCommandResult.Fail( message );
	}

	private static bool CanUseCheatCommand( Connection caller )
	{
		if ( Networking.IsHost && (caller is null || caller == Connection.Local) )
			return true;

		return Game.CheatsEnabled;
	}

	private static bool HasUnresolvedPendingBuyDecision( MonopolyGame game, MonopolyPlayerState player )
	{
		return game is not null &&
			player is not null &&
			game.Phase == MonopolyGamePhase.WaitingForBuyDecision &&
			game.CurrentPlayer == player &&
			game.PendingPurchaseSpaceIndex >= 0;
	}

	private static string GetPendingBuyDecisionMessage( MonopolyGame game )
	{
		var pendingDef = game?.Board?.GetSpaceDef( game.PendingPurchaseSpaceIndex );
		var pendingName = pendingDef?.DisplayName ?? "the pending property";

		return $"Resolve the pending buy decision for {pendingName} before buying other properties for that player.";
	}
}
