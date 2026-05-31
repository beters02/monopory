using System;
using System.Threading.Tasks;
using Sandbox;

public sealed partial class GameController : Component
{
	private const float GambleRevealDelaySeconds = 2f;
	private const float GambleResultHoldSeconds = 3.5f;
	private const int GambleMinBetAmount = 50;
	private const int GambleMaxBetAmount = 100;

	private async Task PlayGambleCardAsync( PlayerState player, CardDef card )
	{
		if ( !Networking.IsHost || player is null || card is null )
			return;

		switch ( card.GambleType )
		{
			case GambleType.CoinFlip:
			default:
				await PlayCoinFlipGambleAsync( player, card );
				break;
		}
	}

	private async Task PlayCoinFlipGambleAsync( PlayerState player, CardDef card )
	{
		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return;

		var randomBetAmount = Game.Random.Int( GambleMinBetAmount, GambleMaxBetAmount );
		var betAmount = Math.Min( randomBetAmount, Math.Max( player.Money, 0 ) );
		BeginGambleScreen(
			playerIndex,
			GambleType.CoinFlip,
			string.IsNullOrWhiteSpace( card.Title ) ? "Coin Flip" : card.Title,
			betAmount > 0
				? $"{player.PlayerName} is flipping for ${betAmount}. Win side pays ${betAmount}; lose side costs ${betAmount}."
				: $"{player.PlayerName} was forced into a coin flip, but has no cash to bet.",
			betAmount
		);

		await Task.DelaySeconds( GambleRevealDelaySeconds );

		if ( !IsSameActiveGamble( playerIndex, GambleType.CoinFlip ) || player.IsBankrupt )
			return;

		if ( betAmount <= 0 )
		{
			ResolveGambleScreen(
				false,
				"No Bet",
				$"{player.PlayerName} has no cash available for this gamble."
			);

			SendPopupToPlayer( player, "Gamble Card", "You were forced into a coin flip, but had no cash to bet.", PopupKind.Warning );
			await ClearGambleScreenAfterHoldAsync();
			return;
		}

		var won = Game.Random.Int( 0, 1 ) == 1;
		if ( won )
		{
			player.Money += betAmount;
			TrySettlePendingForcedPaymentForPlayer( playerIndex );
		}
		else
		{
			player.Money -= betAmount;
		}

		var resultSide = won ? "Win" : "Lose";
		var resultMessage = won
			? $"{player.PlayerName} landed on WIN and gained ${betAmount}."
			: $"{player.PlayerName} landed on LOSE and paid ${betAmount}.";

		ResolveGambleScreen( won, resultSide, resultMessage );
		SendPopupToPlayer( player, "Gamble Card", won ? $"You won the ${betAmount} coin flip!" : $"You lost the ${betAmount} coin flip.", won ? PopupKind.Success : PopupKind.Danger );
		Log.Info( $"{player.PlayerName} {(won ? "won" : "lost")} a ${betAmount} coin flip gamble." );

		await ClearGambleScreenAfterHoldAsync();
	}

	private void BeginGambleScreen( int playerIndex, GambleType gambleType, string title, string description, int betAmount )
	{
		SetHudIsVisibleAll(false);
		ActiveGambleId++;
		ActiveGamblePlayerIndex = playerIndex;
		ActiveGambleType = (int)gambleType;
		ActiveGambleTitle = title ?? "";
		ActiveGambleDescription = description ?? "";
		ActiveGambleBetAmount = betAmount;
		ActiveGambleStartedAt = Time.Now;
		ActiveGambleRevealAt = Time.Now + GambleRevealDelaySeconds;
		ActiveGambleResolved = false;
		ActiveGambleWon = false;
		ActiveGambleResultSide = "";
		ActiveGambleResultMessage = "";
		ActiveGambleEndsAt = Time.Now + GambleRevealDelaySeconds + GambleResultHoldSeconds;
	}

	private bool IsSameActiveGamble( int playerIndex, GambleType gambleType )
	{
		return ActiveGamblePlayerIndex == playerIndex &&
			ActiveGambleType == (int)gambleType &&
			ActiveGambleEndsAt > Time.Now;
	}

	private void ResolveGambleScreen( bool won, string resultSide, string resultMessage )
	{
		ActiveGambleResolved = true;
		ActiveGambleWon = won;
		ActiveGambleResultSide = resultSide ?? "";
		ActiveGambleResultMessage = resultMessage ?? "";
	}

	private async Task ClearGambleScreenAfterHoldAsync()
	{
		await Task.DelaySeconds( GambleResultHoldSeconds );
		ClearGambleScreen();
	}

	private void ClearGambleScreen()
	{
		SetHudIsVisibleAll(true);
		ActiveGamblePlayerIndex = -1;
		ActiveGambleType = 0;
		ActiveGambleTitle = "";
		ActiveGambleDescription = "";
		ActiveGambleBetAmount = 0;
		ActiveGambleStartedAt = 0f;
		ActiveGambleRevealAt = 0f;
		ActiveGambleResolved = false;
		ActiveGambleWon = false;
		ActiveGambleResultSide = "";
		ActiveGambleResultMessage = "";
		ActiveGambleEndsAt = 0f;
	}
}
