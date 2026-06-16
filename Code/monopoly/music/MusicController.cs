namespace Sandbox;

public enum MusicPlaylistScene
{
	MenuLobby,
	Game
}

public sealed class SceneSoundtrackPlaylist
{
	[Property] public MusicPlaylistScene Scene { get; set; }
	[Property] public bool ShuffleSoundtracks { get; set; } = true;
	[Property] public List<GameSoundtrack> Soundtracks { get; set; } = new();
}

public sealed class MusicController : Component
{

	private static MusicController Instance;

	[Property] public bool StartEnabled { get; set; } = true;
	public List<SceneSoundtrackPlaylist> SceneSoundtracks { get; set; } = new()
	{
		new()
		{
			Scene = MusicPlaylistScene.MenuLobby,
			Soundtracks = new() { GameAssets.Soundtracks.Menu0 }
		},
		new()
		{
			Scene = MusicPlaylistScene.Game,
			Soundtracks = new() { GameAssets.Soundtracks.Game0 }
		}
	};

	private SoundHandle SoundHandle;
	private List<GameSoundtrack> activeSoundtracks;
	private bool activeShuffleSoundtracks = true;
	private MusicPlaylistScene? activePlaylistScene;
	private int soundtrackIndex = -1;

	protected override void OnAwake()
	{
		base.OnAwake();

		if ( Instance is not null && Instance != this )
			Instance.StopSoundtrack();

		Instance = this;
		SceneSystemService.SceneLoaded += OnSceneLoaded;
	}

	protected override void OnStart()
	{
		base.OnStart();

		ApplySoundtrackScene( GetInitialGameScene(), false );

		if ( StartEnabled )
			StartSoundtrack();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( SoundHandle is not null && !SoundHandle.IsPlaying && !SoundHandle.Paused )
			PlayNextSoundtrack();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		SceneSystemService.SceneLoaded -= OnSceneLoaded;
		StopSoundtrack();

		if ( Instance == this )
			Instance = null;
	}

	public static void StartSoundtrack( float time = -1f, float delay = 1, float fadeInTime = 2 )
	{
		Instance?.StartSoundtrackInternal( time );
	}

	public static void Stop()
	{
		Instance?.StopSoundtrack();
	}

	public static void Pause()
	{
		if ( Instance?.SoundHandle is not null )
			Instance.SoundHandle.Paused = true;
	}

	public static void Resume()
	{
		if ( Instance?.SoundHandle is not null )
			Instance.SoundHandle.Paused = false;
		else
			StartSoundtrack();
	}

	private void StartSoundtrackInternal( float time = -1f )
	{
		if ( SoundHandle is not null )
		{
			if ( SoundHandle.IsPlaying )
				return;

			if ( SoundHandle.Paused )
			{
				SoundHandle.Paused = false;
				return;
			}

			SoundHandle.Dispose();
			SoundHandle = null;
		}

		PlayNextSoundtrack();

		if ( time >= 0f && SoundHandle is not null )
			SoundHandle.Time = time;
	}

	private void PlayNextSoundtrack()
	{
		var soundtrack = GetNextSoundtrack();
		if ( soundtrack is null || !soundtrack.IsAssigned )
			return;

		SoundHandle?.Dispose();
		SoundHandle = soundtrack.PlayWithHandle();
		if ( SoundHandle is null )
		{
			Log.Warning( $"Failed to play soundtrack '{soundtrack.Path}'." );
			return;
		}

		SoundHandle.TargetMixer = AppSettings.GetMusicMixer();
	}

	private GameSoundtrack GetNextSoundtrack()
	{
		var availableSoundtracks = (activeSoundtracks ?? new())
			.Where( soundtrack => soundtrack is not null && soundtrack.IsAssigned )
			.ToList();

		if ( availableSoundtracks.Count == 0 )
			return null;

		if ( activeShuffleSoundtracks && availableSoundtracks.Count > 1 )
		{
			var nextIndex = Game.Random.Int( 0, availableSoundtracks.Count - 1 );
			if ( nextIndex == soundtrackIndex )
				nextIndex = (nextIndex + 1) % availableSoundtracks.Count;

			soundtrackIndex = nextIndex;
			return availableSoundtracks[soundtrackIndex];
		}

		soundtrackIndex = (soundtrackIndex + 1) % availableSoundtracks.Count;
		return availableSoundtracks[soundtrackIndex];
	}

	private void OnSceneLoaded( GameScene gameScene )
	{
		ApplySoundtrackScene( gameScene, true );
	}

	private GameScene GetInitialGameScene()
	{
		if ( SceneSystemService.TryGetGameScene( Scene, out GameScene gameScene ) )
			return gameScene;

		return SceneSystemService.GetActiveGameScene();
	}

	private void ApplySoundtrackScene( GameScene gameScene, bool restartIfChanged )
	{
		var playlistScene = GetPlaylistScene( gameScene );
		if ( activePlaylistScene == playlistScene )
			return;

		activePlaylistScene = playlistScene;
		soundtrackIndex = -1;

		var playlist = SceneSoundtracks?.FirstOrDefault( playlist => playlist is not null && playlist.Scene == playlistScene );
		activeSoundtracks = playlist?.Soundtracks;
		activeShuffleSoundtracks = playlist?.ShuffleSoundtracks ?? false;

		if ( !restartIfChanged )
			return;

		StopSoundtrack();

		if ( StartEnabled )
			StartSoundtrackInternal();
	}

	private static MusicPlaylistScene GetPlaylistScene( GameScene gameScene )
	{
		if ( gameScene == GameScene.Game || gameScene?.Title == GameScene.Game.Title )
			return MusicPlaylistScene.Game;

		return MusicPlaylistScene.MenuLobby;
	}

	private void StopSoundtrack()
	{
		if ( SoundHandle is null )
			return;

		SoundHandle.Stop();
		SoundHandle.Dispose();
		SoundHandle = null;
	}

}
