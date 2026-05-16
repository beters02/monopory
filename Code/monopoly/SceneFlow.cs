using Sandbox;

public static class SceneFlow
{
	public static void LoadMenu( Scene scene ) => scene?.LoadFromFile( ScenePaths.Menu );
	public static void LoadLobby( Scene scene ) => scene?.LoadFromFile( ScenePaths.Lobby );
	public static void LoadGame( Scene scene ) => scene?.LoadFromFile( ScenePaths.Game );
}
