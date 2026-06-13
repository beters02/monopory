using System;
using System.Threading.Tasks;

public static class LoadingState
{
	public const float ClientTimeoutSeconds = 25f;
	public const float MinimumVisibleSeconds = 2f;
	public const float PresentationDelaySeconds = 0.2f;
	public const float OverlayTransitionSeconds = 0.65f;

	public static bool IsVisible { get; private set; }
	public static string Title { get; private set; } = "";
	public static string Message { get; private set; } = "";
	public static float StartedAt { get; private set; }
	public static float HiddenAt { get; private set; } = -1000f;
	public static int Revision { get; private set; }

	public static readonly bool IsFunctionalityEnabled = true;
	public static bool IsBlockingInteraction => IsVisible || Time.Now - HiddenAt <= OverlayTransitionSeconds;

	public static void Show( string title, string message = "" )
	{
		if ( !IsFunctionalityEnabled )
			return;
		
		Title = string.IsNullOrWhiteSpace( title ) ? "Loading" : title;
		Message = message ?? "";
		StartedAt = Time.Now;
		HiddenAt = -1000f;
		IsVisible = true;
		Revision++;
	}

	public static void Hide()
	{
		if ( !IsVisible )
			return;

		IsVisible = false;
		HiddenAt = Time.Now;
		Revision++;
	}

	public static void HideAfterSceneReady( int frames = 2, float minimumVisibleSeconds = MinimumVisibleSeconds )
	{
		if ( !IsVisible )
			return;

		_ = HideAfterSceneReadyAsync( Revision, frames, minimumVisibleSeconds );
	}

	public static async Task WaitUntilPresentedAsync( float minimumVisibleSeconds = PresentationDelaySeconds )
	{
		if ( !IsVisible )
			return;

		var revision = Revision;
		var readyAt = StartedAt + Math.Max( minimumVisibleSeconds, 0f );
		while ( IsVisible && Revision == revision && Time.Now < readyAt )
			await Task.Delay( 16 );
	}

	private static async Task HideAfterSceneReadyAsync( int revision, int frames, float minimumVisibleSeconds )
	{
		var hideAt = StartedAt + Math.Max( minimumVisibleSeconds, MinimumVisibleSeconds );
		while ( IsVisible && Revision == revision && Time.Now < hideAt )
			await Task.Delay( 16 );

		frames = Math.Max( frames, 0 );
		for ( var i = 0; i < frames && IsVisible && Revision == revision; i++ )
			await Task.Delay( 16 );

		if ( IsVisible && Revision == revision )
			Hide();
	}
}
