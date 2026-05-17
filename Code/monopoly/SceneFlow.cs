using Sandbox;

public static class SceneFlow
{
	public static void LoadMenu( Scene scene ) => ChangeScene( ScenePaths.Menu );
	public static void LoadLobby( Scene scene ) => ChangeScene( ScenePaths.Lobby );
	public static void LoadGame( Scene scene ) => ChangeScene( ScenePaths.Game );

	private static void ChangeScene( string scenePath )
	{
		var options = new SceneLoadOptions();
		if ( !options.SetScene( scenePath ) )
		{
			Log.Warning( $"Failed to set scene path {scenePath}." );
			return;
		}

		if ( !Game.ChangeScene( options ) )
			Log.Warning( $"Failed to change scene to {scenePath}. host={Networking.IsHost} active={Networking.IsActive}" );
	}
}
