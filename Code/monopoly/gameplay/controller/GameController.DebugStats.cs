using System;
using Sandbox;

public sealed partial class GameController : Component
{
	private readonly record struct DebugStatsPlayerEntry( PlayerState Player, int PlayerIndex );

	public bool TryApplyDebugStatsModalData( out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can apply stats modal debug data.";
			return false;
		}

		var assignedPlayers = Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null && entry.player.IsAssigned )
			.Select( entry => new DebugStatsPlayerEntry( entry.player, entry.index ) )
			.ToList();

		if ( assignedPlayers.Count == 0 )
		{
			message = "No assigned players are available for stats modal debug data.";
			return false;
		}

		var purchasableSpaces = Board?.SpaceDefs?
			.Where( def => IsPurchasableSpace( def ) )
			.OrderBy( def => def.Index )
			.ToList() ?? new();

		if ( purchasableSpaces.Count == 0 )
		{
			message = "No purchasable spaces are available for stats modal debug data.";
			return false;
		}

		PropertyOwners.Clear();
		PropertyImprovements.Clear();
		MortgagedProperties.Clear();
		PropertyLandingCounts.Clear();
		PropertyRentEarned.Clear();

		ApplyDebugPlayerTotals( assignedPlayers );
		ApplyDebugPropertyStats( assignedPlayers, purchasableSpaces );

		message = $"Applied stats modal debug data for {assignedPlayers.Count} players and {purchasableSpaces.Count} properties.";
		return true;
	}

	private void ApplyDebugPlayerTotals( List<DebugStatsPlayerEntry> assignedPlayers )
	{
		var startingMoney = Math.Max( Config?.StartingMoney ?? 1500, 0 );
		var debugMoneyDeltas = new[] { -1065, 1550, -1500, -430, 820, -900, 340, -1225 };

		for ( var i = 0; i < assignedPlayers.Count; i++ )
		{
			var player = assignedPlayers[i].Player;
			if ( player is null )
				continue;

			var delta = debugMoneyDeltas[i % debugMoneyDeltas.Length] - (i / debugMoneyDeltas.Length * 175);
			player.Money = Math.Max( startingMoney + delta, 0 );
			player.IsBankrupt = false;
		}
	}

	private void ApplyDebugPropertyStats( List<DebugStatsPlayerEntry> assignedPlayers, List<SpaceDef> purchasableSpaces )
	{
		for ( var i = 0; i < purchasableSpaces.Count; i++ )
		{
			var space = purchasableSpaces[i];
			var ownerEntry = assignedPlayers[i % assignedPlayers.Count];
			var ownerIndex = ownerEntry.PlayerIndex;

			PropertyOwners[space.Index] = ownerIndex;

			if ( space.Type == SpaceType.Property && i % 3 != 0 )
				PropertyImprovements[space.Index] = Math.Clamp( (i % 5) + 1, 1, 5 );

			if ( i % 7 == 0 )
				MortgagedProperties[space.Index] = true;

			for ( var playerOrder = 0; playerOrder < assignedPlayers.Count; playerOrder++ )
			{
				var visitorIndex = assignedPlayers[playerOrder].PlayerIndex;
				var landings = ((i + 3) * (playerOrder + 2)) % 9;

				if ( i == 0 && playerOrder == 0 )
					landings = 18;
				else if ( i == 1 && playerOrder == Math.Min( 1, assignedPlayers.Count - 1 ) )
					landings = 14;
				else if ( i == 2 )
					landings += 6;

				if ( landings > 0 )
					PropertyLandingCounts[BuildPropertyStatKey( visitorIndex, space.Index )] = landings;
			}

			var rentEarned = ((i + 2) * 115) + ((i % 4) * 175);
			if ( i == 0 )
				rentEarned = 2400;
			else if ( i == 1 )
				rentEarned = 1850;
			else if ( i == 2 )
				rentEarned = 1625;

			PropertyRentEarned[BuildPropertyStatKey( ownerIndex, space.Index )] = rentEarned;
		}
	}
}
