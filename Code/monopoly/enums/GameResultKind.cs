public class GameResultKind
{
    public string Message { get; init; } 

    public static GameResultKind Error( string msg ) => new( msg );
    public static GameResultKind Warning( string msg ) => new( msg );
    public static GameResultKind Success( string msg ) => new( msg );
    public static GameResultKind None( string msg ) => new( msg );
    
    protected GameResultKind( string msg )
    {
        Message = msg;
    }
}