using Sandbox;

public sealed partial class LobbyController
{
	public bool TryStartGame()
	{
		if ( !Networking.IsHost || !CanStartGame || !IsLocalEffectiveHost )
			return false;

		if ( MatchBootstrap.Current?.HasLoadedGame == true )
		{
			LoadGameScene();
			return true;
		}

		var startingPlayers = BuildPlayers();
		MatchBootstrap.PrepareGame( GetGameConfig(), startingPlayers );
		LoadGameScene();
		
		return true;
	}

	[Rpc.Broadcast]
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
	public void RequestSetSelectedPiece( string pieceId )
	{
		if ( Rpc.Caller is null )
			return;

		TrySetSelectedPiece( Rpc.Caller.SteamId, pieceId );
	}

	[Rpc.Host]
	public void RequestSetSelectedDiceSkin( string diceSkinId )
	{
		if ( Rpc.Caller is null )
			return;

		TrySetSelectedDiceSkin( Rpc.Caller.SteamId, diceSkinId );
	}

	[Rpc.Host]
	public void RequestStartGame()
	{
		if ( OnlyHostStartsGame && !IsEffectiveHostCaller( Rpc.Caller ) )
			return;

		TryStartGame();
	}

	[Rpc.Host]
	public void RequestBackToMenu()
	{
		if ( !Networking.IsHost || !IsEffectiveHostCaller( Rpc.Caller ) )
			return;

		ClearStagedLoadedGame();
		LoadMenuScene();
	}

	private bool IsHostCaller( Connection caller )
	{
		return Networking.IsHost && (caller is null || caller == Connection.Local);
	}

	private bool IsEffectiveHostCaller( Connection caller )
	{
		if ( !Networking.IsHost || caller is null )
			return false;

		if ( !OnlyHostStartsGame )
			return true;

		return MonopolyApp.IsEffectiveHostCaller( caller );
	}

}
