using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	private void UpdatePopups()
	{
		if ( popups.Count == 0 )
			return;

		for ( var i = popups.Count - 1; i >= 0; i-- )
		{
			var popup = popups[i];
			if ( popup.Lifetime <= 0f )
				continue;

			popup.Lifetime -= Time.Delta;
			if ( popup.Lifetime <= 0f )
				popups.RemoveAt( i );
		}
	}

	public void SendPopupToAll( string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost )
			return;

		ShowPopup( nextPopupId++, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	public void SendPopupToPlayer( int playerIndex, string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost )
			return;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null )
			return;

		SendPopupToPlayer( player, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	public void SendPopupToPlayer( MonopolyPlayerState player, string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost )
			return;

		var connection = GetConnectionForPlayer( player );
		if ( connection is null )
			return;

		SendPopupToConnection( connection, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	public void SendPopupToConnection( Connection connection, string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost || connection is null )
			return;

		using ( Rpc.FilterInclude( connection ) )
		{
			ShowPopup( nextPopupId++, title, message, kind, canDismiss, lifetime, soundEnabled );
		}
	}

	public void DismissPopup( int popupId )
	{
		popups.RemoveAll( popup => popup.Id == popupId );
	}

	public void ShowLocalPopup( string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		ShowPopupLocal( nextPopupId++, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	[Rpc.Broadcast]
	private void ShowPopup( int popupId, string title, string message, PopupKind kind, bool canDismiss, float lifetime, bool soundEnabled )
	{
		ShowPopupLocal( popupId, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	private void ShowPopupLocal( int popupId, string title, string message, PopupKind kind, bool canDismiss, float lifetime, bool soundEnabled )
	{
		popups.RemoveAll( popup => popup.Id == popupId );
		popups.Add( new GamePopup
		{
			Id = popupId,
			Title = title ?? "",
			Message = message ?? "",
			Kind = kind,
			CanDismiss = canDismiss,
			Lifetime = lifetime,
			SoundEnabled = soundEnabled
		} );

		//TODO: add dismiss sound

		if (soundEnabled)
			MonopolyAssets.Sounds.Popup.ForKind(kind).Play();
			

		while ( popups.Count > 4 )
			popups.RemoveAt( 0 );
	}
}
