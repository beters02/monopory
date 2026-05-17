using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	public int GetOwnerIndexForSpace( int spaceIndex )
	{
		if ( PropertyOwners.TryGetValue( spaceIndex, out var ownerIndex ) )
			return ownerIndex;

		return -1;
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

	private static bool IsPurchasableSpace( SpaceDef def )
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
		var colorGroup = def?.ColorGroup ?? ColorGroup.None;

		return colorGroup switch
		{
			ColorGroup.Brown or ColorGroup.LightBlue => 50,
			ColorGroup.Pink or ColorGroup.Orange => 100,
			ColorGroup.Red or ColorGroup.Yellow => 150,
			ColorGroup.Green or ColorGroup.DarkBlue => 200,
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

		if ( ColorGroupHasMortgages( def.ColorGroup ) )
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
			(Phase == GamePhase.WaitingToRoll || Phase == GamePhase.TurnEnded);
	}

	private bool HasPendingForcedPaymentForPlayer( int playerIndex )
	{
		return HasPendingForcedPayment && PendingForcedPaymentPlayerIndex == playerIndex;
	}

	private bool OwnsColorGroup( int playerIndex, ColorGroup colorGroup )
	{
		if ( colorGroup == ColorGroup.None || Board?.SpaceDefs is null )
			return false;

		var group = GetColorGroupProperties( colorGroup );
		return group.Count > 0 && group.All( def => GetOwnerIndexForSpace( def.Index ) == playerIndex );
	}

	private List<SpaceDef> GetColorGroupProperties( ColorGroup colorGroup )
	{
		return Board?.SpaceDefs?
			.Where( def => def is not null && def.Type == SpaceType.Property && def.ColorGroup == colorGroup )
			.OrderBy( def => def.Index )
			.ToList() ?? new();
	}

	private bool ColorGroupHasImprovements( ColorGroup colorGroup )
	{
		if ( colorGroup == ColorGroup.None )
			return false;

		return GetColorGroupProperties( colorGroup )
			.Any( property => GetImprovementCount( property.Index ) > 0 );
	}

	private bool ColorGroupHasMortgages( ColorGroup colorGroup )
	{
		if ( colorGroup == ColorGroup.None )
			return false;

		return GetColorGroupProperties( colorGroup )
			.Any( property => IsMortgaged( property.Index ) );
	}

	private bool CanAddEvenly( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		var group = def is null ? new List<SpaceDef>() : GetColorGroupProperties( def.ColorGroup );
		var current = GetImprovementCount( spaceIndex );
		var min = group.Count == 0 ? 0 : group.Min( property => GetImprovementCount( property.Index ) );

		return current <= min;
	}

	private bool CanRemoveEvenly( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		var group = def is null ? new List<SpaceDef>() : GetColorGroupProperties( def.ColorGroup );
		var current = GetImprovementCount( spaceIndex );
		var max = group.Count == 0 ? 0 : group.Max( property => GetImprovementCount( property.Index ) );

		return current >= max;
	}
}
