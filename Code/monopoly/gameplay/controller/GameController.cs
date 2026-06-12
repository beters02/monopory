using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Sandbox;

public enum GamePhase
{
	WaitingToRoll,
	ResolvingSpace,
	WaitingForBuyDecision,
	Auctioning,
	TurnEnded,
	Error
}

public enum MatchLifecycleState
{
	Lobby,
	Starting,
	InGame,
	Paused,
	GameOver
}

public sealed partial class GameController : Component, Component.INetworkListener
{
	private enum ResolvedActionOutcome
	{
		StayInTurnEnded,
		AdvanceImmediately
	}

	private enum RollExecutionKind
	{
		Physical,
		ForcedAmount,
		JailRelease
	}

	private enum BankPaymentSource
	{
		Other,
		TaxSpace,
		ChanceOrCommunityChest,
		JailFine
	}

	[JsonIgnore] public List<PlayerState> Players { get; set; } = new();
	public MatchConfig Config { get; set; } = new();
	[Property] public GameObject TokenPrefab { get; set; }
	[Property] public MonopolyTheme Theme { get; set; }

	[Sync] public MatchLifecycleState MatchState { get; set; } = MatchLifecycleState.Lobby;
	[Sync] public int WinnerPlayerIndex { get; set; } = -1;
	[Sync] public float GameStartedAt { get; set; }
	[Sync] public int StartingPlayerCount { get; set; }
	[Sync] public int CurrentPlayerIndex { get; set; }
	[Sync] public int LastDieA { get; set; }
	[Sync] public int LastDieB { get; set; }
	[Sync] public bool IsResolvingPhysicalDice { get; set; }
	[Sync] public float PhysicalDiceStartedAt { get; set; }
	[Sync] public int PendingRollPlayerIndex { get; set; } = -1;
	[Sync] public int PendingRollExecutionKind { get; set; } = -1;
	[Sync] public int PendingRollTotal { get; set; }
	[Sync] public bool PendingRollStatsRecorded { get; set; }
	[Sync] public bool PendingRollSuppressDoublesExtraTurn { get; set; }
	[Sync] public bool PendingRollIsJailAttempt { get; set; }
	[Sync] public float PendingRollStartedAt { get; set; }
	[Sync] public long PreferredHostOwnerId { get; set; }
	[Sync] public bool PreferredHostDisconnected { get; set; }
	[Sync] public bool IsRecoveringHostState { get; set; }
	[Sync] public NetDictionary<int, int> PropertyOwners { get; set; } = new();
	[Sync] public NetDictionary<int, int> PropertyImprovements { get; set; } = new();
	[Sync] public NetDictionary<int, bool> MortgagedProperties { get; set; } = new();
	[Sync] public NetDictionary<int, string> PendingTrades { get; set; } = new();
	[Sync] public NetDictionary<int, string> TradeViewers { get; set; } = new();
	[Sync] public NetDictionary<int, string> TokenPhysicsStates { get; set; } = new();
	[Sync] public NetDictionary<int, string> ChatMessages { get; set; } = new();
	[Sync] public NetDictionary<int, int> StatsLogDiceFaceCounts { get; set; } = new();
	[Property] public Board Board { get; set; }

	public PlayerState CurrentPlayer =>
		Players.Count == 0 || CurrentPlayerIndex < 0 || CurrentPlayerIndex >= Players.Count ? null : Players[CurrentPlayerIndex];

	[Sync] public GamePhase Phase { get; set; } = GamePhase.WaitingToRoll;
	[Sync] public int PendingPurchaseSpaceIndex { get; set; } = -1;
	[Sync] public int AuctionSpaceIndex { get; set; } = -1;
	[Sync] public int AuctionCurrentBid { get; set; }
	[Sync] public int AuctionHighBidderIndex { get; set; } = -1;
	[Sync] public float AuctionEndsAt { get; set; }
	[Sync] public int NextTradeId { get; set; } = 1;
	[Sync] public int NextChatMessageId { get; set; } = 1;
	[Sync] public int FreeParkingBank { get; set; }
	[Sync] public bool CurrentTurnGetsExtraRoll { get; set; }
	[Sync] public int CurrentTurnConsecutiveDoubles { get; set; }
	[Sync] public int CurrentTurnDoublesPlayerIndex { get; set; } = -1;
	[Sync] public float CurrentTurnEndsAt { get; set; }
	[Sync] public int CurrentTurnReminderSoundsPlayed { get; set; } = 0;
	[Sync] public int PendingForcedPaymentPlayerIndex { get; set; } = -1;
	[Sync] public int PendingForcedPaymentAmount { get; set; }
	[Sync] public int PendingForcedPaymentReceiverIndex { get; set; } = -1;
	[Sync] public bool PendingForcedPaymentToBank { get; set; }
	[Sync] public bool PendingForcedPaymentAddsToFreeParking { get; set; }
	[Sync] public bool PendingForcedPaymentToEachPlayer { get; set; }
	[Sync] public int PendingForcedPaymentEachPlayerAmount { get; set; }
	[Sync] public int ActiveMovementPlayerIndex { get; set; } = -1;
	[Sync] public int ActiveMovementRemainingSteps { get; set; }
	[Sync] public int ActiveMovementGoPassCount { get; set; }
	[Sync] public int ActiveMovementTargetSpaceIndex { get; set; } = -1;
	[Sync] public float ActiveMovementLastProgressAt { get; set; }
	[Sync] public int PendingLandingPlayerIndex { get; set; } = -1;
	[Sync] public int PendingLandingSpaceIndex { get; set; } = -1;
	[Sync] public int PendingLandingGoPassCount { get; set; }
	[Sync] public bool PendingLandingResolved { get; set; }
	[Sync] public int ActiveGambleId { get; set; }
	[Sync] public int ActiveGamblePlayerIndex { get; set; } = -1;
	[Sync] public int ActiveGambleType { get; set; }
	[Sync] public string ActiveGambleTitle { get; set; } = "";
	[Sync] public string ActiveGambleDescription { get; set; } = "";
	[Sync] public int ActiveGambleBetAmount { get; set; }
	[Sync] public float ActiveGambleStartedAt { get; set; }
	[Sync] public float ActiveGambleRevealAt { get; set; }
	[Sync] public bool ActiveGambleResolved { get; set; }
	[Sync] public bool ActiveGambleWon { get; set; }
	[Sync] public string ActiveGambleResultSide { get; set; } = "";
	[Sync] public string ActiveGambleResultMessage { get; set; } = "";
	[Sync] public float ActiveGambleEndsAt { get; set; }

	public bool HasPendingForcedPayment =>
		PendingForcedPaymentPlayerIndex >= 0 && PendingForcedPaymentAmount > 0;

	public bool IsGambleScreenActive =>
		ActiveGamblePlayerIndex >= 0 && ActiveGambleEndsAt > Time.Now;

	public bool IsInGame => MatchState == MatchLifecycleState.InGame;
	public bool IsPaused => MatchState == MatchLifecycleState.Paused;
	public bool HasStarted => MatchState is MatchLifecycleState.InGame or MatchLifecycleState.Paused or MatchLifecycleState.GameOver;
	public bool CanStartGame => GetLobbyPlayers().Count >= MinPlayers && GetLobbyPlayers().All( player => player.IsReady );
	public int MinPlayers => Math.Max( Config?.MinPlayers ?? 2, 1 );
	public int MaxPlayers => Math.Max( Config?.MaxPlayers ?? Players.Count, MinPlayers );
	public PlayerState Winner =>
		WinnerPlayerIndex >= 0 ? Players.ElementAtOrDefault( WinnerPlayerIndex ) : null;

	private static GameController instance;
	private int nextPopupId = 1;
	private int nextLocalPopupId = -1;
	private readonly List<GamePopup> popups = new();
	private readonly Dictionary<int, Action> confirmPopupActions = new();
	private readonly Dictionary<int, Action> cancelPopupActions = new();
	private readonly List<GameObject> spawnedTokenObjects = new();
	private float pausedTurnRemainingSeconds;
	private float pausedAuctionRemainingSeconds;
	private float auctionPausedTurnRemainingSeconds;
	private bool isContinuingRecoveredMovement;
	private bool isRecoveringPendingRoll;
	private float lastRecoveryAttemptAt;
	private ResolvedActionOutcome resolvedActionOutcome = ResolvedActionOutcome.StayInTurnEnded;

	public static GameController Instance => instance;
	public IReadOnlyList<GamePopup> Popups => popups;
	public bool HasBlockingPopup => popups.Any( popup => popup.IsBlocking );

	public int LocalSelectedSpaceIndex { get; set; } = -1;
	public string LocalSelectedDrawnCardText { get; set; } = "";

	public float LastTimeCurrentTurnReminderPlayed { get; set; } = 0f;


	protected override void OnStart()
	{
		instance = this;
		GameAssets.PrewarmUiAssets();
		SteamInviteBridge.Register( Scene );
		RefreshReplicatedPlayerSlots();

		if ( Theme is null )
			Theme = Scene.GetAllComponents<MonopolyTheme>().FirstOrDefault();

		if ( Theme is null )
			Theme = Components.GetOrCreate<MonopolyTheme>();

		if ( !Networking.IsHost )
		{
			LoadingState.Hide();
			return;
		}

		BeginServerLoading( "Preparing game", "The host is setting up the table state." );
		try
		{
			var bootstrap = MatchBootstrap.Current;
			if ( bootstrap?.HasConfig == true )
				Config = bootstrap.Config;

			StartPrivateConfig();
			EnsurePreferredHostOwnerId();
			EnsurePlayerSlots();
			ResetGameState( false );
			MatchState = MatchLifecycleState.Lobby;
			SyncLobbyConnections();
			
			TryStartBootstrappedGame();

			Log.Info(MaxTurnReminders);
		}
		finally
		{
			ClearServerLoading();
			LoadingState.Hide();
		}
	}

	protected override void OnUpdate()
	{
		RefreshReplicatedPlayerSlots();
		UpdatePopups();
		UpdateVisualTokens();
		RecoverPendingPurchaseSelection();
		UpdateServerLoadingWatchdog();

		if ( !Networking.IsHost )
			return;

		SyncLobbyConnections();

		if ( MatchState == MatchLifecycleState.Lobby )
			TryStartBootstrappedGame();

		if ( MatchState != MatchLifecycleState.InGame )
			return;

		RemoveInvalidTrades();
		UpdateHostRecoveryWatchdog();
		UpdateAuction();
		UpdateTurnTimer();
		CheckForGameOver();
	}

	private void TryStartBootstrappedGame()
	{
		var bootstrap = MatchBootstrap.Current;
		if ( bootstrap?.AutoStartGame != true )
			return;

		if ( bootstrap.HasLoadedGame )
		{
			if ( TryStartLoadedGameFromBootstrap() )
				return;
		}

		var expectedPlayerCount = Math.Max( bootstrap.StartingPlayerCount, MinPlayers );
		if ( GetLobbyPlayers().Count < expectedPlayerCount )
			return;

		if ( TryStartGame( false ) )
			MatchBootstrap.Clear();
	}
}

