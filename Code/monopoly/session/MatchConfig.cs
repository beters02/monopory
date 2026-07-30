using Sandbox;
using System;

public sealed class MatchSettingsPreset
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public string Snapshot { get; set; } = "";
		public bool IsPredefined { get; set; }
	}

[AttributeUsage( AttributeTargets.Field )]
public sealed class MatchConfigEnumDescriptionAttribute : Attribute
{
	public string Description { get; }

	public MatchConfigEnumDescriptionAttribute( string description )
	{
		Description = description ?? "";
	}
}

public enum MatchConfigDependencyComparison
{
	Equal,
	LessThan,
	OneOf
}

public enum UnownedAffordableLandingMode
{
	[MatchConfigEnumDescription( "The player automatically buys an affordable unowned property." )]
	ForceBuy,
	[MatchConfigEnumDescription( "The player chooses whether to buy the property or auction or skip it." )]
	Decision
}

public enum PlayerBankruptedPlayerMode
{
	[MatchConfigEnumDescription( "The creditor receives the bankrupt player's remaining properties." )]
	GivePropertiesToBankrupter,
	[MatchConfigEnumDescription( "The bankrupt player's properties return to the bank unowned." )]
	MakePropertiesUnowned
}

public enum PropertyProfitStatType
{
	[MatchConfigEnumDescription( "Profit includes every property the player owned during the match." )]
	EverOwned,
	[MatchConfigEnumDescription( "Profit includes only properties the player owns when the match ends." )]
	CurrentlyOwned
}

[AttributeUsage( AttributeTargets.Property, AllowMultiple = true )]
public sealed class MatchConfigDependsOnAttribute : Attribute
{
	public string OptionKey { get; }
	public string ExpectedValue { get; }
	public MatchConfigDependencyComparison Comparison { get; set; } = MatchConfigDependencyComparison.Equal;

	public MatchConfigDependsOnAttribute( string optionKey, string expectedValue = "true" )
	{
		OptionKey = optionKey;
		ExpectedValue = expectedValue;
	}
}

public enum VacationCashBehavior
{
	[MatchConfigEnumDescription( "Collect Vacation Cash without a turn penalty." )]
	PayoutOnly,
	[MatchConfigEnumDescription( "Collect Vacation Cash but lose any pending doubles reroll." )]
	LoseExtraRoll,
	[MatchConfigEnumDescription( "Collect Vacation Cash and skip the next rotation turn." )]
	SkipNextTurn
}

public enum VacationCashSkipNextTurnDoublesBehavior
{
	[MatchConfigEnumDescription( "Keep the doubles reroll now and skip the next rotation turn." )]
	UseRerollSkipNext,
	[MatchConfigEnumDescription( "Consume the doubles reroll without adding another skipped turn." )]
	ConsumeRerollOnly,
	[MatchConfigEnumDescription( "Cancel the doubles reroll and skip the next rotation turn." )]
	CancelAndSkipTurn
}

public enum DoublesPenalty
{
	[MatchConfigEnumDescription( "Send the player to Jail after three consecutive doubles rolls." )]
	GoToJail,
	[MatchConfigEnumDescription( "End the player's turn after three consecutive doubles rolls." )]
	EndTurn,
	[MatchConfigEnumDescription( "Apply no penalty for three consecutive doubles rolls." )]
	None
}

public enum PropertyManagementTiming
{
	[MatchConfigEnumDescription( "Players may manage properties only during their own turn." )]
	CurrentTurnOnly,
	[MatchConfigEnumDescription( "Players may manage properties during any player's turn." )]
	AnyTurn,
	[MatchConfigEnumDescription( "Players may manage properties only between active turns." )]
	BetweenTurnsOnly
}

public enum MaximumWagerType
{
	[MatchConfigEnumDescription( "The player's current cash is the maximum wager." )]
	PlayerMoney,
	[MatchConfigEnumDescription( "A percentage of the player's current cash is the maximum wager." )]
	PercentagePlayerMoney,
	[MatchConfigEnumDescription( "A configured fixed amount is the maximum wager." )]
	SetAmount
}

public enum InstantMoveBehavior
{
	[MatchConfigEnumDescription( "Token movement must play normally; no finish-movement button appears." )]
	NotAllowed,
	[MatchConfigEnumDescription( "Players may finish token movement immediately at any time." )]
	Allowed,
	[MatchConfigEnumDescription( "Players may finish token movement after the configured unlock time." )]
	AllowedAfterUnlock,
	[MatchConfigEnumDescription( "Every new token movement completes immediately." )]
	Forced,
	[MatchConfigEnumDescription( "The finish button is available before unlock; new movements are forced after unlock." )]
	AllowedUntilForcedAfterUnlock,
	[MatchConfigEnumDescription( "Movement plays normally before unlock; new movements are forced after unlock." )]
	NotAllowedUntilForcedAfterUnlock
}

public sealed class MatchConfigOptionAttribute : Attribute
{
	private object standaloneValue;

	public string Group { get; }
	public string Label { get; }
	public string Description { get; set; } = "";
	public string CommandId { get; set; } = "";
	public bool IsVisible { get; set; } = true;
	public GameCommandCheatType GameCommandCheatType { get; set; } = GameCommandCheatType.Host;
	public bool RequiresRestart { get; set; }
	public int Order { get; set; }
	public int Min { get; set; } = int.MinValue;
	public int Max { get; set; } = int.MaxValue;
	public int Step { get; set; } = 1;
	public bool HasStandaloneValue { get; private set; }
	public object StandaloneValue
	{
		get => standaloneValue;
		set
		{
			standaloneValue = value;
			HasStandaloneValue = true;
		}
	}

	public MatchConfigOptionAttribute( string group, string label )
	{
		Group = group;
		Label = label;
	}
}

public sealed class MatchConfig
{
	[MatchConfigOption( "Board Configuration", "Board Layout", Description = "Selected board layout definition id.", Order = -2, IsVisible = false )]
	public string BoardId { get; set; } = BoardCatalog.DefaultBoardId;

	[MatchConfigOption( "Board Configuration", "Space Names", Description = "Serialized board space name overrides.", Order = -1, IsVisible = false )]
	public string BoardSpaceNamesSnapshot { get; set; } = "";

	[MatchConfigOption( "Lobby", "Min Players", Description = "Minimum ready players required before the host can start.", CommandId = "min_players", Order = 0, Min = 1, Max = 24, Step = 1 )]
	public int MinPlayers { get; set; } = 1;

	[MatchConfigOption( "Lobby", "Max Players", Description = "Maximum seats allowed in the hosted lobby.", CommandId = "max_players", Order = 1, Min = 1, Max = 24, Step = 1 )]
	public int MaxPlayers { get; set; } = 8;

	[MatchConfigOption( "Lobby", "Only Host Starts Game", Description = "If enabled, only the host can launch the match.", CommandId = "only_host_starts_game", IsVisible = false, Order = 2 )]
	public bool OnlyHostStartsGame { get; set; } = true;

	[MatchConfigOption( "Lobby", "Abandon Timeout Seconds", Description = "How long disconnected players can rejoin before they are abandoned and removed.", CommandId = "abandon_timeout_seconds", Order = 3, Min = 15, Max = 1800, IsVisible = false, Step = 15 )]
	public int AbandonTimeoutSeconds { get; set; } = 180;

	[MatchConfigOption( "Lobby", "Autosave Enabled", Description = "Automatically saves the match at stable recovery points.", CommandId = "autosave_enabled", Order = 4, IsVisible = false )]
	public bool AutosaveEnabled { get; set; } = true;

	[MatchConfigOption( "Lobby", "Autosave On Stable Actions", Description = "Autosaves after safe turn, trade, property, and bankruptcy transitions.", CommandId = "autosave_on_stable_actions", Order = 5, IsVisible = false )]
	public bool AutosaveOnStableActions { get; set; } = true;

	[MatchConfigOption( "Property Rules", "Affordable Unowned Landing", Description = "What happens when a player can afford an unowned property.", CommandId = "affordable_unowned_landing", Order = 10 )]
	public UnownedAffordableLandingMode LandedUnownedCanAffordMode { get; set; } = UnownedAffordableLandingMode.Decision;


	[MatchConfigOption( "Property Rules", "Can Skip Unowned", Description = "Allows players to ignore an unowned property instead of buying or auctioning it.", CommandId = "can_skip_unowned", Order = 12 )]
	public bool CanSkipUnowned { get; set; } = false;

	[MatchConfigOption( "Property Rules", "Can Release Properties", Description = "Allows players to return an owned unmortgaged property to the bank for its mortgage value.", CommandId = "can_release_properties", Order = 13 )]
	public bool CanReleaseProperties { get; set; } = false;

	[MatchConfigOption( "Property Rules", "Property Management Timing", Description = "Controls when players may build, sell, mortgage, unmortgage, or release properties.", CommandId = "property_management_timing", Order = 14 )]
	public PropertyManagementTiming PropertyManagementTiming { get; set; } = PropertyManagementTiming.CurrentTurnOnly;

	[MatchConfigOption( "Economy", "Starting Money", Description = "Cash each player begins the game with.", CommandId = "starting_money", Order = 20, Min = 0, Max = 10000, Step = 100 )]
	public int StartingMoney { get; set; } = 1500;

	[MatchConfigOption( "Economy", "Land On GO Additional Money", Description = "Additional bonus paid on top of Pass GO Money when a move ends on GO.", CommandId = "land_on_go_money", Order = 21, Min = 0, Max = 5000, Step = 50 )]
	public int LandOnGoMoney { get; set; } = 100;

	[MatchConfigOption( "Economy", "Pass GO Money", Description = "Bonus for passing GO during movement.", CommandId = "pass_go_money", Order = 22, Min = 0, Max = 5000, Step = 50 )]
	public int PassGoMoney { get; set; } = 200;

	[MatchConfigOption( "Economy", "Snake Eyes Bonus Money", Description = "Bonus awarded when a player rolls snake eyes.", CommandId = "snake_eyes_bonus_money", Order = 23, Min = 0, Max = 5000, Step = 50 )]
	public int SnakeEyesBonusMoney { get; set; } = 0;

	[MatchConfigOption( "Economy", "Player Bankrupted Player Mode", Description = "What happens to properties when one player bankrupts another.", CommandId = "player_bankrupted_player_mode", Order = 24 )]
	public PlayerBankruptedPlayerMode PlayerBankruptedPlayerMode { get; set; } = PlayerBankruptedPlayerMode.GivePropertiesToBankrupter;

	[MatchConfigOption( "Economy", "Gamble Games Enabled Non-Card", Description = "Users can play gamble games during the game when it is not their turn.", CommandId = "gamble_games_enabled_non_card", Order = 25 )]
	public bool GambleGamesEnabledNonCard { get; set; } = true;

	[MatchConfigOption( "Economy", "Can Gamble Monopoly Money", Description = "Can gamble your in-game money in non-card gamble games.", CommandId = "can_gamble_monopoly_money", Order = 26 )]
	[MatchConfigDependsOn( nameof( GambleGamesEnabledNonCard ) )]
	public bool CanGambleMonopolyMoney { get; set; } = true;

	[MatchConfigOption( "Gambling", "Minimum Wager From Card", Description = "Minimum wager selected by a forced gamble card, capped by the player's effective maximum.", CommandId = "minimum_wager_from_card", Order = 27, Min = 0, Max = 10000, Step = 10 )]
	public int MinimumWagerFromCard { get; set; } = 1;

	[MatchConfigOption( "Gambling", "Maximum Wager Type", Description = "Controls how the maximum money wager is calculated.", CommandId = "maximum_wager_type", Order = 28 )]
	public MaximumWagerType MaximumWagerType { get; set; } = MaximumWagerType.PlayerMoney;

	[MatchConfigOption( "Gambling", "Maximum Wager Bank Percentage", Description = "Maximum percentage of current cash that may be wagered.", CommandId = "maximum_wager_bank_percentage", Order = 29, Min = 0, Max = 100, Step = 5 )]
	[MatchConfigDependsOn( nameof( MaximumWagerType ), nameof( MaximumWagerType.PercentagePlayerMoney ) )]
	public int MaximumWagerBankPercentage { get; set; } = 80;

	[MatchConfigOption( "Gambling", "Maximum Wager Set Amount", Description = "Maximum fixed wager, still capped by the player's current cash.", CommandId = "maximum_wager_set_amount", Order = 30, Min = 0, Max = 10000, Step = 10 )]
	[MatchConfigDependsOn( nameof( MaximumWagerType ), nameof( MaximumWagerType.SetAmount ) )]
	public int MaximumWagerSetAmount { get; set; } = 420;

	[MatchConfigOption( "Gambling", "Gamble Payout Multiplier", Description = "Gross return multiplier applied to a winning money wager. (Bet amount * multiplier)", CommandId = "gamble_payout_multiplier", Order = 31, Min = 1, Max = 20, Step = 1 )]
	public int GamblePayoutMultiplier { get; set; } = 2;

	[MatchConfigOption( "Leaderboard Stats", "Property Profit Scope", Description = "Choose whether property-profit leaderboard stats include every property a player has ever owned or only their current properties.", CommandId = "leaderboard_stat_properties_profit_type", IsVisible = false, Order = 50 )]
	public PropertyProfitStatType LeaderboardStatPropertiesProfitType { get; set; } = PropertyProfitStatType.EverOwned;
	[MatchConfigOption( "Economy", "Can Turn Player Gamble", Description = "Can the current turn player play non-card gamble games.", CommandId = "can_turn_player_gamble_non_card", Order = 27, IsVisible = false )]
	[MatchConfigDependsOn( nameof( GambleGamesEnabledNonCard ) )]
	public bool CanTurnPlayerGambleNonCard { get; set; } = false;

	[MatchConfigOption( "Turn Rules", "Doubles Goes Again", Description = "Lets players take another turn after rolling doubles.", CommandId = "doubles_goes_again", Order = 30 )]
	public bool DoublesGoesAgain { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Doubles Go Again Out Of Vacation Cash Break", Description = "If Doubles Goes Again and Vacation Cash are enabled, a player can get another turn from doubles after their Vacation Cash skipped turn.", CommandId = "doubles_go_again_out_of_vacation_cash_break", Order = 31 )]
	[MatchConfigDependsOn( nameof( DoublesGoesAgain ) )]
	[MatchConfigDependsOn( nameof( VacationCash ) )]
	[MatchConfigDependsOn( nameof( VacationCashBehavior ), nameof( VacationCashBehavior.SkipNextTurn ) )]
	public bool DoublesGoAgainOutOfVacationCashBreak { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Doubles Go Again Out Of Jail", Description = "If Doubles Goes Again is enabled, a player can get another turn from doubles on a roll made after leaving jail.", CommandId = "doubles_go_again_out_of_jail", Order = 32 )]
	[MatchConfigDependsOn( nameof( DoublesGoesAgain ) )]
	public bool DoublesGoAgainOutOfJail { get; set; } = false;

	[MatchConfigOption( "Turn Rules", "Snake Eyes Bonus On Third Doubles", Description = "If Doubles Goes Again is enabled, snake eyes still pays its bonus when it is the third doubles roll in a row.", CommandId = "snake_eyes_bonus_when_rolled_doubles_three_in_a_row", Order = 33 )]
	[MatchConfigDependsOn( nameof( DoublesGoesAgain ) )]
	public bool SnakeEyesBonusWhenRolledDoublesThreeInARow { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Doubles Penalty", Description = "Controls the penalty for rolling doubles three consecutive times.", CommandId = "doubles_penalty", Order = 34 )]
	[MatchConfigDependsOn( nameof( DoublesGoesAgain ) )]
	public DoublesPenalty DoublesPenalty { get; set; } = DoublesPenalty.GoToJail;

	[MatchConfigOption( "Turn Rules", "Get Out Of Jail Fine", Description = "Amount paid to leave Jail.", CommandId = "get_out_of_jail_fine", Order = 35, Min = 0, Max = 10000, Step = 10 )]
	public int GetOutOfJailFine { get; set; } = 50;

	[MatchConfigOption( "Turn Rules", "Jail Roll Attempts", Description = "Number of failed doubles attempts allowed before the final Jail resolution.", CommandId = "jail_roll_attempts", Order = 36, Min = 1, Max = 10, Step = 1 )]
	public int JailRollAttempts { get; set; } = 3;

	[MatchConfigOption( "Turn Rules", "Randomize Turn Order", Description = "Shuffles the starting player order at match start.", CommandId = "randomize_turn_order", Order = 37 )]
	public bool RandomizeTurnOrder { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Force Jail Fine After Failed Doubles", Description = "After the final failed jail roll, automatically pay the fine to leave jail.", CommandId = "force_jail_fine_after_failed_doubles", Order = 38 )]
	public bool ForceJailFineAfterFailedDoubles { get; set; } = false;

	[MatchConfigOption( "Turn Rules", "Turn Time Limit Seconds", Description = "How long each turn can last before timeout handling kicks in.", CommandId = "turn_time_limit_seconds", Order = 39, Min = 15, Max = 900, Step = 15, IsVisible = false)]
	public int TurnTimeLimitSeconds { get; set; } = 180;

	[MatchConfigOption( "Turn Rules", "Instant Move Behavior", Description = "Controls whether players may or must finish token movement immediately.", CommandId = "instant_move_behavior", Order = 40 )]
	public InstantMoveBehavior InstantMoveBehavior { get; set; } = InstantMoveBehavior.Allowed;

	[MatchConfigOption( "Turn Rules", "Instant Move Unlock Seconds", Description = "Elapsed match seconds before after-unlock instant movement becomes available or forced. 0 unlocks it immediately.", CommandId = "instant_move_unlock_seconds", Order = 41, Min = 0, Max = 14400, Step = 5 )]
	[MatchConfigDependsOn( nameof( InstantMoveBehavior ), "AllowedAfterUnlock|AllowedUntilForcedAfterUnlock|NotAllowedUntilForcedAfterUnlock", Comparison = MatchConfigDependencyComparison.OneOf )]
	public int InstantMoveUnlockSeconds { get; set; } = 0;

	[MatchConfigOption( "Turn Rules", "Auto BHop Enabled", Description = "Allows holding jump to automatically jump again when grounded.", CommandId = "auto_bhop_enabled", Order = 42, IsVisible = false )]
	public bool AutoBHopEnabled { get; set; } = true;
	
	[MatchConfigOption( "Turn Rules", "Token Camera Mode Can Always Control", Description = "Allows the local token camera controller to move even during that player's own turn.", CommandId = "token_camera_mode_can_always_control", Order = 43, IsVisible = false )]
	public bool TokenCameraModeCanAlwaysControl { get; set; } = true;

	[MatchConfigOption( "Board Rules", "Vacation Cash", Description = "Awards pooled cash when landing on Free Parking, if enabled.", CommandId = "vacation_cash", Order = 40 )]
	public bool VacationCash { get; set; } = true;

	[MatchConfigOption( "Board Rules", "Vacation Cash Minimum", Description = "Minimum cash kept in Vacation Cash while Vacation Cash is enabled.", CommandId = "vacation_cash_minimum", Order = 41, Min = 0, Max = 5000, Step = 50 )]
	[MatchConfigDependsOn( nameof( VacationCash ) )]
	public int VacationCashMinimum { get; set; } = 100;

	[MatchConfigOption( "Board Rules", "Vacation Cash Behavior", Description = "Controls the turn penalty applied after collecting Vacation Cash.", CommandId = "vacation_cash_behavior", Order = 42 )]
	[MatchConfigDependsOn( nameof( VacationCash ) )]
	public VacationCashBehavior VacationCashBehavior { get; set; } = VacationCashBehavior.SkipNextTurn;

	[MatchConfigOption( "Board Rules", "Vacation Cash Skip-Turn Doubles Behavior", Description = "Controls how a pending doubles reroll interacts with Vacation Cash's Skip Next Turn behavior.", CommandId = "vacation_cash_skip_next_turn_doubles_behavior", Order = 43 )]
	[MatchConfigDependsOn( nameof( VacationCash ) )]
	[MatchConfigDependsOn( nameof( VacationCashBehavior ), nameof( VacationCashBehavior.SkipNextTurn ) )]
	[MatchConfigDependsOn( nameof( DoublesGoesAgain ) )]
	public VacationCashSkipNextTurnDoublesBehavior VacationCashSkipNextTurnDoublesBehavior { get; set; } = VacationCashSkipNextTurnDoublesBehavior.ConsumeRerollOnly;

	[MatchConfigOption( "Board Rules", "Rent In Prison Percentage", Description = "Percentage of normal rent paid to a property owner while they are in Jail.", CommandId = "rent_in_prison_percentage", Order = 44, Min = 0, Max = 100, Step = 1 )]
	public int RentInPrisonPercentage { get; set; } = 100;

	[MatchConfigOption( "Board Rules", "Rent In Prison Leftover Money Goes To Vacation Cash", Description = "Charges full rent and adds the jailed owner's unpaid share to Vacation Cash.", CommandId = "rent_in_prison_leftover_money_goes_to_vacation_cash", Order = 45 )]
	[MatchConfigDependsOn( nameof( VacationCash ) )]
	[MatchConfigDependsOn( nameof( RentInPrisonPercentage ), "100", Comparison = MatchConfigDependencyComparison.LessThan )]
	public bool RentInPrisonLeftoverMoneyGoesToVacationCash { get; set; } = false;

	[MatchConfigOption( "Board Rules", "Even Build", Description = "Requires houses to be built evenly across a color set.", CommandId = "even_build", Order = 46 )]
	public bool EvenBuild { get; set; } = true;

	[MatchConfigOption( "Board Rules", "Buffed Utilities", Description = "Utility rent uses the total spaces moved during the turn instead of only the dice roll.", CommandId = "buffed_utilities", Order = 47 )]
	public bool BuffedUtilities { get; set; } = false;

	[MatchConfigOption( "Auctions", "Auction Initial Duration Seconds", Description = "Initial time available for bidding when an auction starts.", CommandId = "auction_initial_duration_seconds", Order = 60, Min = 1, Max = 300, Step = 1 )]
	public int AuctionInitialDurationSeconds { get; set; } = 15;

	[MatchConfigOption( "Auctions", "Auction Anti-Snipe Extension Seconds", Description = "Minimum time left after a valid bid; zero disables extension.", CommandId = "auction_anti_snipe_extension_seconds", Order = 61, Min = 0, Max = 60, Step = 1 )]
	public int AuctionAntiSnipeExtensionSeconds { get; set; } = 7;

	[MatchConfigOption( "Auctions", "Auction Starting Bid", Description = "Minimum valid opening bid.", CommandId = "auction_starting_bid", Order = 62, Min = 0, Max = 10000, Step = 10 )]
	public int AuctionStartingBid { get; set; } = 0;

	[MatchConfigOption( "Trades", "Sent Trade Limit", Description = "Maximum pending sent trades per player; zero allows unlimited pending trades.", CommandId = "sent_trade_limit", Order = 70, Min = 0, Max = 24, Step = 1 )]
	public int SentTradeLimit { get; set; } = 3;

	[MatchConfigOption( "Trades", "Allow Improved Property Trades", Description = "Allows properties with houses or hotels to be transferred in trades.", CommandId = "allow_improved_property_trades", Order = 71 )]
	public bool AllowImprovedPropertyTrades { get; set; } = false;

	[MatchConfigOption( "Trades", "Allow Mortgaged Property Trades", Description = "Allows mortgaged properties to be transferred in trades.", CommandId = "allow_mortgaged_property_trades", Order = 72 )]
	public bool AllowMortgagedPropertyTrades { get; set; } = true;

}

