using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	public bool TryChangeMoneyForPlayer( PlayerState player, int amount, out string message )
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

	private bool PayBank( PlayerState player, int amount )
	{
		if ( player is null || amount <= 0 )
			return true;

		if ( !TryMakeForcedPayment( player, amount, -1, true ) )
			return false;

		return true;
	}

	private bool PayPlayer( PlayerState player, PlayerState receiver, int amount )
	{
		if ( player is null || receiver is null || player == receiver || amount <= 0 )
			return true;

		return TryMakeForcedPayment( player, amount, GetPlayerIndex( receiver ), false );
	}
}
