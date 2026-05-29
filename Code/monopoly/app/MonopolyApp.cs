public class MonopolyApp : Component
{
    public static bool IsStandalone = false;

	protected override void OnAwake()
	{
#if STANDALONE
        IsStandalone = true;
#endif
        Log.Info($"IsStandalone: {IsStandalone}");
	}

    protected override void OnUpdate()
    {
        if ( Scene.Camera is null )
            return;

        var hud = Scene.Camera.Hud;
        hud.DrawText( new TextRendering.Scope( "Hello!", Color.Red, 32 ), Screen.Width * 0.5f );
    }
}