public sealed partial class GameController : Component
{
    
    public void SetPlayerTokenWalking( PlayerToken token, bool walking )
    {
        token?.SetWalkingAnim( walking );
    }

    private void SetPlayerTokenWalking( PlayerState player, bool walking )
    {
        var playerIndex = GetPlayerIndex( player );
        if ( playerIndex < 0 )
            return;

        SetPlayerTokenWalkingForPlayer( playerIndex, walking );
    }

    [Rpc.Broadcast]
    private void SetPlayerTokenWalkingForPlayer( int playerIndex, bool walking )
    {
        var player = Players.ElementAtOrDefault( playerIndex );
        var token = GetPlayerToken( player );
        token?.SetWalkingAnim( walking );
    }

    private PlayerToken GetPlayerToken( PlayerState player )
    {
        if ( player is null )
            return null;

        return Scene.GetAllComponents<PlayerToken>()
            .FirstOrDefault( token => token is not null && token.PlayerState == player );
    }
}
