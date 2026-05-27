using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private bool TryMakeForcedPayment( PlayerState player, int amount, int receiverIndex, bool toBank, bool showForcedPaymentPopup = true, bool addToFreeParking = false )
	{
		if ( player is null || amount <= 0 )
			return true;

		if ( player.IsBankrupt )
			return false;

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return false;

		if ( HasPendingForcedPayment )
			return TrySettlePendingForcedPayment();

		if ( player.Money >= amount )
		{
			CompleteForcedPayment( playerIndex, amount, receiverIndex, toBank, showForcedPaymentPopup, addToFreeParking );
			return true;
		}

		var totalAssets = GetPlayerLiquidAssetTotal( playerIndex );
		if ( totalAssets < amount )
		{
			BankruptPlayer( playerIndex, Players.ElementAtOrDefault( receiverIndex ), true );
			return false;
		}

		BeginPendingForcedPayment( playerIndex, amount, receiverIndex, toBank, addToFreeParking );
		return false;
	}

	private void CompleteForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank, bool showForcedPaymentPopup = true, bool addToFreeParking = false )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || amount <= 0 )
			return;

		player.Money -= amount;

		if ( toBank )
		{
			if ( addToFreeParking && Config?.VacationCash == true )
				FreeParkingBank += amount;

			if ( showForcedPaymentPopup )
				ShowForcedPaymentToBankPopup( player, amount );
			return;
		}

		var receiver = Players.ElementAtOrDefault( receiverIndex );
		if ( receiver is not null )
		{
			receiver.Money += amount;
			ShowMoneyReceivedPopup( receiver, amount, player.PlayerName );
			if ( showForcedPaymentPopup )
				ShowForcedPaymentPopupToPlayers( player, receiver, amount );
		}
	}

	private void CompleteForcedPaymentToEachPlayer( int playerIndex, int amountPerPlayer )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var receivers = GetAssignedPlayerIndexes()
			.Where( index => index != playerIndex )
			.ToList();

		if ( player is null || amountPerPlayer <= 0 || receivers.Count == 0 )
			return;

		var total = amountPerPlayer * receivers.Count;
		player.Money -= total;

		foreach ( var receiverIndex in receivers )
		{
			var receiver = Players.ElementAtOrDefault( receiverIndex );
			if ( receiver is not null )
			{
				receiver.Money += amountPerPlayer;
				ShowMoneyReceivedPopup( receiver, amountPerPlayer, player.PlayerName );
			}
		}

		ShowForcedPaymentToEachPlayerPopup( player, amountPerPlayer );
		Log.Info( $"{player.PlayerName} paid ${amountPerPlayer} to each player." );
	}

	private void BeginPendingForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank, bool addToFreeParking )
	{
		PendingForcedPaymentPlayerIndex = playerIndex;
		PendingForcedPaymentAmount = amount;
		PendingForcedPaymentReceiverIndex = receiverIndex;
		PendingForcedPaymentToBank = toBank;
		PendingForcedPaymentAddsToFreeParking = addToFreeParking;
		PendingForcedPaymentToEachPlayer = false;
		PendingForcedPaymentEachPlayerAmount = 0;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is not null )
			Log.Info( $"{player.PlayerName} must raise ${amount} before their turn can end." );
	}

	private void BeginPendingForcedPaymentToEachPlayer( int playerIndex, int amountPerPlayer )
	{
		var receiverCount = GetAssignedPlayerIndexes().Count( index => index != playerIndex );
		var total = amountPerPlayer * receiverCount;
		if ( total <= 0 )
			return;

		PendingForcedPaymentPlayerIndex = playerIndex;
		PendingForcedPaymentAmount = total;
		PendingForcedPaymentReceiverIndex = -1;
		PendingForcedPaymentToBank = false;
		PendingForcedPaymentAddsToFreeParking = false;
		PendingForcedPaymentToEachPlayer = true;
		PendingForcedPaymentEachPlayerAmount = amountPerPlayer;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is not null )
			Log.Info( $"{player.PlayerName} must raise ${total} to pay each player ${amountPerPlayer}." );
	}

	private bool TrySettlePendingForcedPaymentForPlayer( int playerIndex )
	{
		if ( !HasPendingForcedPayment || PendingForcedPaymentPlayerIndex != playerIndex )
			return false;

		return TrySettlePendingForcedPayment();
	}

	private bool TrySettlePendingForcedPayment()
	{
		if ( !HasPendingForcedPayment )
			return true;

		var player = Players.ElementAtOrDefault( PendingForcedPaymentPlayerIndex );
		if ( player is null || player.IsBankrupt )
		{
			ClearPendingForcedPayment();
			return false;
		}

		if ( player.Money < PendingForcedPaymentAmount )
			return false;

		var amount = PendingForcedPaymentAmount;
		var receiverIndex = PendingForcedPaymentReceiverIndex;
		var toBank = PendingForcedPaymentToBank;
		var addToFreeParking = PendingForcedPaymentAddsToFreeParking;
		var toEachPlayer = PendingForcedPaymentToEachPlayer;
		var eachPlayerAmount = PendingForcedPaymentEachPlayerAmount;

		if ( toEachPlayer )
			CompleteForcedPaymentToEachPlayer( PendingForcedPaymentPlayerIndex, eachPlayerAmount );
		else
			CompleteForcedPayment( PendingForcedPaymentPlayerIndex, amount, receiverIndex, toBank, true, addToFreeParking );

		ClearPendingForcedPayment();

		Log.Info( $"{player.PlayerName} paid their pending ${amount} debt." );
		return true;
	}

	private void ClearPendingForcedPayment()
	{
		PendingForcedPaymentPlayerIndex = -1;
		PendingForcedPaymentAmount = 0;
		PendingForcedPaymentReceiverIndex = -1;
		PendingForcedPaymentToBank = false;
		PendingForcedPaymentAddsToFreeParking = false;
		PendingForcedPaymentToEachPlayer = false;
		PendingForcedPaymentEachPlayerAmount = 0;
	}
}
