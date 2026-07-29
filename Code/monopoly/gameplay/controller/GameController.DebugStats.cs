using System;
using Sandbox;

public sealed partial class GameController : Component
{
	private readonly record struct DebugStatsPlayerEntry( PlayerState Player, int PlayerIndex );

	private const int DebugLeaderboardPlayerCount = 8;
	private static readonly string[] DebugLeaderboardPlayerNames =
	{
		"Mara Voss",
		"Theodore Longname-Smythe",
		"Juno",
		"Cash Moneyton",
		"Priya Patel",
		"Noah North",
		"Bankrupt Betty",
		"Zero Rent"
	};
	private static readonly int[] DebugLeaderboardDurations =
	{
		9942,
		8621,
		7345,
		5912,
		4080,
		2715,
		973,
		257
	};

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
		PropertyRentPaid.Clear();
		PropertyOwnershipHistory.Clear();
		PlayerFinishPlacements.Clear();
		PlayerTimeLastedSeconds.Clear();

		ApplyDebugPlayerTotals( assignedPlayers );
		ApplyDebugPropertyStats( assignedPlayers, purchasableSpaces );

		message = $"Applied stats modal debug data for {assignedPlayers.Count} players and {purchasableSpaces.Count} properties.";
		return true;
	}

	public bool TryApplyDebugLeaderboardData( out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can apply leaderboard debug data.";
			return false;
		}

		EnsurePlayerSlots();

		var availableSlots = Players
			.Where( player => player is not null )
			.ToList();
		var debugPlayers = availableSlots
			.Take( DebugLeaderboardPlayerCount )
			.ToList();

		if ( debugPlayers.Count == 0 )
		{
			message = "No player slots are available for leaderboard debug data.";
			return false;
		}

		var hasPurchasableSpace = Board?.SpaceDefs?.Any( IsPurchasableSpace ) == true;
		if ( !hasPurchasableSpace )
		{
			message = "No purchasable spaces are available for leaderboard debug data.";
			return false;
		}

		var existingOwnerId = availableSlots.FirstOrDefault( player => player.IsAssigned )?.OwnerId ?? 0;
		long debugOwnerId = Connection.Local?.SteamId ?? existingOwnerId;
		if ( debugOwnerId == 0 )
			debugOwnerId = 1;

		for ( var i = 0; i < debugPlayers.Count; i++ )
		{
			var player = debugPlayers[i];
			ResetPlayerForGame( player );
			player.OwnerId = debugOwnerId;
			player.SteamId = debugOwnerId;
			player.PlayerName = DebugLeaderboardPlayerNames[i % DebugLeaderboardPlayerNames.Length];
			player.ColorSlot = i;
			player.IsReady = true;
			player.IsDisconnected = false;
			player.AbandonEndsAt = 0f;
		}

		foreach ( var player in availableSlots.Skip( debugPlayers.Count ) )
			ClearPlayerSlot( player );

		if ( !TryApplyDebugStatsModalData( out message ) )
			return false;

		var entries = debugPlayers
			.Select( player => new DebugStatsPlayerEntry( player, Players.IndexOf( player ) ) )
			.Where( entry => entry.PlayerIndex >= 0 )
			.ToList();
		ApplyDebugLeaderboardVariations( entries );

		message = $"Filled the leaderboard with {entries.Count} artificial players. Open Stats to preview it.";
		return true;
	}

	private void ApplyDebugLeaderboardVariations( List<DebugStatsPlayerEntry> players )
	{
		for ( var i = 0; i < players.Count; i++ )
		{
			var entry = players[i];
			PlayerFinishPlacements[entry.PlayerIndex] = i + 1;
			PlayerTimeLastedSeconds[entry.PlayerIndex] = DebugLeaderboardDurations[i % DebugLeaderboardDurations.Length];
		}

		var bankruptCount = Math.Min( 2, Math.Max( players.Count - 1, 0 ) );
		for ( var i = players.Count - bankruptCount; i < players.Count; i++ )
			players[i].Player.IsBankrupt = true;

		if ( MatchState == MatchLifecycleState.GameOver && players.Count > 0 )
			WinnerPlayerIndex = players[0].PlayerIndex;

		if ( players.Count < 3 )
			return;

		var zeroStatsPlayerIndex = players[^1].PlayerIndex;
		var fallbackOwnerIndex = players[0].PlayerIndex;

		foreach ( var spaceIndex in PropertyOwners
			.Where( entry => entry.Value == zeroStatsPlayerIndex )
			.Select( entry => entry.Key )
			.ToList() )
		{
			PropertyOwners[spaceIndex] = fallbackOwnerIndex;
			RecordPropertyOwnershipForStats( fallbackOwnerIndex, spaceIndex );
		}

		foreach ( var key in PropertyOwnershipHistory.Keys
			.Where( key => GetPropertyStatPlayerIndex( key ) == zeroStatsPlayerIndex )
			.ToList() )
		{
			PropertyOwnershipHistory.Remove( key );
		}

		foreach ( var key in PropertyRentEarned.Keys
			.Where( key => GetPropertyStatPlayerIndex( key ) == zeroStatsPlayerIndex )
			.ToList() )
		{
			PropertyRentEarned.Remove( key );
		}

		foreach ( var key in PropertyRentPaid.Keys
			.Where( key => GetPropertyStatPlayerIndex( key ) == zeroStatsPlayerIndex )
			.ToList() )
		{
			PropertyRentPaid.Remove( key );
		}
	}

	private static int GetPropertyStatPlayerIndex( int key )
	{
		return key / 1000;
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
			PlayerFinishPlacements[assignedPlayers[i].PlayerIndex] = i + 1;
			PlayerTimeLastedSeconds[assignedPlayers[i].PlayerIndex] = Math.Max( 1800 - (i * 240), 60 );
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
			RecordPropertyOwnershipForStats( ownerIndex, space.Index );
			if ( assignedPlayers.Count > 1 && i % 4 == 0 )
			{
				var previousOwnerIndex = assignedPlayers[(i + 1) % assignedPlayers.Count].PlayerIndex;
				RecordPropertyOwnershipForStats( previousOwnerIndex, space.Index );
			}

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
			var payerIndex = assignedPlayers[(i + 1) % assignedPlayers.Count].PlayerIndex;
			PropertyRentPaid[BuildPropertyStatKey( payerIndex, space.Index )] = rentEarned;
		}
	}
}
