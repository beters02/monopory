public partial class MonopolyApp
{

    public static string GetPlayerDisplayName( long steamId )
    {
        if ( LobbyRef is not null )
        {
            LobbyPlayer lobbyPlayer = LobbyRef.Players.First(player => player.SteamId == steamId);
            if (lobbyPlayer is not null)
                return lobbyPlayer.Name;
        }

        if ( GameRef is not null )
        {
            PlayerState gamePlayer = GameRef.Players.First(player => player.SteamId == steamId);
            if ( gamePlayer is not null )
                return gamePlayer.PlayerName;
        }

        return null;
    }

}