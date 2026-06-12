using System;

public sealed partial class GameController
{
	private const float ServerLoadingTimeoutSeconds = 20f;

	[Sync] public bool IsServerLoading { get; private set; }
	[Sync] public string ServerLoadingTitle { get; private set; } = "";
	[Sync] public string ServerLoadingMessage { get; private set; } = "";
	[Sync] public float ServerLoadingStartedAt { get; private set; }
	[Sync] public int ServerLoadingRevision { get; private set; }

	public float ServerLoadingClientTimeoutSeconds => ServerLoadingTimeoutSeconds + 5f;

	private void BeginServerLoading( string title, string message )
	{
		if ( !LoadingState.IsFunctionalityEnabled )
			return;

		if ( !Networking.IsHost )
			return;

		IsServerLoading = true;
		ServerLoadingTitle = string.IsNullOrWhiteSpace( title ) ? "Loading" : title;
		ServerLoadingMessage = message ?? "";
		ServerLoadingStartedAt = Time.Now;
		ServerLoadingRevision++;
	}

	private void ClearServerLoading()
	{
		if ( !LoadingState.IsFunctionalityEnabled )
			return;
		
		if ( !Networking.IsHost || !IsServerLoading )
			return;

		IsServerLoading = false;
		ServerLoadingRevision++;
	}

	private void UpdateServerLoadingWatchdog()
	{
		if ( !LoadingState.IsFunctionalityEnabled )
			return;

		if ( !Networking.IsHost || !IsServerLoading )
			return;

		if ( Time.Now - ServerLoadingStartedAt <= ServerLoadingTimeoutSeconds )
			return;

		Log.Warning( $"Server loading timed out after {ServerLoadingTimeoutSeconds:0.#}s. Clearing loading overlay: {ServerLoadingTitle}" );
		ClearServerLoading();
	}
}
