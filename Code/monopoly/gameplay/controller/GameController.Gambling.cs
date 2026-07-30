using System;
using System.Threading.Tasks;
using Sandbox;

public sealed partial class GameController : Component
{
	private const float GambleRevealDelaySeconds = 2f;
	private const float GambleResultHoldSeconds = 3.5f;

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

		var maximumWager = GetMaximumGambleWager( playerIndex );
		var minimumWager = Math.Max( Config?.MinimumWagerFromCard ?? 1, 0 );
		var betAmount = maximumWager <= 0
			? 0
			: maximumWager < minimumWager
				? maximumWager
				: Game.Random.Int( minimumWager, maximumWager );
		var payoutMultiplier = GetGamblePayoutMultiplier();
		var grossPayout = CalculateGambleGrossPayout( betAmount, payoutMultiplier );
		BeginGambleScreen(
			playerIndex,
			GambleType.CoinFlip,
			string.IsNullOrWhiteSpace( card.Title ) ? "Coin Flip" : card.Title,
			betAmount > 0
				? @$"{player.PlayerName} is wagering ${betAmount}. A win returns ${grossPayout}; a loss forfeits the wager."
				: @$"{player.PlayerName} was forced into a coin flip, but has no cash to bet.",
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
				@$"{player.PlayerName} has no cash available for this gamble."
			);

			SendPopupToPlayer( player, "Gamble Card", "You were forced into a coin flip, but had no cash to bet.", PopupKind.Warning );
			await ClearGambleScreenAfterHoldAsync();
			return;
		}

		var won = GetActiveCardGambleSession()?.Won ?? (Game.Random.Int( 0, 1 ) == 1);
		player.Money -= betAmount;
		if ( won )
		{
			CreditGamblePayout( player, grossPayout );
			TrySettlePendingForcedPaymentForPlayer( playerIndex );
		}
		else
		{
			AddToVacationCashBank( betAmount );
		}

		var resultSide = won ? "Win" : "Lose";
		var netWinnings = grossPayout - betAmount;
		var resultMessage = won
			? @$"{player.PlayerName} landed on WIN and gained ${netWinnings}."
			: @$"{player.PlayerName} landed on LOSE and paid ${betAmount}.";

		ResolveGambleScreen( won, resultSide, resultMessage );
		SendPopupToPlayer( player, "Gamble Card", won ? $"You won ${netWinnings} on the ${betAmount} coin flip!" : $"You lost the ${betAmount} coin flip.", won ? PopupKind.Success : PopupKind.Danger );
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
		BeginCardGambleSession( playerIndex, ActiveGambleTitle, ActiveGambleDescription, betAmount );
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
		ResolveActiveCardGambleSession( ActiveGambleResultMessage );
	}

	private async Task ClearGambleScreenAfterHoldAsync()
	{
		await Task.DelaySeconds( GambleResultHoldSeconds );
		ClearGambleScreen();
	}

	private void ClearGambleScreen()
	{
		SetHudIsVisibleAll(true);
		ClearActiveCardGambleSession();
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
