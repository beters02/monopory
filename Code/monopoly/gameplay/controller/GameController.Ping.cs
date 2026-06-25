using Sandbox;

public sealed partial class GameController : Component
{
	private const float PingDurationSeconds = 4f;

	public bool TryPingPlayer( PlayerState player, out string message )
	{
		message = "";

		if ( player is null || !player.IsAssigned || player.IsBankrupt )
		{
			message = "Player is not active.";
			return false;
		}

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
		{
			message = "Could not resolve player index.";
			return false;
		}

		if ( Networking.IsHost )
			BroadcastPlayerPing( playerIndex, Time.Now + PingDurationSeconds );
		else
			RequestPingPlayer( playerIndex );

		message = $"Pinged {player.PlayerName}.";
		return true;
	}

	[Rpc.Host]
	public void RequestPingPlayer( int playerIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || !player.IsAssigned || player.IsBankrupt )
			return;

		BroadcastPlayerPing( playerIndex, Time.Now + PingDurationSeconds );
	}

	[Rpc.Broadcast]
	private void BroadcastPlayerPing( int playerIndex, float expiresAt )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var token = GetPlayerToken( player );
		token?.ShowPingMarker( expiresAt );
	}
}
