using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Sandbox;
using Sandbox.Audio;
using Sandbox.Engine.Settings;

public enum FullscreenMode
{
	FullscreenBorderless,
	FullscreenExclusive,
	Windowed
}

public class AppSettingsData
{
	public FullscreenMode FullscreenMode { get; set; } = FullscreenMode.FullscreenExclusive;
	public bool VSync { get; set; } = false;
	public UpscalerMode UpscalerMode { get; set; } = UpscalerMode.Off;
	public Fsr3UpscalerQuality Fsr3Quality { get; set; } = Fsr3UpscalerQuality.Performance;
	public float MotionBlurScale { get; set; } = 0f;
	public int Volume { get; set; } = 100;
	public int MusicVolume { get; set; } = 30;
	public string SelectedPieceId { get; set; } = PieceCatalog.DefaultPieceId;
	public string SelectedDiceSkinId { get; set; } = DiceSkinCatalog.DefaultDiceSkinId;
	public string DefaultGameRulePresetId { get; set; } = GameRulePresets.DefaultPresetId;
	public string DefaultBoardConfigPresetId { get; set; } = BoardNamePresets.DefaultPresetId;
	public List<AppSettingsKeybind> Keybinds { get; set; } = new();
}

public class AppSettingsKeybind
{
	public string ActionName { get; set; } = "";
	public string KeyboardCode { get; set; } = "";
}

public class AppSettings : Component
{
	private const string FileName = "AppSettings.json";
	private const string LegacyClearAllPopupsAction = "ClearAllPopups";
	private const string LegacyClearAllPopupsKey = "C";
	private const string ClearAllPopupsDefaultKey = "X";

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

		MigrateSavedKeybinds();
		Save();
	}

	public static void Save()
	{
		FileSystem.Data.WriteJson( FileName, Data );
	}

	public static AppSettingsData Capture()
	{
		return Copy( Data );
	}

	public static void Restore( AppSettingsData snapshot, bool save = false )
	{
		Data = Copy( snapshot ?? new AppSettingsData() );

		if ( save )
			Save();

		Apply();
	}

	public static bool Matches( AppSettingsData snapshot )
	{
		if ( snapshot is null )
			return false;

		return Data.FullscreenMode == snapshot.FullscreenMode
			&& Data.VSync == snapshot.VSync
			&& Data.UpscalerMode == snapshot.UpscalerMode
			&& Data.Fsr3Quality == snapshot.Fsr3Quality
			&& MathF.Abs( Data.MotionBlurScale - snapshot.MotionBlurScale ) < 0.001f
			&& Data.Volume == snapshot.Volume
			&& Data.MusicVolume == snapshot.MusicVolume
			&& string.Equals( GetDefaultGameRulePresetId(), NormalizePresetId( snapshot.DefaultGameRulePresetId, GameRulePresets.DefaultPresetId ), StringComparison.Ordinal )
			&& string.Equals( GetDefaultBoardConfigPresetId(), NormalizePresetId( snapshot.DefaultBoardConfigPresetId, BoardNamePresets.DefaultPresetId ), StringComparison.Ordinal )
			&& KeybindsMatch( Data.Keybinds, snapshot.Keybinds );
	}

	private static AppSettingsData Copy( AppSettingsData source )
	{
		source ??= new AppSettingsData();

		return new AppSettingsData
		{
			FullscreenMode = source.FullscreenMode,
			VSync = source.VSync,
			UpscalerMode = source.UpscalerMode,
			Fsr3Quality = source.Fsr3Quality,
			MotionBlurScale = source.MotionBlurScale,
			Volume = source.Volume,
			MusicVolume = source.MusicVolume,
			SelectedPieceId = PieceCatalog.GetByIdOrDefault( source.SelectedPieceId ).Id,
			SelectedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( source.SelectedDiceSkinId ).Id,
			DefaultGameRulePresetId = NormalizePresetId( source.DefaultGameRulePresetId, GameRulePresets.DefaultPresetId ),
			DefaultBoardConfigPresetId = NormalizePresetId( source.DefaultBoardConfigPresetId, BoardNamePresets.DefaultPresetId ),
			Keybinds = CopyKeybinds( source.Keybinds )
		};
	}

	public static void Apply()
	{
		ApplyAudio();
		ApplyKeybinds();

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
	public static int GetVolume() => Math.Clamp( Data.Volume, 0, 100 );
	public static int GetMusicVolume() => Math.Clamp( Data.MusicVolume, 0, 100 );
	public static string GetSelectedPieceId() => PieceCatalog.GetByIdOrDefault( Data.SelectedPieceId ).Id;
	public static string GetSelectedDiceSkinId() => DiceSkinCatalog.GetByIdOrDefault( Data.SelectedDiceSkinId ).Id;
	public static string GetDefaultGameRulePresetId() => NormalizePresetId( Data.DefaultGameRulePresetId, GameRulePresets.DefaultPresetId );
	public static string GetDefaultBoardConfigPresetId() => NormalizePresetId( Data.DefaultBoardConfigPresetId, BoardNamePresets.DefaultPresetId );
	public static string GetKeybind( InputAction action )
	{
		if ( action is null )
			return "";

		var savedKeybind = GetSavedKeybind( action.Name );
		if ( !string.IsNullOrWhiteSpace( savedKeybind?.KeyboardCode ) )
			return savedKeybind.KeyboardCode;

		return IGameInstance.Current?.GetBind( action.Name, out bool _, out bool _ ) ?? action.KeyboardCode ?? "";
	}

	public static string GetKeybindConflictName( InputAction action, string keyboardCode )
	{
		var conflict = GetKeybindConflict( action, keyboardCode );
		if ( conflict is null )
			return "";

		return !string.IsNullOrWhiteSpace( conflict.Title ) ? conflict.Title : conflict.Name;
	}

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
		Data.VSync = enabled;
		return true;
	}

	public static bool TrySetMotionBlur( float scale )
	{
		Data.MotionBlurScale = scale;
		return true;
	}

	public static bool TrySetFullscreenMode( FullscreenMode mode )
	{
		Data.FullscreenMode = mode;
		return true;
	}

	public static bool TrySetFsr3Quality( Fsr3UpscalerQuality quality )
	{
		Data.Fsr3Quality = quality;
		return true;
	}

	public static bool TrySetUpscaler( UpscalerMode mode )
	{
		Data.UpscalerMode = mode;
		return true;
	}

	public static bool TrySetVolume( int volume )
	{
		volume = Math.Clamp(volume, 0, 100);
		Data.Volume = volume;
		ApplyAudio();
		return true;
	}

	public static bool TrySetMusicVolume( int volume )
	{
		volume = Math.Clamp( volume, 0, 100 );
		Data.MusicVolume = volume;
		ApplyAudio();
		return true;
	}

	public static bool TrySetSelectedPieceId( string pieceId, bool save = true )
	{
		Data.SelectedPieceId = PieceCatalog.GetByIdOrDefault( pieceId ).Id;
		if ( save )
			Save();
		return true;
	}

	public static bool TrySetSelectedDiceSkinId( string diceSkinId, bool save = true )
	{
		Data.SelectedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( diceSkinId ).Id;
		if ( save )
			Save();
		return true;
	}

	public static bool TrySetDefaultGameRulePresetId( string presetId, bool save = true )
	{
		Data.DefaultGameRulePresetId = NormalizePresetId( presetId, GameRulePresets.DefaultPresetId );
		if ( save )
			Save();
		return true;
	}

	public static bool TrySetDefaultBoardConfigPresetId( string presetId, bool save = true )
	{
		Data.DefaultBoardConfigPresetId = NormalizePresetId( presetId, BoardNamePresets.DefaultPresetId );
		if ( save )
			Save();
		return true;
	}

	public static bool TrySetKeybind( InputAction action, string keyboardCode )
	{
		if ( action is null || string.IsNullOrWhiteSpace( action.Name ) )
			return false;

		keyboardCode = NormalizeKeybindCode( keyboardCode );
		if ( string.IsNullOrWhiteSpace( keyboardCode ) )
			return false;

		if ( GetKeybindConflict( action, keyboardCode ) is not null )
			return false;

		Data.Keybinds ??= new List<AppSettingsKeybind>();
		var savedKeybind = GetSavedKeybind( action.Name );
		if ( savedKeybind is null )
		{
			Data.Keybinds.Add( new AppSettingsKeybind
			{
				ActionName = action.Name,
				KeyboardCode = keyboardCode
			} );
		}
		else
		{
			savedKeybind.KeyboardCode = keyboardCode;
		}

		Log.Info( $"Queued keybind '{action.Name}' to '{keyboardCode}'." );
		return true;
	}
	
	public static bool TryApply()
	{
		Save();
		Apply();
		return true;
	}

	private static void ApplyAudio()
	{
		var volume = Math.Clamp( Data.Volume, 0, 100 ) / 100f;
		var musicVolume = Math.Clamp( Data.MusicVolume, 0, 100 ) / 100f;
		ConsoleSystem.SetValue( "volume", volume );

		if ( Mixer.Master is not null )
			Mixer.Master.Volume = volume;

		var musicMixer = GetMusicMixer();
		if ( musicMixer is not null )
			musicMixer.Volume = musicVolume;
	}

	public static Mixer GetMusicMixer()
	{
		return Mixer.FindMixerByName( "music" );
	}

	private static void ApplyKeybinds()
	{
		var gameInstance = IGameInstance.Current;
		if ( gameInstance is null )
		{
			Log.Warning( "Could not apply keybinds because IGameInstance.Current is null." );
			return;
		}

		var migratedKeybinds = ApplyLegacyKeybindMigrations( gameInstance );
		if ( Data.Keybinds is null || Data.Keybinds.Count == 0 )
		{
			if ( migratedKeybinds )
				gameInstance.SaveBinds();

			return;
		}

		foreach ( var keybind in Data.Keybinds )
		{
			if ( keybind is null || string.IsNullOrWhiteSpace( keybind.ActionName ) || string.IsNullOrWhiteSpace( keybind.KeyboardCode ) )
				continue;

			gameInstance.SetBind( keybind.ActionName, keybind.KeyboardCode );
			Log.Info( $"Applied keybind '{keybind.ActionName}' to '{keybind.KeyboardCode}'." );
		}

		gameInstance.SaveBinds();
	}

	private static void MigrateSavedKeybinds()
	{
		var clearAllPopupsKeybind = GetSavedKeybind( LegacyClearAllPopupsAction );
		if ( !IsSameKeybindCode( clearAllPopupsKeybind?.KeyboardCode, LegacyClearAllPopupsKey ) )
			return;

		clearAllPopupsKeybind.KeyboardCode = ClearAllPopupsDefaultKey;
		Log.Info( $"Migrated keybind '{LegacyClearAllPopupsAction}' from '{LegacyClearAllPopupsKey}' to '{ClearAllPopupsDefaultKey}'." );
	}

	private static bool ApplyLegacyKeybindMigrations( IGameInstance gameInstance )
	{
		if ( gameInstance is null )
			return false;

		var migrated = false;
		var clearAllPopupsKey = gameInstance.GetBind( LegacyClearAllPopupsAction, out bool _, out bool _ );
		if ( IsSameKeybindCode( clearAllPopupsKey, LegacyClearAllPopupsKey ) )
		{
			gameInstance.SetBind( LegacyClearAllPopupsAction, ClearAllPopupsDefaultKey );
			migrated = true;
			Log.Info( $"Migrated keybind '{LegacyClearAllPopupsAction}' from '{LegacyClearAllPopupsKey}' to '{ClearAllPopupsDefaultKey}'." );
		}

		return migrated;
	}

	private static bool KeybindsMatch( List<AppSettingsKeybind> left, List<AppSettingsKeybind> right )
	{
		left = CopyKeybinds( left );
		right = CopyKeybinds( right );

		if ( left.Count != right.Count )
			return false;

		foreach ( var leftKeybind in left )
		{
			var rightKeybind = right.FirstOrDefault( keybind => string.Equals( keybind.ActionName, leftKeybind.ActionName, StringComparison.Ordinal ) );
			if ( rightKeybind is null )
				return false;

			if ( !string.Equals( leftKeybind.KeyboardCode, rightKeybind.KeyboardCode, StringComparison.Ordinal ) )
				return false;
		}

		return true;
	}

	private static List<AppSettingsKeybind> CopyKeybinds( List<AppSettingsKeybind> keybinds )
	{
		return (keybinds ?? new List<AppSettingsKeybind>())
			.Where( keybind => keybind is not null && !string.IsNullOrWhiteSpace( keybind.ActionName ) )
			.Select( keybind => new AppSettingsKeybind
			{
				ActionName = keybind.ActionName,
				KeyboardCode = keybind.KeyboardCode ?? ""
			} )
			.ToList();
	}

	private static AppSettingsKeybind GetSavedKeybind( string actionName )
	{
		if ( string.IsNullOrWhiteSpace( actionName ) || Data.Keybinds is null )
			return null;

		return Data.Keybinds.FirstOrDefault( keybind => string.Equals( keybind?.ActionName, actionName, StringComparison.Ordinal ) );
	}

	private static string NormalizeKeybindCode( string keyboardCode )
	{
		return (keyboardCode ?? "").Trim();
	}

	private static bool IsSameKeybindCode( string left, string right )
	{
		return string.Equals( NormalizeKeybindCode( left ), NormalizeKeybindCode( right ), StringComparison.OrdinalIgnoreCase );
	}

	private static string NormalizePresetId( string presetId, string fallback )
	{
		return string.IsNullOrWhiteSpace( presetId ) ? fallback : presetId.Trim();
	}

	private static InputAction GetKeybindConflict( InputAction targetAction, string keyboardCode )
	{
		if ( targetAction is null )
			return null;

		keyboardCode = NormalizeKeybindCode( keyboardCode );
		if ( string.IsNullOrWhiteSpace( keyboardCode ) )
			return null;

		foreach ( var action in Input.GetActions() )
		{
			if ( action is null || string.IsNullOrWhiteSpace( action.Name ) )
				continue;

			if ( ShouldIgnoreKeybindConflict( action ) )
				continue;

			if ( string.Equals( action.Name, targetAction.Name, StringComparison.Ordinal ) )
				continue;

			var actionKeyboardCode = IGameInstance.Current?.GetBind( action.Name, out bool _, out bool _ ) ?? GetKeybind( action );
			if ( string.Equals( NormalizeKeybindCode( actionKeyboardCode ), keyboardCode, StringComparison.OrdinalIgnoreCase ) )
				return action;
		}

		return null;
	}

	private static bool ShouldIgnoreKeybindConflict( InputAction action )
	{
		return !Application.IsEditor &&
			string.Equals( action?.GroupName, "Editor", StringComparison.OrdinalIgnoreCase );
	}
}
