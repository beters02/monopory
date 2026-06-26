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

		ShowLocalPlayerPing( playerIndex, Time.Now + PingDurationSeconds );

		message = $"Pinged {player.PlayerName}.";
		return true;
	}

	private void ShowLocalPlayerPing( int playerIndex, float expiresAt )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var token = GetPlayerToken( player );
		token?.ShowPingMarker( expiresAt );
	}
}
