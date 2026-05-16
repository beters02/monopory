using System;
using Sandbox;


public enum MonopolyColorGroup
{
	None,
	Brown,
	LightBlue,
	Pink,
	Orange,
	Red,
	Yellow,
	Green,
	DarkBlue
}

public static class MonopolyColorGroups
{
	public static bool TryParse( string value, out MonopolyColorGroup colorGroup )
	{
		colorGroup = MonopolyColorGroup.None;

		if ( string.IsNullOrWhiteSpace( value ) )
			return true;

		colorGroup = value.Trim().ToLowerInvariant() switch
		{
			"brown" => MonopolyColorGroup.Brown,
			"light_blue" or "lightblue" => MonopolyColorGroup.LightBlue,
			"pink" => MonopolyColorGroup.Pink,
			"orange" => MonopolyColorGroup.Orange,
			"red" => MonopolyColorGroup.Red,
			"yellow" => MonopolyColorGroup.Yellow,
			"green" => MonopolyColorGroup.Green,
			"dark_blue" or "darkblue" => MonopolyColorGroup.DarkBlue,
			"none" => MonopolyColorGroup.None,
			_ => MonopolyColorGroup.None
		};

		return colorGroup != MonopolyColorGroup.None || value.Trim().Equals( "none", StringComparison.OrdinalIgnoreCase );
	}

	public static string ToCssClass( MonopolyColorGroup colorGroup )
	{
		return colorGroup switch
		{
			MonopolyColorGroup.Brown => "brown",
			MonopolyColorGroup.LightBlue => "light_blue",
			MonopolyColorGroup.Pink => "pink",
			MonopolyColorGroup.Orange => "orange",
			MonopolyColorGroup.Red => "red",
			MonopolyColorGroup.Yellow => "yellow",
			MonopolyColorGroup.Green => "green",
			MonopolyColorGroup.DarkBlue => "dark_blue",
			_ => "none"
		};
	}
}
