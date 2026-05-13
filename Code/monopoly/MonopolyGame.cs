using System.Threading.Tasks;
using System;
using Sandbox;

public enum MonopolyGamePhase
{
	WaitingToRoll,
	ResolvingSpace,
	WaitingForBuyDecision,
	TurnEnded
}

public sealed class MonopolyGame : Component
{
	[Property] public List<MonopolyPlayerState> Players { get; set; } = new();

	[Property, Sync] public int CurrentPlayerIndex { get; set; }
	[Property, Sync] public int LastDieA { get; set; }
	[Property, Sync] public int LastDieB { get; set; }
	[Property, Sync] public NetDictionary<int, int> PropertyOwners { get; set; } = new();
	[Property, Sync] public NetDictionary<int, string> PendingTrades { get; set; } = new();
	[Property] public MonopolyBoard Board { get; set; }

	public MonopolyPlayerState CurrentPlayer =>
		Players.Count == 0 ? null : Players[CurrentPlayerIndex];

	[Property, Sync] public MonopolyGamePhase Phase { get; set; } = MonopolyGamePhase.WaitingToRoll;
	[Property, Sync] public int PendingPurchaseSpaceIndex { get; set; } = -1;
	[Property, Sync] public int NextTradeId { get; set; } = 1;

	private static MonopolyGame instance;

	public static MonopolyGame Instance => instance;

	public int LocalSelectedSpaceIndex { get; set; } = -1;

	public MonopolySpaceDef SelectedSpace =>
		LocalSelectedSpaceIndex >= 0 && LocalSelectedSpaceIndex < Board.Spaces.Count
			? Board.GetSpaceDef(LocalSelectedSpaceIndex)
			: null;

	private void SelectSpaceAsync( int spaceIndex ) => LocalSelectedSpaceIndex = spaceIndex;

	public void SelectSpace( int spaceIndex )
	{
		if ( spaceIndex == LocalSelectedSpaceIndex )
			spaceIndex = -1;

		SelectSpaceAsync(spaceIndex);
	}

	protected override void OnStart()
	{
		instance = this;

		if ( !Networking.IsHost )
			return;

		foreach ( var connection in Connection.All )
		{
			RegisterPlayer( connection );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var connection in Connection.All )
		{
			if ( GetPlayerForConnection( connection ) is null )
			{
				RegisterPlayer( connection );
			}
		}

		RemoveInvalidTrades();
	}

	public MonopolyPlayerState LocalPlayer => Players.FirstOrDefault( p => p.OwnerId == Connection.Local.SteamId );

	public int LocalPlayerIndex => Players.IndexOf( LocalPlayer );

	public int GetOwnerIndexForSpace( int spaceIndex )
	{
		if ( PropertyOwners.TryGetValue( spaceIndex, out var ownerIndex ) )
			return ownerIndex;

		return -1;
	}

	public int GetPlayerIndex( MonopolyPlayerState player )
	{
		return Players.IndexOf( player );
	}

	private MonopolyPlayerState GetPlayerForConnection( Connection connection )
	{
		if ( connection is null )
			return null;

		return Players.FirstOrDefault( x => x.OwnerId == connection.SteamId );
	}

	private Connection GetConnectionForPlayer( MonopolyPlayerState player )
	{
		return Connection.All.FirstOrDefault( c => c.SteamId == player.OwnerId );
	}

	private int GetPlayerIndexForCaller( Connection caller )
	{
		var player = GetPlayerForConnection( caller );
		if ( player is null )
			return -1;

		return GetPlayerIndex( player );
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
		emptySlot.Money = 1500;
		emptySlot.SpaceIndex = 0;
		emptySlot.IsInJail = false;

		Log.Info( $"Assigned {connection.DisplayName} to Monopoly player slot {Players.IndexOf( emptySlot )}" );
	}

	[Button( "Roll Dice" )]
	public async Task RollDiceAsync()
	{
		if ( !Networking.IsHost )
			return;

		if ( CurrentPlayer is null )
			return;

		if ( Phase != MonopolyGamePhase.WaitingToRoll )
			return;

		if ( CurrentPlayer is null || !CurrentPlayer.IsAssigned )
		{
			AdvanceTurn();
			return;
		}

		Phase = MonopolyGamePhase.ResolvingSpace;

		// move player...

		LastDieA = Game.Random.Int( 1, 6 );
		LastDieB = Game.Random.Int( 1, 6 );

		var total = LastDieA + LastDieB;

		//var SpaceIndex = (CurrentPlayer.SpaceIndex + total) % 40;
		//CurrentPlayer.SpaceIndex = SpaceIndex;
		await MovePlayerSteps( CurrentPlayer, total );

		Log.Info( $"Player {CurrentPlayerIndex + 1} rolled {LastDieA} + {LastDieB} = {total}" );

		if ( Phase == MonopolyGamePhase.ResolvingSpace )
		{
			AdvanceTurn();
			Phase = MonopolyGamePhase.WaitingToRoll;
		}
	}

	private async Task MovePlayerSteps(MonopolyPlayerState player, int steps)
	{
		for ( int i = 0; i < steps; i++ )
		{
			player.SpaceIndex =
				(player.SpaceIndex + 1) % 40;

			await Task.DelaySeconds( 0.4f );

			if ( player.SpaceIndex == 0 )
			{
				player.Money += 200;
			}
		}

		ResolveLanding( player );
	}

	private void ResolveLanding( MonopolyPlayerState player )
	{
		if ( player is null || Board is null )
			return;

		var space = Board.GetSpace( player.SpaceIndex );
		MonopolySpaceDef spaceDef = Board.GetSpaceDef(player.SpaceIndex);

		if ( space is null || spaceDef is null )
			return;

		Log.Info( $"{player.PlayerName} landed on {spaceDef.DisplayName}" );

		//if (spaceDef.Type != SpaceType.Go && spaceDef.Type )
		ShowCardForPlayerWhoLanded(player);

		switch ( spaceDef.Type )
		{
			case SpaceType.Go:
				player.Money += 200;
				Log.Info( $"{player.PlayerName} collected $200." );
				break;

			case SpaceType.Tax:
				player.Money -= spaceDef.TaxAmount;
				Log.Info( $"{player.PlayerName} paid ${spaceDef.TaxAmount} tax." );
				break;

			case SpaceType.GoToJail:
				SendPlayerToJail( player );
				break;

			case SpaceType.Property:
			case SpaceType.Railroad:
			case SpaceType.Utility:
				ResolvePropertyLanding( player, spaceDef );
				break;

			case SpaceType.Chance:
				Log.Info( $"{player.PlayerName} drew a Chance card." );
				break;

			case SpaceType.CommunityChest:
				Log.Info( $"{player.PlayerName} drew a Community Chest card." );
				break;

			case SpaceType.Jail:
				Log.Info( $"{player.PlayerName} is just visiting Jail." );
				break;

			case SpaceType.FreeParking:
				Log.Info( $"{player.PlayerName} landed on Free Parking." );
				break;
		}
	}

	private void ResolvePropertyLanding( MonopolyPlayerState player, MonopolySpaceDef def )
	{
		if (!PropertyOwners.ContainsKey(def.Index))
		{
			PendingPurchaseSpaceIndex = def.Index;
			Phase = MonopolyGamePhase.WaitingForBuyDecision;

			Log.Info( $"{player.PlayerName} can buy {def.DisplayName} for ${def.Price}." );
			return;
		}

		if ( PropertyOwners.TryGetValue( def.Index, out var ownerIndex ) )
		{
			var owner = Players.ElementAtOrDefault( ownerIndex );

			if ( owner is null || owner == player )
				return;

			player.Money -= def.BaseRent;
			owner.Money += def.BaseRent;

			Log.Info( $"{player.PlayerName} paid ${def.BaseRent} rent to {owner.PlayerName}." );
			return;
		}
	}

	private void ShowCardForPlayerWhoLanded( MonopolyPlayerState player )
	{
		var spaceIndex = player.SpaceIndex;
		var connection = GetConnectionForPlayer( player );

		if ( connection is null )
			return;

		using ( Rpc.FilterInclude( connection ) )
		{
			ShowLandedSpaceCard( spaceIndex );
		}
	}

	[Rpc.Broadcast]
	private void ShowLandedSpaceCard( int spaceIndex )
	{
		LocalSelectedSpaceIndex = spaceIndex;
	}

	[Button( "Buy Pending Property" )]
	public void BuyPendingProperty()
	{
		if ( !Networking.IsHost )
			return;

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		var player = CurrentPlayer;
		var def = Board.GetSpaceDef( PendingPurchaseSpaceIndex );

		if ( player is null || def is null )
			return;

		if ( player.Money >= def.Price )
		{
			PropertyOwners[def.Index] = CurrentPlayerIndex;
			player.Money -= def.Price;

			Log.Info( $"{player.PlayerName} bought {def.DisplayName} for ${def.Price}." );
		}

		PendingPurchaseSpaceIndex = -1;
		AdvanceTurn();
		Phase = MonopolyGamePhase.WaitingToRoll;
	}

	[Button( "Skip Pending Property" )]
	public void SkipPendingProperty()
	{
		if ( !Networking.IsHost )
			return;

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		Log.Info( $"{CurrentPlayer?.PlayerName} skipped buying." );

		PendingPurchaseSpaceIndex = -1;
		AdvanceTurn();
		Phase = MonopolyGamePhase.WaitingToRoll;
	}

	private bool CanCurrentPlayerAct( Connection caller )
	{
		if ( caller == null )
			return false;

		var currentPlayer = CurrentPlayer;

		if ( currentPlayer == null )
			return false;

		// Host can always act during local testing
		if ( Networking.IsHost && caller == Connection.Local )
			return true;

		return currentPlayer.OwnerId == caller.SteamId;
	}

	[Rpc.Host]
	public void RequestRollDice()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;
		_ = RollDiceAsync();
	}

	[Rpc.Host]
	public void RequestBuyPendingProperty()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;
		BuyPendingProperty();
	}

	[Rpc.Host]
	public void RequestSkipPendingProperty()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;
		SkipPendingProperty();
	}

	[Rpc.Host]
	public void RequestCreateTrade( int receiverPlayerIndex, int senderMoney, int receiverMoney, string senderPropertyIndexes, string receiverPropertyIndexes )
	{
		var senderPlayerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( senderPlayerIndex < 0 )
			return;

		var request = new MonopolyTradeRequest
		{
			Id = NextTradeId++,
			SenderPlayerIndex = senderPlayerIndex,
			ReceiverPlayerIndex = receiverPlayerIndex,
			SenderMoney = Math.Max( senderMoney, 0 ),
			ReceiverMoney = Math.Max( receiverMoney, 0 ),
			SenderPropertyIndexes = ParseSpaceIndexList( senderPropertyIndexes ),
			ReceiverPropertyIndexes = ParseSpaceIndexList( receiverPropertyIndexes )
		};

		if ( request.IsEmpty || !IsTradeValid( request ) )
			return;

		PendingTrades[request.Id] = request.Serialize();
		Log.Info( $"{Players[senderPlayerIndex].PlayerName} offered a trade to {Players[receiverPlayerIndex].PlayerName}." );
	}

	[Rpc.Host]
	public void RequestAcceptTrade( int tradeId )
	{
		if ( !TryGetTrade( tradeId, out var trade ) )
			return;

		if ( GetPlayerIndexForCaller( Rpc.Caller ) != trade.ReceiverPlayerIndex )
			return;

		if ( !IsTradeValid( trade ) )
		{
			PendingTrades.Remove( tradeId );
			return;
		}

		var sender = Players[trade.SenderPlayerIndex];
		var receiver = Players[trade.ReceiverPlayerIndex];

		sender.Money -= trade.SenderMoney;
		receiver.Money += trade.SenderMoney;

		receiver.Money -= trade.ReceiverMoney;
		sender.Money += trade.ReceiverMoney;

		foreach ( var spaceIndex in trade.SenderPropertyIndexes )
			PropertyOwners[spaceIndex] = trade.ReceiverPlayerIndex;

		foreach ( var spaceIndex in trade.ReceiverPropertyIndexes )
			PropertyOwners[spaceIndex] = trade.SenderPlayerIndex;

		PendingTrades.Remove( tradeId );
		RemoveInvalidTrades();

		Log.Info( $"{receiver.PlayerName} accepted a trade from {sender.PlayerName}." );
	}

	[Rpc.Host]
	public void RequestDenyTrade( int tradeId )
	{
		if ( !TryGetTrade( tradeId, out var trade ) )
			return;

		var callerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( callerIndex != trade.ReceiverPlayerIndex && callerIndex != trade.SenderPlayerIndex )
			return;

		PendingTrades.Remove( tradeId );
	}

	private void SendPlayerToJail( MonopolyPlayerState player )
	{
		player.SpaceIndex = 10;
		player.IsInJail = true;

		Log.Info( $"{player.PlayerName} was sent to Jail." );
	}

	private void AdvanceTurn()
	{
		if ( Players.Count == 0 )
			return;

		for ( int i = 0; i < Players.Count; i++ )
		{
			CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;

			if ( Players[CurrentPlayerIndex].IsAssigned )
				return;
		}
	}

	public List<MonopolyTradeRequest> GetTrades()
	{
		return PendingTrades
			.Select( entry => MonopolyTradeRequest.TryDeserialize( entry.Key, entry.Value, out var trade ) ? trade : null )
			.Where( trade => trade is not null )
			.OrderBy( trade => trade.Id )
			.ToList();
	}

	public bool TryGetTrade( int tradeId, out MonopolyTradeRequest trade )
	{
		trade = null;

		if ( !PendingTrades.TryGetValue( tradeId, out var value ) )
			return false;

		return MonopolyTradeRequest.TryDeserialize( tradeId, value, out trade );
	}

	public List<int> GetOwnedPropertyIndexes( int playerIndex )
	{
		return PropertyOwners
			.Where( entry => entry.Value == playerIndex )
			.Select( entry => entry.Key )
			.OrderBy( index => index )
			.ToList();
	}

	public bool IsTradeValid( MonopolyTradeRequest trade )
	{
		if ( trade is null )
			return false;

		var sender = Players.ElementAtOrDefault( trade.SenderPlayerIndex );
		var receiver = Players.ElementAtOrDefault( trade.ReceiverPlayerIndex );

		if ( sender is null || receiver is null || !sender.IsAssigned || !receiver.IsAssigned )
			return false;

		if ( trade.SenderPlayerIndex == trade.ReceiverPlayerIndex )
			return false;

		if ( sender.Money < trade.SenderMoney || receiver.Money < trade.ReceiverMoney )
			return false;

		foreach ( var spaceIndex in trade.SenderPropertyIndexes )
		{
			if ( GetOwnerIndexForSpace( spaceIndex ) != trade.SenderPlayerIndex )
				return false;
		}

		foreach ( var spaceIndex in trade.ReceiverPropertyIndexes )
		{
			if ( GetOwnerIndexForSpace( spaceIndex ) != trade.ReceiverPlayerIndex )
				return false;
		}

		return true;
	}

	private void RemoveInvalidTrades()
	{
		foreach ( var trade in GetTrades() )
		{
			if ( !IsTradeValid( trade ) )
				PendingTrades.Remove( trade.Id );
		}
	}

	private static List<int> ParseSpaceIndexList( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return new();

		return value.Split( ',', StringSplitOptions.RemoveEmptyEntries )
			.Select( part => int.TryParse( part, out var index ) ? index : -1 )
			.Where( index => index >= 0 )
			.Distinct()
			.OrderBy( index => index )
			.ToList();
	}
}
