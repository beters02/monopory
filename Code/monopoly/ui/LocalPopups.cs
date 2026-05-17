using Sandbox;

public static class LocalPopups
{
	private static int nextPopupId = 1;
	private static readonly List<GamePopup> popups = new();
	private static readonly Dictionary<int, float> expiresAt = new();

	public static IReadOnlyList<GamePopup> Popups
	{
		get
		{
			Update();
			return popups;
		}
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
			Lifetime = lifetime,
			SoundEnabled = soundEnabled
		};

		popups.Add( popup );

		if ( lifetime > 0f )
			expiresAt[popup.Id] = Time.Now + lifetime;
		else
			expiresAt.Remove( popup.Id );

		if ( soundEnabled )
			GameAssets.Sounds.Popup.ForKind( kind ).Play();
	}

	public static void Dismiss( int popupId )
	{
		popups.RemoveAll( popup => popup.Id == popupId );
		expiresAt.Remove( popupId );
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
