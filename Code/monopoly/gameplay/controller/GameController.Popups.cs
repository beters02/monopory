using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
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
				DismissPopup( popup.Id );
		}
	}

	public void SendPopupToAll( string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		SendTableChatMessage( title, message );
	}

	public void SendGlobalPopupToAll( string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
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

	public void SendPopupToPlayer( PlayerState player, string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
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
		confirmPopupActions.Remove( popupId );
		cancelPopupActions.Remove( popupId );
	}

	public void ClearPopups()
	{
		popups.Clear();
		confirmPopupActions.Clear();
		cancelPopupActions.Clear();
	}

	public void ShowLocalPopup( string title, string message, PopupKind kind = PopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		ShowPopupLocal( nextLocalPopupId--, title, message, kind, canDismiss, lifetime, false, "Confirm", "Cancel", soundEnabled );
	}

	public int ShowLocalConfirmation( string title, string message, Action onConfirm, Action onCancel = null, string confirmLabel = "Confirm", string cancelLabel = "Cancel", bool soundEnabled = true )
	{
		var popupId = nextLocalPopupId--;
		ShowPopupLocal(
			popupId,
			title,
			string.IsNullOrWhiteSpace( message ) ? "Are you sure?" : message,
			PopupKind.Confirmation,
			false,
			0f,
			true,
			string.IsNullOrWhiteSpace( confirmLabel ) ? "Confirm" : confirmLabel,
			string.IsNullOrWhiteSpace( cancelLabel ) ? "Cancel" : cancelLabel,
			soundEnabled
		);

		confirmPopupActions[popupId] = onConfirm;
		cancelPopupActions[popupId] = onCancel;
		return popupId;
	}

	public void ResolveConfirmationPopup( int popupId, bool confirmed )
	{
		Action callback = null;

		if ( confirmed )
			confirmPopupActions.TryGetValue( popupId, out callback );
		else
			cancelPopupActions.TryGetValue( popupId, out callback );

		DismissPopup( popupId );
		callback?.Invoke();
	}

	public void SendTableChatMessage( string title, string message )
	{
		if ( !Networking.IsHost )
			return;

		SendSystemChatMessage( FormatNotificationForChat( title, message ) );
	}

	[Rpc.Broadcast]
	private void ShowPopup( int popupId, string title, string message, PopupKind kind, bool canDismiss, float lifetime, bool soundEnabled )
	{
		ShowPopupLocal( popupId, title, message, kind, canDismiss, lifetime, false, "Confirm", "Cancel", soundEnabled );
	}

	private void ShowPopupLocal( int popupId, string title, string message, PopupKind kind, bool canDismiss, float lifetime, bool isBlocking, string confirmLabel, string cancelLabel, bool soundEnabled )
	{
		DismissPopup( popupId );
		popups.Add( new GamePopup
		{
			Id = popupId,
			Title = title ?? "",
			Message = message ?? "",
			Kind = kind,
			CanDismiss = canDismiss,
			Lifetime = lifetime,
			IsBlocking = isBlocking,
			ConfirmLabel = confirmLabel,
			CancelLabel = cancelLabel
		} );

		if ( soundEnabled )
			GameAssets.Sounds.Popup.ForKind( kind ).Play();

		TrimPopupsToMaxVisible();
	}

	private void TrimPopupsToMaxVisible()
	{
		var maxVisiblePopups = Math.Max( 1, MaxVisiblePopups );
		var standardPopupCount = popups.Count( popup => popup.Kind != PopupKind.Confirmation );
		while ( standardPopupCount > maxVisiblePopups )
		{
			var oldestStandardPopup = popups.FirstOrDefault( popup => popup.Kind != PopupKind.Confirmation );
			if ( oldestStandardPopup is null )
				return;

			DismissPopup( oldestStandardPopup.Id );
			standardPopupCount--;
		}
	}

	// Custom Game Popups
	public void ShowPropertyBoughtPopup( PlayerState player, SpaceDef def )
	{
		if ( player is null || def is null )
			return;

		var title = "Property purchased";
		var message = $"{player.PlayerName} has purchased {def.DisplayName} for {def.Price}!";
		SendPopupToPlayer( player, title, $"You purchased {def.DisplayName} for {def.Price}.", PopupKind.Success, true, 4f );
		SendTableChatMessage( title, message );
	}

	private void ShowMoneyReceivedPopup( PlayerState player, int amount, string source )
	{
		if ( player is null || amount <= 0 )
			return;

		var message = string.IsNullOrWhiteSpace( source )
			? $"You received ${amount}."
			: $"You received ${amount} from {source}.";

		SendPopupToPlayer( player, "Money received", message, PopupKind.Success, true, 4f );
	}

	private void ShowTradeReceivedNotification( TradeRequest trade )
	{
		if ( trade is null )
			return;

		var sender = Players.ElementAtOrDefault( trade.SenderPlayerIndex );
		var receiver = Players.ElementAtOrDefault( trade.ReceiverPlayerIndex );
		if ( sender is null || receiver is null )
			return;

		if ( ShowTradeReceivedPopup )
		{
			SendPopupToPlayer(
				receiver,
				"Trade received",
				$"{sender.PlayerName} sent you a trade.",
				PopupKind.Info,
				true,
				4f,
				!TradeReceivedSound.IsAssigned
			);
		}

		PlayTradeNotificationSound( receiver, TradeReceivedSound );
	}

	private void ShowTradeAcceptedNotification( PlayerState player, PlayerState otherPlayer )
	{
		if ( player is null || otherPlayer is null )
			return;

		if ( ShowTradeAcceptedPopup )
		{
			SendPopupToPlayer(
				player,
				"Trade accepted",
				$"Your trade with {otherPlayer.PlayerName} was accepted.",
				PopupKind.Success,
				true,
				4f,
				!TradeAcceptedSound.IsAssigned
			);
		}

		PlayTradeNotificationSound( player, TradeAcceptedSound );
	}

	private void ShowTradeDeniedNotification( TradeRequest trade, int deniedByPlayerIndex )
	{
		if ( trade is null )
			return;

		var sender = Players.ElementAtOrDefault( trade.SenderPlayerIndex );
		var receiver = Players.ElementAtOrDefault( trade.ReceiverPlayerIndex );
		var deniedBy = Players.ElementAtOrDefault( deniedByPlayerIndex );
		if ( sender is null || receiver is null || deniedBy is null )
			return;

		var notifyPlayer = deniedByPlayerIndex == trade.SenderPlayerIndex ? receiver : sender;
		var otherPlayerName = deniedBy.PlayerName;

		if ( ShowTradeDeniedPopup )
		{
			SendPopupToPlayer(
				notifyPlayer,
				"Trade denied",
				$"{otherPlayerName} denied your trade.",
				PopupKind.Danger,
				true,
				4f,
				!TradeDeniedSound.IsAssigned
			);
		}

		PlayTradeNotificationSound( sender, TradeDeniedSound );
		PlayTradeNotificationSound( receiver, TradeDeniedSound );
	}

	private void ShowTradeNegotiationReceivedNotification( TradeRequest trade )
	{
		if ( trade is null )
			return;

		var sender = Players.ElementAtOrDefault( trade.SenderPlayerIndex );
		var receiver = Players.ElementAtOrDefault( trade.ReceiverPlayerIndex );
		if ( sender is null || receiver is null )
			return;

		if ( ShowTradeNegotiationReceivedPopup )
		{
			SendPopupToPlayer(
				receiver,
				"Negotiation received",
				$"{sender.PlayerName} sent you a counteroffer.",
				PopupKind.Info,
				true,
				4f,
				!TradeNegotiationReceivedSound.IsAssigned
			);
		}

		PlayTradeNotificationSound( receiver, TradeNegotiationReceivedSound );
	}

	private void PlayTradeNotificationSound( PlayerState player, GameSound sound )
	{
		if ( player is null || !sound.IsAssigned )
			return;

		PlaySoundToConnection( GetConnectionForPlayer( player ), sound );
	}

	private void ShowForcedPaymentPopupToPlayers( PlayerState payer, PlayerState receiver, int amount )
	{
		if ( payer is null || receiver is null || amount <= 0 )
			return;

		var title = "Forced payment";
		var message = $"{payer.PlayerName} paid ${amount} to {receiver.PlayerName}.";

		SendPopupToPlayer( payer, title, message, PopupKind.Danger, true, 4f );
		SendTableChatMessage( title, message );
	}

	private void ShowForcedPaymentToBankPopup( PlayerState payer, int amount )
	{
		if ( payer is null || amount <= 0 )
			return;

		var title = "Forced payment";
		var message = $"{payer.PlayerName} paid ${amount} to the bank.";

		SendPopupToPlayer( payer, title, message, PopupKind.Danger, true, 4f );
		SendTableChatMessage( title, message );
	}

	private void ShowForcedPaymentToEachPlayerPopup( PlayerState payer, int amountPerPlayer )
	{
		if ( payer is null || amountPerPlayer <= 0 )
			return;

		var title = "Forced payment";
		var message = $"{payer.PlayerName} paid ${amountPerPlayer} to every other player.";

		SendPopupToPlayer( payer, title, message, PopupKind.Danger, true, 4f );
		SendTableChatMessage( title, message );
	}

	private HashSet<string> CaptureOwnedSetKeys( params int[] playerIndexes )
	{
		var keys = new HashSet<string>();

		foreach ( var playerIndex in playerIndexes.Distinct().Where( index => index >= 0 ) )
		{
			foreach ( var setKey in GetOwnedSetKeysForPlayer( playerIndex ) )
				keys.Add( setKey );
		}

		return keys;
	}

	private IEnumerable<string> GetOwnedSetKeysForPlayer( int playerIndex )
	{
		if ( playerIndex < 0 )
			yield break;

		foreach ( ColorGroup colorGroup in Enum.GetValues<ColorGroup>() )
		{
			if ( colorGroup != ColorGroup.None && OwnsColorGroup( playerIndex, colorGroup ) )
				yield return $"color:{playerIndex}:{colorGroup}";
		}

		if ( GetRailroadProperties().Count > 0 && GetPlayerOwnedRailroadCount( playerIndex ) == GetRailroadProperties().Count )
			yield return $"railroad:{playerIndex}";

		if ( GetUtilityProperties().Count > 0 && GetPlayerOwnedUtilityCount( playerIndex ) == GetUtilityProperties().Count )
			yield return $"utility:{playerIndex}";
	}

	private void ShowNewlyOwnedSetPopups( HashSet<string> previousKeys, params int[] playerIndexes )
	{
		if ( previousKeys is null )
			return;

		ReportNewlyOwnedSetAchievements( previousKeys, playerIndexes );

		foreach ( var playerIndex in playerIndexes.Distinct().Where( index => index >= 0 ) )
		{
			var player = Players.ElementAtOrDefault( playerIndex );
			if ( player is null || !player.IsAssigned || player.IsBankrupt )
				continue;

			foreach ( var setKey in GetOwnedSetKeysForPlayer( playerIndex ) )
			{
				if ( previousKeys.Contains( setKey ) )
					continue;

				var title = "Property set owned";
				var message = $"{player.PlayerName} now owns {GetOwnedSetDisplayName( setKey )}.";
				SendPopupToPlayer( player, title, $"You now own {GetOwnedSetDisplayName( setKey )}.", PopupKind.Success, true, 5f );
				SendTableChatMessage( title, message );
			}
		}
	}

	private static string FormatNotificationForChat( string title, string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return title;

		return message;
	}

	private static string GetOwnedSetDisplayName( string setKey )
	{
		if ( string.IsNullOrWhiteSpace( setKey ) )
			return "a full set";

		if ( setKey.StartsWith( "color:", StringComparison.Ordinal ) )
		{
			var parts = setKey.Split( ':' );
			if ( parts.Length >= 3 && Enum.TryParse<ColorGroup>( parts[2], out var colorGroup ) )
				return $"the {GetColorGroupDisplayName( colorGroup )} set";
		}

		if ( setKey.StartsWith( "railroad:", StringComparison.Ordinal ) )
			return "the railroad set";

		if ( setKey.StartsWith( "utility:", StringComparison.Ordinal ) )
			return "the utility set";

		return "a full set";
	}

	private static string GetColorGroupDisplayName( ColorGroup colorGroup )
	{
		return colorGroup switch
		{
			ColorGroup.LightBlue => "Light Blue",
			ColorGroup.DarkBlue => "Dark Blue",
			_ => colorGroup.ToString()
		};
	}

}
