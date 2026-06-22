using System;
using System.Text.Json.Serialization;

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
    private static SceneSystemService Instance;

    private static readonly GameScene[] KnownScenes =
    [
        GameScene.Menu,
        GameScene.Lobby,
        GameScene.Game
    ];

    [Sync(SyncFlags.FromHost)]
    private int CurrentLoadedGameSceneId { get; set; } = 0;

    public static event Action<GameScene> SceneLoaded;

	protected override void OnAwake()
	{
		base.OnAwake();

        Instance = this;
	}

    public static bool IsGameSceneActiveScene( GameScene gameScene ) =>
        GetActiveGameScene() == gameScene;

    public static GameScene GetActiveGameScene() =>
        KnownScenes[Instance.CurrentLoadedGameSceneId];

    [Description("Called from SceneFlow when a Scene is loaded.")]
    public static void OnSceneLoaded(GameScene gameScene)
    {
        if ( Networking.IsHost )
        {
            bool gotId = TryGetGameSceneIndex( gameScene, out int index );
            if ( gotId )
                Instance.CurrentLoadedGameSceneId = index;
            else
                Log.Warning($"Unable to receive scene index from {gameScene.Name}. CurrentLoadedGameSceneId will not be changed.");
        }
        
        SceneLoaded?.Invoke( gameScene );
        Log.Info($"[SceneSystemService] Successfully loaded GameScene {gameScene.Name}");
    }

    private static bool TryGetGameSceneIndex( GameScene scene, out int index )
    {
        for( int i = 0; i < KnownScenes.Length; i++ )
        {
            if ( KnownScenes[i] == scene )
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    public static bool TryGetGameScene( Scene scene, out GameScene gameScene )
    {
        if ( scene is null )
        {
            gameScene = null;
            return false;
        }

        SceneInformation sceneInformation = scene.GetComponentInChildren<SceneInformation>();
        string sceneTitle = sceneInformation?.Title ?? scene.Name;

        foreach ( GameScene knownScene in KnownScenes )
        {
            if ( scene.Name == knownScene.Title || sceneTitle == knownScene.Title )
            {
                gameScene = knownScene;
                return true;
            }
        }

        gameScene = null;
        return false;
    }

    public static bool TryGetSceneIfActive( GameScene gameScene, out Scene scene )
    {
        foreach( Scene sc in Scene.All )
        {
            if ( TryGetGameScene( sc, out GameScene activeGameScene ) && activeGameScene == gameScene )
            {
                scene = sc;
                return true;
            }
        }

        scene = null;
        return false;
    }

}
