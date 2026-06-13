namespace Sandbox;

public sealed class MusicController : Component
{

	private static MusicController Instance;

	private SoundEvent SoundEvent;
	private SoundHandle SoundHandle;

	private bool isEnabled = false;

	protected override void OnAwake()
	{
		if (!isEnabled) return;
		base.OnAwake();
		Instance = this;
	}

	protected override void OnStart()
	{
		base.OnStart();

		if (!isEnabled) return;

		SoundEvent = GameAssets.Soundtracks.Nolan01.GetSoundEvent();
		StartSoundtrack();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if (SoundHandle != null)
		{
			SoundHandle.Stop();
			SoundHandle.Dispose();
		}
			
	}

	public static void StartSoundtrack(float time = -1f)
	{

		SoundHandle handle = Instance.SoundHandle;
		if (handle != null)
		{
			if (handle.IsPlaying)
			{
				return;
			} else if (handle.Paused)
			{
				handle.Paused = false;
				return;
			} else
			{
				handle.Dispose();
			}
		}

		Instance.SoundHandle = Sound.Play(Instance.SoundEvent);
		
		if (time != -1f)
			Instance.SoundHandle.Time = time;
	}

}
