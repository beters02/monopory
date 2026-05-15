using System;

public enum MonopolySoundType
{
	SoundFile,
	SoundEvent
}

public readonly struct MonopolySound
{
	public MonopolySound( string path )
    {
        Path = path ?? "";
		SoundType = GetSoundType( path );
    }

	public string Path { get; }

	public bool IsAssigned => !string.IsNullOrWhiteSpace( Path );

	public readonly MonopolySoundType SoundType;

	public bool Play()
	{
		if ( !IsAssigned )
			return false;

		if ( SoundType == MonopolySoundType.SoundFile )
			Sound.PlayFile( SoundFile.Load( Path ) );
		else
			Sound.Play( Path );

		return true;
	}

	private static MonopolySoundType GetSoundType( string path )
	{
		return System.IO.Path.GetExtension( path ).Equals( ".sound", StringComparison.OrdinalIgnoreCase )
			? MonopolySoundType.SoundEvent
			: MonopolySoundType.SoundFile;
	}

	public override string ToString() => Path;

	public static implicit operator MonopolySound( string path ) => new( path );
}
