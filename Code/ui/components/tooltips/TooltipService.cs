using Sandbox;
using System;

namespace Sandbox.ui.components;

public enum TooltipTriggerMode
{
	Hover,
	Input
}

public sealed record TooltipRequest(
	string Key,
	string Title,
	string Message,
	Vector2 ScreenPosition
);

public static class TooltipService
{
	public static TooltipRequest Active { get; private set; }
	public static int Revision { get; private set; }
	public static float ActivatedAt { get; private set; }

	public static bool IsActive( string key )
	{
		return Active is not null &&
			string.Equals( Active.Key, key, StringComparison.Ordinal );
	}

	public static void Show(
		string key,
		string title,
		string message,
		Vector2 screenPosition )
	{
		if ( string.IsNullOrWhiteSpace( key ) ||
			(string.IsNullOrWhiteSpace( title ) && string.IsNullOrWhiteSpace( message )) )
			return;

		Active = new TooltipRequest(
			key,
			title?.Trim() ?? "",
			message?.Trim() ?? "",
			screenPosition
		);
		ActivatedAt = Time.Now;
		Revision++;
	}

	public static void Toggle(
		string key,
		string title,
		string message,
		Vector2 screenPosition )
	{
		if ( IsActive( key ) )
		{
			Dismiss( key );
			return;
		}

		Show( key, title, message, screenPosition );
	}

	public static void Dismiss( string key = null )
	{
		if ( Active is null )
			return;

		if ( !string.IsNullOrWhiteSpace( key ) &&
			!string.Equals( Active.Key, key, StringComparison.Ordinal ) )
			return;

		Active = null;
		Revision++;
	}
}
