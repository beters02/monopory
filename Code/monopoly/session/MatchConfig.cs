using Sandbox;
using System;

public sealed class MatchSettingsPreset
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public string Snapshot { get; set; } = "";
		public bool IsPredefined { get; set; }
	}

public enum UnownedAffordableLandingMode
{
	ForceBuy,
	Decision
}

public enum UnownedUnaffordableLandingMode
{
	ForceAuction,
	Decision
}

public enum PlayerBankruptedPlayerMode
{
	GivePropertiesToBankrupter,
	MakePropertiesUnowned
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

	[MatchConfigOption( "Lobby", "Only Host Starts Game", Description = "If enabled, only the host can launch the match.", CommandId = "only_host_starts_game", Order = 2 )]
	public bool OnlyHostStartsGame { get; set; } = true;

	[MatchConfigOption( "Lobby", "Abandon Timeout Seconds", Description = "How long disconnected players can rejoin before they are abandoned and removed.", CommandId = "abandon_timeout_seconds", Order = 3, Min = 15, Max = 1800, Step = 15 )]
	public int AbandonTimeoutSeconds { get; set; } = 180;

	[MatchConfigOption( "Lobby", "Autosave Enabled", Description = "Automatically saves the match at stable recovery points.", CommandId = "autosave_enabled", Order = 4, IsVisible = false )]
	public bool AutosaveEnabled { get; set; } = true;

	[MatchConfigOption( "Lobby", "Autosave On Stable Actions", Description = "Autosaves after safe turn, trade, property, and bankruptcy transitions.", CommandId = "autosave_on_stable_actions", Order = 5, IsVisible = false )]
	public bool AutosaveOnStableActions { get; set; } = true;

	[MatchConfigOption( "Property Rules", "Affordable Unowned Landing", Description = "What happens when a player can afford an unowned property.", CommandId = "affordable_unowned_landing", Order = 10 )]
	public UnownedAffordableLandingMode LandedUnownedCanAffordMode { get; set; } = UnownedAffordableLandingMode.Decision;

	[MatchConfigOption( "Property Rules", "Unaffordable Unowned Landing", Description = "What happens when a player cannot afford an unowned property.", CommandId = "unaffordable_unowned_landing", Order = 11 )]
	public UnownedUnaffordableLandingMode LandedUnownedCantAffordMode { get; set; } = UnownedUnaffordableLandingMode.ForceAuction;

	[MatchConfigOption( "Property Rules", "Can Skip Unowned", Description = "Allows players to ignore an unowned property instead of buying or auctioning it.", CommandId = "can_skip_unowned", Order = 12 )]
	public bool CanSkipUnowned { get; set; } = false;

	[MatchConfigOption( "Property Rules", "Can Release Properties", Description = "Allows players to return an owned unmortgaged property to the bank for its mortgage value.", CommandId = "can_release_properties", Order = 13 )]
	public bool CanReleaseProperties { get; set; } = false;

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
	public bool CanGambleMonopolyMoney { get; set; } = true;
	[MatchConfigOption( "Economy", "Can Turn Player Gamble", Description = "Can the current turn player play non-card gamble games.", CommandId = "can_turn_player_gamble_non_card", Order = 27, IsVisible = false )]
	public bool CanTurnPlayerGambleNonCard { get; set; } = false;

	[MatchConfigOption( "Turn Rules", "Doubles Goes Again", Description = "Lets players take another turn after rolling doubles.", CommandId = "doubles_goes_again", Order = 30 )]
	public bool DoublesGoesAgain { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Doubles Go Again Out Of Vacation Cash Break", Description = "If Doubles Goes Again and Vacation Cash are enabled, a player can get another turn from doubles after their Vacation Cash skipped turn.", CommandId = "doubles_go_again_out_of_vacation_cash_break", Order = 31 )]
	public bool DoublesGoAgainOutOfVacationCashBreak { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Doubles Go Again Out Of Jail", Description = "If Doubles Goes Again is enabled, a player can get another turn from doubles on a roll made after leaving jail.", CommandId = "doubles_go_again_out_of_jail", Order = 32 )]
	public bool DoublesGoAgainOutOfJail { get; set; } = false;

	[MatchConfigOption( "Turn Rules", "Snake Eyes Bonus On Third Doubles", Description = "If Doubles Goes Again is enabled, snake eyes still pays its bonus when it is the third doubles roll in a row.", CommandId = "snake_eyes_bonus_when_rolled_doubles_three_in_a_row", Order = 33 )]
	public bool SnakeEyesBonusWhenRolledDoublesThreeInARow { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Randomize Turn Order", Description = "Shuffles the starting player order at match start.", CommandId = "randomize_turn_order", Order = 34 )]
	public bool RandomizeTurnOrder { get; set; } = true;

	[MatchConfigOption( "Turn Rules", "Force Jail Fine After Failed Doubles", Description = "After the final failed jail roll, automatically pay the fine to leave jail.", CommandId = "force_jail_fine_after_failed_doubles", Order = 35 )]
	public bool ForceJailFineAfterFailedDoubles { get; set; } = false;

	[MatchConfigOption( "Turn Rules", "Turn Time Limit Seconds", Description = "How long each turn can last before timeout handling kicks in.", CommandId = "turn_time_limit_seconds", Order = 36, Min = 15, Max = 900, Step = 15 )]
	public int TurnTimeLimitSeconds { get; set; } = 180;

	[MatchConfigOption( "Turn Rules", "Instant Move Button Unlock Minutes", Description = "Elapsed match minutes before the finish-movement button can appear. 0 allows it immediately.", CommandId = "instant_move_button_unlock_minutes", Order = 37, Min = 0, Max = 240, Step = 5 )]
	public int InstantMoveButtonUnlockMinutes { get; set; } = 0;

	[MatchConfigOption( "Turn Rules", "Instant Move Always", Description = "Automatically finishes token movement after dice resolve.", CommandId = "instant_move_always", Order = 38 )]
	public bool InstantMoveAlways { get; set; } = false;

	[MatchConfigOption( "Turn Rules", "Auto BHop Enabled", Description = "Allows holding jump to automatically jump again when grounded.", CommandId = "auto_bhop_enabled", Order = 39, IsVisible = false )]
	public bool AutoBHopEnabled { get; set; } = true;
	
	[MatchConfigOption( "Turn Rules", "Token Camera Mode Can Always Control", Description = "Allows the local token camera controller to move even during that player's own turn.", CommandId = "token_camera_mode_can_always_control", Order = 40, IsVisible = false )]
	public bool TokenCameraModeCanAlwaysControl { get; set; } = true;

	[MatchConfigOption( "Board Rules", "Vacation Cash", Description = "Awards pooled cash when landing on Free Parking, if enabled.", CommandId = "vacation_cash", Order = 40 )]
	public bool VacationCash { get; set; } = true;

	[MatchConfigOption( "Board Rules", "Vacation Cash Minimum", Description = "Minimum cash kept in Vacation Cash while Vacation Cash is enabled.", CommandId = "vacation_cash_minimum", Order = 41, Min = 0, Max = 5000, Step = 50 )]
	public int VacationCashMinimum { get; set; } = 100;

	[MatchConfigOption( "Board Rules", "No Rent While In Prison", Description = "Prevents jailed players from collecting rent.", CommandId = "dont_collect_rent_while_in_prison", Order = 42 )]
	public bool DontCollectRentWhileInPrison { get; set; } = false;

	[MatchConfigOption( "Board Rules", "Even Build", Description = "Requires houses to be built evenly across a color set.", CommandId = "even_build", Order = 43 )]
	public bool EvenBuild { get; set; } = true;

	[MatchConfigOption( "Board Rules", "Buffed Utilities", Description = "Utility rent uses the total spaces moved during the turn instead of only the dice roll.", CommandId = "buffed_utilities", Order = 44 )]
	public bool BuffedUtilities { get; set; } = false;

}

