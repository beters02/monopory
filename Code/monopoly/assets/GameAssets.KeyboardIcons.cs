using System;
using System.Collections.Generic;

public static partial class GameAssets
{
	public static partial class KeyboardIcons
	{
		private const string BasePath = "textures/icons/keyboard/";

		private static readonly string[] Names =
		[
			"keyboard",
			"keyboard_0",
			"keyboard_0_outline",
			"keyboard_1",
			"keyboard_1_outline",
			"keyboard_2",
			"keyboard_2_outline",
			"keyboard_3",
			"keyboard_3_outline",
			"keyboard_4",
			"keyboard_4_outline",
			"keyboard_5",
			"keyboard_5_outline",
			"keyboard_6",
			"keyboard_6_outline",
			"keyboard_7",
			"keyboard_7_outline",
			"keyboard_8",
			"keyboard_8_outline",
			"keyboard_9",
			"keyboard_9_outline",
			"keyboard_a",
			"keyboard_a_outline",
			"keyboard_alt",
			"keyboard_alt_outline",
			"keyboard_any",
			"keyboard_any_outline",
			"keyboard_apostrophe",
			"keyboard_apostrophe_outline",
			"keyboard_arrow_down",
			"keyboard_arrow_down_outline",
			"keyboard_arrow_left",
			"keyboard_arrow_left_outline",
			"keyboard_arrow_right",
			"keyboard_arrow_right_outline",
			"keyboard_arrow_up",
			"keyboard_arrow_up_outline",
			"keyboard_arrows",
			"keyboard_arrows_all",
			"keyboard_arrows_down",
			"keyboard_arrows_down_outline",
			"keyboard_arrows_horizontal",
			"keyboard_arrows_horizontal_outline",
			"keyboard_arrows_left",
			"keyboard_arrows_left_outline",
			"keyboard_arrows_none",
			"keyboard_arrows_right",
			"keyboard_arrows_right_outline",
			"keyboard_arrows_up",
			"keyboard_arrows_up_outline",
			"keyboard_arrows_vertical",
			"keyboard_arrows_vertical_outline",
			"keyboard_asterisk",
			"keyboard_asterisk_outline",
			"keyboard_b",
			"keyboard_b_outline",
			"keyboard_backspace",
			"keyboard_backspace_icon",
			"keyboard_backspace_icon_alternative",
			"keyboard_backspace_icon_alternative_outline",
			"keyboard_backspace_icon_outline",
			"keyboard_backspace_outline",
			"keyboard_bracket_close",
			"keyboard_bracket_close_outline",
			"keyboard_bracket_greater",
			"keyboard_bracket_greater_outline",
			"keyboard_bracket_less",
			"keyboard_bracket_less_outline",
			"keyboard_bracket_open",
			"keyboard_bracket_open_outline",
			"keyboard_c",
			"keyboard_c_outline",
			"keyboard_capslock",
			"keyboard_capslock_icon",
			"keyboard_capslock_icon_outline",
			"keyboard_capslock_outline",
			"keyboard_caret",
			"keyboard_caret_outline",
			"keyboard_colon",
			"keyboard_colon_outline",
			"keyboard_comma",
			"keyboard_comma_outline",
			"keyboard_command",
			"keyboard_command_outline",
			"keyboard_ctrl",
			"keyboard_ctrl_outline",
			"keyboard_d",
			"keyboard_d_outline",
			"keyboard_delete",
			"keyboard_delete_outline",
			"keyboard_e",
			"keyboard_e_outline",
			"keyboard_end",
			"keyboard_end_outline",
			"keyboard_enter",
			"keyboard_enter_outline",
			"keyboard_equals",
			"keyboard_equals_outline",
			"keyboard_escape",
			"keyboard_escape_outline",
			"keyboard_exclamation",
			"keyboard_exclamation_outline",
			"keyboard_f",
			"keyboard_f_outline",
			"keyboard_f1",
			"keyboard_f1_outline",
			"keyboard_f10",
			"keyboard_f10_outline",
			"keyboard_f11",
			"keyboard_f11_outline",
			"keyboard_f12",
			"keyboard_f12_outline",
			"keyboard_f2",
			"keyboard_f2_outline",
			"keyboard_f3",
			"keyboard_f3_outline",
			"keyboard_f4",
			"keyboard_f4_outline",
			"keyboard_f5",
			"keyboard_f5_outline",
			"keyboard_f6",
			"keyboard_f6_outline",
			"keyboard_f7",
			"keyboard_f7_outline",
			"keyboard_f8",
			"keyboard_f8_outline",
			"keyboard_f9",
			"keyboard_f9_outline",
			"keyboard_function",
			"keyboard_function_outline",
			"keyboard_g",
			"keyboard_g_outline",
			"keyboard_h",
			"keyboard_h_outline",
			"keyboard_home",
			"keyboard_home_outline",
			"keyboard_i",
			"keyboard_i_outline",
			"keyboard_insert",
			"keyboard_insert_outline",
			"keyboard_j",
			"keyboard_j_outline",
			"keyboard_k",
			"keyboard_k_outline",
			"keyboard_l",
			"keyboard_l_outline",
			"keyboard_m",
			"keyboard_m_outline",
			"keyboard_minus",
			"keyboard_minus_outline",
			"keyboard_n",
			"keyboard_n_outline",
			"keyboard_numlock",
			"keyboard_numlock_outline",
			"keyboard_numpad_enter",
			"keyboard_numpad_enter_outline",
			"keyboard_numpad_plus",
			"keyboard_numpad_plus_outline",
			"keyboard_o",
			"keyboard_o_outline",
			"keyboard_option",
			"keyboard_option_outline",
			"keyboard_outline",
			"keyboard_p",
			"keyboard_p_outline",
			"keyboard_page_down",
			"keyboard_page_down_outline",
			"keyboard_page_up",
			"keyboard_page_up_outline",
			"keyboard_pause",
			"keyboard_pause_break",
			"keyboard_pause_break_outline",
			"keyboard_pause_outline",
			"keyboard_period",
			"keyboard_period_outline",
			"keyboard_plus",
			"keyboard_plus_outline",
			"keyboard_printscreen",
			"keyboard_printscreen_outline",
			"keyboard_q",
			"keyboard_q_outline",
			"keyboard_question",
			"keyboard_question_outline",
			"keyboard_quote",
			"keyboard_quote_outline",
			"keyboard_r",
			"keyboard_r_outline",
			"keyboard_return",
			"keyboard_return_outline",
			"keyboard_s",
			"keyboard_s_outline",
			"keyboard_scroll_lock",
			"keyboard_scroll_lock_outline",
			"keyboard_semicolon",
			"keyboard_semicolon_outline",
			"keyboard_shift",
			"keyboard_shift_icon",
			"keyboard_shift_icon_outline",
			"keyboard_shift_outline",
			"keyboard_slash_back",
			"keyboard_slash_back_outline",
			"keyboard_slash_forward",
			"keyboard_slash_forward_outline",
			"keyboard_space",
			"keyboard_space_icon",
			"keyboard_space_icon_outline",
			"keyboard_space_outline",
			"keyboard_t",
			"keyboard_t_outline",
			"keyboard_tab",
			"keyboard_tab_icon",
			"keyboard_tab_icon_alternative",
			"keyboard_tab_icon_alternative_outline",
			"keyboard_tab_icon_outline",
			"keyboard_tab_outline",
			"keyboard_tilde",
			"keyboard_tilde_outline",
			"keyboard_u",
			"keyboard_u_outline",
			"keyboard_underscore",
			"keyboard_underscore_outline",
			"keyboard_v",
			"keyboard_v_outline",
			"keyboard_w",
			"keyboard_w_outline",
			"keyboard_win",
			"keyboard_win_outline",
			"keyboard_x",
			"keyboard_x_outline",
			"keyboard_y",
			"keyboard_y_outline",
			"keyboard_z",
			"keyboard_z_outline",
		];

		private static readonly Dictionary<string, GameIcon> iconsByName = BuildIcons();

		public static IReadOnlyDictionary<string, GameIcon> All => iconsByName;

		public static readonly GameIcon Keyboard = Get( "keyboard" );
		public static readonly GameIcon Any = Get( "any" );
		public static readonly GameIcon Escape = Get( "escape" );
		public static readonly GameIcon Enter = Get( "enter" );
		public static readonly GameIcon Space = Get( "space" );
		public static readonly GameIcon Tab = Get( "tab" );
		public static readonly GameIcon Shift = Get( "shift" );
		public static readonly GameIcon Ctrl = Get( "ctrl" );
		public static readonly GameIcon Alt = Get( "alt" );
		public static readonly GameIcon ArrowUp = Get( "arrow_up" );
		public static readonly GameIcon ArrowDown = Get( "arrow_down" );
		public static readonly GameIcon ArrowLeft = Get( "arrow_left" );
		public static readonly GameIcon ArrowRight = Get( "arrow_right" );

		public static GameIcon Get( string key, bool outline = false )
		{
			if ( TryGet( key, out var icon, outline ) )
				return icon;

			return outline && TryGet( "any", out icon, true ) ? icon : Any;
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

				var shortName = name.StartsWith( "keyboard_", StringComparison.OrdinalIgnoreCase )
					? name["keyboard_".Length..]
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

			if ( name.StartsWith( "textures/icons/keyboard/", StringComparison.OrdinalIgnoreCase ) )
				name = name["textures/icons/keyboard/".Length..];

			if ( name.StartsWith( "keyboard/", StringComparison.OrdinalIgnoreCase ) )
				name = name["keyboard/".Length..];

			if ( name.StartsWith( "keyboard_", StringComparison.OrdinalIgnoreCase ) )
				name = name["keyboard_".Length..];

			name = name.Replace( " ", "_" );
			name = name.Replace( "-", "_" );

			return name switch
			{
				"esc" => "escape",
				"spacebar" => "space",
				"control" => "ctrl",
				"cmd" => "command",
				"windows" => "win",
				"up" => "arrow_up",
				"down" => "arrow_down",
				"left" => "arrow_left",
				"right" => "arrow_right",
				_ => name
			};
		}
	}
}
