using Sandbox;
using System;

public enum GameSaveType
{
	Manual,
	Autosave
}

public sealed class GameSaveSummary
{
	public string SaveId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public GameSaveType SaveType { get; set; } = GameSaveType.Manual;
	public string GameIdentifier { get; set; } = "";
	public string SourceSaveId { get; set; } = "";
	public string CreatedAtUtc { get; set; } = "";
	public string UpdatedAtUtc { get; set; } = "";
	public string GameVersion { get; set; } = "";
	public int SchemaVersion { get; set; }
	public List<string> PlayerNames { get; set; } = new();
	public string TurnSummary { get; set; } = "";
	public bool IsLastLoadedRestorePoint { get; set; }
}

public sealed class GameSaveFile
{
	public GameSaveSummary Summary { get; set; } = new();
	public GameSaveSnapshot Snapshot { get; set; } = new();
}

public sealed class GameSaveSnapshot
{
	public string MatchConfigSnapshot { get; set; } = "";
	public string MatchConfigDefaultSnapshot { get; set; } = "";
	public MatchLifecycleState MatchState { get; set; } = MatchLifecycleState.InGame;
	public GamePhase Phase { get; set; } = GamePhase.WaitingToRoll;
	public int WinnerPlayerIndex { get; set; } = -1;
	public float GameElapsedSeconds { get; set; }
	public int StartingPlayerCount { get; set; }
	public int CurrentPlayerIndex { get; set; }
	public int LastDieA { get; set; }
	public int LastDieB { get; set; }
	public bool IsResolvingPhysicalDice { get; set; }
	public int PendingRollPlayerIndex { get; set; } = -1;
	public int PendingRollExecutionKind { get; set; } = -1;
	public int PendingRollTotal { get; set; }
	public bool PendingRollStatsRecorded { get; set; }
	public bool PendingRollSuppressDoublesExtraTurn { get; set; }
	public bool PendingRollIsJailAttempt { get; set; }
	public float PendingRollElapsedSeconds { get; set; }
	public long PreferredHostOwnerId { get; set; }
	public List<GameSavePlayerState> Players { get; set; } = new();
	public List<GameSaveIntIntEntry> PropertyOwners { get; set; } = new();
	public List<GameSaveIntIntEntry> PropertyImprovements { get; set; } = new();
	public List<GameSaveIntBoolEntry> MortgagedProperties { get; set; } = new();
	public List<GameSaveIntStringEntry> PendingTrades { get; set; } = new();
	public List<GameSaveIntStringEntry> TradeHistory { get; set; } = new();
	public List<GameSaveIntStringEntry> TradeViewers { get; set; } = new();
	public List<GameSaveIntStringEntry> TradeEditors { get; set; } = new();
	public List<GameSaveIntStringEntry> ChatMessages { get; set; } = new();
	public List<GameSaveIntIntEntry> StatsLogDiceFaceCounts { get; set; } = new();
	public bool CheatsEnabledEver { get; set; }
	public bool AdminCommandUsedEver { get; set; }
	public bool MatchConfigChangedAfterStart { get; set; }
	public string DiceCommitmentHash { get; set; } = "";
	public string RevealedSeed { get; set; } = "";
	public string PrivateDiceSeed { get; set; } = "";
	public int NextDiceRollIndex { get; set; }
	public int NextAdminHistoryId { get; set; } = 1;
	public int NextCommandHistoryId { get; set; } = 1;
	public int NextMoveHistoryTurnNumber { get; set; } = 1;
	public List<GameSaveIntStringEntry> DiceHistory { get; set; } = new();
	public List<GameSaveIntStringEntry> AdminHistory { get; set; } = new();
	public List<GameSaveIntStringEntry> CommandHistory { get; set; } = new();
	public List<GameSaveIntStringEntry> MoveHistory { get; set; } = new();
	public List<GameSaveIntStringEntry> GambleHistory { get; set; } = new();
	public List<GameSaveIntStringEntry> GambleSessions { get; set; } = new();
	public int NextGambleHistoryId { get; set; } = 1;
	public int NextGambleSessionId { get; set; } = 1;
	public List<GameSaveIntIntEntry> PropertyLandingCounts { get; set; } = new();
	public List<GameSaveIntIntEntry> PropertyRentEarned { get; set; } = new();
	public int PendingPurchaseSpaceIndex { get; set; } = -1;
	public int AuctionSpaceIndex { get; set; } = -1;
	public int AuctionCurrentBid { get; set; }
	public int AuctionHighBidderIndex { get; set; } = -1;
	public float AuctionRemainingSeconds { get; set; }
	public int NextTradeId { get; set; } = 1;
	public int NextTradeHistoryId { get; set; } = 1;
	public int NextChatMessageId { get; set; } = 1;
	public int FreeParkingBank { get; set; }
	public bool CurrentTurnGetsExtraRoll { get; set; }
	public int CurrentTurnConsecutiveDoubles { get; set; }
	public int CurrentTurnDoublesPlayerIndex { get; set; } = -1;
	public float CurrentTurnRemainingSeconds { get; set; }
	public int CurrentTurnReminderSoundsPlayed { get; set; }
	public int PendingForcedPaymentPlayerIndex { get; set; } = -1;
	public int PendingForcedPaymentAmount { get; set; }
	public int PendingForcedPaymentReceiverIndex { get; set; } = -1;
	public bool PendingForcedPaymentToBank { get; set; }
	public bool PendingForcedPaymentAddsToFreeParking { get; set; }
	public bool PendingForcedPaymentToEachPlayer { get; set; }
	public int PendingForcedPaymentEachPlayerAmount { get; set; }
	public int ActiveMovementPlayerIndex { get; set; } = -1;
	public int ActiveMovementRemainingSteps { get; set; }
	public int ActiveMovementGoPassCount { get; set; }
	public int ActiveMovementTargetSpaceIndex { get; set; } = -1;
	public float ActiveMovementElapsedSeconds { get; set; }
	public int PendingLandingPlayerIndex { get; set; } = -1;
	public int PendingLandingSpaceIndex { get; set; } = -1;
	public int PendingLandingGoPassCount { get; set; }
	public bool PendingLandingResolved { get; set; }
	public int ActiveGambleId { get; set; }
	public int ActiveGamblePlayerIndex { get; set; } = -1;
	public int ActiveGambleType { get; set; }
	public string ActiveGambleTitle { get; set; } = "";
	public string ActiveGambleDescription { get; set; } = "";
	public int ActiveGambleBetAmount { get; set; }
	public float ActiveGambleElapsedSeconds { get; set; }
	public float ActiveGambleRevealRemainingSeconds { get; set; }
	public bool ActiveGambleResolved { get; set; }
	public bool ActiveGambleWon { get; set; }
	public string ActiveGambleResultSide { get; set; } = "";
	public string ActiveGambleResultMessage { get; set; } = "";
	public float ActiveGambleEndsRemainingSeconds { get; set; }
	public List<string> ChanceDrawPileCardKeys { get; set; } = new();
	public List<string> CommunityChestDrawPileCardKeys { get; set; } = new();
}

public sealed class GameSavePlayerState
{
	public int SeatIndex { get; set; }
	public long OwnerId { get; set; }
	public long SteamId { get; set; }
	public string PlayerName { get; set; } = "Player";
	public int SpaceIndex { get; set; }
	public int Money { get; set; }
	public bool IsInJail { get; set; }
	public int JailTurnsRemaining { get; set; }
	public int ChanceGetOutOfJailFreeCards { get; set; }
	public int CommunityChestGetOutOfJailFreeCards { get; set; }
	public int ConsecutiveDoubles { get; set; }
	public bool SkipsNextTurn { get; set; }
	public bool IsReturningFromVacationCashBreak { get; set; }
	public bool IsBankrupt { get; set; }
	public bool IsDisconnected { get; set; }
	public float AbandonRemainingSeconds { get; set; }
	public int TurnTimeoutCount { get; set; }
	public int ColorSlot { get; set; } = -1;
	public string SelectedPieceId { get; set; } = PieceCatalog.DefaultPieceId;
	public string SelectedDiceSkinId { get; set; } = DiceSkinCatalog.DefaultDiceSkinId;
}

public sealed class LoadedSeatAssignment
{
	public int SeatIndex { get; set; }
	public long AssignedOwnerId { get; set; }
	public string AssignedPlayerName { get; set; } = "";
	public bool KeepDisconnected { get; set; }
}

public sealed class GameSaveIntIntEntry
{
	public int Key { get; set; }
	public int Value { get; set; }
}

public sealed class GameSaveIntBoolEntry
{
	public int Key { get; set; }
	public bool Value { get; set; }
}

public sealed class GameSaveIntStringEntry
{
	public int Key { get; set; }
	public string Value { get; set; } = "";
}
