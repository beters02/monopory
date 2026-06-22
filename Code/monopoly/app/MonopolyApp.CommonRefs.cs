public partial class MonopolyApp
{

    //
    // Lobby Ref
    //
    public static LobbyController LobbyRef => GetLobbyRef();
    private static LobbyController CachedLobbyRef;

    private static LobbyController GetLobbyRef()
    {
        if ( CachedLobbyRef is not null && CachedLobbyRef.IsValid )
            return CachedLobbyRef;

        bool foundLobbyScene = SceneSystemService.TryGetSceneIfActive(GameScene.Lobby, out Scene lobbyScene);
        if ( foundLobbyScene )
        {
            CachedLobbyRef = lobbyScene.GetComponentInChildren<LobbyController>();
            return CachedLobbyRef;
        }

        return null;
    }

    //
    // Game Ref
    //

    public static GameController GameRef => GetGameRef();
    private static GameController CachedGameRef;

    private static GameController GetGameRef()
    {
        if ( CachedGameRef is not null && CachedGameRef.IsValid )
            return CachedGameRef;

        bool foundGameScene = SceneSystemService.TryGetSceneIfActive(GameScene.Game, out Scene gameScene);
        if ( foundGameScene )
        {
            CachedGameRef = gameScene.GetComponentInChildren<GameController>();
            return CachedGameRef;
        }

        return null;
    }

}