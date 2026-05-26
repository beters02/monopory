using Sandbox;
using System;
using System.Threading.Tasks;

public static class NetworkSession
{
	public static long RejoinLobbyId { get; private set; }
	public static string RejoinConnectTarget { get; private set; } = "";
	public static float RejoinExpiresAt { get; private set; }

	public static bool CanRejoinLobby =>
		(RejoinLobbyId != 0 || !string.IsNullOrWhiteSpace( RejoinConnectTarget )) && RejoinExpiresAt > Time.Now;

	public static int RejoinSecondsRemaining =>
		CanRejoinLobby ? Math.Max( 0, (int)MathF.Ceiling( RejoinExpiresAt - Time.Now ) ) : 0;

	public static void SetRejoinWindow( long lobbyId, string connectTarget, float expiresAt )
	{
		if ( lobbyId == 0 && string.IsNullOrWhiteSpace( connectTarget ) )
			return;

		if ( expiresAt <= Time.Now )
			return;

		RejoinLobbyId = lobbyId;
		RejoinConnectTarget = connectTarget ?? "";
		RejoinExpiresAt = expiresAt;
	}

	public static void ClearRejoinWindow()
	{
		RejoinLobbyId = 0;
		RejoinConnectTarget = "";
		RejoinExpiresAt = 0f;
	}

	public static async Task<bool> TryRejoinLobby( Scene scene )
	{
		try
		{
			if ( !CanRejoinLobby )
				return false;

			if ( Networking.IsActive )
				Networking.Disconnect();

			var connected = false;
			if ( RejoinLobbyId != 0 )
			{
				connected = await Networking.TryConnectSteamId( RejoinLobbyId, 3 );
			}
			else if ( !string.IsNullOrWhiteSpace( RejoinConnectTarget ) )
			{
				Networking.Connect( RejoinConnectTarget );
				connected = await WaitForConnectionAsync( 6f );
			}

			if ( !connected )
				return false;

			ClearRejoinWindow();
			if ( Networking.IsHost )
				SceneFlow.LoadLobby( scene );
			return true;
		}
		catch ( Exception exception )
		{
			Log.Warning( $"TryRejoinLobby failed: {exception.Message}" );
			return false;
		}
	}

	private static async Task<bool> WaitForConnectionAsync( float timeoutSeconds )
	{
		var deadline = Time.Now + Math.Max( timeoutSeconds, 0.25f );
		while ( Time.Now < deadline )
		{
			if ( Networking.IsActive )
				return true;

			await Task.Delay( 100 );
		}

		return Networking.IsActive;
	}

	public static void LeaveCurrentLobby( Scene scene )
	{
		if ( Networking.IsActive )
			Networking.Disconnect();

		SceneFlow.LoadMenu( scene );
	}
}
