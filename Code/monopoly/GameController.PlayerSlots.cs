using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private PlayerState GetPlayerForConnection( Connection connection )
	{
		if ( connection is null )
			return null;

		return Players.FirstOrDefault( x => x.OwnerId == connection.SteamId );
	}

	private Connection GetConnectionForPlayer( PlayerState player )
	{
		return Connection.All.FirstOrDefault( c => c.SteamId == player.OwnerId );
	}

	public List<PlayerState> GetLobbyPlayers()
	{
		return Players
			.Where( player => player is not null && player.IsAssigned )
			.Take( MaxPlayers )
			.ToList();
	}

	private List<PlayerState> GetAssignedPlayers()
	{
		return Players
			.Where( player => player is not null && player.IsAssigned )
			.ToList();
	}

	private void SyncLobbyConnections()
	{
		if ( !Networking.IsHost )
			return;

		if ( HasStarted && MatchState != MonopolyMatchState.GameOver )
			return;

		EnsurePlayerSlots();

		var availableSlotCount = Players.Count( player => player is not null );
		var registrationLimit = Math.Min( MaxPlayers, availableSlotCount );

		foreach ( var player in Players.Where( player => player is not null && player.IsAssigned ).ToList() )
		{
			if ( Connection.All.Any( connection => connection.SteamId == player.OwnerId ) )
				continue;

			ClearPlayerSlot( player );
		}

		foreach ( var connection in Connection.All )
		{
			if ( GetPlayerForConnection( connection ) is not null )
				continue;

			if ( GetAssignedPlayers().Count >= registrationLimit )
				continue;

			RegisterPlayer( connection );
		}
	}

	private void EnsurePlayerSlots()
	{
		var targetSlotCount = Math.Max( MaxPlayers, 1 );
		while ( Players.Count < targetSlotCount )
		{
			var slotNumber = Players.Count + 1;
			var player = CreatePlayerStateObject( slotNumber );
			player.PlayerName = $"Player {slotNumber}";
			Players.Add( player );
		}

		for ( var i = 0; i < Players.Count; i++ )
			EnsurePlayerStateObject( i );
	}

	private PlayerState CreatePlayerStateObject( int slotNumber )
	{
		var playerObject = new GameObject( true, $"PlayerState_{slotNumber:00}" );
		playerObject.SetParent( GameObject );
		return playerObject.Components.Create<PlayerState>();
	}

	private void EnsurePlayerStateObject( int playerIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var slotNumber = playerIndex + 1;

		if ( player is null || player.GameObject is null || !player.GameObject.IsValid() )
		{
			Players[playerIndex] = CreatePlayerStateObject( slotNumber );
			Players[playerIndex].PlayerName = $"Player {slotNumber}";
			return;
		}

		player.GameObject.Name = $"PlayerState_{slotNumber:00}";
		player.GameObject.SetParent( GameObject );
	}

	private void ClearPlayerSlot( PlayerState player )
	{
		if ( player is null )
			return;

		player.OwnerId = 0;
		player.PlayerName = "Player";
		player.IsReady = false;
		ResetPlayerForGame( player );
	}

	private void ResetPlayerForGame( PlayerState player )
	{
		if ( player is null )
			return;

		player.Money = Math.Max( Config?.StartingMoney ?? 1500, 0 );
		player.SpaceIndex = 0;
		player.IsInJail = false;
		player.ConsecutiveDoubles = 0;
		player.SkipsNextTurn = false;
		player.IsBankrupt = false;
	}

	private void ResetGameState( bool resetPlayers )
	{
		CurrentPlayerIndex = 0;
		LastDieA = 0;
		LastDieB = 0;
		Phase = MonopolyGamePhase.WaitingToRoll;
		PendingPurchaseSpaceIndex = -1;
		ClearAuction();
		NextTradeId = 1;
		FreeParkingBank = 0;
		CurrentTurnGetsExtraRoll = false;
		CurrentTurnEndsAt = 0f;
		WinnerPlayerIndex = -1;
		GameStartedAt = 0f;
		StartingPlayerCount = 0;
		pausedTurnRemainingSeconds = 0f;
		pausedAuctionRemainingSeconds = 0f;
		LocalSelectedDrawnCardText = "";
		ClearPendingForcedPayment();
		PropertyOwners.Clear();
		PropertyImprovements.Clear();
		MortgagedProperties.Clear();
		PendingTrades.Clear();

		foreach ( var player in Players )
		{
			if ( resetPlayers )
				ClearPlayerSlot( player );
			else
				ResetPlayerForGame( player );
		}
	}

	private void RegisterPlayer( Connection connection )
	{
		if ( connection is null )
			return;

		if ( GetPlayerForConnection( connection ) is not null )
			return;

		var emptySlot = Players.FirstOrDefault( x => !x.IsAssigned );

		if ( emptySlot is null )
		{
			Log.Warning( $"No available Monopoly player slot for {connection.DisplayName}" );
			return;
		}

		emptySlot.OwnerId = connection.SteamId;
		emptySlot.PlayerName = connection.DisplayName;
		emptySlot.IsReady = false;
		ResetPlayerForGame( emptySlot );

		Log.Info( $"Assigned {connection.DisplayName} to Monopoly player slot {Players.IndexOf( emptySlot )}" );
	}
}
