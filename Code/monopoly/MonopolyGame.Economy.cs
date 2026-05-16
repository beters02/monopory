using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	public bool TryChangeMoneyForPlayer( MonopolyPlayerState player, int amount, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can change player money directly.";
			return false;
		}

		if ( player is null )
		{
			message = "Player does not exist.";
			return false;
		}

		if ( GetPlayerIndex( player ) < 0 || !player.IsAssigned )
		{
			message = "Player is not part of this game.";
			return false;
		}

		if ( player.IsBankrupt )
		{
			message = $"{player.PlayerName} is bankrupt.";
			return false;
		}

		player.Money += amount;
		TrySettlePendingForcedPaymentForPlayer( GetPlayerIndex( player ) );

		var direction = amount >= 0 ? "added to" : "removed from";
		var absoluteAmount = Math.Abs( amount );
		Log.Info( $"Cheat changed {player.PlayerName}'s money: ${absoluteAmount} {direction} balance." );
		message = $"{player.PlayerName} now has ${player.Money}.";
		return true;
	}

	private bool PayBank( MonopolyPlayerState player, int amount )
	{
		if ( player is null || amount <= 0 )
			return true;

		if ( !TryMakeForcedPayment( player, amount, -1, true ) )
			return false;

		return true;
	}

	private bool PayPlayer( MonopolyPlayerState player, MonopolyPlayerState receiver, int amount )
	{
		if ( player is null || receiver is null || player == receiver || amount <= 0 )
			return true;

		return TryMakeForcedPayment( player, amount, GetPlayerIndex( receiver ), false );
	}

	private bool TryMakeForcedPayment( MonopolyPlayerState player, int amount, int receiverIndex, bool toBank )
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

	public int GetPlayerLiquidAssetTotal( int playerIndex )
	{
		if ( playerIndex < 0 )
			return 0;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || player.IsBankrupt )
			return 0;

		var total = Math.Max( player.Money, 0 );
		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			total += GetImprovementCount( spaceIndex ) * GetImprovementSellValue( spaceIndex );

			if ( !IsMortgaged( spaceIndex ) )
				total += GetMortgageValue( spaceIndex );
		}

		return total;
	}

	private void BankruptPlayer( int playerIndex, MonopolyPlayerState creditor )
	{
		if ( playerIndex < 0 )
			return;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || player.IsBankrupt )
			return;

		player.IsBankrupt = true;
		player.Money = 0;
		player.IsInJail = false;
		player.ConsecutiveDoubles = 0;
		player.SkipsNextTurn = false;

		if ( PendingForcedPaymentPlayerIndex == playerIndex )
			ClearPendingForcedPayment();

		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			PropertyOwners.Remove( spaceIndex );
			PropertyImprovements.Remove( spaceIndex );
			MortgagedProperties.Remove( spaceIndex );
		}

		foreach ( var trade in GetTrades() )
		{
			if ( trade.SenderPlayerIndex == playerIndex || trade.ReceiverPlayerIndex == playerIndex )
				PendingTrades.Remove( trade.Id );
		}

		if ( AuctionHighBidderIndex == playerIndex )
		{
			AuctionHighBidderIndex = -1;
			AuctionCurrentBid = 0;
		}

		var creditorText = creditor is null ? "" : $" while owing {creditor.PlayerName}";
		Log.Info( $"{player.PlayerName} went bankrupt{creditorText}." );
		CheckForGameOver();
	}

	private void CheckForGameOver()
	{
		if ( MatchState != MonopolyMatchState.InGame )
			return;

		if ( StartingPlayerCount <= 1 )
			return;

		var remaining = Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null && entry.player.IsAssigned && !entry.player.IsBankrupt )
			.ToList();

		if ( remaining.Count > 1 )
			return;

		WinnerPlayerIndex = remaining.Count == 1 ? remaining[0].index : -1;
		CurrentTurnEndsAt = 0f;
		ClearAuction();
		ClearPendingForcedPayment();
		PendingPurchaseSpaceIndex = -1;
		Phase = MonopolyGamePhase.TurnEnded;
		MatchState = MonopolyMatchState.GameOver;

		var winnerName = Winner?.PlayerName ?? "No one";
		SendPopupToAll( "Game over", $"{winnerName} won the game.", MonopolyPopupKind.Success, true, 8f );
		Log.Info( $"Monopoly game over. Winner: {winnerName}." );
	}
}
