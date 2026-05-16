using Sandbox;

public sealed class MonopolyCommandWatcher : Component
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

	[ConCmd( "change_money" )]
	private static void ChangeMoney( Connection connection, int amount, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "change_money", GameCommandManager.ChangeMoney( connection, amount, JoinPlayerName( playerName, playerNameTail ) ) );
	}

	[ConCmd( "debug_refactor_stage_test" )]
	private static void DebugRefactorStageTest( Connection connection, string playerName = "self", params string[] playerNameTail )
	{
		LogCommandResult( "debug_refactor_stage_test", GameCommandManager.DebugRefactorStageTest(connection, JoinPlayerName(playerName, playerNameTail)));
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

		MonopolyGame.Instance?.SendPopupToAll(
			"Server cheats changed",
			$"sv_cheats is now {(newValue ? "enabled" : "disabled")}.",
			newValue ? PopupKind.Warning : PopupKind.Info
		);
	}
}
