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

public enum MonopolyMatchState
{
	Lobby,
	Starting,
	InGame,
	Paused,
	GameOver
}

public sealed class MonopolyGame : Component
{
	[Property] public List<MonopolyPlayerState> Players { get; set; } = new();
	[Property] public MonopolyGameConfig Config { get; set; } = new();
	[Property] public GameObject TokenPrefab { get; set; }

	[Property, Sync] public MonopolyMatchState MatchState { get; set; } = MonopolyMatchState.Lobby;
	[Property, Sync] public int WinnerPlayerIndex { get; set; } = -1;
	[Property, Sync] public float GameStartedAt { get; set; }
	[Property, Sync] public int StartingPlayerCount { get; set; }
	[Property, Sync] public int CurrentPlayerIndex { get; set; }
	[Property, Sync] public int LastDieA { get; set; }
	[Property, Sync] public int LastDieB { get; set; }
	[Property, Sync] public NetDictionary<int, int> PropertyOwners { get; set; } = new();
	[Property, Sync] public NetDictionary<int, int> PropertyImprovements { get; set; } = new();
	[Property, Sync] public NetDictionary<int, bool> MortgagedProperties { get; set; } = new();
	[Property, Sync] public NetDictionary<int, string> PendingTrades { get; set; } = new();
	[Property] public MonopolyBoard Board { get; set; }

	public MonopolyPlayerState CurrentPlayer =>
		Players.Count == 0 || CurrentPlayerIndex < 0 || CurrentPlayerIndex >= Players.Count ? null : Players[CurrentPlayerIndex];

	[Property, Sync] public MonopolyGamePhase Phase { get; set; } = MonopolyGamePhase.WaitingToRoll;
	[Property, Sync] public int PendingPurchaseSpaceIndex { get; set; } = -1;
	[Property, Sync] public int AuctionSpaceIndex { get; set; } = -1;
	[Property, Sync] public int AuctionCurrentBid { get; set; }
	[Property, Sync] public int AuctionHighBidderIndex { get; set; } = -1;
	[Property, Sync] public float AuctionEndsAt { get; set; }
	[Property, Sync] public int NextTradeId { get; set; } = 1;
	[Property, Sync] public int FreeParkingBank { get; set; }
	[Property, Sync] public bool CurrentTurnGetsExtraRoll { get; set; }
	[Property, Sync] public float CurrentTurnEndsAt { get; set; }
	[Property, Sync] public int PendingForcedPaymentPlayerIndex { get; set; } = -1;
	[Property, Sync] public int PendingForcedPaymentAmount { get; set; }
	[Property, Sync] public int PendingForcedPaymentReceiverIndex { get; set; } = -1;
	[Property, Sync] public bool PendingForcedPaymentToBank { get; set; }
	[Property, Sync] public bool PendingForcedPaymentToEachPlayer { get; set; }
	[Property, Sync] public int PendingForcedPaymentEachPlayerAmount { get; set; }

	public bool HasPendingForcedPayment =>
		PendingForcedPaymentPlayerIndex >= 0 && PendingForcedPaymentAmount > 0;

	public bool IsInGame => MatchState == MonopolyMatchState.InGame;
	public bool IsPaused => MatchState == MonopolyMatchState.Paused;
	public bool HasStarted => MatchState is MonopolyMatchState.InGame or MonopolyMatchState.Paused or MonopolyMatchState.GameOver;
	public bool CanStartGame => GetLobbyPlayers().Count >= MinPlayers && GetLobbyPlayers().All( player => player.IsReady );
	public int MinPlayers => Math.Max( Config?.MinPlayers ?? 2, 1 );
	public int MaxPlayers => Math.Max( Config?.MaxPlayers ?? Players.Count, MinPlayers );
	public MonopolyPlayerState Winner =>
		WinnerPlayerIndex >= 0 ? Players.ElementAtOrDefault( WinnerPlayerIndex ) : null;

	private static MonopolyGame instance;
	private int nextPopupId = 1;
	private readonly List<MonopolyPopup> popups = new();
	private readonly List<GameObject> spawnedTokenObjects = new();
	private float pausedTurnRemainingSeconds;
	private float pausedAuctionRemainingSeconds;

	public static MonopolyGame Instance => instance;
	public IReadOnlyList<MonopolyPopup> Popups => popups;

	public int LocalSelectedSpaceIndex { get; set; } = -1;

	public Logger landingLogger = new("GameLanding");

	public MonopolySpaceDef SelectedSpace =>
		Board is not null && LocalSelectedSpaceIndex >= 0 && LocalSelectedSpaceIndex < Board.Spaces.Count
			? Board.GetSpaceDef(LocalSelectedSpaceIndex)
			: null;

	private void SelectSpaceAsync( int spaceIndex ) => LocalSelectedSpaceIndex = spaceIndex;

	public void SelectSpace( int spaceIndex )
	{
		if ( !CanLeaveCurrentSelectedSpace() )
			return;

		if ( spaceIndex == LocalSelectedSpaceIndex )
			spaceIndex = -1;

		SelectSpaceAsync( spaceIndex );
	}

	public bool CanLeaveCurrentSelectedSpace()
	{
		return CurrentPlayerIndex != LocalPlayerIndex ||
			Phase != MonopolyGamePhase.WaitingForBuyDecision ||
			PendingPurchaseSpaceIndex < 0 ||
			LocalSelectedSpaceIndex != PendingPurchaseSpaceIndex;
	}

	protected override void OnStart()
	{
		instance = this;
		MonopolySteamInviteBridge.Register( Scene );

		if ( !Networking.IsHost )
			return;

		var bootstrap = MonopolyMatchBootstrap.Current;
		if ( bootstrap?.Config is not null )
			Config = bootstrap.Config;

		EnsurePlayerSlots();
		SyncLobbyConnections();
		ResetGameState( false );

		if ( bootstrap?.AutoStartGame == true )
		{
			TryStartGame( false );
			StartingPlayerCount = Math.Max( StartingPlayerCount, bootstrap.StartingPlayerCount );
			MonopolyMatchBootstrap.Clear();
		}
	}

	protected override void OnUpdate()
	{
		UpdatePopups();

		if ( !Networking.IsHost )
			return;

		SyncLobbyConnections();

		if ( MatchState != MonopolyMatchState.InGame )
			return;

		RemoveInvalidTrades();
		UpdateAuction();
		UpdateTurnTimer();
		CheckForGameOver();

		
	}

	private void UpdateTurnTimer()
	{
		var limit = Config?.TurnTimeLimitSeconds ?? 180;
		if ( limit <= 0 )
		{
			CurrentTurnEndsAt = 0f;
			return;
		}

		var player = CurrentPlayer;
		if ( player is null || !player.IsAssigned || player.IsBankrupt )
		{
			AdvanceTurn();
			return;
		}

		if ( Phase == MonopolyGamePhase.ResolvingSpace )
			return;

		if ( CurrentTurnEndsAt <= 0f )
			StartTurnTimer();

		if ( CurrentTurnEndsAt > 0f && Time.Now >= CurrentTurnEndsAt )
			SkipCurrentTurnForTimeout();
	}

	private void StartTurnTimer()
	{
		var limit = Config?.TurnTimeLimitSeconds ?? 180;
		CurrentTurnEndsAt = limit <= 0 ? 0f : Time.Now + limit;
	}

	private void SkipCurrentTurnForTimeout()
	{
		if ( CurrentPlayer is null )
			return;

		var skippedPlayerIndex = CurrentPlayerIndex;
		var skippedPlayer = CurrentPlayer;

		if ( HasPendingForcedPaymentForPlayer( skippedPlayerIndex ) )
			BankruptPlayer( skippedPlayerIndex, Players.ElementAtOrDefault( PendingForcedPaymentReceiverIndex ) );

		PendingPurchaseSpaceIndex = -1;

		if ( Phase == MonopolyGamePhase.Auctioning )
			ClearAuction();

		CurrentTurnGetsExtraRoll = false;
		Phase = MonopolyGamePhase.WaitingToRoll;

		Log.Info( $"{skippedPlayer.PlayerName}'s turn timed out and was skipped." );
		SendPopupToAll( "Turn skipped", $"{skippedPlayer.PlayerName}'s turn timed out.", MonopolyPopupKind.Warning, true, 4f );

		AdvanceTurn();
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

	public List<MonopolyPlayerState> GetLobbyPlayers()
	{
		return Players
			.Where( player => player is not null && player.IsAssigned )
			.Take( MaxPlayers )
			.ToList();
	}

	private List<MonopolyPlayerState> GetAssignedPlayers()
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

	private MonopolyPlayerState CreatePlayerStateObject( int slotNumber )
	{
		var playerObject = new GameObject( true, $"PlayerState_{slotNumber:00}" );
		playerObject.SetParent( GameObject );
		return playerObject.Components.Create<MonopolyPlayerState>();
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

	private void ClearPlayerSlot( MonopolyPlayerState player )
	{
		if ( player is null )
			return;

		player.OwnerId = 0;
		player.PlayerName = "Player";
		player.IsReady = false;
		ResetPlayerForGame( player );
	}

	private void ResetPlayerForGame( MonopolyPlayerState player )
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

	private bool IsHostCaller( Connection caller )
	{
		if ( Networking.IsHost && (caller is null || caller == Connection.Local) )
			return true;

		return false;
	}

	private bool CanAcceptGameplayInput()
	{
		return MatchState == MonopolyMatchState.InGame;
	}

	public bool TryStartGame( bool requireReady = true )
	{
		if ( !Networking.IsHost )
			return false;

		SyncLobbyConnections();

		if ( requireReady && !CanStartGame )
			return false;

		if ( !requireReady && GetLobbyPlayers().Count < MinPlayers )
			return false;

		var activePlayers = GetLobbyPlayers();
		ResetGameState( false );
		StartingPlayerCount = activePlayers.Count;

		foreach ( var player in Players )
		{
			if ( player is not null && player.IsAssigned && !activePlayers.Contains( player ) )
				ClearPlayerSlot( player );
		}

		foreach ( var player in activePlayers )
		{
			ResetPlayerForGame( player );
			player.IsReady = false;
		}

		SpawnTokensForPlayers( activePlayers );

		var firstPlayerIndex = Players.FindIndex( player => player is not null && player.IsAssigned );
		CurrentPlayerIndex = Math.Max( firstPlayerIndex, 0 );
		MatchState = MonopolyMatchState.Starting;
		GameStartedAt = Time.Now;
		MatchState = MonopolyMatchState.InGame;
		StartTurnTimer();

		SendPopupToAll( "Game started", "The first turn is live.", MonopolyPopupKind.Success, true, 4f );
		return true;
	}

	private void SpawnTokensForPlayers( IReadOnlyList<MonopolyPlayerState> activePlayers )
	{
		ClearSpawnedTokens();
		ClearExistingTokenObjects();

		if ( TokenPrefab is null )
		{
			Log.Warning( "MonopolyGame has no TokenPrefab assigned, so player tokens were not spawned." );
			return;
		}

		if ( Board is null )
			Board = Scene.GetAllComponents<MonopolyBoard>().FirstOrDefault();

		for ( var i = 0; i < activePlayers.Count; i++ )
		{
			var player = activePlayers[i];
			if ( player is null || !player.IsAssigned )
				continue;

			var tokenObject = TokenPrefab.Clone();
			tokenObject.Name = $"Token_{i + 1:00}";
			tokenObject.SetParent( GameObject );

			var token = tokenObject.Components.Get<MonopolyToken>() ?? tokenObject.Components.Create<MonopolyToken>();
			token.Board = Board;
			token.PlayerState = player;

			if ( Board is not null )
				tokenObject.WorldPosition = Board.GetSpacePosition( player.SpaceIndex ) + Vector3.Up * token.HeightOffset;

			spawnedTokenObjects.Add( tokenObject );
		}
	}

	private void ClearExistingTokenObjects()
	{
		foreach ( var token in Scene.GetAllComponents<MonopolyToken>().ToList() )
		{
			if ( token?.GameObject is null || token.GameObject == TokenPrefab )
				continue;

			token.GameObject.Destroy();
		}
	}

	private void ClearSpawnedTokens()
	{
		for ( var i = spawnedTokenObjects.Count - 1; i >= 0; i-- )
		{
			if ( spawnedTokenObjects[i] is not null )
				spawnedTokenObjects[i].Destroy();
		}

		spawnedTokenObjects.Clear();
	}

	public bool TrySetReady( MonopolyPlayerState player, bool isReady )
	{
		if ( !Networking.IsHost || MatchState != MonopolyMatchState.Lobby )
			return false;

		if ( player is null || !player.IsAssigned )
			return false;

		player.IsReady = isReady;
		return true;
	}

	public bool TryPauseGame()
	{
		if ( !Networking.IsHost || MatchState != MonopolyMatchState.InGame )
			return false;

		pausedTurnRemainingSeconds = CurrentTurnEndsAt <= 0f ? 0f : Math.Max( 0f, CurrentTurnEndsAt - Time.Now );
		pausedAuctionRemainingSeconds = AuctionEndsAt <= 0f ? 0f : Math.Max( 0f, AuctionEndsAt - Time.Now );
		CurrentTurnEndsAt = 0f;
		AuctionEndsAt = 0f;
		MatchState = MonopolyMatchState.Paused;
		SendPopupToAll( "Paused", "The host paused the game.", MonopolyPopupKind.Info, true, 4f );
		return true;
	}

	public bool TryResumeGame()
	{
		if ( !Networking.IsHost || MatchState != MonopolyMatchState.Paused )
			return false;

		if ( pausedTurnRemainingSeconds > 0f )
			CurrentTurnEndsAt = Time.Now + pausedTurnRemainingSeconds;

		if ( pausedAuctionRemainingSeconds > 0f )
			AuctionEndsAt = Time.Now + pausedAuctionRemainingSeconds;

		pausedTurnRemainingSeconds = 0f;
		pausedAuctionRemainingSeconds = 0f;
		MatchState = MonopolyMatchState.InGame;
		SendPopupToAll( "Resumed", "Back to the board.", MonopolyPopupKind.Success, true, 3f );
		return true;
	}

	public bool TryReturnToLobby()
	{
		if ( !Networking.IsHost )
			return false;

		ClearSpawnedTokens();
		ResetGameState( false );
		SyncLobbyConnections();
		MatchState = MonopolyMatchState.Lobby;
		return true;
	}

	internal void ForceEndGameFromException( Exception ex )
	{
		if ( !Networking.IsHost )
			return;

		Log.Error( ex );

		CurrentTurnEndsAt = 0f;
		ClearAuction();
		ClearPendingForcedPayment();
		PendingPurchaseSpaceIndex = -1;
		CurrentTurnGetsExtraRoll = false;

		Phase = MonopolyGamePhase.TurnEnded;
		MatchState = MonopolyMatchState.GameOver;

		SendPopupToAll(
			"Game ended",
			"The game hit a fatal rules error and was ended by the host.",
			MonopolyPopupKind.Danger,
			true,
			8f
		);
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
			.Where( player => player is not null && player.IsAssigned && !player.IsBankrupt )
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

	public bool TryNormalizePlayerIndex( int index, out int normalizedIndex )
	{
		normalizedIndex = NormalizePlayerIndex(index);
		return normalizedIndex != index;
	}

	public int NormalizePlayerIndex( int index )
	{
		if ( Players.Count <= 0 )
			return -1;

		return ((index % Players.Count) + Players.Count) % Players.Count;
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
		emptySlot.IsReady = false;
		ResetPlayerForGame( emptySlot );

		Log.Info( $"Assigned {connection.DisplayName} to Monopoly player slot {Players.IndexOf( emptySlot )}" );
	}

	[Button( "Roll Dice" )]
	public async Task RollDiceAsync(int amount = -1)
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( CurrentPlayer is null )
			return;

		if ( Phase != MonopolyGamePhase.WaitingToRoll )
			return;

		if ( CurrentPlayer is null || !CurrentPlayer.IsAssigned || CurrentPlayer.IsBankrupt )
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
			if ( CurrentPlayer is not null && CurrentPlayer.IsBankrupt )
			{
				CompleteTurn();
				Phase = MonopolyGamePhase.WaitingToRoll;
			}
			else if ( amount >= 0 && !HasPendingForcedPayment )
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
			player.SpaceIndex = NormalizeSpaceIndex(player.SpaceIndex + 1);

			await Task.DelaySeconds( 0.4f );

			if ( player.SpaceIndex == 0 )
				player.Money += 200;
		}

		ResolveLanding( player );
	}

	private void ResolveLanding( MonopolyPlayerState player )
	{
		if ( player is null || Board is null )
		{
			landingLogger.Error($"FAILED FOR PLAYER/BOARD NULL: IsPlayerNull: {player == null} . IsBoardNull: {Board == null}");
			return;
		}

		landingLogger.Info($"RESOLVING PLAYER LANDING FOR PLAYER {player.PlayerName}");
		landingLogger.Info($"GETTING BOARD SPACE DEF FOR INDEX: {player.SpaceIndex}");
		var spaceDef = Board.GetSpaceDef( player.SpaceIndex );
		landingLogger.Info($"SAVED BOARD SPACE DEF");

		if ( spaceDef is null )
		{
			landingLogger.Error($"FAILED FOR SPACE DEF NULL: SpaceIndex: {player.SpaceIndex}");
			ForceEndGameFromException(new InvalidOperationException($"No board definition for space index {player.SpaceIndex}."));
			return;
		}

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
				if ( PayBank( player, spaceDef.TaxAmount ) )
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
				ResolveCardLanding( player, MonopolyCardDeck.Chance );
				break;

			case SpaceType.CommunityChest:
				ResolveCardLanding( player, MonopolyCardDeck.CommunityChest );
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
			if ( PayPlayer( player, owner, rent ) )
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

	private void ResolveCardLanding( MonopolyPlayerState player, MonopolyCardDeck deck )
	{
		var card = DrawCard( deck );
		if ( player is null || card is null )
			return;

		SendPopupToAll( card.Title, card.Description, MonopolyPopupKind.Info, true, 6f );
		Log.Info( $"{player.PlayerName} drew {deck}: {card.Title}." );

		ApplyCard( player, card );
	}

	private MonopolyCardDef DrawCard( MonopolyCardDeck deck )
	{
		var cards = deck == MonopolyCardDeck.Chance
			? Board?.ChanceCards
			: Board?.CommunityChestCards;

		if ( cards is null || cards.Count == 0 )
			return null;

		return cards[Game.Random.Int( 0, cards.Count - 1 )];
	}

	private void ApplyCard( MonopolyPlayerState player, MonopolyCardDef card )
	{
		if ( player is null || card is null )
			return;

		switch ( card.Action )
		{
			case MonopolyCardAction.CollectFromBank:
				player.Money += Math.Max( card.Amount, 0 );
				TrySettlePendingForcedPaymentForPlayer( GetPlayerIndex( player ) );
				Log.Info( $"{player.PlayerName} collected ${card.Amount} from {card.Title}." );
				break;

			case MonopolyCardAction.PayBank:
				if ( PayBank( player, card.Amount ) )
					Log.Info( $"{player.PlayerName} paid ${card.Amount} from {card.Title}." );
				break;

			case MonopolyCardAction.MoveToSpace:
				MovePlayerToCardDestination( player, card.TargetSpaceIndex, card.CollectGo, card.ResolveDestination );
				break;

			case MonopolyCardAction.MoveRelative:
				MovePlayerByCardOffset( player, card.RelativeSpaces, card.CollectGo, card.ResolveDestination );
				break;

			case MonopolyCardAction.GoToJail:
				SendPlayerToJail( player );
				CompleteTurn();
				Phase = MonopolyGamePhase.WaitingToRoll;
				break;

			case MonopolyCardAction.CollectFromEachPlayer:
				CollectFromEachPlayerForCard( player, card.Amount );
				break;

			case MonopolyCardAction.PayEachPlayer:
				PayEachPlayerForCard( player, card.Amount );
				break;

			case MonopolyCardAction.PayPerImprovement:
				PayPerImprovementForCard( player, card.HouseAmount, card.HotelAmount );
				break;
		}
	}

	private void MovePlayerToCardDestination( MonopolyPlayerState player, int targetSpaceIndex, bool collectGo, bool resolveDestination )
	{
		if ( player is null || Board is null || targetSpaceIndex < 0 )
			return;

		targetSpaceIndex = NormalizeSpaceIndex( targetSpaceIndex );
		var passedGo = collectGo && targetSpaceIndex != 0 && targetSpaceIndex < player.SpaceIndex;

		if ( passedGo )
			player.Money += 200;

		player.SpaceIndex = targetSpaceIndex;

		if ( resolveDestination )
			ResolveLanding( player );
	}

	private void MovePlayerByCardOffset( MonopolyPlayerState player, int relativeSpaces, bool collectGo, bool resolveDestination )
	{
		if ( player is null )
			return;

		var targetSpaceIndex = NormalizeSpaceIndex( player.SpaceIndex + relativeSpaces );
		var passedGo = collectGo && relativeSpaces > 0 && targetSpaceIndex < player.SpaceIndex;

		if ( passedGo )
			player.Money += 200;

		player.SpaceIndex = targetSpaceIndex;

		if ( resolveDestination )
			ResolveLanding( player );
	}

	private void PayPerImprovementForCard( MonopolyPlayerState player, int houseAmount, int hotelAmount )
	{
		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return;

		var houses = 0;
		var hotels = 0;
		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			var count = GetImprovementCount( spaceIndex );
			if ( count >= 5 )
				hotels++;
			else
				houses += count;
		}

		var amount = houses * Math.Max( houseAmount, 0 ) + hotels * Math.Max( hotelAmount, 0 );
		if ( amount <= 0 )
		{
			Log.Info( $"{player.PlayerName} had no repair fees." );
			return;
		}

		if ( PayBank( player, amount ) )
			Log.Info( $"{player.PlayerName} paid ${amount} for repairs." );
	}

	private void PayEachPlayerForCard( MonopolyPlayerState player, int amountPerPlayer )
	{
		var playerIndex = GetPlayerIndex( player );
		var receivers = GetAssignedPlayerIndexes()
			.Where( index => index != playerIndex )
			.ToList();

		var amount = Math.Max( amountPerPlayer, 0 );
		var total = amount * receivers.Count;
		if ( playerIndex < 0 || amount <= 0 || total <= 0 )
			return;

		if ( player.Money >= total )
		{
			CompleteForcedPaymentToEachPlayer( playerIndex, amount );
			return;
		}

		if ( GetPlayerLiquidAssetTotal( playerIndex ) < total )
		{
			BankruptPlayer( playerIndex, null );
			return;
		}

		BeginPendingForcedPaymentToEachPlayer( playerIndex, amount );
	}

	private void CollectFromEachPlayerForCard( MonopolyPlayerState player, int amountPerPlayer )
	{
		var receiverIndex = GetPlayerIndex( player );
		var amount = Math.Max( amountPerPlayer, 0 );
		if ( receiverIndex < 0 || amount <= 0 )
			return;

		foreach ( var payerIndex in GetAssignedPlayerIndexes().Where( index => index != receiverIndex ) )
		{
			var payer = Players.ElementAtOrDefault( payerIndex );
			if ( payer is null || payer.Money < amount )
				continue;

			payer.Money -= amount;
			player.Money += amount;
		}
	}

	public bool TryChangeMoneyForPlayer( MonopolyPlayerState player, int amount, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can change player money directly.";
			return false;
		}

		if ( player is null )
		{
			message = "Player does not exist.";
			return false;
		}

		if ( GetPlayerIndex( player ) < 0 || !player.IsAssigned )
		{
			message = "Player is not part of this game.";
			return false;
		}

		if ( player.IsBankrupt )
		{
			message = $"{player.PlayerName} is bankrupt.";
			return false;
		}

		player.Money += amount;
		TrySettlePendingForcedPaymentForPlayer( GetPlayerIndex( player ) );

		var direction = amount >= 0 ? "added to" : "removed from";
		var absoluteAmount = Math.Abs( amount );
		Log.Info( $"Cheat changed {player.PlayerName}'s money: ${absoluteAmount} {direction} balance." );
		message = $"{player.PlayerName} now has ${player.Money}.";
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

	[Button( "Auction Pending Property" )]
	public void AuctionPendingProperty()
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != MonopolyGamePhase.WaitingForBuyDecision )
			return;

		StartAuction( PendingPurchaseSpaceIndex );
	}

	private void StartAuction( int spaceIndex )
	{
		if ( !CanAcceptGameplayInput() )
			return;

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
			if ( winner is not null && winner.IsAssigned && winner.Money >= AuctionCurrentBid && PayBank( winner, AuctionCurrentBid ) )
			{
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
		if ( !CanAcceptGameplayInput() )
			return false;

		if ( caller == null )
			return false;

		var currentPlayer = CurrentPlayer;

		if ( currentPlayer == null || currentPlayer.IsBankrupt )
			return false;

		// Host can always act during local testing
		if ( Networking.IsHost && caller == Connection.Local )
			return true;

		return currentPlayer.OwnerId == caller.SteamId;
	}

	[Rpc.Host]
	public void RequestSetReady( bool isReady )
	{
		var player = GetPlayerForConnection( Rpc.Caller );
		TrySetReady( player, isReady );
	}

	[Rpc.Host]
	public void RequestStartGame()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryStartGame();
	}

	[Rpc.Host]
	public void RequestPauseGame()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryPauseGame();
	}

	[Rpc.Host]
	public void RequestResumeGame()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryResumeGame();
	}

	[Rpc.Host]
	public void RequestReturnToLobby()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryReturnToLobby();
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
		if ( !CanAcceptGameplayInput() )
			return;

		var bidderIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( bidderIndex < 0 )
			return;

		PlaceAuctionBid( bidderIndex, bidAmount );
	}

	public void PlaceAuctionBid( int bidderIndex, int bidAmount )
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != MonopolyGamePhase.Auctioning )
			return;

		var bidder = Players.ElementAtOrDefault( bidderIndex );
		var def = Board?.GetSpaceDef( AuctionSpaceIndex );
		if ( bidder is null || !bidder.IsAssigned || bidder.IsBankrupt || def is null )
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

	[Rpc.Host]
	public void RequestCreateTrade( int receiverPlayerIndex, int senderMoney, int receiverMoney, string senderPropertyIndexes, string receiverPropertyIndexes )
	{
		if ( !CanAcceptGameplayInput() )
			return;

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
		if ( !CanAcceptGameplayInput() )
			return;

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
		if ( !CanAcceptGameplayInput() )
			return;

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
		if ( !PayBank( player, def.Price ) )
			return;

		PropertyOwners[def.Index] = ownerIndex;

		Log.Info( $"{player.PlayerName} bought {def.DisplayName} for ${def.Price}." );
	}

	private bool PayBank( MonopolyPlayerState player, int amount )
	{
		if ( player is null || amount <= 0 )
			return true;

		if ( !TryMakeForcedPayment( player, amount, -1, true ) )
			return false;

		return true;
	}

	private bool PayPlayer( MonopolyPlayerState player, MonopolyPlayerState receiver, int amount )
	{
		if ( player is null || receiver is null || player == receiver || amount <= 0 )
			return true;

		return TryMakeForcedPayment( player, amount, GetPlayerIndex( receiver ), false );
	}

	private bool TryMakeForcedPayment( MonopolyPlayerState player, int amount, int receiverIndex, bool toBank )
	{
		if ( player is null || amount <= 0 )
			return true;

		if ( player.IsBankrupt )
			return false;

		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return false;

		if ( HasPendingForcedPayment )
			return TrySettlePendingForcedPayment();

		if ( player.Money >= amount )
		{
			CompleteForcedPayment( playerIndex, amount, receiverIndex, toBank );
			return true;
		}

		var totalAssets = GetPlayerLiquidAssetTotal( playerIndex );
		if ( totalAssets < amount )
		{
			BankruptPlayer( playerIndex, Players.ElementAtOrDefault( receiverIndex ) );
			return false;
		}

		BeginPendingForcedPayment( playerIndex, amount, receiverIndex, toBank );
		return false;
	}

	private void CompleteForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || amount <= 0 )
			return;

		player.Money -= amount;

		if ( toBank )
		{
			if ( Config?.VacationCash == true )
				FreeParkingBank += amount;

			return;
		}

		var receiver = Players.ElementAtOrDefault( receiverIndex );
		if ( receiver is not null )
			receiver.Money += amount;
	}

	private void CompleteForcedPaymentToEachPlayer( int playerIndex, int amountPerPlayer )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		var receivers = GetAssignedPlayerIndexes()
			.Where( index => index != playerIndex )
			.ToList();

		if ( player is null || amountPerPlayer <= 0 || receivers.Count == 0 )
			return;

		var total = amountPerPlayer * receivers.Count;
		player.Money -= total;

		foreach ( var receiverIndex in receivers )
		{
			var receiver = Players.ElementAtOrDefault( receiverIndex );
			if ( receiver is not null )
				receiver.Money += amountPerPlayer;
		}

		Log.Info( $"{player.PlayerName} paid ${amountPerPlayer} to each player." );
	}

	private void BeginPendingForcedPayment( int playerIndex, int amount, int receiverIndex, bool toBank )
	{
		PendingForcedPaymentPlayerIndex = playerIndex;
		PendingForcedPaymentAmount = amount;
		PendingForcedPaymentReceiverIndex = receiverIndex;
		PendingForcedPaymentToBank = toBank;
		PendingForcedPaymentToEachPlayer = false;
		PendingForcedPaymentEachPlayerAmount = 0;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is not null )
			Log.Info( $"{player.PlayerName} must raise ${amount} before their turn can end." );
	}

	private void BeginPendingForcedPaymentToEachPlayer( int playerIndex, int amountPerPlayer )
	{
		var receiverCount = GetAssignedPlayerIndexes().Count( index => index != playerIndex );
		var total = amountPerPlayer * receiverCount;
		if ( total <= 0 )
			return;

		PendingForcedPaymentPlayerIndex = playerIndex;
		PendingForcedPaymentAmount = total;
		PendingForcedPaymentReceiverIndex = -1;
		PendingForcedPaymentToBank = false;
		PendingForcedPaymentToEachPlayer = true;
		PendingForcedPaymentEachPlayerAmount = amountPerPlayer;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is not null )
			Log.Info( $"{player.PlayerName} must raise ${total} to pay each player ${amountPerPlayer}." );
	}

	private bool TrySettlePendingForcedPaymentForPlayer( int playerIndex )
	{
		if ( !HasPendingForcedPayment || PendingForcedPaymentPlayerIndex != playerIndex )
			return false;

		return TrySettlePendingForcedPayment();
	}

	private bool TrySettlePendingForcedPayment()
	{
		if ( !HasPendingForcedPayment )
			return true;

		var player = Players.ElementAtOrDefault( PendingForcedPaymentPlayerIndex );
		if ( player is null || player.IsBankrupt )
		{
			ClearPendingForcedPayment();
			return false;
		}

		if ( player.Money < PendingForcedPaymentAmount )
			return false;

		var amount = PendingForcedPaymentAmount;
		var receiverIndex = PendingForcedPaymentReceiverIndex;
		var toBank = PendingForcedPaymentToBank;
		var toEachPlayer = PendingForcedPaymentToEachPlayer;
		var eachPlayerAmount = PendingForcedPaymentEachPlayerAmount;

		if ( toEachPlayer )
			CompleteForcedPaymentToEachPlayer( PendingForcedPaymentPlayerIndex, eachPlayerAmount );
		else
			CompleteForcedPayment( PendingForcedPaymentPlayerIndex, amount, receiverIndex, toBank );

		ClearPendingForcedPayment();

		Log.Info( $"{player.PlayerName} paid their pending ${amount} debt." );
		return true;
	}

	private void ClearPendingForcedPayment()
	{
		PendingForcedPaymentPlayerIndex = -1;
		PendingForcedPaymentAmount = 0;
		PendingForcedPaymentReceiverIndex = -1;
		PendingForcedPaymentToBank = false;
		PendingForcedPaymentToEachPlayer = false;
		PendingForcedPaymentEachPlayerAmount = 0;
	}

	public int GetPlayerLiquidAssetTotal( int playerIndex )
	{
		if ( playerIndex < 0 )
			return 0;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || player.IsBankrupt )
			return 0;

		var total = Math.Max( player.Money, 0 );
		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			total += GetImprovementCount( spaceIndex ) * GetImprovementSellValue( spaceIndex );

			if ( !IsMortgaged( spaceIndex ) )
				total += GetMortgageValue( spaceIndex );
		}

		return total;
	}

	private void BankruptPlayer( int playerIndex, MonopolyPlayerState creditor )
	{
		if ( playerIndex < 0 )
			return;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || player.IsBankrupt )
			return;

		player.IsBankrupt = true;
		player.Money = 0;
		player.IsInJail = false;
		player.ConsecutiveDoubles = 0;
		player.SkipsNextTurn = false;

		if ( PendingForcedPaymentPlayerIndex == playerIndex )
			ClearPendingForcedPayment();

		foreach ( var spaceIndex in GetOwnedPropertyIndexes( playerIndex ) )
		{
			PropertyOwners.Remove( spaceIndex );
			PropertyImprovements.Remove( spaceIndex );
			MortgagedProperties.Remove( spaceIndex );
		}

		foreach ( var trade in GetTrades() )
		{
			if ( trade.SenderPlayerIndex == playerIndex || trade.ReceiverPlayerIndex == playerIndex )
				PendingTrades.Remove( trade.Id );
		}

		if ( AuctionHighBidderIndex == playerIndex )
		{
			AuctionHighBidderIndex = -1;
			AuctionCurrentBid = 0;
		}

		var creditorText = creditor is null ? "" : $" while owing {creditor.PlayerName}";
		Log.Info( $"{player.PlayerName} went bankrupt{creditorText}." );
		CheckForGameOver();
	}

	private void CheckForGameOver()
	{
		if ( MatchState != MonopolyMatchState.InGame )
			return;

		if ( StartingPlayerCount <= 1 )
			return;

		var remaining = Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null && entry.player.IsAssigned && !entry.player.IsBankrupt )
			.ToList();

		if ( remaining.Count > 1 )
			return;

		WinnerPlayerIndex = remaining.Count == 1 ? remaining[0].index : -1;
		CurrentTurnEndsAt = 0f;
		ClearAuction();
		ClearPendingForcedPayment();
		PendingPurchaseSpaceIndex = -1;
		Phase = MonopolyGamePhase.TurnEnded;
		MatchState = MonopolyMatchState.GameOver;

		var winnerName = Winner?.PlayerName ?? "No one";
		SendPopupToAll( "Game over", $"{winnerName} won the game.", MonopolyPopupKind.Success, true, 8f );
		Log.Info( $"Monopoly game over. Winner: {winnerName}." );
	}

	private void CompleteTurn()
	{
		if ( CurrentTurnGetsExtraRoll )
		{
			CurrentTurnGetsExtraRoll = false;
			StartTurnTimer();
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

		if ( !CanAcceptGameplayInput() )
			return;

		if ( Phase != MonopolyGamePhase.TurnEnded )
			return;

		if ( HasPendingForcedPayment )
			return;

		CompleteTurn();
		Phase = MonopolyGamePhase.WaitingToRoll;
		StartTurnTimer();
	}

	private void AdvanceTurn()
	{
		CheckForGameOver();
		if ( MatchState == MonopolyMatchState.GameOver )
			return;

		if ( Players.Count == 0 )
			return;

		for ( int i = 0; i < Players.Count; i++ )
		{
			CurrentPlayerIndex = NormalizePlayerIndex(CurrentPlayerIndex + 1);
			//CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;

			var player = Players[CurrentPlayerIndex];
			if ( !player.IsAssigned || player.IsBankrupt )
				continue;

			if ( player.SkipsNextTurn )
			{
				player.SkipsNextTurn = false;
				Log.Info( $"{player.PlayerName} skipped their turn." );
				continue;
			}

			if ( player.IsAssigned && !player.IsBankrupt )
			{
				StartTurnTimer();
				return;
			}
		}

		for ( int i = 0; i < Players.Count; i++ )
		{
			//CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
			CurrentPlayerIndex = NormalizePlayerIndex(CurrentPlayerIndex + 1);

			if ( Players[CurrentPlayerIndex].IsAssigned && !Players[CurrentPlayerIndex].IsBankrupt )
			{
				StartTurnTimer();
				return;
			}
		}

		CurrentTurnEndsAt = 0f;
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

	private List<int> GetAssignedPlayerIndexes()
	{
		return Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null && entry.player.IsAssigned && !entry.player.IsBankrupt )
			.Select( entry => entry.index )
			.ToList();
	}

	// returns false if index did not need to be normalized.
	public static bool TryNormalizeSpaceIndex(int spaceIndex, out int normalizedSpaceIndex)
	{
		normalizedSpaceIndex = NormalizeSpaceIndex(spaceIndex);
		return normalizedSpaceIndex != spaceIndex;
	}

	public static int NormalizeSpaceIndex( int spaceIndex )
	{
		return ((spaceIndex % 40) + 40) % 40;
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

	public bool IsTradeValid( MonopolyTradeRequest trade )
	{
		if ( trade is null )
			return false;

		var sender = Players.ElementAtOrDefault( trade.SenderPlayerIndex );
		var receiver = Players.ElementAtOrDefault( trade.ReceiverPlayerIndex );

		if ( sender is null || receiver is null || !sender.IsAssigned || !receiver.IsAssigned || sender.IsBankrupt || receiver.IsBankrupt )
			return false;

		if ( HasPendingForcedPaymentForPlayer( trade.SenderPlayerIndex ) || HasPendingForcedPaymentForPlayer( trade.ReceiverPlayerIndex ) )
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
