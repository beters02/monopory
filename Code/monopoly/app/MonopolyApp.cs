using System;
using System.Threading.Tasks;

public enum InputActionIconType
{
    Default,
    Outline
}

public partial class MonopolyApp : Component
{

    public static readonly string GameVersion = "0.0.95";
    public static readonly ulong AppId = 4745160;
    
    private static bool Standalone = false;
    private static bool DebugEnabled = false;
    private static bool GameLaunchedWithDebugConvar = false;

    public static readonly InputActionIconType InputActionIconType = InputActionIconType.Outline;
    public static readonly bool IsGameStatusPanelActionButtonsEnabled = false;

    private static readonly bool EditorIsStandalone = true;

    [Sync( SyncFlags.FromHost )] public bool CheatsEnabled { get; set; } = false;

	protected override void OnAwake()
	{
        Log.Info("Instance registered");
		RegisterInstance();
		AwakeStandalone();
		DebugEnabled = HandleLaunchedDebugEnabled();
		Log.Info($"IsStandalone: {IsStandalone()}");
	}

	protected override void OnStart()
	{
		base.OnStart();

        SceneInformation sceneInfo = Scene.Get<SceneInformation>();
        if ( sceneInfo is null )
            Log.Warning("Could not find SceneInformation to verify that MonopolyApp exists in lobby scene.");

        if ( !Networking.IsHost )
            return;

        bool isLobbyScene = false;
        bool isGameScene = false;
        if ( sceneInfo is not null )
        {
            isLobbyScene = sceneInfo.Title == "Lobby";
            isGameScene = sceneInfo.Title == "Main";
        }

        if ( isLobbyScene && Standalone && CheatsEnabled == true )
        {
            CheatsEnabled = false;
        }

        if ( isGameScene )
        {
            Log.Info(CheatsEnabled);
        }
	}

	protected override void OnDestroy()
	{
        Log.Info("Instance destroyedf");
		UnregisterInstance();
	}

    protected override void OnUpdate()
    {
        UpdateStandalone();
    }

    private bool HandleLaunchedDebugEnabled()
    {
        var debugConvarParsed = bool.TryParse(ConsoleSystem.GetValue( DebugConVar.Name ), out bool debugConvar);
        GameLaunchedWithDebugConvar = debugConvar;
		return debugConvarParsed && debugConvar;
    }

    public static bool IsCallerHost( Connection caller )
    {
        return IsEffectiveHostCaller( caller ) || caller.IsHost;
    }

    public static void SetCheatsEnabled(bool enabled)
    {
        if (Networking.IsHost)
        {
            if ( Instance is not null )
                Instance.CheatsEnabled = enabled;

            return;
        }

        Instance?.SetCheatsEnabledHost(enabled);
    }

    [Rpc.Host]
    private void SetCheatsEnabledHost(bool enabled)
    {
        CheatsEnabled = enabled;        
    }

    public static bool IsCheatsEnabled()
    {
        return Instance?.CheatsEnabled ?? false;
    }

    public static bool IsStandalone()
    {
        if (Application.IsEditor && EditorIsStandalone)
            return true;
        
        return Standalone;
    }
    public static bool IsDebugEnabled() => DebugEnabled;
    public static bool IsGameLaunchedWithDebugConvar() => GameLaunchedWithDebugConvar;
}
