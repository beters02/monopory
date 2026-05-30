public sealed class GameIcon : GameAssetImage<GameIcon>
{
	public GameIcon( string path ) : base( path ) { }
    
	public static implicit operator GameIcon( string path ) => new( path );
}