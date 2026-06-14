using System;
using System.Threading.Tasks;

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

	public virtual SoundHandle PlayWithHandle( float volume = 1, float pitch = 1, float delay = 0, float fadeInTime = 0 )
	{
		if ( !IsAssigned )
			return null;

		if ( SoundType == GameSoundType.SoundFile )
			return Sound.PlayFile( SoundFile.Load( Path ), volume, pitch, delay, fadeInTime );
		else
		{
			var handle = Sound.Play( Path, fadeInTime );
			if ( delay <= 0f )
				return handle;

			if ( handle is null )
				return null;

			handle.Paused = true;
			_ = ResumeAfterDelay( handle, delay );
			return handle;
		}
	}

	private static async Task ResumeAfterDelay( SoundHandle handle, float delaySeconds )
	{
		await Task.Delay( (int) Math.Round(delaySeconds * 1000f) );

		if ( handle is not null )
			handle.Paused = false;
	}

	public virtual SoundHandle PlayWithHandle( float delay = 0, float fadeInTime = 0 ) => 
		PlayWithHandle(1f, 1f, delay, fadeInTime);

	public virtual SoundHandle PlayWithHandle() =>
		PlayWithHandle(1f, 1f, 0f, 0f);


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
