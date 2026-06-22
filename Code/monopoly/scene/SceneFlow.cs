using Sandbox;
using System;
using System.Threading.Tasks;

public static class SceneFlow
{
	public static void LoadMenu( Scene scene ) => ChangeScene( GameScene.Menu, "Loading menu", "Returning to the main menu." );
	public static void LoadLobby( Scene scene ) => ChangeScene( GameScene.Lobby, "Loading lobby", "Preparing the lobby." );
	public static void LoadGame( Scene scene ) => ChangeScene( GameScene.Game, "Loading game", "Setting up the board." );

	private static void ChangeScene( GameScene gameScene, string title, string message )
	{
		LoadingState.Show( title, message );
		_ = ChangeSceneAsync( gameScene );
	}

	private static async Task ChangeSceneAsync( GameScene gameScene )
	{
		await LoadingState.WaitUntilPresentedAsync();

		var options = new SceneLoadOptions();
		if ( !options.SetScene( gameScene.Path ) )
		{
			Log.Warning( $"Failed to set scene path {gameScene.Path}." );
			LoadingState.Hide();
			return;
		}

		if ( !Game.ChangeScene( options ) )
		{
			Log.Warning( $"Failed to change scene to {gameScene.Path}. host={Networking.IsHost} active={Networking.IsActive}" );
			LoadingState.Hide();
			return;
		}

		SceneSystemService.OnSceneLoaded( gameScene );
	}
}
