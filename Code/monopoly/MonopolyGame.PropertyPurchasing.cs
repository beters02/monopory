using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	public bool CanBuyPendingProperty( PlayerState player, int spaceIndex )
	{
		return Phase == MonopolyGamePhase.WaitingForBuyDecision &&
			CurrentPlayer == player &&
			PendingPurchaseSpaceIndex == spaceIndex;
	}

	public bool TryBuyPendingPropertyForPlayer( PlayerState player, out string message )
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

	public bool TryBuyPropertyForPlayer( PlayerState player, int index, bool useMoney, out string message )
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

	public bool TryBuyPropertySetForPlayer( PlayerState player, IReadOnlyList<SpaceDef> properties, bool useMoney, out string message )
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

		var propertiesToBuy = new List<SpaceDef>();
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

	private void BuyUnownedPropertyForPlayer( PlayerState player, SpaceDef def, int ownerIndex )
	{
		if ( !PayBank( player, def.Price ) )
			return;

		PropertyOwners[def.Index] = ownerIndex;

		Log.Info( $"{player.PlayerName} bought {def.DisplayName} for ${def.Price}." );
	}
}
