public sealed class GameSoundtrack : GameSoundBase<GameSoundtrack>
{

	public float FadeInTime { get; set; } = 0;
	public float Delay { get; set; } = 0;

	public GameSoundtrack( string path ) : base( path ) { }
	public GameSoundtrack ( string path, float fadeInTime = 0, float delay = 0 ) : base ( path )
	{
		FadeInTime = fadeInTime;
		Delay = 0;
	}

	
    
	public static implicit operator GameSoundtrack( string path ) => new( path );
}