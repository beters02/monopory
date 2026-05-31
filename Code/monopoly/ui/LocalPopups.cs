using Sandbox;
using System;

public static class LocalPopups
{
	private static int nextPopupId = 1;
	private static readonly List<GamePopup> popups = new();
	private static readonly Dictionary<int, float> expiresAt = new();
	private static readonly Dictionary<int, Action> confirmActions = new();
	private static readonly Dictionary<int, Action> cancelActions = new();

	public static IReadOnlyList<GamePopup> Popups
	{
		get
		{
			Update();
			return popups;
		}
	}

	public static bool HasBlockingPopup => popups.Any( popup => popup.IsBlocking );

	public static void Clear()
	{
		popups.Clear();
		expiresAt.Clear();
		confirmActions.Clear();
		cancelActions.Clear();
		nextPopupId = 1;
	}

	public static void Show( string title, string message, PopupKind kind = PopupKind.Warning, bool canDismiss = true, float lifetime = 3f, bool soundEnabled = true )
	{
		Update();

		var popup = new GamePopup
		{
			Id = nextPopupId++,
			Title = title ?? "",
			Message = string.IsNullOrWhiteSpace( message ) ? "This action is not available right now." : message,
			Kind = kind,
			CanDismiss = canDismiss,
			Lifetime = lifetime
		};

		popups.Add( popup );

		if ( lifetime > 0f )
			expiresAt[popup.Id] = Time.Now + lifetime;
		else
			expiresAt.Remove( popup.Id );

		if ( soundEnabled )
			GameAssets.Sounds.Popup.ForKind( kind ).Play();
	}

	public static int ShowConfirmation( string title, string message, Action onConfirm, Action onCancel = null, string confirmLabel = "Confirm", string cancelLabel = "Cancel", bool soundEnabled = true )
	{
		Update();

		var popup = new GamePopup
		{
			Id = nextPopupId++,
			Title = title ?? "Confirm action",
			Message = string.IsNullOrWhiteSpace( message ) ? "Are you sure?" : message,
			Kind = PopupKind.Confirmation,
			CanDismiss = false,
			Lifetime = 0f,
			IsBlocking = true,
			ConfirmLabel = string.IsNullOrWhiteSpace( confirmLabel ) ? "Confirm" : confirmLabel,
			CancelLabel = string.IsNullOrWhiteSpace( cancelLabel ) ? "Cancel" : cancelLabel
		};

		popups.Add( popup );
		expiresAt.Remove( popup.Id );

		confirmActions[popup.Id] = onConfirm;
		cancelActions[popup.Id] = onCancel;

		if ( soundEnabled )
			GameAssets.Sounds.Popup.ForKind( popup.Kind ).Play();

		return popup.Id;
	}

	public static void ResolveConfirmation( int popupId, bool confirmed )
	{
		Action callback = null;

		if ( confirmed )
			confirmActions.TryGetValue( popupId, out callback );
		else
			cancelActions.TryGetValue( popupId, out callback );

		Dismiss( popupId );
		callback?.Invoke();
	}

	public static void Dismiss( int popupId )
	{
		popups.RemoveAll( popup => popup.Id == popupId );
		expiresAt.Remove( popupId );
		confirmActions.Remove( popupId );
		cancelActions.Remove( popupId );
	}

	public static void Update()
	{
		if ( expiresAt.Count == 0 )
			return;

		var expiredIds = expiresAt
			.Where( entry => Time.Now >= entry.Value )
			.Select( entry => entry.Key )
			.ToList();

		foreach ( var popupId in expiredIds )
			Dismiss( popupId );
	}
}
