using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	[Rpc.Host]
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

		if ( !PayBank( player, cost, false ) )
			return;

		PropertyImprovements[spaceIndex] = count + 1;

		var spaceName = Board.GetSpaceDef( spaceIndex )?.DisplayName ?? "property";
		SendPopupToPlayer( player, "Improvement purchased", $"You built on {spaceName} for ${cost}.", PopupKind.Success, true, 4f );
		SendTableChatMessage( "Improvement purchased", $"{player.PlayerName} built on {spaceName} for ${cost}." );
		Log.Info( $"{player.PlayerName} built on {spaceName} for ${cost}." );
		TryAutosaveStablePoint( "Improvement purchased" );
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

		var soldSpaceName = Board.GetSpaceDef( spaceIndex )?.DisplayName ?? "property";
		SendPopupToPlayer( player, "Improvement sold", $"You sold an improvement on {soldSpaceName} for ${refund}.", PopupKind.Warning, true, 4f );
		SendTableChatMessage( "Improvement sold", $"{player.PlayerName} sold an improvement on {soldSpaceName} for ${refund}." );
		Log.Info( $"{player.PlayerName} sold an improvement on {soldSpaceName} for ${refund}." );
		TryAutosaveStablePoint( "Improvement sold" );
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

		var mortgagedSpaceName = Board.GetSpaceDef( spaceIndex )?.DisplayName ?? "property";
		SendPopupToPlayer( player, "Property sold", $"You mortgaged {mortgagedSpaceName} for ${value}.", PopupKind.Warning, true, 4f );
		SendTableChatMessage( "Property sold", $"{player.PlayerName} mortgaged {mortgagedSpaceName} for ${value}." );
		Log.Info( $"{player.PlayerName} mortgaged {mortgagedSpaceName} for ${value}." );
		TryAutosaveStablePoint( "Property mortgaged" );
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

		if ( !PayBank( player, cost, false ) )
			return;

		MortgagedProperties.Remove( spaceIndex );

		var unmortgagedSpaceName = Board.GetSpaceDef( spaceIndex )?.DisplayName ?? "property";
		SendPopupToPlayer( player, "Property purchased", $"You unmortgaged {unmortgagedSpaceName} for ${cost}.", PopupKind.Success, true, 4f );
		SendTableChatMessage( "Property purchased", $"{player.PlayerName} unmortgaged {unmortgagedSpaceName} for ${cost}." );
		Log.Info( $"{player.PlayerName} unmortgaged {unmortgagedSpaceName} for ${cost}." );
		TryAutosaveStablePoint( "Property unmortgaged" );
	}
}
