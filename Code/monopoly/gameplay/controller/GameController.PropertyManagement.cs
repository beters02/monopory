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
		SendGlobalPopupToAll( "Improvement purchased", $"{player.PlayerName} built on {spaceName} for ${cost}.", PopupKind.Success, true, 4f );
		SendTableChatMessage( "Improvement purchased", $"{player.PlayerName} built on {spaceName} for ${cost}." );
		Log.Info( $"{player.PlayerName} built on {spaceName} for ${cost}." );
		TryAutosaveStablePoint( "Improvement purchased" );
	}

	[Rpc.Host]
	public void RequestBuildColorSetImprovement( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		TryBuildColorSetImprovement( playerIndex, spaceIndex );
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
	public void RequestSellColorSetImprovement( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		TrySellColorSetImprovement( playerIndex, spaceIndex );
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
	public void RequestMortgageColorSet( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		TryMortgageColorSet( playerIndex, spaceIndex );
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

	private bool TryBuildColorSetImprovement( int playerIndex, int sourceSpaceIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var properties = GetManageableColorSetProperties( playerIndex, sourceSpaceIndex );
		if ( player is null || properties.Count == 0 )
			return false;

		var targets = properties.Where( property => CanBuildImprovement( playerIndex, property.Index ) ).ToList();
		if ( targets.Count == 0 )
			return false;

		var totalCost = targets.Sum( property => GetImprovementCost( property.Index ) );
		if ( totalCost <= 0 || player.Money < totalCost )
			return false;

		foreach ( var property in targets )
		{
			var count = GetImprovementCount( property.Index );
			var cost = GetImprovementCost( property.Index );
			if ( !PayBank( player, cost, false ) )
				return false;

			PropertyImprovements[property.Index] = count + 1;
		}

		var colorSetName = targets.FirstOrDefault()?.ColorGroup.ToString() ?? "color";
		SendGlobalPopupToAll( "Improvements purchased", $"{player.PlayerName} bought 1 house on each {colorSetName} property set!", PopupKind.Success, true, 4f );
		SendTableChatMessage( "Improvements purchased", $"{player.PlayerName} built 1 improvement on {targets.Count} color set properties for ${totalCost}." );
		TryAutosaveStablePoint( "Color set improvements purchased" );
		return true;
	}

	private bool TrySellColorSetImprovement( int playerIndex, int sourceSpaceIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var properties = GetManageableColorSetProperties( playerIndex, sourceSpaceIndex );
		if ( player is null || properties.Count == 0 )
			return false;

		var targets = properties.Where( property => CanSellImprovement( playerIndex, property.Index ) ).ToList();
		if ( targets.Count == 0 )
			return false;

		var totalRefund = 0;
		foreach ( var property in targets )
		{
			var refund = GetImprovementSellValue( property.Index );
			var count = GetImprovementCount( property.Index );
			totalRefund += refund;
			player.Money += refund;

			if ( count <= 1 )
				PropertyImprovements.Remove( property.Index );
			else
				PropertyImprovements[property.Index] = count - 1;
		}

		TrySettlePendingForcedPaymentForPlayer( playerIndex );
		SendTableChatMessage( "Improvements sold", $"{player.PlayerName} sold 1 improvement from {targets.Count} color set properties for ${totalRefund}." );
		TryAutosaveStablePoint( "Color set improvements sold" );
		return true;
	}

	private bool TryMortgageColorSet( int playerIndex, int sourceSpaceIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var properties = GetManageableColorSetProperties( playerIndex, sourceSpaceIndex );
		if ( player is null || properties.Count == 0 )
			return false;

		var targets = properties.Where( property => CanMortgageProperty( playerIndex, property.Index ) ).ToList();
		if ( targets.Count == 0 )
			return false;

		var totalValue = 0;
		foreach ( var property in targets )
		{
			var value = GetMortgageValue( property.Index );
			totalValue += value;
			player.Money += value;
			MortgagedProperties[property.Index] = true;
		}

		TrySettlePendingForcedPaymentForPlayer( playerIndex );
		SendTableChatMessage( "Properties mortgaged", $"{player.PlayerName} mortgaged {targets.Count} color set properties for ${totalValue}." );
		TryAutosaveStablePoint( "Color set mortgaged" );
		return true;
	}

	private List<SpaceDef> GetManageableColorSetProperties( int playerIndex, int sourceSpaceIndex )
	{
		var source = Board?.GetSpaceDef( sourceSpaceIndex );
		if ( source is null || source.Type != SpaceType.Property || source.ColorGroup == ColorGroup.None )
			return new();

		return GetColorGroupProperties( source.ColorGroup )
			.Where( property => GetOwnerIndexForSpace( property.Index ) == playerIndex )
			.ToList();
	}
}
