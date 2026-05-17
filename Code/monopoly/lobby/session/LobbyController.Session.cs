using Sandbox;

public sealed partial class LobbyController
{
	public bool TryStartGame()
	{
		if ( !Networking.IsHost || !CanStartGame )
			return false;

		MatchBootstrap.PrepareGame( GetGameConfig(), Players.Count );
		LoadGameScene();
		return true;
	}

	private void LoadGameScene()
	{
		SceneFlow.LoadGame( Scene );
	}

	[Rpc.Broadcast]
	private void LoadMenuScene()
	{
		SceneFlow.LoadMenu( Scene );
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

	[Rpc.Host]
	public void RequestBackToMenu()
	{
		if ( !Networking.IsHost )
			return;

		LoadMenuScene();
	}

	private bool IsHostCaller( Connection caller )
	{
		return Networking.IsHost && (caller is null || caller == Connection.Local);
	}

}
