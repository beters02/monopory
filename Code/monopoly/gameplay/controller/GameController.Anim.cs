public sealed partial class GameController : Component
{
    
    [Rpc.Broadcast]
    public void SetPlayerTokenWalking( PlayerToken token, bool walking )
    {
        token?.SetWalkingAnim( walking );
    }

    private void SetPlayerTokenWalking( PlayerState player, bool walking )
    {
        var token = GetPlayerToken( player );
        if ( token is null )
            return;

        SetPlayerTokenWalking( token, walking );
    }

    private PlayerToken GetPlayerToken( PlayerState player )
    {
        if ( player is null )
            return null;

        return Scene.GetAllComponents<PlayerToken>()
            .FirstOrDefault( token => token is not null && token.PlayerState == player );
    }
}
