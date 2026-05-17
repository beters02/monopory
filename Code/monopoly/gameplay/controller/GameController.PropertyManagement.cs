using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	public void RequestBuildImprovement( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !CanBuildImprovement( playerIndex, spaceIndex ) )
			return;

		var player = Players[playerIndex];
		var cost = GetImprovementCost( spaceIndex );
		var count = GetImprovementCount( spaceIndex );

		if ( !PayBank( player, cost ) )
			return;

		PropertyImprovements[spaceIndex] = count + 1;

		Log.Info( $"{player.PlayerName} built on {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${cost}." );
	}

	[Rpc.Host]
	public void RequestSellImprovement( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !CanSellImprovement( playerIndex, spaceIndex ) )
			return;

		var player = Players[playerIndex];
		var refund = GetImprovementSellValue( spaceIndex );
		var count = GetImprovementCount( spaceIndex );

		player.Money += refund;

		if ( count <= 1 )
			PropertyImprovements.Remove( spaceIndex );
		else
			PropertyImprovements[spaceIndex] = count - 1;

		TrySettlePendingForcedPaymentForPlayer( playerIndex );

		Log.Info( $"{player.PlayerName} sold an improvement on {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${refund}." );
	}

	[Rpc.Host]
	public void RequestMortgageProperty( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !CanMortgageProperty( playerIndex, spaceIndex ) )
			return;

		var player = Players[playerIndex];
		var value = GetMortgageValue( spaceIndex );

		player.Money += value;
		MortgagedProperties[spaceIndex] = true;

		TrySettlePendingForcedPaymentForPlayer( playerIndex );

		Log.Info( $"{player.PlayerName} mortgaged {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${value}." );
	}

	[Rpc.Host]
	public void RequestUnmortgageProperty( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !CanUnmortgageProperty( playerIndex, spaceIndex ) )
			return;

		var player = Players[playerIndex];
		var cost = GetUnmortgageCost( spaceIndex );

		if ( !PayBank( player, cost ) )
			return;

		MortgagedProperties.Remove( spaceIndex );

		Log.Info( $"{player.PlayerName} unmortgaged {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${cost}." );
	}
}
