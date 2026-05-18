using System.Reflection;
using System.Xml;
using Sandbox.Engine.Settings;

public enum FullscreenMode
{
	FullscreenBorderless,
	FullscreenExclusive,
	Windowed
}

public class AppSettingsData
{
	public FullscreenMode FullscreenMode { get; set; } = FullscreenMode.FullscreenBorderless;
	public bool VSync { get; set; } = true;
	public UpscalerMode UpscalerMode { get; set; } = UpscalerMode.Off;
	public Fsr3UpscalerQuality Fsr3Quality { get; set; } = Fsr3UpscalerQuality.Performance;
	public float MotionBlurScale { get; set; } = 0f;
}

public class AppSettings : Component
{
	private const string FileName = "AppSettings.json";

	public static AppSettingsData Data { get; private set; } = new();

	public static RenderSettings Settings => Application.RenderSettings;
	public static bool SettingsAvailable => Settings is not null;

	protected override void OnStart()
	{
		Load();
		Apply();
	}

	public static void Load()
	{
		if ( FileSystem.Data.FileExists( FileName ) )
			Data = FileSystem.Data.ReadJson<AppSettingsData>( FileName ) ?? new AppSettingsData();

		Save();
	}

	public static void Save()
	{
		FileSystem.Data.WriteJson( FileName, Data );
	}

	public static void Apply()
	{
		if (Settings is null)
			return;

		var fullscreen = Settings.Fullscreen;
		var borderless = Settings.Borderless;
		var currMonWidth = Settings.ResolutionWidth;
		var currMonHeight = Settings.ResolutionHeight;
		
		Settings.ResetVideoConfig();
		var monWidth = Settings.ResolutionWidth;
		var monHeight = Settings.ResolutionHeight;

		Settings.VSync = Data.VSync;
		Settings.MotionBlurScale = Data.MotionBlurScale;
		Settings.UpscalerMode = Data.UpscalerMode;
		Settings.Fsr3UpscalerQuality = Data.Fsr3Quality;

		Settings.Fullscreen = Data.FullscreenMode != FullscreenMode.Windowed && Data.FullscreenMode != FullscreenMode.FullscreenBorderless;
		Settings.Borderless = Data.FullscreenMode == FullscreenMode.FullscreenBorderless;

		Log.Info(Data.FullscreenMode);

		if (Data.FullscreenMode == FullscreenMode.Windowed && !(!fullscreen && !borderless))
		{
			Settings.ResolutionHeight = 720;
			Settings.ResolutionWidth = 1280;
		} else if (Data.FullscreenMode == FullscreenMode.Windowed)
		{
			Settings.ResolutionWidth = currMonWidth;
			Settings.ResolutionHeight = currMonHeight;
		} else
		{
			Settings.ResolutionHeight = monHeight;
			Settings.ResolutionWidth = monWidth;
		}

		Settings.Apply();
	}

	public static FullscreenMode GetFullscreenMode() => Data.FullscreenMode;
	public static bool GetVSyncEnabled() => Data.VSync;
	public static bool GetMotionBlurEnabled() => Data.MotionBlurScale == 0f ? false : true;
	public static Fsr3UpscalerQuality GetFsr3Quality() => Data.Fsr3Quality;
	public static UpscalerMode GetUpscaler() => Data.UpscalerMode; 

	public static string GetFullscreenModeString(FullscreenMode mode)
	{
		return mode switch
		{
			FullscreenMode.FullscreenBorderless => "Fullscreen Borderless",
			FullscreenMode.FullscreenExclusive => "Fullscreen Exclusive",
			_ => "Windowed"
		};
	}


	public static bool TrySetVSync( bool enabled )
	{
		if ( Settings is null )
			return false;

		Data.VSync = enabled;
		return true;
	}

	public static bool TrySetMotionBlur( float scale )
	{
		if ( Settings is null )
			return false;

		Data.MotionBlurScale = scale;
		return true;
	}

	public static bool TrySetVsync( bool enabled )
	{
		if ( Settings is null )
			return false;

		Data.VSync = enabled;
		return true;
	}

	public static bool TrySetFullscreenMode( FullscreenMode mode )
	{
		if ( Settings is null )
			return false;

		Data.FullscreenMode = mode;
		return true;
	}

	public static bool TrySetFsr3Quality( Fsr3UpscalerQuality quality )
	{
		if ( Settings is null )
			return false;

		Data.Fsr3Quality = quality;
		return true;
	}

	public static bool TrySetUpscaler( UpscalerMode mode )
	{
		if ( Settings is null )
			return false;

		Data.UpscalerMode = mode;
		return true;
	}
	
	public static bool TryApply()
	{
		if ( Settings is null )
			return false;

		Save();
		Apply();
		return true;
	}
}