using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private bool TryMakeForcedPayment( PlayerState player, int amount, int receiverIndex, bool toBank, bool showForcedPaymentPopup = true, bool addToFreeParking = false, int rentSpaceIndex = -1, int vacationCashAmount = 0 )
	{
		if ( player is null || amount <= 0 )
			return true;

		if ( player.IsBankrupt )
			return false;

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return false;

		vacationCashAmount = Math.Clamp( vacationCashAmount, 0, amount );
		if ( HasPendingForcedPayment )
			return TrySettlePendingForcedPayment();

		if ( player.Money >= amount )
		{
			CompleteForcedPayment( playerIndex, amount, receiverIndex, toBank, showForcedPaymentPopup, addToFreeParking, rentSpaceIndex, vacationCashAmount );
			return true;
		}

		var totalAssets = GetPlayerLiquidAssetTotal( playerIndex );
		if ( totalAssets < amount )
		{
			var creditorAmount = Math.Max( amount - vacationCashAmount, 0 );
			var creditor = creditorAmount > 0 ? Players.ElementAtOrDefault( receiverIndex ) : null;
			BankruptPlayer( playerIndex, creditor, true, creditorAmount );
			return false;
		}

		BeginPendingForcedPayment( playerIndex, amount, receiverIndex, toBank, addToFreeParking, rentSpaceIndex, vacationCashAmount );
		return false;
	}

	private void CompleteForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank, bool showForcedPaymentPopup = true, bool addToFreeParking = false, int rentSpaceIndex = -1, int vacationCashAmount = 0 )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || amount <= 0 )
			return;

		vacationCashAmount = Math.Clamp( vacationCashAmount, 0, amount );
		player.Money -= amount;

		if ( toBank )
		{
			if ( addToFreeParking && Config?.VacationCash == true )
				AddToVacationCashBank( amount );

			if ( showForcedPaymentPopup )
				ShowForcedPaymentToBankPopup( player, amount );
			return;
		}

		var receiverAmount = Math.Max( amount - vacationCashAmount, 0 );
		var receiver = Players.ElementAtOrDefault( receiverIndex );
		if ( receiver is not null && receiverAmount > 0 )
		{
			receiver.Money += receiverAmount;
			ShowMoneyReceivedPopup( receiver, receiverAmount, player.PlayerName );
			if ( showForcedPaymentPopup )
				ShowForcedPaymentPopupToPlayers( player, receiver, receiverAmount );
		}

		RecordPropertyRentPaymentForStats( playerIndex, receiverIndex, rentSpaceIndex, amount, receiverAmount );

		if ( vacationCashAmount > 0 )
			AddToVacationCashBank( vacationCashAmount );
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

	private void BeginPendingForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank, bool addToFreeParking, int rentSpaceIndex = -1, int vacationCashAmount = 0 )
	{
		PendingForcedPaymentPlayerIndex = playerIndex;
		PendingForcedPaymentAmount = amount;
		PendingForcedPaymentReceiverIndex = receiverIndex;
		PendingForcedPaymentToBank = toBank;
		PendingForcedPaymentAddsToFreeParking = addToFreeParking;
		PendingForcedPaymentToEachPlayer = false;
		PendingForcedPaymentEachPlayerAmount = 0;
		PendingForcedPaymentRentSpaceIndex = rentSpaceIndex;
		PendingForcedPaymentVacationCashAmount = Math.Clamp( vacationCashAmount, 0, amount );

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is not null )
		{
			var needed = Math.Max( amount - player.Money, 0 );
			SendConfirmationNoticeToPlayer( player, "Mortgage required", $"You need ${needed} more to pay this ${amount} debt.", "OK", "Bankrupt" );
			Log.Info( $"{player.PlayerName} must raise ${amount} before their turn can end." );
		}
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
		PendingForcedPaymentRentSpaceIndex = -1;
		PendingForcedPaymentVacationCashAmount = 0;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is not null )
		{
			var needed = Math.Max( total - player.Money, 0 );
			SendConfirmationNoticeToPlayer( player, "Mortgage required", $"You need ${needed} more to pay ${amountPerPlayer} to each player.", "OK", "Bankrupt" );
			Log.Info( $"{player.PlayerName} must raise ${total} to pay each player ${amountPerPlayer}." );
		}
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
		var rentSpaceIndex = PendingForcedPaymentRentSpaceIndex;
		var vacationCashAmount = PendingForcedPaymentVacationCashAmount;

		if ( toEachPlayer )
			CompleteForcedPaymentToEachPlayer( PendingForcedPaymentPlayerIndex, eachPlayerAmount );
		else
			CompleteForcedPayment( PendingForcedPaymentPlayerIndex, amount, receiverIndex, toBank, true, addToFreeParking, rentSpaceIndex, vacationCashAmount );

		ClearPendingForcedPayment();

		Log.Info( $"{player.PlayerName} paid their pending ${amount} debt." );

		if ( Phase == GamePhase.TurnEnded && player == CurrentPlayer )
			SetPostActionPhase();

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
		PendingForcedPaymentRentSpaceIndex = -1;
		PendingForcedPaymentVacationCashAmount = 0;
	}
}
