using Sandbox;
using System;
using System.Threading.Tasks;

public static class NetworkSession
{
	public static long RejoinLobbyId { get; private set; }
	public static float RejoinExpiresAt { get; private set; }

	public static bool CanRejoinLobby =>
		RejoinLobbyId != 0 && RejoinExpiresAt > Time.Now;

	public static int RejoinSecondsRemaining =>
		CanRejoinLobby ? Math.Max( 0, (int)MathF.Ceiling( RejoinExpiresAt - Time.Now ) ) : 0;

	public static void SetRejoinWindow( long lobbyId, float expiresAt )
	{
		if ( lobbyId == 0 || expiresAt <= Time.Now )
			return;

		RejoinLobbyId = lobbyId;
		RejoinExpiresAt = expiresAt;
	}

	public static async Task<bool> TryRejoinLobby( Scene scene )
	{
		if ( !CanRejoinLobby )
			return false;

		if ( Networking.IsActive )
			Networking.Disconnect();

		var ok = await Networking.TryConnectSteamId( RejoinLobbyId, 3 );
		if ( !ok )
			return false;

		SceneFlow.LoadLobby( scene );
		return true;
	}

	public static void LeaveCurrentLobby( Scene scene )
	{
		if ( Networking.IsActive )
			Networking.Disconnect();

		SceneFlow.LoadMenu( scene );
	}
}
