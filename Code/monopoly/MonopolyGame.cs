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

	[Button( "Roll Dice" )]
	public void RollDice()
	{
		if ( !Networking.IsHost )
			return;

		if ( CurrentPlayer is null )
			return;

		if ( Phase != MonopolyGamePhase.WaitingToRoll )
			return;

		Phase = MonopolyGamePhase.ResolvingSpace;

		// move player...

		LastDieA = Game.Random.Int( 1, 6 );
		LastDieB = Game.Random.Int( 1, 6 );

		var total = LastDieA + LastDieB;

		var SpaceIndex = (CurrentPlayer.SpaceIndex + total) % 40;
		Log.Info(SpaceIndex);
		CurrentPlayer.SpaceIndex = SpaceIndex;

		Log.Info( $"Player {CurrentPlayerIndex + 1} rolled {LastDieA} + {LastDieB} = {total}" );

		ResolveLanding( CurrentPlayer );

		if ( Phase == MonopolyGamePhase.ResolvingSpace )
		{
			AdvanceTurn();
			Phase = MonopolyGamePhase.WaitingToRoll;
		}
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
		RollDice();
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

		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
	}
}