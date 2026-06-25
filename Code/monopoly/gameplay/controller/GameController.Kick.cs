using Sandbox;

public sealed partial class GameController : Component
{
	public bool TryKickPlayer( PlayerState player, Connection caller, bool forceAbandon, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can kick players.";
			return false;
		}

		if ( player is null || !player.IsAssigned )
		{
			message = "Player is not active.";
			return false;
		}

		var callerPlayer = GetPlayerForCaller( caller );
		if ( callerPlayer == player && !forceAbandon )
		{
			message = "Use forceAbandon=true to kick yourself.";
			return false;
		}

		if ( forceAbandon )
		{
			if ( player.IsBankrupt )
			{
				message = $"{player.PlayerName} is already removed from the match.";
				return false;
			}

			FinalizeAbandonedPlayer( player );
			message = $"Abandoned {player.PlayerName}.";
			return true;
		}

		var connection = GetConnectionForPlayer( player );
		if ( connection is not null )
		{
			connection.Kick( "Kicked by host." );
			message = $"Kicked {player.PlayerName}.";
			return true;
		}

		if ( player.IsDisconnected )
		{
			message = $"{player.PlayerName} is already disconnected.";
			return false;
		}

		HandleDisconnectedPlayerSlot( player );
		message = $"Marked {player.PlayerName} disconnected.";
		return true;
	}
}
