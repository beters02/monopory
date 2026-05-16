using Sandbox;

public static class MonopolyNetworkSession
{
	public static void LeaveCurrentLobby( Scene scene )
	{
		if ( Networking.IsActive )
			Networking.Disconnect();

		MonopolySceneFlow.LoadMenu( scene );
	}
}
