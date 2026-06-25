using System;
using System.Linq;
using Sandbox;

public sealed partial class LobbyController
{
	public bool TryKickPlayer( long ownerId, Connection caller, bool forceAbandon, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can kick players.";
			return false;
		}

		if ( ownerId == 0 || !GetKnownOwnerIds().Contains( ownerId ) )
		{
			message = "Player is not in the lobby.";
			return false;
		}

		var localSteamId = GetLocalSteamId();
		if ( localSteamId.HasValue && ownerId == localSteamId.Value && !forceAbandon )
		{
			message = "Use forceAbandon=true to kick yourself.";
			return false;
		}

		if ( ownerId == CurrentHostOwnerId && !forceAbandon )
		{
			message = "Cannot kick the lobby host.";
			return false;
		}

		var playerName = GetKnownNameForOwner( ownerId );

		if ( forceAbandon )
		{
			FinalizeLobbyPlayer( ownerId );
			message = $"Abandoned {playerName}.";
			return true;
		}

		var connection = GetConnections().FirstOrDefault( c => c.SteamId == ownerId );
		if ( connection is not null )
		{
			connection.Kick( "Kicked by host." );
			message = $"Kicked {playerName}.";
			return true;
		}

		if ( IsMarkedDisconnected( ownerId ) )
		{
			message = $"{playerName} is already disconnected.";
			return false;
		}

		var timeoutSeconds = Math.Max( Config?.AbandonTimeoutSeconds ?? 180, 1 );
		DisconnectedPlayers[GetReadyKey( ownerId )] = Time.Now + timeoutSeconds;
		message = $"Marked {playerName} disconnected.";
		return true;
	}

	private void FinalizeLobbyPlayer( long ownerId )
	{
		var key = GetReadyKey( ownerId );
		var connection = GetConnections().FirstOrDefault( c => c.SteamId == ownerId );
		connection?.Kick( "Abandoned by host." );

		DisconnectedPlayers.Remove( key );
		ReadyPlayers.Remove( key );
		KnownPlayerNames.Remove( key );
		SelectedPieces.Remove( key );
		SelectedDiceSkins.Remove( key );

		if ( PreferredHostOwnerId == ownerId )
			PreferredHostOwnerId = Connection.Host?.SteamId ?? 0L;
	}
}
