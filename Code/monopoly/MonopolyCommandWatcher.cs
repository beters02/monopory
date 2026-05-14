using Sandbox;

public sealed class MonopolyCommandWatcher : Component
{
	private bool lastCheatsEnabled;
	private bool firstRun = true;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		var cheatsEnabled = Game.CheatsEnabled;

		if ( cheatsEnabled == lastCheatsEnabled )
			return;

		var oldValue = lastCheatsEnabled;
		lastCheatsEnabled = cheatsEnabled;

		OnSvCheatsChanged( oldValue, cheatsEnabled, firstRun );

		if ( firstRun )
			firstRun = false; 
	}

	private void OnSvCheatsChanged( bool oldValue, bool newValue, bool wasFirstRun )
	{
		Log.Info( $"sv_cheats changed: {oldValue} -> {newValue}" );

		if ( wasFirstRun )
			return;

		MonopolyGame.Instance?.SendPopupToAll(
			"Server cheats changed",
			$"sv_cheats is now {(newValue ? "enabled" : "disabled")}.",
			newValue ? MonopolyPopupKind.Warning : MonopolyPopupKind.Info
		);
	}
}
