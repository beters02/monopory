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

public sealed partial class MonopolyGame : Component
{
	[Property] public List<MonopolyPlayerState> Players { get; set; } = new();
	[Property] public MatchConfig Config { get; set; } = new();
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
	private readonly List<GamePopup> popups = new();
	private readonly List<GameObject> spawnedTokenObjects = new();
	private float pausedTurnRemainingSeconds;
	private float pausedAuctionRemainingSeconds;
	private bool currentRollDrewCard;

	public static MonopolyGame Instance => instance;
	public IReadOnlyList<GamePopup> Popups => popups;


	protected override void OnStart()
	{
		instance = this;
		SteamInviteBridge.Register( Scene );

		if ( !Networking.IsHost )
			return;

		var bootstrap = MatchBootstrap.Current;
		if ( bootstrap?.HasConfig == true )
			Config = bootstrap.Config;

		EnsurePlayerSlots();
		SyncLobbyConnections();
		ResetGameState( false );

		if ( bootstrap?.AutoStartGame == true )
		{
			TryStartGame( false );
			StartingPlayerCount = Math.Max( StartingPlayerCount, bootstrap.StartingPlayerCount );
			MatchBootstrap.Clear();
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
}
