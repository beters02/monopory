using System;

public static class LoadingState
{
	public const float ClientTimeoutSeconds = 25f;

	public static bool IsVisible { get; private set; }
	public static string Title { get; private set; } = "";
	public static string Message { get; private set; } = "";
	public static float StartedAt { get; private set; }
	public static int Revision { get; private set; }

	public static readonly bool IsFunctionalityEnabled = false;

	public static void Show( string title, string message = "" )
	{
		if ( !IsFunctionalityEnabled )
			return;
		
		Title = string.IsNullOrWhiteSpace( title ) ? "Loading" : title;
		Message = message ?? "";
		StartedAt = Time.Now;
		IsVisible = true;
		Revision++;
	}

	public static void Hide()
	{
		if ( !IsFunctionalityEnabled )
			return;
		
		if ( !IsVisible )
			return;

		IsVisible = false;
		Revision++;
	}
}
