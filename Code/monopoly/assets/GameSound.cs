using System;

public enum GameSoundType
{
	SoundFile,
	SoundEvent
}

public readonly struct GameSound
{
	public GameSound( string path )
    {
        Path = path ?? "";
		SoundType = GetSoundType( path );
    }

	public string Path { get; }

	public bool IsAssigned => !string.IsNullOrWhiteSpace( Path );

	public readonly GameSoundType SoundType;

	public bool Preload()
	{
		if ( !IsAssigned )
			return false;

		if ( SoundType == GameSoundType.SoundFile )
			return SoundFile.Load( Path ) is not null;

		return true;
	}

	public bool Play()
	{
		if ( !IsAssigned )
			return false;

		if ( SoundType == GameSoundType.SoundFile )
			Sound.PlayFile( SoundFile.Load( Path ) );
		else
			Sound.Play( Path );

		return true;
	}

	public SoundHandle PlayWithHandle()
	{
		if ( !IsAssigned )
			return null;

		if ( SoundType == GameSoundType.SoundFile )
			return Sound.PlayFile( SoundFile.Load( Path ) );
		else
			return Sound.Play( Path );
	}

	private static GameSoundType GetSoundType( string path )
	{
		return System.IO.Path.GetExtension( path ).Equals( ".sound", StringComparison.OrdinalIgnoreCase )
			? GameSoundType.SoundEvent
			: GameSoundType.SoundFile;
	}

	public override string ToString() => Path;

	public static implicit operator GameSound( string path ) => new( path );
}
