public class GamePhaseDef
{
    public string DisplayName;

    public GamePhaseDef(string _DisplayName)
    {
        DisplayName = _DisplayName;
    }

    public string ParseNameWithPlayer(PlayerState player) =>
        DisplayName.Replace("{player}", player.PlayerName);
}

public sealed partial class GameController : Component
{
    
    private Dictionary<GamePhase, GamePhaseDef> GamePhaseDefs = new()
    {
		[GamePhase.Auctioning] = new("Auction started by {player}"),
		[GamePhase.ResolvingDiceRoll] = new("{player} is rolling dice"),
		[GamePhase.ResolvingSpace] = new("{player}'s piece is moving"),
		[GamePhase.TurnEnded] = new("Turn actions finished, waiting on {player} to end turn"),
        [GamePhase.WaitingForBuyDecision] = new("Waiting for {player}'s decision on unowned property"),
        [GamePhase.WaitingToRoll] = new("Waiting for {player} to roll."),
        [GamePhase.Error] = new("Error parsing Phase")
    };

    public GamePhaseDef GetGamePhaseDef(GamePhase phase)
    {
        if (!GamePhaseDefs.TryGetValue(phase, out var definition))
            return GamePhaseDefs[GamePhase.Error];
        
        return definition;
    }

    public GamePhaseDef GetGamePhaseDef() => GetGamePhaseDef(Phase);

    public string ParseCurrentGamePhaseWithCurrentPlayer()
    {
        GamePhaseDef def = GetGamePhaseDef();
        PlayerState player = GetPlayerForIndex(CurrentPlayerIndex);

        if ( def is null || player is null )
            return GetGamePhaseDef(GamePhase.Error).DisplayName;
        
        return def.ParseNameWithPlayer(player);
    }

}
