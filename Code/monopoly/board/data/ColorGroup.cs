using System;
using Sandbox;


public enum ColorGroup
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

public static class ColorGroups
{
	public static bool TryParse( string value, out ColorGroup colorGroup )
	{
		colorGroup = ColorGroup.None;

		if ( string.IsNullOrWhiteSpace( value ) )
			return true;

		colorGroup = value.Trim().ToLowerInvariant() switch
		{
			"brown" => ColorGroup.Brown,
			"light_blue" or "lightblue" => ColorGroup.LightBlue,
			"pink" => ColorGroup.Pink,
			"orange" => ColorGroup.Orange,
			"red" => ColorGroup.Red,
			"yellow" => ColorGroup.Yellow,
			"green" => ColorGroup.Green,
			"dark_blue" or "darkblue" => ColorGroup.DarkBlue,
			"none" => ColorGroup.None,
			_ => ColorGroup.None
		};

		return colorGroup != ColorGroup.None || value.Trim().Equals( "none", StringComparison.OrdinalIgnoreCase );
	}

	public static string ToCssClass( ColorGroup colorGroup )
	{
		return colorGroup switch
		{
			ColorGroup.Brown => "brown",
			ColorGroup.LightBlue => "light_blue",
			ColorGroup.Pink => "pink",
			ColorGroup.Orange => "orange",
			ColorGroup.Red => "red",
			ColorGroup.Yellow => "yellow",
			ColorGroup.Green => "green",
			ColorGroup.DarkBlue => "dark_blue",
			_ => "none"
		};
	}
}
