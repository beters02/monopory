using System;

public enum GameSoundType
{
	SoundFile,
	SoundEvent
}

public abstract class GameSoundBase<TSelf> where TSelf : GameSoundBase<TSelf>
{

	protected GameSoundBase( string path )
	{
		Path = path ?? "";
		SoundType = GetSoundType( path );
	}

	public string Path { get; }

	public bool IsAssigned => !string.IsNullOrWhiteSpace( Path );

	public readonly GameSoundType SoundType;

	private SoundEvent SoundEvent { get; set; }

	public bool Preload()
	{
		if ( !IsAssigned )
			return false;

		if ( SoundType == GameSoundType.SoundFile )
			return SoundFile.Load( Path ) is not null;

		SoundEvent = ResourceLibrary.Get<SoundEvent>( Path );

		return true;
	}

	public bool Play()
	{
		return Play(null);
	}

	public bool Play(Vector3? Position)
	{
		if ( !IsAssigned )
			return false;

		if ( SoundType == GameSoundType.SoundFile )
			Sound.PlayFile( SoundFile.Load( Path ) );
		else
		{
			if (Position is not null)
				Sound.Play( SoundEvent, (Vector3) Position );
			else
				Sound.Play( Path );
		}
			
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

	public SoundEvent GetSoundEvent()
	{
		Log.Info(SoundEvent);
		if (SoundType != GameSoundType.SoundEvent)
			return null;

		return SoundEvent;
	}

	public override string ToString() => Path;

	
}