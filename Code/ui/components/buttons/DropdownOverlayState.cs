using Sandbox;
using Sandbox.UI;
using System;

namespace Sandbox.ui.components.buttons;

public static class DropdownOverlayState
{
	public static int ActiveId { get; private set; }
	public static int Version { get; private set; }
	public static Rect AnchorRect { get; private set; }
	public static IReadOnlyList<string> Items { get; private set; } = Array.Empty<string>();
	public static string Value { get; private set; } = "";
	public static bool SoundEnabled { get; private set; } = true;
	public static Panel ScrollTarget { get; private set; }

	private static Action<string> OnSelected;

	public static bool IsOpen => ActiveId != 0;

	public static void Open( int id, Rect anchorRect, IReadOnlyList<string> items, string value, bool soundEnabled, Panel scrollTarget, Action<string> onSelected )
	{
		ActiveId = id;
		AnchorRect = anchorRect;
		Items = items ?? Array.Empty<string>();
		Value = value ?? "";
		SoundEnabled = soundEnabled;
		ScrollTarget = scrollTarget;
		OnSelected = onSelected;
		Version++;
	}

	public static void UpdateAnchor( int id, Rect anchorRect )
	{
		if ( ActiveId != id )
			return;

		AnchorRect = anchorRect;
	}

	public static void Close( int id = 0 )
	{
		if ( id != 0 && ActiveId != id )
			return;

		if ( ActiveId == 0 )
			return;

		ActiveId = 0;
		Items = Array.Empty<string>();
		Value = "";
		ScrollTarget = null;
		OnSelected = null;
		Version++;
	}

	public static void Select( string item )
	{
		var callback = OnSelected;
		Close();
		callback?.Invoke( item );
	}
}
