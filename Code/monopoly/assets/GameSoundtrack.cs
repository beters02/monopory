using System.Text.Json.Serialization;

public sealed class GameSoundtrack : GameSoundBase<GameSoundtrack>
{

	public float FadeInTime { get; set; } = 0;
	public float Delay { get; set; } = 0;

	[JsonConstructor]
	public GameSoundtrack( string path ) : base( path ) { }
	public GameSoundtrack ( string path, float delay = 0, float fadeInTime = 0 ) : base ( path )
	{
		FadeInTime = fadeInTime;
		Delay = delay;
	}

	public override SoundHandle PlayWithHandle( ) => 
		PlayWithHandle( Delay, FadeInTime );

	public static implicit operator GameSoundtrack( string path ) => new( path );
}
