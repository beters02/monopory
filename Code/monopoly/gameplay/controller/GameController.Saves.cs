using Sandbox;
using System;

public sealed partial class GameController : Component
{
	private string loadedSourceSaveId = "";
	private string currentManualSaveId = "";
	private bool hasLoadedRestorePoint;
	private float lastAutosaveAt;
	private string lastAutosaveFingerprint = "";

	public bool HasLoadedRestorePoint => hasLoadedRestorePoint && !string.IsNullOrWhiteSpace( loadedSourceSaveId );
	public bool HasCurrentManualSave => !string.IsNullOrWhiteSpace( currentManualSaveId );
	[Sync] public string CurrentGameIdentifier { get; private set; } = "";

	public bool TrySaveGame( string slotName, bool isAutosave, out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can save games.";
			return false;
		}

		if ( MatchState is not (MatchLifecycleState.InGame or MatchLifecycleState.Paused or MatchLifecycleState.GameOver) )
		{
			message = "Only active games can be saved.";
			return false;
		}

		if ( isAutosave && !CanAutosaveNow() )
		{
			message = "Autosave skipped until the game reaches a stable point.";
			return false;
		}

		var saveType = isAutosave ? GameSaveType.Autosave : GameSaveType.Manual;

		var save = BuildSaveFile( slotName, saveType );
		if ( !GameSaveService.WriteSave( save, saveType ) )
		{
			message = "Could not write save file.";
			return false;
		}

		if ( saveType == GameSaveType.Manual )
			currentManualSaveId = save.Summary.SaveId;

		message = isAutosave ? "Autosaved game." : $"Saved game: {save.Summary.DisplayName}.";
		return true;
	}

	public bool TryReloadLastLoadedSave( out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can reload saves.";
			return false;
		}

		var saveId = !string.IsNullOrWhiteSpace( loadedSourceSaveId )
			? loadedSourceSaveId
			: GameSaveService.GetLastLoadedSaveId();

		if ( string.IsNullOrWhiteSpace( saveId ) )
		{
			message = "No loaded restore point is available.";
			return false;
		}

		var save = GameSaveService.ReadSave( saveId );
		if ( save is null )
		{
			message = "Could not read the last loaded save.";
			return false;
		}

		var assignments = BuildSeatAssignmentsForCurrentConnections( save );
		MatchBootstrap.PrepareLoadedGame( save, assignments, save.Summary.SaveId );
		GameSaveService.SetLastLoadedSaveId( save.Summary.SaveId );
		SceneFlow.LoadGame( Scene );
		return true;
	}

	public bool TryOverwriteCurrentManualSave( out string message )
	{
		message = "";

		if ( !Networking.IsHost )
		{
			message = "Only the host can overwrite saves.";
			return false;
		}

		var saveId = !string.IsNullOrWhiteSpace( currentManualSaveId )
			? currentManualSaveId
			: loadedSourceSaveId;

		if ( string.IsNullOrWhiteSpace( saveId ) )
		{
			message = "No loaded manual save is available to overwrite.";
			return false;
		}

		var existing = GameSaveService.ReadSave( saveId );
		if ( existing?.Summary is null || existing.Summary.SaveType != GameSaveType.Manual )
		{
			message = "Only manual restore points can be overwritten.";
			return false;
		}

		var replacement = BuildSaveFile( existing.Summary.DisplayName, GameSaveType.Manual );
		replacement.Summary.SaveId = existing.Summary.SaveId;
		replacement.Summary.CreatedAtUtc = existing.Summary.CreatedAtUtc;
		replacement.Summary.GameIdentifier = existing.Summary.GameIdentifier;
		if ( !GameSaveService.WriteSave( replacement, GameSaveType.Manual ) )
		{
			message = "Could not overwrite save file.";
			return false;
		}

		currentManualSaveId = replacement.Summary.SaveId;
		message = $"Overwrote save: {replacement.Summary.DisplayName}.";
		return true;
	}

	private bool TryStartLoadedGameFromBootstrap()
	{
		var bootstrap = MatchBootstrap.Current;
		if ( bootstrap?.HasLoadedGame != true || bootstrap.LoadedGame is null )
			return false;

		Config = MatchConfigSchema.Deserialize( bootstrap.LoadedGame.Snapshot.MatchConfigSnapshot );
		StartPrivateConfig();
		ApplyLoadedSnapshot( bootstrap.LoadedGame, bootstrap.LoadedSeatAssignments );
		loadedSourceSaveId = bootstrap.LoadedSourceSaveId ?? bootstrap.LoadedGame.Summary?.SaveId ?? "";
		currentManualSaveId = loadedSourceSaveId;
		hasLoadedRestorePoint = !string.IsNullOrWhiteSpace( loadedSourceSaveId );
		CurrentGameIdentifier = bootstrap.LoadedGame.Summary?.GameIdentifier;
		if ( string.IsNullOrWhiteSpace( CurrentGameIdentifier ) )
			CurrentGameIdentifier = GameSaveService.CreateGameIdentifier( bootstrap.LoadedGame.Summary?.PlayerNames );
		GameSaveService.SetLastLoadedSaveId( loadedSourceSaveId );
		MatchBootstrap.Clear();
		SendGlobalPopupToAll( "Game loaded", "The saved game is live.", PopupKind.Success, true, 4f );
		return true;
	}

	private void TryAutosaveStablePoint( string reason )
	{
		if ( !Networking.IsHost || Config?.AutosaveEnabled != true || Config?.AutosaveOnStableActions != true )
			return;

		if ( Time.Now - lastAutosaveAt < 1.0f )
			return;

		if ( !CanAutosaveNow() )
			return;

		var fingerprint = BuildSaveFingerprint();
		if ( string.Equals( fingerprint, lastAutosaveFingerprint, StringComparison.Ordinal ) )
			return;

		if ( TrySaveGame( reason, true, out _ ) )
		{
			lastAutosaveAt = Time.Now;
			lastAutosaveFingerprint = fingerprint;
		}
	}

	private bool CanAutosaveNow()
	{
		if ( MatchState is not (MatchLifecycleState.InGame or MatchLifecycleState.Paused or MatchLifecycleState.GameOver) )
			return false;

		if ( IsResolvingPhysicalDice || ActiveMovementRemainingSteps > 0 || IsGambleScreenActive )
			return false;

		return Phase is GamePhase.WaitingToRoll or GamePhase.WaitingForBuyDecision or GamePhase.Auctioning or GamePhase.TurnEnded;
	}

	private GameSaveFile BuildSaveFile( string slotName, GameSaveType saveType )
	{
		var displayName = string.IsNullOrWhiteSpace( slotName )
			? (saveType == GameSaveType.Manual ? "Manual Save" : "Autosave")
			: slotName.Trim();

		var snapshot = CaptureSaveSnapshot();
		if ( string.IsNullOrWhiteSpace( CurrentGameIdentifier ) )
			CurrentGameIdentifier = GameSaveService.CreateGameIdentifier( snapshot.Players.Where( player => player.OwnerId != 0 ).Select( player => player.PlayerName ).ToList() );

		var summary = new GameSaveSummary
		{
			DisplayName = displayName,
			SaveType = saveType,
			GameIdentifier = CurrentGameIdentifier,
			SourceSaveId = HasLoadedRestorePoint ? loadedSourceSaveId : "",
			SchemaVersion = GameSaveService.CurrentSchemaVersion,
			GameVersion = MonopolyApp.GameVersion,
			PlayerNames = snapshot.Players.Where( player => player.OwnerId != 0 ).Select( player => player.PlayerName ).ToList(),
			TurnSummary = BuildTurnSummary()
		};

		return new GameSaveFile
		{
			Summary = summary,
			Snapshot = snapshot
		};
	}

	private GameSaveSnapshot CaptureSaveSnapshot()
	{
		var now = Time.Now;
		return new GameSaveSnapshot
		{
			MatchConfigSnapshot = MatchConfigSchema.Serialize( Config ),
			MatchState = MatchState,
			Phase = Phase,
			WinnerPlayerIndex = WinnerPlayerIndex,
			GameElapsedSeconds = GameStartedAt <= 0f ? 0f : Math.Max( 0f, now - GameStartedAt ),
			StartingPlayerCount = StartingPlayerCount,
			CurrentPlayerIndex = CurrentPlayerIndex,
			LastDieA = LastDieA,
			LastDieB = LastDieB,
			IsResolvingPhysicalDice = false,
			PendingRollPlayerIndex = PendingRollPlayerIndex,
			PendingRollExecutionKind = PendingRollExecutionKind,
			PendingRollTotal = PendingRollTotal,
			PendingRollStatsRecorded = PendingRollStatsRecorded,
			PendingRollSuppressDoublesExtraTurn = PendingRollSuppressDoublesExtraTurn,
			PendingRollIsJailAttempt = PendingRollIsJailAttempt,
			PendingRollElapsedSeconds = PendingRollStartedAt <= 0f ? 0f : Math.Max( 0f, now - PendingRollStartedAt ),
			PreferredHostOwnerId = PreferredHostOwnerId,
			Players = CapturePlayerStates( now ),
			PropertyOwners = CaptureIntIntDictionary( PropertyOwners ),
			PropertyImprovements = CaptureIntIntDictionary( PropertyImprovements ),
			MortgagedProperties = CaptureIntBoolDictionary( MortgagedProperties ),
			PendingTrades = CaptureIntStringDictionary( PendingTrades ),
			TradeViewers = CaptureIntStringDictionary( TradeViewers ),
			ChatMessages = CaptureIntStringDictionary( ChatMessages ),
			StatsLogDiceFaceCounts = CaptureIntIntDictionary( StatsLogDiceFaceCounts ),
			PendingPurchaseSpaceIndex = PendingPurchaseSpaceIndex,
			AuctionSpaceIndex = AuctionSpaceIndex,
			AuctionCurrentBid = AuctionCurrentBid,
			AuctionHighBidderIndex = AuctionHighBidderIndex,
			AuctionRemainingSeconds = AuctionEndsAt <= 0f ? 0f : Math.Max( 0f, AuctionEndsAt - now ),
			NextTradeId = NextTradeId,
			NextChatMessageId = NextChatMessageId,
			FreeParkingBank = FreeParkingBank,
			CurrentTurnGetsExtraRoll = CurrentTurnGetsExtraRoll,
			CurrentTurnConsecutiveDoubles = CurrentTurnConsecutiveDoubles,
			CurrentTurnDoublesPlayerIndex = CurrentTurnDoublesPlayerIndex,
			CurrentTurnRemainingSeconds = CurrentTurnEndsAt <= 0f ? 0f : Math.Max( 0f, CurrentTurnEndsAt - now ),
			CurrentTurnReminderSoundsPlayed = CurrentTurnReminderSoundsPlayed,
			PendingForcedPaymentPlayerIndex = PendingForcedPaymentPlayerIndex,
			PendingForcedPaymentAmount = PendingForcedPaymentAmount,
			PendingForcedPaymentReceiverIndex = PendingForcedPaymentReceiverIndex,
			PendingForcedPaymentToBank = PendingForcedPaymentToBank,
			PendingForcedPaymentAddsToFreeParking = PendingForcedPaymentAddsToFreeParking,
			PendingForcedPaymentToEachPlayer = PendingForcedPaymentToEachPlayer,
			PendingForcedPaymentEachPlayerAmount = PendingForcedPaymentEachPlayerAmount,
			ActiveMovementPlayerIndex = ActiveMovementPlayerIndex,
			ActiveMovementRemainingSteps = ActiveMovementRemainingSteps,
			ActiveMovementGoPassCount = ActiveMovementGoPassCount,
			ActiveMovementTargetSpaceIndex = ActiveMovementTargetSpaceIndex,
			ActiveMovementElapsedSeconds = ActiveMovementLastProgressAt <= 0f ? 0f : Math.Max( 0f, now - ActiveMovementLastProgressAt ),
			PendingLandingPlayerIndex = PendingLandingPlayerIndex,
			PendingLandingSpaceIndex = PendingLandingSpaceIndex,
			PendingLandingGoPassCount = PendingLandingGoPassCount,
			PendingLandingResolved = PendingLandingResolved,
			ActiveGambleId = ActiveGambleId,
			ActiveGamblePlayerIndex = ActiveGamblePlayerIndex,
			ActiveGambleType = ActiveGambleType,
			ActiveGambleTitle = ActiveGambleTitle,
			ActiveGambleDescription = ActiveGambleDescription,
			ActiveGambleBetAmount = ActiveGambleBetAmount,
			ActiveGambleElapsedSeconds = ActiveGambleStartedAt <= 0f ? 0f : Math.Max( 0f, now - ActiveGambleStartedAt ),
			ActiveGambleRevealRemainingSeconds = ActiveGambleRevealAt <= 0f ? 0f : Math.Max( 0f, ActiveGambleRevealAt - now ),
			ActiveGambleResolved = ActiveGambleResolved,
			ActiveGambleWon = ActiveGambleWon,
			ActiveGambleResultSide = ActiveGambleResultSide,
			ActiveGambleResultMessage = ActiveGambleResultMessage,
			ActiveGambleEndsRemainingSeconds = ActiveGambleEndsAt <= 0f ? 0f : Math.Max( 0f, ActiveGambleEndsAt - now ),
			ChanceDrawPileCardKeys = chanceDrawPile.Select( card => card?.Key ?? "" ).Where( key => !string.IsNullOrWhiteSpace( key ) ).ToList(),
			CommunityChestDrawPileCardKeys = communityChestDrawPile.Select( card => card?.Key ?? "" ).Where( key => !string.IsNullOrWhiteSpace( key ) ).ToList()
		};
	}

	private List<GameSavePlayerState> CapturePlayerStates( float now )
	{
		return Players
			.Select( ( player, index ) => new { player, index } )
			.Where( entry => entry.player is not null )
			.Select( entry => new GameSavePlayerState
			{
				SeatIndex = entry.index,
				OwnerId = entry.player.OwnerId,
				SteamId = entry.player.SteamId,
				PlayerName = entry.player.PlayerName,
				SpaceIndex = entry.player.SpaceIndex,
				Money = entry.player.Money,
				IsInJail = entry.player.IsInJail,
				JailTurnsRemaining = entry.player.JailTurnsRemaining,
				ChanceGetOutOfJailFreeCards = entry.player.ChanceGetOutOfJailFreeCards,
				CommunityChestGetOutOfJailFreeCards = entry.player.CommunityChestGetOutOfJailFreeCards,
				ConsecutiveDoubles = entry.player.ConsecutiveDoubles,
				SkipsNextTurn = entry.player.SkipsNextTurn,
				IsReturningFromVacationCashBreak = entry.player.IsReturningFromVacationCashBreak,
				IsBankrupt = entry.player.IsBankrupt,
				IsDisconnected = entry.player.IsDisconnected,
				AbandonRemainingSeconds = entry.player.AbandonEndsAt <= 0f ? 0f : Math.Max( 0f, entry.player.AbandonEndsAt - now ),
				TurnTimeoutCount = entry.player.TurnTimeoutCount,
				ColorSlot = entry.player.ColorSlot,
				SelectedPieceId = entry.player.SelectedPieceId,
				SelectedDiceSkinId = entry.player.SelectedDiceSkinId
			} )
			.ToList();
	}

	private void ApplyLoadedSnapshot( GameSaveFile save, IReadOnlyList<LoadedSeatAssignment> assignments )
	{
		var snapshot = save.Snapshot;
		var now = Time.Now;
		var assignmentBySeat = assignments?
			.Where( assignment => assignment is not null )
			.ToDictionary( assignment => assignment.SeatIndex, assignment => assignment )
			?? new Dictionary<int, LoadedSeatAssignment>();

		EnsurePlayerSlots();
		ResetGameState( true );

		Config = MatchConfigSchema.Deserialize( snapshot.MatchConfigSnapshot );
		StartPrivateConfig();
		PreferredHostOwnerId = snapshot.PreferredHostOwnerId;
		PreferredHostDisconnected = false;
		MatchState = snapshot.MatchState == MatchLifecycleState.Lobby ? MatchLifecycleState.InGame : snapshot.MatchState;
		Phase = snapshot.Phase;
		WinnerPlayerIndex = snapshot.WinnerPlayerIndex;
		GameStartedAt = snapshot.GameElapsedSeconds <= 0f ? now : now - snapshot.GameElapsedSeconds;
		StartingPlayerCount = snapshot.StartingPlayerCount;
		CurrentPlayerIndex = snapshot.CurrentPlayerIndex;
		LastDieA = snapshot.LastDieA;
		LastDieB = snapshot.LastDieB;
		IsResolvingPhysicalDice = false;
		PendingRollPlayerIndex = snapshot.PendingRollPlayerIndex;
		PendingRollExecutionKind = snapshot.PendingRollExecutionKind;
		PendingRollTotal = snapshot.PendingRollTotal;
		PendingRollStatsRecorded = snapshot.PendingRollStatsRecorded;
		PendingRollSuppressDoublesExtraTurn = snapshot.PendingRollSuppressDoublesExtraTurn;
		PendingRollIsJailAttempt = snapshot.PendingRollIsJailAttempt;
		PendingRollStartedAt = snapshot.PendingRollElapsedSeconds <= 0f ? 0f : now - snapshot.PendingRollElapsedSeconds;
		PendingPurchaseSpaceIndex = snapshot.PendingPurchaseSpaceIndex;
		AuctionSpaceIndex = snapshot.AuctionSpaceIndex;
		AuctionCurrentBid = snapshot.AuctionCurrentBid;
		AuctionHighBidderIndex = snapshot.AuctionHighBidderIndex;
		AuctionEndsAt = snapshot.AuctionRemainingSeconds <= 0f ? 0f : now + snapshot.AuctionRemainingSeconds;
		NextTradeId = Math.Max( snapshot.NextTradeId, 1 );
		NextChatMessageId = Math.Max( snapshot.NextChatMessageId, 1 );
		FreeParkingBank = snapshot.FreeParkingBank;
		CurrentTurnGetsExtraRoll = snapshot.CurrentTurnGetsExtraRoll;
		CurrentTurnConsecutiveDoubles = snapshot.CurrentTurnConsecutiveDoubles;
		CurrentTurnDoublesPlayerIndex = snapshot.CurrentTurnDoublesPlayerIndex;
		CurrentTurnEndsAt = snapshot.CurrentTurnRemainingSeconds <= 0f ? 0f : now + snapshot.CurrentTurnRemainingSeconds;
		CurrentTurnReminderSoundsPlayed = snapshot.CurrentTurnReminderSoundsPlayed;
		PendingForcedPaymentPlayerIndex = snapshot.PendingForcedPaymentPlayerIndex;
		PendingForcedPaymentAmount = snapshot.PendingForcedPaymentAmount;
		PendingForcedPaymentReceiverIndex = snapshot.PendingForcedPaymentReceiverIndex;
		PendingForcedPaymentToBank = snapshot.PendingForcedPaymentToBank;
		PendingForcedPaymentAddsToFreeParking = snapshot.PendingForcedPaymentAddsToFreeParking;
		PendingForcedPaymentToEachPlayer = snapshot.PendingForcedPaymentToEachPlayer;
		PendingForcedPaymentEachPlayerAmount = snapshot.PendingForcedPaymentEachPlayerAmount;
		ActiveMovementPlayerIndex = snapshot.ActiveMovementPlayerIndex;
		ActiveMovementRemainingSteps = snapshot.ActiveMovementRemainingSteps;
		ActiveMovementGoPassCount = snapshot.ActiveMovementGoPassCount;
		ActiveMovementTargetSpaceIndex = snapshot.ActiveMovementTargetSpaceIndex;
		ActiveMovementLastProgressAt = snapshot.ActiveMovementElapsedSeconds <= 0f ? 0f : now - snapshot.ActiveMovementElapsedSeconds;
		PendingLandingPlayerIndex = snapshot.PendingLandingPlayerIndex;
		PendingLandingSpaceIndex = snapshot.PendingLandingSpaceIndex;
		PendingLandingGoPassCount = snapshot.PendingLandingGoPassCount;
		PendingLandingResolved = snapshot.PendingLandingResolved;
		ActiveGambleId = snapshot.ActiveGambleId;
		ActiveGamblePlayerIndex = snapshot.ActiveGamblePlayerIndex;
		ActiveGambleType = snapshot.ActiveGambleType;
		ActiveGambleTitle = snapshot.ActiveGambleTitle;
		ActiveGambleDescription = snapshot.ActiveGambleDescription;
		ActiveGambleBetAmount = snapshot.ActiveGambleBetAmount;
		ActiveGambleStartedAt = snapshot.ActiveGambleElapsedSeconds <= 0f ? 0f : now - snapshot.ActiveGambleElapsedSeconds;
		ActiveGambleRevealAt = snapshot.ActiveGambleRevealRemainingSeconds <= 0f ? 0f : now + snapshot.ActiveGambleRevealRemainingSeconds;
		ActiveGambleResolved = snapshot.ActiveGambleResolved;
		ActiveGambleWon = snapshot.ActiveGambleWon;
		ActiveGambleResultSide = snapshot.ActiveGambleResultSide;
		ActiveGambleResultMessage = snapshot.ActiveGambleResultMessage;
		ActiveGambleEndsAt = snapshot.ActiveGambleEndsRemainingSeconds <= 0f ? 0f : now + snapshot.ActiveGambleEndsRemainingSeconds;

		ApplyPlayerStates( snapshot, assignmentBySeat, now );
		ApplyIntIntDictionary( PropertyOwners, snapshot.PropertyOwners );
		ApplyIntIntDictionary( PropertyImprovements, snapshot.PropertyImprovements );
		ApplyIntBoolDictionary( MortgagedProperties, snapshot.MortgagedProperties );
		ApplyIntStringDictionary( PendingTrades, snapshot.PendingTrades );
		ApplyIntStringDictionary( TradeViewers, snapshot.TradeViewers );
		ApplyIntStringDictionary( ChatMessages, snapshot.ChatMessages );
		ApplyIntIntDictionary( StatsLogDiceFaceCounts, snapshot.StatsLogDiceFaceCounts );
		ApplyCardDrawPile( chanceDrawPile, Board?.ChanceCards, snapshot.ChanceDrawPileCardKeys );
		ApplyCardDrawPile( communityChestDrawPile, Board?.CommunityChestCards, snapshot.CommunityChestDrawPileCardKeys );

		SpawnTokensForPlayers( GetLobbyPlayers() );
	}

	private void ApplyPlayerStates( GameSaveSnapshot snapshot, Dictionary<int, LoadedSeatAssignment> assignmentBySeat, float now )
	{
		var abandonTimeoutSeconds = Math.Max( Config?.AbandonTimeoutSeconds ?? 180, 1 );
		foreach ( var savedPlayer in snapshot.Players )
		{
			var player = Players.ElementAtOrDefault( savedPlayer.SeatIndex );
			if ( player is null )
				continue;

			assignmentBySeat.TryGetValue( savedPlayer.SeatIndex, out var assignment );
			var ownerId = assignment?.AssignedOwnerId > 0 ? assignment.AssignedOwnerId : savedPlayer.OwnerId;
			var playerName = !string.IsNullOrWhiteSpace( assignment?.AssignedPlayerName ) ? assignment.AssignedPlayerName : savedPlayer.PlayerName;
			var hasConnection = Connection.All.Any( connection => connection.SteamId == ownerId );
			var keepDisconnected = assignment?.KeepDisconnected == true || !hasConnection;

			player.OwnerId = ownerId;
			player.SteamId = ownerId;
			player.PlayerName = string.IsNullOrWhiteSpace( playerName ) ? "Player" : playerName;
			player.SpaceIndex = savedPlayer.SpaceIndex;
			player.Money = savedPlayer.Money;
			player.IsInJail = savedPlayer.IsInJail;
			player.JailTurnsRemaining = savedPlayer.JailTurnsRemaining;
			player.ChanceGetOutOfJailFreeCards = savedPlayer.ChanceGetOutOfJailFreeCards;
			player.CommunityChestGetOutOfJailFreeCards = savedPlayer.CommunityChestGetOutOfJailFreeCards;
			player.ConsecutiveDoubles = savedPlayer.ConsecutiveDoubles;
			player.SkipsNextTurn = savedPlayer.SkipsNextTurn;
			player.IsReturningFromVacationCashBreak = savedPlayer.IsReturningFromVacationCashBreak;
			player.IsBankrupt = savedPlayer.IsBankrupt;
			player.IsReady = false;
			player.IsDisconnected = keepDisconnected || savedPlayer.IsDisconnected;
			player.AbandonEndsAt = player.IsDisconnected
				? now + Math.Max( savedPlayer.AbandonRemainingSeconds, abandonTimeoutSeconds )
				: 0f;
			player.TurnTimeoutCount = savedPlayer.TurnTimeoutCount;
			player.ColorSlot = savedPlayer.ColorSlot;
			player.SelectedPieceId = PieceCatalog.GetByIdOrDefault( savedPlayer.SelectedPieceId ).Id;
			player.SelectedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( savedPlayer.SelectedDiceSkinId ).Id;
		}
	}

	private List<LoadedSeatAssignment> BuildSeatAssignmentsForCurrentConnections( GameSaveFile save )
	{
		return save?.Snapshot?.Players?
			.Where( player => player is not null && player.OwnerId != 0 )
			.Select( player =>
			{
				var connection = Connection.All.FirstOrDefault( connection => connection.SteamId == player.OwnerId );
				return new LoadedSeatAssignment
				{
					SeatIndex = player.SeatIndex,
					AssignedOwnerId = player.OwnerId,
					AssignedPlayerName = connection?.Name ?? player.PlayerName,
					KeepDisconnected = connection is null
				};
			} )
			.ToList() ?? new();
	}

	private static List<GameSaveIntIntEntry> CaptureIntIntDictionary( NetDictionary<int, int> dictionary )
	{
		return dictionary.Select( entry => new GameSaveIntIntEntry { Key = entry.Key, Value = entry.Value } ).ToList();
	}

	private static List<GameSaveIntBoolEntry> CaptureIntBoolDictionary( NetDictionary<int, bool> dictionary )
	{
		return dictionary.Select( entry => new GameSaveIntBoolEntry { Key = entry.Key, Value = entry.Value } ).ToList();
	}

	private static List<GameSaveIntStringEntry> CaptureIntStringDictionary( NetDictionary<int, string> dictionary )
	{
		return dictionary.Select( entry => new GameSaveIntStringEntry { Key = entry.Key, Value = entry.Value } ).ToList();
	}

	private static void ApplyIntIntDictionary( NetDictionary<int, int> dictionary, IReadOnlyList<GameSaveIntIntEntry> entries )
	{
		dictionary.Clear();
		foreach ( var entry in entries ?? new List<GameSaveIntIntEntry>() )
			dictionary[entry.Key] = entry.Value;
	}

	private static void ApplyIntBoolDictionary( NetDictionary<int, bool> dictionary, IReadOnlyList<GameSaveIntBoolEntry> entries )
	{
		dictionary.Clear();
		foreach ( var entry in entries ?? new List<GameSaveIntBoolEntry>() )
			dictionary[entry.Key] = entry.Value;
	}

	private static void ApplyIntStringDictionary( NetDictionary<int, string> dictionary, IReadOnlyList<GameSaveIntStringEntry> entries )
	{
		dictionary.Clear();
		foreach ( var entry in entries ?? new List<GameSaveIntStringEntry>() )
			dictionary[entry.Key] = entry.Value ?? "";
	}

	private static void ApplyCardDrawPile( List<CardDef> drawPile, IReadOnlyList<CardDef> cardSource, IReadOnlyList<string> cardKeys )
	{
		drawPile.Clear();
		if ( cardSource is null || cardKeys is null )
			return;

		foreach ( var key in cardKeys )
		{
			var card = cardSource.FirstOrDefault( card => string.Equals( card?.Key, key, StringComparison.Ordinal ) );
			if ( card is not null )
				drawPile.Add( card );
		}
	}

	private string BuildTurnSummary()
	{
		var playerName = CurrentPlayer?.PlayerName ?? "No current player";
		return MatchState == MatchLifecycleState.GameOver && Winner is not null
			? $"{Winner.PlayerName} won"
			: $"{playerName} - {Phase}";
	}

	private string BuildSaveFingerprint()
	{
		return string.Join( "|",
			MatchState,
			Phase,
			CurrentPlayerIndex,
			Players.Sum( player => player?.Money ?? 0 ),
			PropertyOwners.Count,
			PropertyImprovements.Count,
			MortgagedProperties.Count,
			FreeParkingBank,
			NextTradeId,
			WinnerPlayerIndex );
	}
}
