using Sandbox.Engine.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

public enum AppSettingOptionKind
{
	Bool,
	Enum
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
	public Func<bool> IsAvailable { get; init; } = () => AppSettings.SettingsAvailable;
	public Func<bool> GetBoolValue { get; init; } = () => false;
	public Func<bool> ToggleBoolValue { get; init; } = () => false;
	public Func<string> GetEnumValue { get; init; } = () => "";
	public Func<string, bool> SetEnumValue { get; init; } = _ => false;
	public Func<int> GetIntValue { get; init; } = () => 0;
}

public static class AppSettingsSchema
{
	private static readonly IReadOnlyList<AppSettingCategory> categories = BuildCategories();
	private static readonly IReadOnlyList<AppSettingOption> options = BuildOptions();

	public static IReadOnlyList<AppSettingCategory> Categories => categories;
	public static IReadOnlyList<AppSettingOption> Options => options;

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
			new() { Key = "general", Label = "General", Description = "Core display and comfort settings.", Order = 0 },
			new() { Key = "video", Label = "Video", Description = "Rendering and upscaling settings.", Order = 10 },
			new() { Key = "audio", Label = "Audio", Description = "Sound and mix settings.", Order = 20 },
			new() { Key = "game", Label = "Game", Description = "Gameplay-facing preferences.", Order = 30 }
		};
	}

	private static IReadOnlyList<AppSettingOption> BuildOptions()
	{
		return new List<AppSettingOption>
		{
			EnumOption<FullscreenMode>(
				key: "display.fullscreen",
				category: "general",
				section: "Display",
				label: "Fullscreen",
				description: "Choose windowed, exclusive fullscreen, or borderless presentation.",
				order: 0,
				getter: AppSettings.GetFullscreenMode,
				setter: AppSettings.TrySetFullscreenMode
			),
			BoolOption(
				key: "display.vsync",
				category: "general",
				section: "Display",
				label: "VSync",
				description: "Synchronizes frames to your display refresh rate.",
				order: 1,
				getter: AppSettings.GetVSyncEnabled,
				setter: enabled => AppSettings.TrySetVSync( enabled )
			),
			BoolOption(
				key: "display.motion_blur",
				category: "general",
				section: "Display",
				label: "Motion Blur",
				description: "Controls camera and post-process motion blur.",
				order: 2,
				getter: AppSettings.GetMotionBlurEnabled,
				setter: enabled => AppSettings.TrySetMotionBlur( enabled ? 1f : 0f )
			),
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
			
		}
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

	private static AppSettingOption IntOption(
		string key,
		string category,
		string section,
		string label,
		string description,
		int order,
		Func<int> getter,
		Func<int, bool> setter,
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
			GetIntValue = getter,
			SetIntValue = rawValue => setter(rawValue)
		};
	}
}
