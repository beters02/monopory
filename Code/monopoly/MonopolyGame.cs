using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public enum MonopolyGamePhase
{
	WaitingToRoll,
	ResolvingSpace,
	WaitingForBuyDecision,
	Auctioning,
	TurnEnded
}

public sealed class MonopolyGame : Component
{
	[Property] public List<MonopolyPlayerState> Players { get; set; } = new();
	[Property] public MonopolyGameConfig Config { get; set; } = new();

	[Property, Sync] public int CurrentPlayerIndex { get; set; }
	[Property, Sync] public int LastDieA { get; set; }
	[Property, Sync] public int LastDieB { get; set; }
	[Property, Sync] public NetDictionary<int, int> PropertyOwners { get; set; } = new();
	[Property, Sync] public NetDictionary<int, int> PropertyImprovements { get; set; } = new();
	[Property, Sync] public NetDictionary<int, bool> MortgagedProperties { get; set; } = new();
	[Property, Sync] public NetDictionary<int, string> PendingTrades { get; set; } = new();
	[Property] public MonopolyBoard Board { get; set; }

	public MonopolyPlayerState CurrentPlayer =>
		Players.Count == 0 ? null : Players[CurrentPlayerIndex];

	[Property, Sync] public MonopolyGamePhase Phase { get; set; } = MonopolyGamePhase.WaitingToRoll;
	[Property, Sync] public int PendingPurchaseSpaceIndex { get; set; } = -1;
	[Property, Sync] public int AuctionSpaceIndex { get; set; } = -1;
	[Property, Sync] public int AuctionCurrentBid { get; set; }
	[Property, Sync] public int AuctionHighBidderIndex { get; set; } = -1;
	[Property, Sync] public float AuctionEndsAt { get; set; }
	[Property, Sync] public int NextTradeId { get; set; } = 1;
	[Property, Sync] public int FreeParkingBank { get; set; }
	[Property, Sync] public bool CurrentTurnGetsExtraRoll { get; set; }

	private static MonopolyGame instance;
	private int nextPopupId = 1;
	private readonly List<MonopolyPopup> popups = new();

	public static MonopolyGame Instance => instance;
	public IReadOnlyList<MonopolyPopup> Popups => popups;

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
		UpdatePopups();

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
		UpdateAuction();
	}

	private void UpdateAuction()
	{
		if ( Phase != MonopolyGamePhase.Auctioning )
			return;

		if ( AuctionSpaceIndex < 0 || Time.Now < AuctionEndsAt )
			return;

		FinishAuction();
	}

	private void UpdatePopups()
	{
		if ( popups.Count == 0 )
			return;

		for ( var i = popups.Count - 1; i >= 0; i-- )
		{
			var popup = popups[i];
			if ( popup.Lifetime <= 0f )
				continue;

			popup.Lifetime -= Time.Delta;
			if ( popup.Lifetime <= 0f )
				popups.RemoveAt( i );
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

	public MonopolyPlayerState GetPlayerForString( string playerString )
	{
		return ResolvePlayerReference( playerString, null );
	}

	public MonopolyPlayerState ResolvePlayerReference( string playerString, Connection caller = null )
	{
		if ( string.IsNullOrWhiteSpace( playerString ) )
			return null;

		playerString = TrimPlayerReference( playerString );

		if ( playerString.Equals( "self", StringComparison.OrdinalIgnoreCase ) ||
			playerString.Equals( "me", StringComparison.OrdinalIgnoreCase ) )
		{
			return GetPlayerForConnection( caller ) ?? LocalPlayer;
		}

		if ( long.TryParse( playerString, out var steamId ) )
		{
			var steamIdMatch = Players.FirstOrDefault( player => player is not null && player.OwnerId == steamId );
			if ( steamIdMatch is not null )
				return steamIdMatch;
		}

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.Exact, out var exactMatch ) )
			return exactMatch;

		if ( playerString.Contains( '_' ) &&
			TryResolvePlayerByName( playerString.Replace( '_', ' ' ), PlayerNameMatchMode.Exact, out var underscoreMatch ) )
		{
			return underscoreMatch;
		}

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.RegexNormalizedExact, out var normalizedMatch ) )
			return normalizedMatch;

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.Partial, out var partialMatch ) )
			return partialMatch;

		if ( playerString.Contains( '_' ) &&
			TryResolvePlayerByName( playerString.Replace( '_', ' ' ), PlayerNameMatchMode.Partial, out var underscorePartialMatch ) )
		{
			return underscorePartialMatch;
		}

		if ( TryResolvePlayerByName( playerString, PlayerNameMatchMode.RegexNormalizedPartial, out var normalizedPartialMatch ) )
			return normalizedPartialMatch;

		return null;
	}

	private enum PlayerNameMatchMode
	{
		Exact,
		Partial,
		RegexNormalizedExact,
		RegexNormalizedPartial
	}

	private bool TryResolvePlayerByName( string playerName, PlayerNameMatchMode matchMode, out MonopolyPlayerState match )
	{
		match = null;

		var reference = matchMode switch
		{
			PlayerNameMatchMode.RegexNormalizedExact or PlayerNameMatchMode.RegexNormalizedPartial => NormalizePlayerReference( playerName ),
			_ => playerName
		};

		if ( string.IsNullOrWhiteSpace( reference ) )
			return false;

		var matches = Players
			.Where( player => player is not null && player.IsAssigned )
			.Where( player => IsPlayerNameMatch( player.PlayerName, reference, matchMode ) )
			.ToList();

		if ( matches.Count == 1 )
		{
			match = matches[0];
			return true;
		}

		if ( matches.Count > 1 )
		{
			Log.Warning( $"Multiple Monopoly players matched \"{playerName}\" with {matchMode} matching." );
			return false;
		}

		return false;
	}

	private static bool IsPlayerNameMatch( string playerName, string reference, PlayerNameMatchMode matchMode )
	{
		if ( string.IsNullOrWhiteSpace( playerName ) )
			return false;

		return matchMode switch
		{
			PlayerNameMatchMode.Exact => string.Equals( playerName, reference, StringComparison.OrdinalIgnoreCase ),
			PlayerNameMatchMode.Partial => playerName.Contains( reference, StringComparison.OrdinalIgnoreCase ),
			PlayerNameMatchMode.RegexNormalizedExact => NormalizePlayerReference( playerName ) == reference,
			PlayerNameMatchMode.RegexNormalizedPartial => NormalizePlayerReference( playerName ).Contains( reference, StringComparison.OrdinalIgnoreCase ),
			_ => false
		};
	}

	private static string TrimPlayerReference( string playerString )
	{
		playerString = playerString.Trim();

		if ( playerString.Length >= 2 &&
			((playerString[0] == '"' && playerString[^1] == '"') ||
			(playerString[0] == '\'' && playerString[^1] == '\'')) )
		{
			return playerString[1..^1].Trim();
		}

		return playerString;
	}

	private static string NormalizePlayerReference( string playerString )
	{
		if ( string.IsNullOrWhiteSpace( playerString ) )
			return "";

		return Regex.Replace( playerString, @"[\W_]+", "" ).ToLowerInvariant();
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
		emptySlot.Money = Math.Max( Config?.StartingMoney ?? 1500, 0 );
		emptySlot.SpaceIndex = 0;
		emptySlot.IsInJail = false;
		emptySlot.ConsecutiveDoubles = 0;
		emptySlot.SkipsNextTurn = false;

		Log.Info( $"Assigned {connection.DisplayName} to Monopoly player slot {Players.IndexOf( emptySlot )}" );
	}

	[Button( "Roll Dice" )]
	public async Task RollDiceAsync(int amount = -1)
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
		CurrentTurnGetsExtraRoll = false;

		// move player...

		int total;
		bool rolledDoubles = false;

		if (amount != -1)
			total = amount;
		else
		{
			LastDieA = Game.Random.Int( 1, 6 );
			LastDieB = Game.Random.Int( 1, 6 );
			total = LastDieA + LastDieB;
			rolledDoubles = LastDieA == LastDieB;
		}

		if ( Config?.DoublesGoesAgain == true )
		{
			if ( rolledDoubles )
			{
				CurrentPlayer.ConsecutiveDoubles++;
				if ( CurrentPlayer.ConsecutiveDoubles >= 3 )
				{
					SendPlayerToJail( CurrentPlayer );
					CurrentPlayer.ConsecutiveDoubles = 0;
					Log.Info( $"{CurrentPlayer.PlayerName} rolled three doubles in a row and went to Jail." );
					CompleteTurn();
					Phase = MonopolyGamePhase.WaitingToRoll;
					return;
				}

				CurrentTurnGetsExtraRoll = true;
			}
			else
			{
				CurrentPlayer.ConsecutiveDoubles = 0;
			}
		}

		//var SpaceIndex = (CurrentPlayer.SpaceIndex + total) % 40;
		//CurrentPlayer.SpaceIndex = SpaceIndex;
		await MovePlayerSteps( CurrentPlayer, total );

		Log.Info( $"Player {CurrentPlayerIndex + 1} rolled {LastDieA} + {LastDieB} = {total}" );

		if ( Phase == MonopolyGamePhase.ResolvingSpace )
		{
			if ( amount >= 0 )
			{
				CompleteTurn();
				Phase = MonopolyGamePhase.WaitingToRoll;
			}
			else
			{
				Phase = MonopolyGamePhase.TurnEnded;
			}
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
				PayBank( player, spaceDef.TaxAmount );
				Log.Info( $"{player.PlayerName} paid ${spaceDef.TaxAmount} tax." );
				break;

			case SpaceType.GoToJail:
				SendPlayerToJail( player );
				CompleteTurn();
				Phase = MonopolyGamePhase.WaitingToRoll;
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
				ResolveFreeParkingLanding( player );
				if ( Config?.VacationCash == true )
				{
					CompleteTurn();
					Phase = MonopolyGamePhase.WaitingToRoll;
				}
				break;
		}
	}

	private void ResolvePropertyLanding( MonopolyPlayerState player, MonopolySpaceDef def )
	{
		if (!PropertyOwners.ContainsKey(def.Index))
		{
			ResolveUnownedPropertyLanding( player, def );
			return;
		}

		if ( PropertyOwners.TryGetValue( def.Index, out var ownerIndex ) )
		{
			var owner = Players.ElementAtOrDefault( ownerIndex );

			if ( owner is null || owner == player )
				return;

			if ( Config?.DontCollectRentWhileInPrison == true && owner.IsInJail )
			{
				Log.Info( $"{owner.PlayerName} is in Jail and cannot collect rent from {player.PlayerName}." );
				return;
			}

			var rent = GetRentForSpace( def.Index );
			player.Money -= rent;
			owner.Money += rent;

			Log.Info( $"{player.PlayerName} paid ${rent} rent to {owner.PlayerName}." );
			return;
		}
	}

	private void ResolveUnownedPropertyLanding( MonopolyPlayerState player, MonopolySpaceDef def )
	{
		switch ( Config?.LandedUnownedMode ?? MonopolyUnownedLandingMode.SkipOrAuction )
		{
			case MonopolyUnownedLandingMode.ForceAuction:
				StartAuction( def.Index );
				return;

			case MonopolyUnownedLandingMode.ForceBuyIfPossible:
				if ( player.Money >= def.Price )
				{
					BuyUnownedPropertyForPlayer( player, def, CurrentPlayerIndex );
					return;
				}

				Log.Info( $"{player.PlayerName} could not afford {def.DisplayName}." );
				return;

			case MonopolyUnownedLandingMode.SkipOrAuction:
			default:
				PendingPurchaseSpaceIndex = def.Index;
				Phase = MonopolyGamePhase.WaitingForBuyDecision;

				Log.Info( $"{player.PlayerName} can buy {def.DisplayName} for ${def.Price}." );
				return;
		}
	}

	private void ResolveFreeParkingLanding( MonopolyPlayerState player )
	{
		if ( Config?.VacationCash != true )
		{
			Log.Info( $"{player.PlayerName} landed on Free Parking." );
			return;
		}

		var payout = FreeParkingBank;
		FreeParkingBank = 0;

		if ( payout > 0 )
			player.Money += payout;

		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and skipped their extra roll." );
			return;
		}

		player.SkipsNextTurn = true;
		Log.Info( $"{player.PlayerName} collected ${payout} from Free Parking and will skip their next turn." );
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

	public void SendPopupToAll( string title, string message, MonopolyPopupKind kind = MonopolyPopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost )
			return;

		ShowPopup( nextPopupId++, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	public void SendPopupToPlayer( int playerIndex, string title, string message, MonopolyPopupKind kind = MonopolyPopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost )
			return;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null )
			return;

		SendPopupToPlayer( player, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	public void SendPopupToPlayer( MonopolyPlayerState player, string title, string message, MonopolyPopupKind kind = MonopolyPopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost )
			return;

		var connection = GetConnectionForPlayer( player );
		if ( connection is null )
			return;

		SendPopupToConnection( connection, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	public void SendPopupToConnection( Connection connection, string title, string message, MonopolyPopupKind kind = MonopolyPopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		if ( !Networking.IsHost || connection is null )
			return;

		using ( Rpc.FilterInclude( connection ) )
		{
			ShowPopup( nextPopupId++, title, message, kind, canDismiss, lifetime, soundEnabled );
		}
	}

	public void DismissPopup( int popupId )
	{
		popups.RemoveAll( popup => popup.Id == popupId );
	}

	public void ShowLocalPopup( string title, string message, MonopolyPopupKind kind = MonopolyPopupKind.Info, bool canDismiss = true, float lifetime = 5f, bool soundEnabled = true )
	{
		ShowPopupLocal( nextPopupId++, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	[Rpc.Broadcast]
	private void ShowPopup( int popupId, string title, string message, MonopolyPopupKind kind, bool canDismiss, float lifetime, bool soundEnabled )
	{
		ShowPopupLocal( popupId, title, message, kind, canDismiss, lifetime, soundEnabled );
	}

	private void ShowPopupLocal( int popupId, string title, string message, MonopolyPopupKind kind, bool canDismiss, float lifetime, bool soundEnabled )
	{
		popups.RemoveAll( popup => popup.Id == popupId );
		popups.Add( new MonopolyPopup
		{
			Id = popupId,
			Title = title ?? "",
			Message = message ?? "",
			Kind = kind,
			CanDismiss = canDismiss,
			Lifetime = lifetime,
			SoundEnabled = soundEnabled
		} );

		//TODO: add dismiss sound

		if (soundEnabled)
			MonopolyAssets.Sounds.Popup.ForKind(kind).Play();
			

		while ( popups.Count > 4 )
			popups.RemoveAt( 0 );
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
			
			PayBank( player, def.Price );
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

		if ( useMoney )
			PayBank( player, totalPrice );

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

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		Log.Info( $"{CurrentPlayer?.PlayerName} skipped buying." );

		PendingPurchaseSpaceIndex = -1;
		Phase = MonopolyGamePhase.TurnEnded;
	}

	[Button( "Auction Pending Property" )]
	public void AuctionPendingProperty()
	{
		if ( !Networking.IsHost )
			return;

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		StartAuction( PendingPurchaseSpaceIndex );
	}

	private void StartAuction( int spaceIndex )
	{
		var def = Board?.GetSpaceDef( spaceIndex );
		if ( def is null || !IsPurchasableSpace( def ) || GetOwnerIndexForSpace( spaceIndex ) >= 0 )
		{
			PendingPurchaseSpaceIndex = -1;
			Phase = MonopolyGamePhase.TurnEnded;
			return;
		}

		PendingPurchaseSpaceIndex = -1;
		AuctionSpaceIndex = spaceIndex;
		AuctionCurrentBid = 0;
		AuctionHighBidderIndex = -1;
		AuctionEndsAt = Time.Now + 15f;
		Phase = MonopolyGamePhase.Auctioning;

		SendPopupToAll( "Auction started", $"{def.DisplayName} is up for auction.", MonopolyPopupKind.Info, true, 4f );
		Log.Info( $"Auction started for {def.DisplayName}." );
	}

	private void FinishAuction()
	{
		if ( !Networking.IsHost )
			return;

		var def = Board?.GetSpaceDef( AuctionSpaceIndex );

		if ( def is not null && AuctionHighBidderIndex >= 0 )
		{
			var winner = Players.ElementAtOrDefault( AuctionHighBidderIndex );
			if ( winner is not null && winner.IsAssigned && winner.Money >= AuctionCurrentBid )
			{
				PayBank( winner, AuctionCurrentBid );
				PropertyOwners[def.Index] = AuctionHighBidderIndex;
				SendPopupToAll( "Auction won", $"{winner.PlayerName} won {def.DisplayName} for ${AuctionCurrentBid}.", MonopolyPopupKind.Success, true, 5f );
				Log.Info( $"{winner.PlayerName} won {def.DisplayName} for ${AuctionCurrentBid}." );
			}
		}
		else if ( def is not null )
		{
			SendPopupToAll( "Auction ended", $"{def.DisplayName} received no bids.", MonopolyPopupKind.Warning, true, 5f );
			Log.Info( $"Auction for {def.DisplayName} ended with no bids." );
		}

		ClearAuction();
		Phase = MonopolyGamePhase.TurnEnded;
	}

	private void ClearAuction()
	{
		AuctionSpaceIndex = -1;
		AuctionCurrentBid = 0;
		AuctionHighBidderIndex = -1;
		AuctionEndsAt = 0f;
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
	public void RequestRollDice(int amount = -1)
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;
		_ = RollDiceAsync(amount);
	}

	[Rpc.Host]
	public void RequestEndTurn()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;

		EndTurn();
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
	public void RequestStartPendingAuction()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;

		AuctionPendingProperty();
	}

	[Rpc.Host]
	public void RequestAuctionBid( int bidAmount )
	{
		var bidderIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( bidderIndex < 0 )
			return;

		PlaceAuctionBid( bidderIndex, bidAmount );
	}

	public void PlaceAuctionBid( int bidderIndex, int bidAmount )
	{
		if ( !Networking.IsHost )
			return;

		if ( Phase != MonopolyGamePhase.Auctioning )
			return;

		var bidder = Players.ElementAtOrDefault( bidderIndex );
		var def = Board?.GetSpaceDef( AuctionSpaceIndex );
		if ( bidder is null || !bidder.IsAssigned || def is null )
			return;

		if ( bidAmount <= AuctionCurrentBid || bidAmount > bidder.Money )
			return;

		AuctionCurrentBid = bidAmount;
		AuctionHighBidderIndex = bidderIndex;
		AuctionEndsAt = MathF.Max( AuctionEndsAt, Time.Now + 7f );

		Log.Info( $"{bidder.PlayerName} bid ${AuctionCurrentBid} on {def.DisplayName}." );
	}

	[Rpc.Host]
	public void RequestBuildImprovement( int spaceIndex )
	{
		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !CanBuildImprovement( playerIndex, spaceIndex ) )
			return;

		var player = Players[playerIndex];
		var cost = GetImprovementCost( spaceIndex );
		var count = GetImprovementCount( spaceIndex );

		PayBank( player, cost );
		PropertyImprovements[spaceIndex] = count + 1;

		Log.Info( $"{player.PlayerName} built on {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${cost}." );
	}

	[Rpc.Host]
	public void RequestSellImprovement( int spaceIndex )
	{
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

		Log.Info( $"{player.PlayerName} sold an improvement on {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${refund}." );
	}

	[Rpc.Host]
	public void RequestMortgageProperty( int spaceIndex )
	{
		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !CanMortgageProperty( playerIndex, spaceIndex ) )
			return;

		var player = Players[playerIndex];
		var value = GetMortgageValue( spaceIndex );

		player.Money += value;
		MortgagedProperties[spaceIndex] = true;

		Log.Info( $"{player.PlayerName} mortgaged {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${value}." );
	}

	[Rpc.Host]
	public void RequestUnmortgageProperty( int spaceIndex )
	{
		var playerIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( !CanUnmortgageProperty( playerIndex, spaceIndex ) )
			return;

		var player = Players[playerIndex];
		var cost = GetUnmortgageCost( spaceIndex );

		PayBank( player, cost );
		MortgagedProperties.Remove( spaceIndex );

		Log.Info( $"{player.PlayerName} unmortgaged {Board.GetSpaceDef( spaceIndex )?.DisplayName} for ${cost}." );
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
		player.ConsecutiveDoubles = 0;
		CurrentTurnGetsExtraRoll = false;

		Log.Info( $"{player.PlayerName} was sent to Jail." );
	}

	private void BuyUnownedPropertyForPlayer( MonopolyPlayerState player, MonopolySpaceDef def, int ownerIndex )
	{
		PropertyOwners[def.Index] = ownerIndex;
		PayBank( player, def.Price );

		Log.Info( $"{player.PlayerName} bought {def.DisplayName} for ${def.Price}." );
	}

	private void PayBank( MonopolyPlayerState player, int amount )
	{
		if ( player is null || amount <= 0 )
			return;

		player.Money -= amount;

		if ( Config?.VacationCash == true )
			FreeParkingBank += amount;
	}

	private void CompleteTurn()
	{
		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			return;
		}

		CurrentPlayer.ConsecutiveDoubles = 0;
		AdvanceTurn();
	}

	[Button( "End Turn" )]
	public void EndTurn()
	{
		if ( !Networking.IsHost )
			return;

		if ( Phase != MonopolyGamePhase.TurnEnded )
			return;

		CompleteTurn();
		Phase = MonopolyGamePhase.WaitingToRoll;
	}

	private void AdvanceTurn()
	{
		if ( Players.Count == 0 )
			return;

		for ( int i = 0; i < Players.Count; i++ )
		{
			CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;

			var player = Players[CurrentPlayerIndex];
			if ( !player.IsAssigned )
				continue;

			if ( player.SkipsNextTurn )
			{
				player.SkipsNextTurn = false;
				Log.Info( $"{player.PlayerName} skipped their turn." );
				continue;
			}

			if ( player.IsAssigned )
				return;
		}

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

		if ( GetOwnerIndexForSpace( spaceIndex ) != playerIndex )
			return false;

		if ( !IsMortgaged( spaceIndex ) )
			return false;

		return player.Money >= GetUnmortgageCost( spaceIndex );
	}

	private bool CanPlayerManageProperties( int playerIndex )
	{
		return playerIndex >= 0 &&
			CurrentPlayerIndex == playerIndex &&
			(Phase == MonopolyGamePhase.WaitingToRoll || Phase == MonopolyGamePhase.TurnEnded);
	}

	private void RemoveInvalidTrades()
	{
		foreach ( var trade in GetTrades() )
		{
			if ( !IsTradeValid( trade ) )
				PendingTrades.Remove( trade.Id );
		}
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
