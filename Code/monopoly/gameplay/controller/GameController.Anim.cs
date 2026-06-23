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

	private void SnapPlayerTokenToSpace( PlayerState player )
	{
		var playerIndex = GetPlayerIndex( player );
		if ( playerIndex < 0 )
			return;

		SnapPlayerTokenToSpaceForPlayer( playerIndex, player.SpaceIndex );
	}

	[Rpc.Broadcast]
	private void SnapPlayerTokenToSpaceForPlayer( int playerIndex, int spaceIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null )
			return;

		player.SpaceIndex = NormalizeSpaceIndex( spaceIndex );
		var token = GetPlayerToken( player );
		token?.SnapToAssignedSpace();
	}

	[Rpc.Broadcast]
	private void SetPlayerTokenWalkingForPlayer( int playerIndex, bool walking )
	{
        var player = Players.ElementAtOrDefault( playerIndex );
        var token = GetPlayerToken( player );
        token?.SetWalkingAnim( walking );
    }

    [Rpc.Broadcast]
    private void PlayPlayerTokenStepForPlayer( int playerIndex, Vector3 playerPos )
    {
        if ( playerIndex < 0 || playerIndex >= Players.Count )
            return;

        GameAssets.Sounds.TokenStep.Play();
    }

    private PlayerToken GetPlayerToken( PlayerState player )
    {
        if ( player is null )
            return null;

        return Scene.GetAllComponents<PlayerToken>()
            .FirstOrDefault( token => token is not null && token.PlayerState == player );
    }

	public static PlayerToken GetPlayerTokenStatic( PlayerState player )
	{
		if ( Instance is null )
			return null;

		return Instance.GetPlayerToken( player );
	}
}
