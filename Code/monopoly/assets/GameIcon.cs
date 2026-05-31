public sealed class GameIcon : GameImageBase<GameIcon>
{
	public GameIcon( string path ) : base( path ) { }
    
	public static implicit operator GameIcon( string path ) => new( path );
}