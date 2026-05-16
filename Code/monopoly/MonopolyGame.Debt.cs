using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	private bool TryMakeForcedPayment( PlayerState player, int amount, int receiverIndex, bool toBank )
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
			CompleteForcedPayment( playerIndex, amount, receiverIndex, toBank );
			return true;
		}

		var totalAssets = GetPlayerLiquidAssetTotal( playerIndex );
		if ( totalAssets < amount )
		{
			BankruptPlayer( playerIndex, Players.ElementAtOrDefault( receiverIndex ) );
			return false;
		}

		BeginPendingForcedPayment( playerIndex, amount, receiverIndex, toBank );
		return false;
	}

	private void CompleteForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || amount <= 0 )
			return;

		player.Money -= amount;

		if ( toBank )
		{
			if ( Config?.VacationCash == true )
				FreeParkingBank += amount;

			return;
		}

		var receiver = Players.ElementAtOrDefault( receiverIndex );
		if ( receiver is not null )
			receiver.Money += amount;
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
				receiver.Money += amountPerPlayer;
		}

		Log.Info( $"{player.PlayerName} paid ${amountPerPlayer} to each player." );
	}

	private void BeginPendingForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank )
	{
		PendingForcedPaymentPlayerIndex = playerIndex;
		PendingForcedPaymentAmount = amount;
		PendingForcedPaymentReceiverIndex = receiverIndex;
		PendingForcedPaymentToBank = toBank;
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
		var toEachPlayer = PendingForcedPaymentToEachPlayer;
		var eachPlayerAmount = PendingForcedPaymentEachPlayerAmount;

		if ( toEachPlayer )
			CompleteForcedPaymentToEachPlayer( PendingForcedPaymentPlayerIndex, eachPlayerAmount );
		else
			CompleteForcedPayment( PendingForcedPaymentPlayerIndex, amount, receiverIndex, toBank );

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
		PendingForcedPaymentToEachPlayer = false;
		PendingForcedPaymentEachPlayerAmount = 0;
	}
}
