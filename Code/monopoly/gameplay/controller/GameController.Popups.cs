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
			Lifetime = lifetime
		} );

		if ( soundEnabled )
			GameAssets.Sounds.Popup.ForKind( kind ).Play();

		while ( popups.Count > 4 )
			popups.RemoveAt( 0 );
	}

	// Custom Game Popups
	public void ShowPropertyBoughtPopup( PlayerState player, SpaceDef def )
	{
		SendPopupToAll(
			"Property purchased",
			$"{player.PlayerName} has purchased {def.DisplayName} for {def.Price}!",
			PopupKind.Success
		);
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

	private void ShowTradeAcceptedPopup( PlayerState player, PlayerState otherPlayer )
	{
		if ( player is null || otherPlayer is null )
			return;

		SendPopupToPlayer(
			player,
			"Trade accepted",
			$"Your trade with {otherPlayer.PlayerName} was accepted.",
			PopupKind.Success,
			true,
			4f
		);
	}

	private void ShowForcedPaymentPopupToPlayers( PlayerState payer, PlayerState receiver, int amount )
	{
		if ( payer is null || receiver is null || amount <= 0 )
			return;

		var title = "Forced payment";
		var message = $"{payer.PlayerName} paid ${amount} to {receiver.PlayerName}.";

		foreach ( var player in Players.Where( player => player is not null && player.IsAssigned && !player.IsBankrupt ) )
		{
			var kind = player == payer ? PopupKind.Danger : PopupKind.Success;
			SendPopupToPlayer( player, title, message, kind, true, 4f );
		}
	}

	private void ShowForcedPaymentToBankPopup( PlayerState payer, int amount )
	{
		if ( payer is null || amount <= 0 )
			return;

		var title = "Forced payment";
		var message = $"{payer.PlayerName} paid ${amount} to the bank.";

		foreach ( var player in Players.Where( player => player is not null && player.IsAssigned && !player.IsBankrupt ) )
		{
			var kind = player == payer ? PopupKind.Danger : PopupKind.Success;
			SendPopupToPlayer( player, title, message, kind, true, 4f );
		}
	}

	private void ShowForcedPaymentToEachPlayerPopup( PlayerState payer, int amountPerPlayer )
	{
		if ( payer is null || amountPerPlayer <= 0 )
			return;

		var title = "Forced payment";
		var message = $"{payer.PlayerName} paid ${amountPerPlayer} to every other player.";

		foreach ( var player in Players.Where( player => player is not null && player.IsAssigned && !player.IsBankrupt ) )
		{
			var kind = player == payer ? PopupKind.Danger : PopupKind.Success;
			SendPopupToPlayer( player, title, message, kind, true, 4f );
		}
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

		foreach ( var playerIndex in playerIndexes.Distinct().Where( index => index >= 0 ) )
		{
			var player = Players.ElementAtOrDefault( playerIndex );
			if ( player is null || !player.IsAssigned || player.IsBankrupt )
				continue;

			foreach ( var setKey in GetOwnedSetKeysForPlayer( playerIndex ) )
			{
				if ( previousKeys.Contains( setKey ) )
					continue;

				SendPopupToAll(
					"Property set owned",
					$"{player.PlayerName} now owns {GetOwnedSetDisplayName( setKey )}.",
					PopupKind.Success,
					true,
					5f
				);
			}
		}
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
