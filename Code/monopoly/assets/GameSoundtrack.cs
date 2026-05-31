public sealed class GameSoundtrack : GameSoundBase<GameSoundtrack>
{
	public GameSoundtrack( string path ) : base( path ) { }
    
	public static implicit operator GameSoundtrack( string path ) => new( path );
}