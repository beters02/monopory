using System;
using System.Collections.Generic;

public static partial class GameAssets
{
	public static partial class MouseIcons
	{
		private const string BasePath = "textures/icons/mouse/";

		private static readonly string[] Names =
		[
			"mouse",
			"mouse_horizontal",
			"mouse_left",
			"mouse_left_outline",
			"mouse_move",
			"mouse_outline",
			"mouse_right",
			"mouse_right_outline",
			"mouse_scroll",
			"mouse_scroll_down",
			"mouse_scroll_down_outline",
			"mouse_scroll_outline",
			"mouse_scroll_up",
			"mouse_scroll_up_outline",
			"mouse_scroll_vertical",
			"mouse_scroll_vertical_outline",
			"mouse_side",
			"mouse_side_back",
			"mouse_side_back_outline",
			"mouse_side_forward",
			"mouse_side_forward_outline",
			"mouse_side_outline",
			"mouse_small",
			"mouse_vertical",
		];

		private static readonly Dictionary<string, GameIcon> iconsByName = BuildIcons();

		public static IReadOnlyDictionary<string, GameIcon> All => iconsByName;

		public static readonly GameIcon Mouse = Get( "mouse" );
		public static readonly GameIcon Left = Get( "left" );
		public static readonly GameIcon Right = Get( "right" );
		public static readonly GameIcon Middle = Get( "scroll" );
		public static readonly GameIcon Scroll = Get( "scroll" );
		public static readonly GameIcon ScrollUp = Get( "scroll_up" );
		public static readonly GameIcon ScrollDown = Get( "scroll_down" );
		public static readonly GameIcon Move = Get( "move" );
		public static readonly GameIcon Side = Get( "side" );
		public static readonly GameIcon SideBack = Get( "side_back" );
		public static readonly GameIcon SideForward = Get( "side_forward" );

		public static GameIcon Get( string key, bool outline = false )
		{
			if ( TryGet( key, out var icon, outline ) )
				return icon;

			return outline && TryGet( "mouse", out icon, true ) ? icon : Mouse;
		}

		public static bool TryGet( string key, out GameIcon icon, bool outline = false )
		{
			icon = null;

			var name = NormalizeName( key );
			if ( string.IsNullOrWhiteSpace( name ) )
				return false;

			if ( outline && !name.EndsWith( "_outline", StringComparison.OrdinalIgnoreCase ) )
				name += "_outline";

			return iconsByName.TryGetValue( name, out icon );
		}

		public static void Preload()
		{
			foreach ( var name in Names )
			{
				iconsByName[name].Preload();
			}
		}

		private static Dictionary<string, GameIcon> BuildIcons()
		{
			var icons = new Dictionary<string, GameIcon>( StringComparer.OrdinalIgnoreCase );

			foreach ( var name in Names )
			{
				var icon = new GameIcon( $"{BasePath}{name}.svg" );
				icons[name] = icon;

				var shortName = name.StartsWith( "mouse_", StringComparison.OrdinalIgnoreCase )
					? name["mouse_".Length..]
					: name;

				icons.TryAdd( shortName, icon );
			}

			return icons;
		}

		private static string NormalizeName( string key )
		{
			var name = (key ?? "").Trim().ToLowerInvariant();
			if ( name.EndsWith( ".svg", StringComparison.OrdinalIgnoreCase ) )
				name = name[..^4];

			if ( name.StartsWith( "textures/icons/mouse/", StringComparison.OrdinalIgnoreCase ) )
				name = name["textures/icons/mouse/".Length..];

			if ( name.StartsWith( "mouse/", StringComparison.OrdinalIgnoreCase ) )
				name = name["mouse/".Length..];

			if ( name.StartsWith( "mouse_", StringComparison.OrdinalIgnoreCase ) )
				name = name["mouse_".Length..];

			name = name.Replace( " ", "_" );
			name = name.Replace( "-", "_" );

			return name switch
			{
				"left_click" => "left",
				"click_left" => "left",
				"attack1" => "left",
				"primary" => "left",
				"right_click" => "right",
				"click_right" => "right",
				"attack2" => "right",
				"secondary" => "right",
				"middle" => "scroll",
				"middle_click" => "scroll",
				"click_middle" => "scroll",
				"wheel" => "scroll",
				"wheel_up" => "scroll_up",
				"wheel_down" => "scroll_down",
				"side1" => "side_back",
				"side_1" => "side_back",
				"mouse4" => "side_back",
				"mouse_4" => "side_back",
				"side2" => "side_forward",
				"side_2" => "side_forward",
				"mouse5" => "side_forward",
				"mouse_5" => "side_forward",
				_ => name
			};
		}
	}
}
