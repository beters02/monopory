using Sandbox;

public sealed class MonopolyTheme : Component
{
	[Property] public Color Player1Color { get; set; } = ColorUtils.FromHex( "ff4d4d" );
	[Property] public Color Player2Color { get; set; } = ColorUtils.FromHex( "4da6ff" );
	[Property] public Color Player3Color { get; set; } = ColorUtils.FromHex( "57d67b" );
	[Property] public Color Player4Color { get; set; } = ColorUtils.FromHex( "ffb84d" );
	[Property] public Color Player5Color { get; set; } = ColorUtils.FromHex( "d36bff" );
	[Property] public Color Player6Color { get; set; } = ColorUtils.FromHex( "4de1d4" );
	[Property] public Color Player7Color { get; set; } = ColorUtils.FromHex( "ffe45e" );
	[Property] public Color Player8Color { get; set; } = ColorUtils.FromHex( "ff7ab6" );

	public static Color MonopolyGoldColor { get; set; } = ColorUtils.FromHex("e0bd68");

	public Color GetPlayerColor( int playerIndex )
	{
		return playerIndex switch
		{
			0 => Player1Color,
			1 => Player2Color,
			2 => Player3Color,
			3 => Player4Color,
			4 => Player5Color,
			5 => Player6Color,
			6 => Player7Color,
			7 => Player8Color,
			_ => Color.White
		};
	}
}
