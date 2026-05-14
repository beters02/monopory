using System;
using System.Threading.Tasks;
using Sandbox;

public class MonopolyCommandResult
{
	public string Message;
	public bool Ok;

	public MonopolyCommandResult(bool success, string message = "")
	{
		Message = message;
		Ok = success;
	}
}

public sealed class MonopolyCommandWatcher : Component
{
	
private bool lastCheatsEnabled;
	private bool firstRun = true;

    // COMPONENT

    protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;
		
		HandlePreviouslyExistingCommands();

		if ( firstRun )
			firstRun = false; 
	}

    // CUSTOM COMMANDS

	[ConCmd("buy_property")]
	static void BuyProperty(Connection connection, int propertyIndex, string playerName = "self")
	{
		MonopolyCommandResult result = MonopolyGame.Instance?.BuyPropertyCommand(connection, propertyIndex, playerName);
		if (!result.Ok)
		{
			Log.Warning("buy_property command failed: " + result.Message);
			return;
		}
		//MonopolyGame.Instance?.BuyPendingProperty
	}

	private static MonopolyCommandResult BuyPropertySetCommand(Connection connection, string propertySet, string playerName = "self")
	{
		if(!Networking.IsHost && ConsoleSystem.GetValue("sv_cheats").ToLower() == "true")
			return new MonopolyCommandResult(false, "cheats is required to use this command.");

		ColorGroup colorGroup = MonopolySpaceSettings.StringToColorGroup(propertySet);
		if (colorGroup == ColorGroup.None)
			return new MonopolyCommandResult(false, "Property set " + propertySet +" does not exist.");

		MonopolyPlayerState player = MonopolyGame.Instance?.GetPlayerForString(playerName);
		if (player == null)
			return new MonopolyCommandResult(false, "Cannot find player " + playerName);
		
		List<MonopolySpaceDef> SpaceDefs = MonopolyBoard.Instance?.SpaceDefs;
		for (int i = 0; i < SpaceDefs.Count; i++)
		{
			if (SpaceDefs[i].ColorGroup == propertySet)
			{
				MonopolyCommandResult result = MonopolyGame.Instance?.BuyPropertyCommand(connection, i, playerName);
				if (!result.Ok)
					return result;
			}
		}

		return new MonopolyCommandResult(true);
	}

	[ConCmd("buy_property_set")]
	static void BuyPropertySet(Connection connection, string propertySet, string playerName = "self")
	{
		MonopolyCommandResult result = BuyPropertySetCommand(connection, propertySet, playerName);
		if (!result.Ok)
		{
			Log.Warning("buy_property_set command failed: " + result.Message);
			return;
		}
	}

	private static MonopolyCommandResult RollDiceCommand(Connection connection, int amount = -1, string playerName = "self")
	{
		MonopolyPlayerState player = MonopolyGame.Instance?.GetPlayerForString(playerName);
		if (player == null)
			return new MonopolyCommandResult(false, "Cannot find player " + playerName);

		if (MonopolyGame.Instance?.CurrentPlayer != player)
			return new MonopolyCommandResult(false, "it is not that player's turn.");

		if(!Networking.IsHost && ConsoleSystem.GetValue("sv_cheats").ToLower() == "true")
			return new MonopolyCommandResult(false, "cheats is required to use this command.");

		if(Networking.IsHost)
			MonopolyGame.Instance?.RollDiceAsync(amount);
		else
			MonopolyGame.Instance?.RequestRollDice(amount);
		
		return new MonopolyCommandResult(true);
	}

	[ConCmd("roll_dice")]
	static void RollDice(Connection connection, int amount = 6, string playerName = "self")
	{
		MonopolyCommandResult result = RollDiceCommand(connection, amount, playerName);
		if (!result.Ok)
		{
			Log.Warning("roll_dice command failed: " + result.Message);
			return;
		}
	}

	// PREVIOUSLY EXISTING COMMANDS

    // Existing commands Helpers

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

	// Existing commands Handlers

	private void OnSvCheatsChanged( bool oldValue, bool newValue, bool wasFirstRun )
	{
		Log.Info( $"sv_cheats changed: {oldValue} -> {newValue}" );

		if ( wasFirstRun )
			return;

		MonopolyGame.Instance?.SendPopupToAll(
			"Server cheats changed",
			$"sv_cheats is now {(newValue ? "enabled" : "disabled")}.",
			newValue ? MonopolyPopupKind.Warning : MonopolyPopupKind.Info
		);
	}

}
