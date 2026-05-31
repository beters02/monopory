public sealed class GameImage : GameImageBase<GameImage>
{
	public GameImage( string path ) : base( path ) { }
    
	public static implicit operator GameImage( string path ) => new( path );
}