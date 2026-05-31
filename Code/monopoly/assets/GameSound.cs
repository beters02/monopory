public sealed class GameSound : GameSoundBase<GameSound>
{
	public GameSound( string path ) : base( path ) { }
    
	public static implicit operator GameSound( string path ) => new( path );
}