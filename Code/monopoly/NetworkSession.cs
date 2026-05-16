using Sandbox;

public static class NetworkSession
{
	public static void LeaveCurrentLobby( Scene scene )
	{
		if ( Networking.IsActive )
			Networking.Disconnect();

		SceneFlow.LoadMenu( scene );
	}
}
