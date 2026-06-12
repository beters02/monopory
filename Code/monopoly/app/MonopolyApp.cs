using System;
using System.Threading.Tasks;

public partial class MonopolyApp : Component
{

    public static readonly string GameVersion = "0.0.941";
    public static readonly ulong AppId = 4745160;
    

    private static bool Standalone = false;
    private static bool DebugEnabled = false;
    private static bool GameLaunchedWithDebugConvar = false;

	protected override void OnAwake()
	{
        AwakeStandalone();
        DebugEnabled = HandleLaunchedDebugEnabled();
        Log.Info($"IsStandalone: {IsStandalone()}");
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

    public static bool IsStandalone() => Standalone;
    public static bool IsDebugEnabled() => DebugEnabled;
    public static bool IsGameLaunchedWithDebugConvar() => GameLaunchedWithDebugConvar;
}
