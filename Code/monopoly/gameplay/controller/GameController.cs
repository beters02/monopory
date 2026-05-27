using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public enum GamePhase
{
	WaitingToRoll,
	ResolvingSpace,
	WaitingForBuyDecision,
	Auctioning,
	TurnEnded
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
		ChanceOrCommunityChest
	}

	[Property] public List<PlayerState> Players { get; set; } = new();
	public MatchConfig Config { get; set; } = new();
	[Property] public GameObject TokenPrefab { get; set; }
	[Property] public MonopolyTheme Theme { get; set; }

	[Property, Sync] public MatchLifecycleState MatchState { get; set; } = MatchLifecycleState.Lobby;
	[Property, Sync] public int WinnerPlayerIndex { get; set; } = -1;
	[Property, Sync] public float GameStartedAt { get; set; }
	[Property, Sync] public int StartingPlayerCount { get; set; }
	[Property, Sync] public int CurrentPlayerIndex { get; set; }
	[Property, Sync] public int LastDieA { get; set; }
	[Property, Sync] public int LastDieB { get; set; }
	[Property, Sync] public bool IsResolvingPhysicalDice { get; set; }
	[Property, Sync] public float PhysicalDiceStartedAt { get; set; }
	[Property, Sync] public int PendingRollPlayerIndex { get; set; } = -1;
	[Property, Sync] public int PendingRollExecutionKind { get; set; } = -1;
	[Property, Sync] public int PendingRollTotal { get; set; }
	[Property, Sync] public bool PendingRollSuppressDoublesExtraTurn { get; set; }
	[Property, Sync] public bool PendingRollIsJailAttempt { get; set; }
	[Property, Sync] public float PendingRollStartedAt { get; set; }
	[Property, Sync] public long PreferredHostOwnerId { get; set; }
	[Property, Sync] public bool PreferredHostDisconnected { get; set; }
	[Property, Sync] public bool IsRecoveringHostState { get; set; }
	[Property, Sync] public NetDictionary<int, int> PropertyOwners { get; set; } = new();
	[Property, Sync] public NetDictionary<int, int> PropertyImprovements { get; set; } = new();
	[Property, Sync] public NetDictionary<int, bool> MortgagedProperties { get; set; } = new();
	[Property, Sync] public NetDictionary<int, string> PendingTrades { get; set; } = new();
	[Property, Sync] public NetDictionary<int, string> TradeViewers { get; set; } = new();
	[Property, Sync] public NetDictionary<int, string> TokenPhysicsStates { get; set; } = new();
	[Property, Sync] public NetDictionary<int, string> ChatMessages { get; set; } = new();
	[Property] public Board Board { get; set; }

	public PlayerState CurrentPlayer =>
		Players.Count == 0 || CurrentPlayerIndex < 0 || CurrentPlayerIndex >= Players.Count ? null : Players[CurrentPlayerIndex];

	[Property, Sync] public GamePhase Phase { get; set; } = GamePhase.WaitingToRoll;
	[Property, Sync] public int PendingPurchaseSpaceIndex { get; set; } = -1;
	[Property, Sync] public int AuctionSpaceIndex { get; set; } = -1;
	[Property, Sync] public int AuctionCurrentBid { get; set; }
	[Property, Sync] public int AuctionHighBidderIndex { get; set; } = -1;
	[Property, Sync] public float AuctionEndsAt { get; set; }
	[Property, Sync] public int NextTradeId { get; set; } = 1;
	[Property, Sync] public int NextChatMessageId { get; set; } = 1;
	[Property, Sync] public int FreeParkingBank { get; set; }
	[Property, Sync] public bool CurrentTurnGetsExtraRoll { get; set; }
	[Property, Sync] public int CurrentTurnConsecutiveDoubles { get; set; }
	[Property, Sync] public int CurrentTurnDoublesPlayerIndex { get; set; } = -1;
	[Property, Sync] public float CurrentTurnEndsAt { get; set; }
	[Property, Sync] public int PendingForcedPaymentPlayerIndex { get; set; } = -1;
	[Property, Sync] public int PendingForcedPaymentAmount { get; set; }
	[Property, Sync] public int PendingForcedPaymentReceiverIndex { get; set; } = -1;
	[Property, Sync] public bool PendingForcedPaymentToBank { get; set; }
	[Property, Sync] public bool PendingForcedPaymentAddsToFreeParking { get; set; }
	[Property, Sync] public bool PendingForcedPaymentToEachPlayer { get; set; }
	[Property, Sync] public int PendingForcedPaymentEachPlayerAmount { get; set; }
	[Property, Sync] public int ActiveMovementPlayerIndex { get; set; } = -1;
	[Property, Sync] public int ActiveMovementRemainingSteps { get; set; }
	[Property, Sync] public int ActiveMovementGoPassCount { get; set; }
	[Property, Sync] public int ActiveMovementTargetSpaceIndex { get; set; } = -1;
	[Property, Sync] public float ActiveMovementLastProgressAt { get; set; }
	[Property, Sync] public int PendingLandingPlayerIndex { get; set; } = -1;
	[Property, Sync] public int PendingLandingSpaceIndex { get; set; } = -1;
	[Property, Sync] public int PendingLandingGoPassCount { get; set; }
	[Property, Sync] public bool PendingLandingResolved { get; set; }

	public bool HasPendingForcedPayment =>
		PendingForcedPaymentPlayerIndex >= 0 && PendingForcedPaymentAmount > 0;

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
	private readonly List<GamePopup> popups = new();
	private readonly List<GameObject> spawnedTokenObjects = new();
	private float pausedTurnRemainingSeconds;
	private float pausedAuctionRemainingSeconds;
	private bool isContinuingRecoveredMovement;
	private bool isRecoveringPendingRoll;
	private float lastRecoveryAttemptAt;
	private ResolvedActionOutcome resolvedActionOutcome = ResolvedActionOutcome.StayInTurnEnded;

	public static GameController Instance => instance;
	public IReadOnlyList<GamePopup> Popups => popups;


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
			return;

		var bootstrap = MatchBootstrap.Current;
		if ( bootstrap?.HasConfig == true )
			Config = bootstrap.Config;

		EnsurePreferredHostOwnerId();
		EnsurePlayerSlots();
		ResetGameState( false );
		MatchState = MatchLifecycleState.Lobby;
		SyncLobbyConnections();

		TryStartBootstrappedGame();
	}

	protected override void OnUpdate()
	{
		RefreshReplicatedPlayerSlots();
		UpdatePopups();
		UpdateVisualTokens();
		RecoverPendingPurchaseSelection();

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

		var expectedPlayerCount = Math.Max( bootstrap.StartingPlayerCount, MinPlayers );
		if ( GetLobbyPlayers().Count < expectedPlayerCount )
			return;

		if ( TryStartGame( false ) )
			MatchBootstrap.Clear();
	}
}

