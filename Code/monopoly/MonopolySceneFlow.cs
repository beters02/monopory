using Sandbox;

public static class MonopolySceneFlow
{
	public static void LoadMenu( Scene scene ) => scene?.LoadFromFile( MonopolyScenePaths.Menu );
	public static void LoadLobby( Scene scene ) => scene?.LoadFromFile( MonopolyScenePaths.Lobby );
	public static void LoadGame( Scene scene ) => scene?.LoadFromFile( MonopolyScenePaths.Game );
}
