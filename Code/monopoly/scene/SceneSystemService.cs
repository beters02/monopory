public class GameScene
{

    public string Title { get; }
    public string Path { get; }

    [Description("Alias for the scene's title.")]
    public string Name => Title;

    private GameScene(string title, string path)
    {
        Title = title;
        Path = path;
    }

    public static readonly GameScene Game = new("Main", "scenes/main.scene");
    public static readonly GameScene Lobby = new("Lobby", "scenes/lobby.scene");
    public static readonly GameScene Menu = new("Menu", "scenes/menu.scene");
}

public sealed class SceneSystemService : Component, Component.INetworkListener
{
    [Sync(SyncFlags.FromHost)] public static GameScene CurrentLoadedGameScene { get; set; } = GameScene.Menu;

    [Description("Called from SceneFlow when a Scene is loaded.")]
    public static void OnSceneLoaded(GameScene gameScene)
    {
        if ( !Networking.IsHost )
            return;

        CurrentLoadedGameScene = gameScene;
    }

    public static bool TryGetSceneIfActive( GameScene gameScene, out Scene scene )
    {
        foreach( Scene sc in Scene.All )
        {
            SceneInformation sceneInformation = sc.GetComponentInChildren<SceneInformation>();
            if ( sc.Name == gameScene.Title || (sceneInformation is not null && sceneInformation.Title == gameScene.Title ))
            {
                scene = sc;
                return true;
            }
        }

        scene = null;
        return false;
    }

}