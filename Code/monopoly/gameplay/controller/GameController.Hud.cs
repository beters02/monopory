public sealed partial class GameController : Component
{

    [Sync(SyncFlags.FromHost)] private bool IsHudVisible { get; set; } = true;
	[Sync(SyncFlags.FromHost)] public bool ForceHiddenUiVisible { get; private set; }

    public void SetHudIsVisibleAll(bool isVisible)
    {
        if ( !Networking.IsHost )
            return;

        IsHudVisible = isVisible;
    }

	public void SetForceHiddenUiVisible( bool isVisible )
	{
		if ( !Networking.IsHost )
			return;

		ForceHiddenUiVisible = isVisible;
	}

    public bool IsGameHudVisible() => IsHudVisible;
    public static bool IsGameHudVisibleStatic()
    {
        if (Instance is null)
            return true;
        return Instance.IsHudVisible;
    }

}
