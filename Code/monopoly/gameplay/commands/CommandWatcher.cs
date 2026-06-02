using Sandbox;

public sealed class CommandWatcher : Component
{
	private bool lastCheatsEnabled;
	private bool firstRun = true;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;
		
		HandlePreviouslyExistingCommands();

		if ( firstRun )
			firstRun = false; 
	}

	[ConCmd( "buy_property" )]
	private static void BuyProperty( Connection connection, int propertyIndex, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "buy_property", GameCommandManager.BuyProperty( connection, propertyIndex, JoinPlayerName( playerName, playerNameTail ) ) );
	}

	[ConCmd( "buy_property_set" )]
	private static void BuyPropertySet( Connection connection, string propertySet, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "buy_property_set", GameCommandManager.BuyPropertySet( connection, propertySet, JoinPlayerName( playerName, playerNameTail ) ) );
	}

	[ConCmd( "roll_dice" )]
	private static void RollDice( Connection connection, int amount = -1, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "roll_dice", GameCommandManager.RollDice( connection, amount, JoinPlayerName( playerName, playerNameTail ) ) );
	}

	[ConCmd( "roll_two_dice" )]
	private static void RollTwoDice( Connection connection, int dieA, int dieB, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "roll_two_dice", GameCommandManager.RollTwoDice( connection, dieA, dieB, JoinPlayerName( playerName, playerNameTail ) ) );
	}

	[ConCmd( "change_money" )]
	private static void ChangeMoney( Connection connection, int amount, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "change_money", GameCommandManager.ChangeMoney( connection, amount, JoinPlayerName( playerName, playerNameTail ) ) );
	}

	[ConCmd( "change_vacation_cash" )]
	private static void ChangeVacationCash( Connection connection, int amount )
	{
		LogCommandResult( "change_vacation_cash", GameCommandManager.ChangeVacationCash( connection, amount ) );
	}

	[ConCmd( "force_end_game_win" )]
	private static void ForceEndGameWin( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "force_end_game_win", GameCommandManager.ForceEndGameWin( connection, JoinPlayerName( playerName, playerNameTail ) ) );
	}

	[ConCmd( "jail_player" )]
	private static void JailPlayer( Connection connection, string playerName = "self" )
	{
		LogCommandResult( "jail_player", GameCommandManager.SendToJail( connection, playerName ) );
	}

	[ConVar( "debug" )]
	public static bool Debug { get; set; } = false;

	private static string JoinPlayerName( string playerName, string[] playerNameTail )
	{
		if ( playerNameTail is null || playerNameTail.Length == 0 )
			return playerName;

		return string.Join( " ", new[] { playerName }.Concat( playerNameTail ) );
	}

	private static void LogCommandResult( string commandName, CommandResult result )
	{
		if ( result is null )
		{
			Log.Warning( $"{commandName} command failed: no command result." );
			return;
		}

		if ( !result.Ok )
			Log.Warning( $"{commandName} command failed: {result.Message}" );
	}

	private void HandlePreviouslyExistingCommands()
	{
		var cheatsEnabled = Game.CheatsEnabled;
		if ( cheatsEnabled != lastCheatsEnabled )
		{
			var oldValue = lastCheatsEnabled;
			lastCheatsEnabled = cheatsEnabled;
			OnSvCheatsChanged( oldValue, cheatsEnabled, firstRun );
		}
	}

	private void OnSvCheatsChanged( bool oldValue, bool newValue, bool wasFirstRun )
	{
		Log.Info( $"sv_cheats changed: {oldValue} -> {newValue}" );

		if ( wasFirstRun )
			return;

		GameController.Instance?.SendTableChatMessage(
			"Server cheats changed",
			$"sv_cheats is now {(newValue ? "enabled" : "disabled")}."
		);
	}
}
