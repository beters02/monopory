using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	public int GetOwnerIndexForSpace( int spaceIndex )
	{
		if ( PropertyOwners.TryGetValue( spaceIndex, out var ownerIndex ) )
			return ownerIndex;

		return -1;
	}

	public bool CanBuyPendingProperty( MonopolyPlayerState player, int spaceIndex )
	{
		return Phase == MonopolyGamePhase.WaitingForBuyDecision &&
			CurrentPlayer == player &&
			PendingPurchaseSpaceIndex == spaceIndex;
	}

	public bool TryBuyPendingPropertyForPlayer( MonopolyPlayerState player, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can buy pending properties directly.";
			return false;
		}

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision || CurrentPlayer != player )
		{
			message = "That player does not have a pending buy decision.";
			return false;
		}

		var def = Board?.GetSpaceDef( PendingPurchaseSpaceIndex );
		if ( def is null )
		{
			message = "Pending property does not exist.";
			return false;
		}

		if ( player.Money < def.Price )
		{
			message = $"{player.PlayerName} cannot afford {def.DisplayName}.";
			return false;
		}

		BuyPendingProperty();
		message = $"{player.PlayerName} bought {def.DisplayName}.";
		return true;
	}

	public bool TryBuyPropertyForPlayer( MonopolyPlayerState player, int index, bool useMoney, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can buy properties directly.";
			return false;
		}

		if ( player is null )
		{
			message = "Player does not exist.";
			return false;
		}

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
		{
			message = "Player is not part of this game.";
			return false;
		}

		var def = Board?.GetSpaceDef( index );
		if ( def is null || !IsPurchasableSpace( def ) )
		{
			message = $"Space {index} is not purchasable.";
			return false;
		}

		if ( GetOwnerIndexForSpace( def.Index ) >= 0 )
		{
			message = $"{def.DisplayName} is already owned.";
			return false;
		}

		if (useMoney)
		{
			if (player.Money < def.Price)
			{
				message = $"{player.PlayerName} cannot afford {def.DisplayName}.";
				return false;
			}
			
			if ( !PayBank( player, def.Price ) )
			{
				message = $"{player.PlayerName} could not pay for {def.DisplayName}.";
				return false;
			}
		}
		
		PropertyOwners[def.Index] = playerIndex;
		Log.Info( $"{player.PlayerName} bought {def.DisplayName} for ${def.Price}." );
		message = $"{player.PlayerName} bought {def.DisplayName}.";
		return true;
	}

	public bool TryBuyPropertySetForPlayer( MonopolyPlayerState player, IReadOnlyList<MonopolySpaceDef> properties, bool useMoney, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can buy property sets directly.";
			return false;
		}

		if ( player is null )
		{
			message = "Player does not exist.";
			return false;
		}

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
		{
			message = "Player is not part of this game.";
			return false;
		}

		if ( properties is null || properties.Count == 0 )
		{
			message = "Property set has no purchasable properties.";
			return false;
		}

		var propertiesToBuy = new List<MonopolySpaceDef>();
		foreach ( var def in properties )
		{
			if ( def is null || !IsPurchasableSpace( def ) )
			{
				message = $"Space {def?.Index ?? -1} is not purchasable.";
				return false;
			}

			var ownerIndex = GetOwnerIndexForSpace( def.Index );
			if ( ownerIndex == playerIndex )
				continue;

			if ( ownerIndex >= 0 )
			{
				message = $"{def.DisplayName} is already owned by another player.";
				return false;
			}

			propertiesToBuy.Add( def );
		}

		if ( propertiesToBuy.Count == 0 )
		{
			message = $"{player.PlayerName} already owns that property set.";
			return true;
		}

		var totalPrice = propertiesToBuy.Sum( def => def.Price );
		if ( useMoney && player.Money < totalPrice )
		{
			message = $"{player.PlayerName} cannot afford that property set. Needs ${totalPrice}, has ${player.Money}.";
			return false;
		}

		if ( useMoney && !PayBank( player, totalPrice ) )
		{
			message = $"{player.PlayerName} could not pay for that property set.";
			return false;
		}

		foreach ( var def in propertiesToBuy )
		{
			PropertyOwners[def.Index] = playerIndex;
			Log.Info( $"{player.PlayerName} bought {def.DisplayName} for ${def.Price}." );
		}

		message = $"{player.PlayerName} bought {propertiesToBuy.Count} properties for ${totalPrice}.";
		return true;
	}

	[Button( "Buy Pending Property" )]
	public void BuyPendingProperty()
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		var player = CurrentPlayer;
		var def = Board.GetSpaceDef( PendingPurchaseSpaceIndex );

		if ( player is null || def is null )
			return;

		if ( player.Money >= def.Price )
		{
			BuyUnownedPropertyForPlayer( player, def, CurrentPlayerIndex );
		}

		PendingPurchaseSpaceIndex = -1;
		Phase = MonopolyGamePhase.TurnEnded;
	}

	[Button( "Skip Pending Property" )]
	public void SkipPendingProperty()
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		Log.Info( $"{CurrentPlayer?.PlayerName} skipped buying." );

		PendingPurchaseSpaceIndex = -1;
		Phase = MonopolyGamePhase.TurnEnded;
	}

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

	private void BuyUnownedPropertyForPlayer( MonopolyPlayerState player, MonopolySpaceDef def, int ownerIndex )
	{
		if ( !PayBank( player, def.Price ) )
			return;

		PropertyOwners[def.Index] = ownerIndex;

		Log.Info( $"{player.PlayerName} bought {def.DisplayName} for ${def.Price}." );
	}

	public List<int> GetOwnedPropertyIndexes( int playerIndex )
	{
		return PropertyOwners
			.Where( entry => entry.Value == playerIndex )
			.Select( entry => entry.Key )
			.OrderBy( index => index )
			.ToList();
	}

	public bool IsMortgaged( int spaceIndex )
	{
		return MortgagedProperties.TryGetValue( spaceIndex, out var isMortgaged ) && isMortgaged;
	}

	private static bool IsPurchasableSpace( MonopolySpaceDef def )
	{
		return def is not null &&
			def.Price > 0 &&
			(def.Type == SpaceType.Property || def.Type == SpaceType.Railroad || def.Type == SpaceType.Utility);
	}

	public int GetImprovementCount( int spaceIndex )
	{
		if ( PropertyImprovements.TryGetValue( spaceIndex, out var count ) )
			return Math.Clamp( count, 0, 5 );

		return 0;
	}

	public int GetImprovementCost( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		var colorGroup = def?.ColorGroup ?? MonopolyColorGroup.None;

		return colorGroup switch
		{
			MonopolyColorGroup.Brown or MonopolyColorGroup.LightBlue => 50,
			MonopolyColorGroup.Pink or MonopolyColorGroup.Orange => 100,
			MonopolyColorGroup.Red or MonopolyColorGroup.Yellow => 150,
			MonopolyColorGroup.Green or MonopolyColorGroup.DarkBlue => 200,
			_ => 0
		};
	}

	public int GetImprovementSellValue( int spaceIndex ) => GetImprovementCost( spaceIndex ) / 2;

	public int GetMortgageValue( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		return def is null ? 0 : def.Price / 2;
	}

	public int GetUnmortgageCost( int spaceIndex )
	{
		var mortgageValue = GetMortgageValue( spaceIndex );
		return mortgageValue + (int)MathF.Ceiling( mortgageValue * 0.1f );
	}

	public int GetRentForSpace( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		if ( def is null )
			return 0;

		if ( IsMortgaged( spaceIndex ) )
			return 0;

		return GetImprovementCount( spaceIndex ) switch
		{
			1 => def.OneHouseRent,
			2 => def.TwoHouseRent,
			3 => def.ThreeHouseRent,
			4 => def.FourHouseRent,
			5 => def.HotelRent,
			_ => def.BaseRent
		};
	}

	public bool CanBuildImprovement( int playerIndex, int spaceIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var def = Board?.GetSpaceDef( spaceIndex );

		if ( player is null || def is null || def.Type != SpaceType.Property )
			return false;

		if ( !CanPlayerManageProperties( playerIndex ) )
			return false;

		if ( HasPendingForcedPaymentForPlayer( playerIndex ) )
			return false;

		if ( GetOwnerIndexForSpace( spaceIndex ) != playerIndex )
			return false;

		if ( IsMortgaged( spaceIndex ) )
			return false;

		var cost = GetImprovementCost( spaceIndex );
		if ( cost <= 0 || player.Money < cost )
			return false;

		if ( GetImprovementCount( spaceIndex ) >= 5 )
			return false;

		if ( !OwnsColorGroup( playerIndex, def.ColorGroup ) )
			return false;

		return Config?.EvenBuild != true || CanAddEvenly( spaceIndex );
	}

	public bool CanSellImprovement( int playerIndex, int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );

		if ( def is null || def.Type != SpaceType.Property )
			return false;

		if ( !CanPlayerManageProperties( playerIndex ) )
			return false;

		if ( GetOwnerIndexForSpace( spaceIndex ) != playerIndex )
			return false;

		if ( IsMortgaged( spaceIndex ) )
			return false;

		if ( GetImprovementCount( spaceIndex ) <= 0 )
			return false;

		if ( !OwnsColorGroup( playerIndex, def.ColorGroup ) )
			return false;

		return Config?.EvenBuild != true || CanRemoveEvenly( spaceIndex );
	}

	public bool CanMortgageProperty( int playerIndex, int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		var player = Players.ElementAtOrDefault( playerIndex );

		if ( player is null || def is null || !IsPurchasableSpace( def ) )
			return false;

		if ( !CanPlayerManageProperties( playerIndex ) )
			return false;

		if ( GetOwnerIndexForSpace( spaceIndex ) != playerIndex )
			return false;

		if ( IsMortgaged( spaceIndex ) )
			return false;

		if ( GetImprovementCount( spaceIndex ) > 0 )
			return false;

		if ( def.Type == SpaceType.Property && ColorGroupHasImprovements( def.ColorGroup ) )
			return false;

		return GetMortgageValue( spaceIndex ) > 0;
	}

	public bool CanUnmortgageProperty( int playerIndex, int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		var player = Players.ElementAtOrDefault( playerIndex );

		if ( player is null || def is null || !IsPurchasableSpace( def ) )
			return false;

		if ( !CanPlayerManageProperties( playerIndex ) )
			return false;

		if ( HasPendingForcedPaymentForPlayer( playerIndex ) )
			return false;

		if ( GetOwnerIndexForSpace( spaceIndex ) != playerIndex )
			return false;

		if ( !IsMortgaged( spaceIndex ) )
			return false;

		return player.Money >= GetUnmortgageCost( spaceIndex );
	}

	private bool CanPlayerManageProperties( int playerIndex )
	{
		return playerIndex >= 0 &&
			CanAcceptGameplayInput() &&
			CurrentPlayerIndex == playerIndex &&
			Players.ElementAtOrDefault( playerIndex )?.IsBankrupt != true &&
			(Phase == MonopolyGamePhase.WaitingToRoll || Phase == MonopolyGamePhase.TurnEnded);
	}

	private bool HasPendingForcedPaymentForPlayer( int playerIndex )
	{
		return HasPendingForcedPayment && PendingForcedPaymentPlayerIndex == playerIndex;
	}

	private bool OwnsColorGroup( int playerIndex, MonopolyColorGroup colorGroup )
	{
		if ( colorGroup == MonopolyColorGroup.None || Board?.SpaceDefs is null )
			return false;

		var group = GetColorGroupProperties( colorGroup );
		return group.Count > 0 && group.All( def => GetOwnerIndexForSpace( def.Index ) == playerIndex );
	}

	private List<MonopolySpaceDef> GetColorGroupProperties( MonopolyColorGroup colorGroup )
	{
		return Board?.SpaceDefs?
			.Where( def => def is not null && def.Type == SpaceType.Property && def.ColorGroup == colorGroup )
			.OrderBy( def => def.Index )
			.ToList() ?? new();
	}

	private bool ColorGroupHasImprovements( MonopolyColorGroup colorGroup )
	{
		if ( colorGroup == MonopolyColorGroup.None )
			return false;

		return GetColorGroupProperties( colorGroup )
			.Any( property => GetImprovementCount( property.Index ) > 0 );
	}

	private bool CanAddEvenly( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		var group = def is null ? new List<MonopolySpaceDef>() : GetColorGroupProperties( def.ColorGroup );
		var current = GetImprovementCount( spaceIndex );
		var min = group.Count == 0 ? 0 : group.Min( property => GetImprovementCount( property.Index ) );

		return current <= min;
	}

	private bool CanRemoveEvenly( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		var group = def is null ? new List<MonopolySpaceDef>() : GetColorGroupProperties( def.ColorGroup );
		var current = GetImprovementCount( spaceIndex );
		var max = group.Count == 0 ? 0 : group.Max( property => GetImprovementCount( property.Index ) );

		return current >= max;
	}
}
