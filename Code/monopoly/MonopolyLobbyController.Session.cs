using Sandbox;

public sealed partial class MonopolyLobbyController
{
	public bool TryStartGame()
	{
		if ( !Networking.IsHost || !CanStartGame )
			return false;

		MatchBootstrap.PrepareGame( GetGameConfig(), Players.Count );
		LoadGameScene();
		return true;
	}

	[Rpc.Broadcast]
	private void LoadGameScene()
	{
		SceneFlow.LoadGame( Scene );
	}

	[Rpc.Host]
	public void RequestSetReady( bool isReady )
	{
		if ( Rpc.Caller is null )
			return;

		TrySetReady( Rpc.Caller.SteamId, isReady );
	}

	[Rpc.Host]
	public void RequestStartGame()
	{
		if ( OnlyHostStartsGame && !IsHostCaller( Rpc.Caller ) )
			return;

		TryStartGame();
	}

	private bool IsHostCaller( Connection caller )
	{
		return Networking.IsHost && (caller is null || caller == Connection.Local);
	}

	public void LeaveLobby()
	{
		NetworkSession.LeaveCurrentLobby( Scene );
	}
}
