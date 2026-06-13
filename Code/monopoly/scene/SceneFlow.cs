using Sandbox;
using System;
using System.Threading.Tasks;

public static class SceneFlow
{
	public static void LoadMenu( Scene scene ) => ChangeScene( ScenePaths.Menu, "Loading menu", "Returning to the main menu." );
	public static void LoadLobby( Scene scene ) => ChangeScene( ScenePaths.Lobby, "Loading lobby", "Preparing the lobby." );
	public static void LoadGame( Scene scene ) => ChangeScene( ScenePaths.Game, "Loading game", "Setting up the board." );

	private static void ChangeScene( string scenePath, string title, string message )
	{
		LoadingState.Show( title, message );
		_ = ChangeSceneAsync( scenePath );
	}

	private static async Task ChangeSceneAsync( string scenePath )
	{
		await LoadingState.WaitUntilPresentedAsync();

		var options = new SceneLoadOptions();
		if ( !options.SetScene( scenePath ) )
		{
			Log.Warning( $"Failed to set scene path {scenePath}." );
			LoadingState.Hide();
			return;
		}

		if ( !Game.ChangeScene( options ) )
		{
			Log.Warning( $"Failed to change scene to {scenePath}. host={Networking.IsHost} active={Networking.IsActive}" );
			LoadingState.Hide();
		}
	}
}
