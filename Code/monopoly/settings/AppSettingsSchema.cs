using Sandbox.Engine.Settings;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public enum AppSettingOptionKind
{
	Bool,
	Enum,
	Int,
	Keybind
}

public sealed class AppSettingCategory
{
	public string Key { get; init; }
	public string Label { get; init; }
	public string Description { get; init; } = "";
	public int Order { get; init; }
}

public sealed class AppSettingOption
{
	public string Key { get; init; }
	public string Category { get; init; }
	public string Section { get; init; }
	public string Label { get; init; }
	public string Description { get; init; } = "";
	public int Order { get; init; }
	public AppSettingOptionKind Kind { get; init; }
	public string[] EnumNames { get; init; } = Array.Empty<string>();
	public Func<bool> IsVisible { get; init; } = () => true;
	public Func<bool> IsAvailable { get; init; } = () => true;
	public Func<bool> GetBoolValue { get; init; } = () => false;
	public Func<bool> ToggleBoolValue { get; init; } = () => false;
	public Func<string> GetEnumValue { get; init; } = () => "";
	public Func<string, bool> SetEnumValue { get; init; } = _ => false;
	public Func<int> GetIntValue { get; init; } = () => 0;
	public Func<int, bool> SetIntValue { get; init; } = _ => false;
	public Func<string> GetKeybindValue { get; init; } = () => "";
	public Func<string, bool> SetKeybindValue { get; init; } = _ => false;
	public Func<string, string> GetKeybindConflict { get; init; } = _ => "";
	public int IntMin { get; init; }
	public int IntMax { get; init; } = 100;
	public int IntStep { get; init; } = 1;
}

public static partial class AppSettingsSchema
{
	private static readonly IReadOnlyList<AppSettingCategory> categories = BuildCategories();

	public static IReadOnlyList<AppSettingCategory> Categories => categories;
	public static IReadOnlyList<AppSettingOption> Options => BuildOptions();

	public static IEnumerable<string> GetSectionsForCategory( string categoryKey )
	{
		return Options
			.Where( option => string.Equals( option.Category, categoryKey, StringComparison.Ordinal ) && option.IsVisible() )
			.OrderBy( option => option.Order )
			.Select( option => option.Section )
			.Distinct();
	}

	public static IEnumerable<AppSettingOption> GetOptionsForSection( string categoryKey, string section )
	{
		return Options.Where(
			option =>
				string.Equals( option.Category, categoryKey, StringComparison.Ordinal ) &&
				string.Equals( option.Section, section, StringComparison.Ordinal ) &&
				option.IsVisible()
		);
	}

	private static IReadOnlyList<AppSettingCategory> BuildCategories()
	{
		return new List<AppSettingCategory>
		{
			new() { Key = "general", Label = "General", Description = "Gameplay-facing preferences and comfort settings.", Order = 0 },
			new() { Key = "controls", Label = "Controls", Description = "Keyboard bindings for common actions.", Order = 5 },
			new() { Key = "video", Label = "Video", Description = "Rendering and upscaling settings.", Order = 10 },
			new() { Key = "audio", Label = "Audio", Description = "Sound and mix settings.", Order = 20 },
		};
	}

	private static IReadOnlyList<AppSettingOption> BuildOptions()
	{
		var options = new List<AppSettingOption>
		{
			EnumOption<FullscreenMode>(
				key: "display.fullscreen",
				category: "video",
				section: "Display",
				label: "Fullscreen",
				description: "Choose windowed, exclusive fullscreen, or borderless presentation.",
				order: 0,
				getter: AppSettings.GetFullscreenMode,
				setter: AppSettings.TrySetFullscreenMode
			),
			BoolOption(
				key: "display.vsync",
				category: "video",
				section: "Display",
				label: "VSync",
				description: "Synchronizes frames to your display refresh rate.",
				order: 1,
				getter: AppSettings.GetVSyncEnabled,
				setter: enabled => AppSettings.TrySetVSync( enabled )
			),
			BoolOption(
				key: "display.motion_blur",
				category: "video",
				section: "Display",
				label: "Motion Blur",
				description: "Controls camera and post-process motion blur.",
				order: 2,
				getter: AppSettings.GetMotionBlurEnabled,
				setter: enabled => AppSettings.TrySetMotionBlur( enabled ? 1f : 0f )
			),
			EnumOption<TextureQuality>(
				key: "video.texture_quality", category: "video", section: "Quality", label: "Texture Quality",
				description: "Controls texture detail and memory use.", order: 3,
				getter: AppSettings.GetTextureQuality, setter: AppSettings.TrySetTextureQuality ),
			EnumOption<ShadowQuality>(
				key: "video.shadow_quality", category: "video", section: "Quality", label: "Shadow Quality",
				description: "Controls shadow resolution and detail.", order: 4,
				getter: AppSettings.GetShadowQuality, setter: AppSettings.TrySetShadowQuality ),
			EnumOption<PostProcessQuality>(
				key: "video.post_process_quality", category: "video", section: "Quality", label: "Post-process Quality",
				description: "Controls ambient occlusion, depth of field, and other effects.", order: 5,
				getter: AppSettings.GetPostProcessQuality, setter: AppSettings.TrySetPostProcessQuality ),
			NamedEnumOption(
				key: "video.anti_aliasing", category: "video", section: "Quality", label: "Anti-aliasing",
				description: "Smooths jagged edges using the selected sample count.", order: 6,
				getter: AppSettings.GetAntiAliasQuality, setter: AppSettings.TrySetAntiAliasQuality,
				choices: new Dictionary<string, MultisampleAmount>
				{
					["Screen Default"] = MultisampleAmount.MultisampleScreen,
					["Off"] = MultisampleAmount.MultisampleNone,
					["2x MSAA"] = MultisampleAmount.Multisample2x,
					["4x MSAA"] = MultisampleAmount.Multisample4x,
					["6x MSAA"] = MultisampleAmount.Multisample6x,
					["8x MSAA"] = MultisampleAmount.Multisample8x,
					["16x MSAA"] = MultisampleAmount.Multisample16x
				} ),
			NamedEnumOption(
				key: "video.frame_rate", category: "video", section: "Performance", label: "Frame-rate Limit",
				description: "Caps rendered frames per second.", order: 7,
				getter: AppSettings.GetFrameRateLimit, setter: AppSettings.TrySetFrameRateLimit,
				choices: new Dictionary<string, FrameRateLimit>
				{
					["Unlimited"] = FrameRateLimit.Unlimited,
					["30 FPS"] = FrameRateLimit.Fps30,
					["60 FPS"] = FrameRateLimit.Fps60,
					["90 FPS"] = FrameRateLimit.Fps90,
					["120 FPS"] = FrameRateLimit.Fps120,
					["144 FPS"] = FrameRateLimit.Fps144,
					["165 FPS"] = FrameRateLimit.Fps165,
					["240 FPS"] = FrameRateLimit.Fps240
				} ),
			BoolOption(
				key: "video.volumetric_fog_enabled", category: "video", section: "Volumetric Fog", label: "Enabled",
				description: "Enables scene volumetric fog volumes.", order: 8,
				getter: AppSettings.GetVolumetricFogEnabled, setter: AppSettings.TrySetVolumetricFogEnabled ),
			EnumOption<VolumetricFogQuality>(
				key: "video.volumetric_fog_quality", category: "video", section: "Volumetric Fog", label: "Quality",
				description: "Controls volumetric fog rendering quality.", order: 9,
				getter: AppSettings.GetVolumetricFogQuality, setter: AppSettings.TrySetVolumetricFogQuality,
				isVisible: AppSettings.GetVolumetricFogEnabled ),
			EnumOption<UpscalerMode>(
				key: "video.upscaler",
				category: "video",
				section: "Upscaler",
				label: "Mode",
				description: "Sets the active render upscaler.",
				order: 10,
				getter: AppSettings.GetUpscaler,
				setter: AppSettings.TrySetUpscaler
			),
			EnumOption<Fsr3UpscalerQuality>(
				key: "video.fsr3_quality",
				category: "video",
				section: "Upscaler",
				label: "FSR3 Quality",
				description: "Adjusts the quality preset used when FSR3 is enabled.",
				order: 11,
				getter: AppSettings.GetFsr3Quality,
				setter: AppSettings.TrySetFsr3Quality,
				isVisible: () => AppSettings.GetUpscaler().ToString().Equals( "FSR3", StringComparison.OrdinalIgnoreCase )
			),
			IntOption(
				key: "audio.master_volume",
				category: "audio",
				section: "Mixer",
				label: "Master Volume",
				description: "Adjusts the master audio mix.",
				order: 20,
				min: 0,
				max: 100,
				step: 1,
				getter: AppSettings.GetVolume,
				setter: AppSettings.TrySetVolume,
				isAvailable: () => true
			),
			IntOption(
				key: "audio.music_volume",
				category: "audio",
				section: "Music",
				label: "Music Volume",
				description: "Adjusts soundtrack music without changing game or UI sounds.",
				order: 21,
				min: 0,
				max: 100,
				step: 1,
				getter: AppSettings.GetMusicVolume,
				setter: AppSettings.TrySetMusicVolume,
				isAvailable: () => true
			),
		};

		options.AddRange( GetEditableKeybinds().Select( ( action, index ) => KeybindOption(
			action: action,
			category: "controls",
			section: string.IsNullOrWhiteSpace( action.GroupName ) ? "Keyboard" : action.GroupName,
			order: 100 + index
		) ) );

		return options
		.OrderBy( option => option.Order )
		.ThenBy( option => option.Label )
		.ToList();
	}

	private static AppSettingOption BoolOption(
		string key,
		string category,
		string section,
		string label,
		string description,
		int order,
		Func<bool> getter,
		Func<bool, bool> setter,
		Func<bool> isVisible = null )
	{
		return new AppSettingOption
		{
			Key = key,
			Category = category,
			Section = section,
			Label = label,
			Description = description,
			Order = order,
			Kind = AppSettingOptionKind.Bool,
			IsVisible = isVisible ?? (() => true),
			GetBoolValue = getter,
			ToggleBoolValue = () => setter( !getter() )
		};
	}

	private static AppSettingOption EnumOption<TEnum>(
		string key,
		string category,
		string section,
		string label,
		string description,
		int order,
		Func<TEnum> getter,
		Func<TEnum, bool> setter,
		Func<bool> isVisible = null ) where TEnum : struct, Enum
	{
		return new AppSettingOption
		{
			Key = key,
			Category = category,
			Section = section,
			Label = label,
			Description = description,
			Order = order,
			Kind = AppSettingOptionKind.Enum,
			EnumNames = Enum.GetNames<TEnum>(),
			IsVisible = isVisible ?? (() => true),
			GetEnumValue = () => getter().ToString(),
			SetEnumValue = rawValue =>
			{
				if ( !Enum.TryParse<TEnum>( rawValue, true, out var parsedValue ) )
					return false;

				return setter( parsedValue );
			}
		};
	}

	private static AppSettingOption NamedEnumOption<TEnum>(
		string key, string category, string section, string label, string description, int order,
		Func<TEnum> getter, Func<TEnum, bool> setter, IReadOnlyDictionary<string, TEnum> choices,
		Func<bool> isVisible = null ) where TEnum : struct, Enum
	{
		return new AppSettingOption
		{
			Key = key,
			Category = category,
			Section = section,
			Label = label,
			Description = description,
			Order = order,
			Kind = AppSettingOptionKind.Enum,
			EnumNames = choices.Keys.ToArray(),
			IsVisible = isVisible ?? (() => true),
			GetEnumValue = () => choices.FirstOrDefault( pair => EqualityComparer<TEnum>.Default.Equals( pair.Value, getter() ) ).Key ?? choices.Keys.FirstOrDefault() ?? "",
			SetEnumValue = rawValue => choices.TryGetValue( rawValue ?? "", out var value ) && setter( value )
		};
	}

	private static AppSettingOption IntOption(
		string key,
		string category,
		string section,
		string label,
		string description,
		int order,
		int min,
		int max,
		int step,
		Func<int> getter,
		Func<int, bool> setter,
		Func<bool> isVisible = null,
		Func<bool> isAvailable = null )
	{
		return new AppSettingOption
		{
			Key = key,
			Category = category,
			Section = section,
			Label = label,
			Description = description,
			Order = order,
			Kind = AppSettingOptionKind.Int,
			IsVisible = isVisible ?? (() => true),
			IsAvailable = isAvailable ?? (() => true),
			IntMin = min,
			IntMax = max,
			IntStep = step,
			GetIntValue = getter,
			SetIntValue = rawValue => setter( rawValue )
		};
	}

	private static AppSettingOption KeybindOption(
		InputAction action,
		string category,
		string section,
		int order )
	{
		var actionName = action?.Name ?? "";
		var label = !string.IsNullOrWhiteSpace( action?.Title ) ? action.Title : actionName;

		return new AppSettingOption
		{
			Key = $"keybind.{actionName}",
			Category = category,
			Section = section,
			Label = label,
			Description = "Click to change this keyboard binding.",
			Order = order,
			Kind = AppSettingOptionKind.Keybind,
			GetKeybindValue = () => AppSettings.GetKeybind( action ),
			SetKeybindValue = keyboardCode => AppSettings.TrySetKeybind( action, keyboardCode ),
			GetKeybindConflict = keyboardCode => AppSettings.GetKeybindConflictName( action, keyboardCode )
		};
	}
}
