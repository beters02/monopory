using System;
using System.Threading.Tasks;

public enum InputActionIconType
{
    Default,
    Outline
}

public partial class MonopolyApp : Component
{

    public static readonly string GameVersion = "0.0.943";
    public static readonly ulong AppId = 4745160;
    
    private static bool Standalone = false;
    private static bool DebugEnabled = false;
    private static bool GameLaunchedWithDebugConvar = false;

    public static readonly InputActionIconType InputActionIconType = InputActionIconType.Outline;
    public static readonly bool IsGameStatusPanelActionButtonsEnabled = false;

    [Sync( SyncFlags.FromHost )] public static bool CheatsEnabled { get; set; } = false;

	protected override void OnAwake()
	{
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
            CheatsEnabled = enabled;
            return;
        }
            

        instance?.SetCheatsEnabledHost(enabled);
    }

    [Rpc.Host]
    private void SetCheatsEnabledHost(bool enabled)
    {
        CheatsEnabled = enabled;        
    }

    public static bool IsStandalone() => Standalone;
    public static bool IsDebugEnabled() => DebugEnabled;
    public static bool IsGameLaunchedWithDebugConvar() => GameLaunchedWithDebugConvar;
}
