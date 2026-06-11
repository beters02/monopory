using System;

public sealed partial class GameController : Component
{

	private void StartPrivateConfig()
	{
		double div = Config.TurnTimeLimitSeconds / MaxTurnReminders;
		SecondsBetweenTurnReminders = (int) Math.Ceiling(div);
		BankruptedPieceMaterial?.Preload();
	}
    
    // Jail
    private const int JailFineAmount = 50;
	private const int JailTurnCount = 3;
	private const float TurnSoundDelay = 0.5f;

    // Chat
    private const int MaxChatMessages = 80;
	private const int MaxChatMessageLength = 220;

    // Dice
    [Property, Group("Dice")] public DiceComponent DieA { get; set; }
	[Property, Group("Dice")] public DiceComponent DieB { get; set; }
	public Vector3 DiceThrowCenter { get; set; } = Vector3.Zero;
	public float DiceThrowHeight { get; set; } = 110f;
	public float DiceSpawnSpacing { get; set; } = 12f;
	public float DiceMinDropSpeed { get; set; } = 260f;
	public float DiceMaxDropSpeed { get; set; } = 540f;
	public float DiceMinHorizontalSpeed { get; set; } = 35f;
	public float DiceMaxHorizontalSpeed { get; set; } = 95f;
	public float DiceMinSpinSpeed { get; set; } = 12f;
	public float DiceMaxSpinSpeed { get; set; } = 30f;
	public float DiceSettleTimeout { get; set; } = 5f;
	public bool UsePhysicalDice { get; set; } = true;

	// Host Recovery
	private const float ResolvingSpaceRecoveryDelay = 2.5f;
	private const float RecoveryRetryDelay = 1.0f;

	// Selection
	public bool ShowSelectedSpaceCardOnGo { get; set; } = false;
	public bool ShowSelectedSpaceCardOnTax { get; set; } = false;
	public bool ShowSelectedSpaceCardOnJail { get; set; } = false;
	public bool ShowSelectedSpaceCardOnGoToJail { get; set; } = false;
	public bool ShowSelectedSpaceCardOnFreeParking { get; set; } = false;
	

	// Trades
	public bool ShowTradeReceivedPopup = true;
	public bool ShowTradeAcceptedPopup = true;
	public bool ShowTradeDeniedPopup = true;
	public bool ShowTradeNegotiationReceivedPopup = true;
	public int MaxPendingSentTradesPerPlayer = 3;
	public GameSound TradeReceivedSound = GameAssets.Sounds.TradeReceived; // retro 8
	public GameSound TradeAcceptedSound = GameAssets.Sounds.Success; // success
	public GameSound TradeDeniedSound = GameAssets.Sounds.Warning; // error
	public GameSound TradeNegotiationReceivedSound = GameAssets.Sounds.TradeNegotiated; // retro 9

	// Turns
	private int MaxTurnReminders { get; set; } = 4;
	private int SecondsBetweenTurnReminders { get; set; }

	// Popups
	public int MaxVisiblePopups { get; set; } = 3;

	// Tokens
	public bool RestrictPieceThrowToCurrentTurn { get; set; } = true;
	public GameMaterial BankruptedPieceMaterial { get; set; } = "materials/pieces/piece_bankrupt.vmat";

}
