using System.Threading.Tasks;
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
	[Property] public MonopolyBoard Board { get; set; }

	public MonopolyPlayerState CurrentPlayer =>
		Players.Count == 0 ? null : Players[CurrentPlayerIndex];

	[Property, Sync] public MonopolyGamePhase Phase { get; set; } = MonopolyGamePhase.WaitingToRoll;
	[Property, Sync] public int PendingPurchaseSpaceIndex { get; set; } = -1;

	private static MonopolyGame instance;

	public static MonopolyGame Instance => instance;

	public int LocalSelectedSpaceIndex { get; set; } = -1;

	public MonopolySpaceDef SelectedSpace =>
		LocalSelectedSpaceIndex >= 0 && LocalSelectedSpaceIndex < Board.Spaces.Count
			? Board.GetSpaceDef(LocalSelectedSpaceIndex)
			: null;

	public void SelectSpace( int spaceIndex )
	{
		LocalSelectedSpaceIndex = spaceIndex;
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

		//var total = LastDieA + LastDieB;
		var total = 4;

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
}