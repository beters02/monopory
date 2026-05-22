using System;
using Sandbox;

public static class ColorUtils
{
	public static Color FromHex( string hex )
	{
		return FromRgb255( HexToRgbVector3( hex ) );
	}

	public static Color FromRgb255( Vector3 rgb )
	{
		return new Color( ToByte( rgb.x ) / 255f, ToByte( rgb.y ) / 255f, ToByte( rgb.z ) / 255f, 1f );
	}

	public static Vector3 HexToRgbVector3( string hex )
	{
		var normalized = NormalizeHex( hex );

		return new Vector3(
			Convert.ToInt32( normalized.Substring( 0, 2 ), 16 ),
			Convert.ToInt32( normalized.Substring( 2, 2 ), 16 ),
			Convert.ToInt32( normalized.Substring( 4, 2 ), 16 ) );
	}

	public static uint ToRgbUInt( Vector3 rgb )
	{
		return ((uint)ToByte( rgb.x ) << 16)
			| ((uint)ToByte( rgb.y ) << 8)
			| ToByte( rgb.z );
	}

	private static string NormalizeHex( string hex )
	{
		if ( string.IsNullOrWhiteSpace( hex ) )
			throw new ArgumentException( "Hex color cannot be empty.", nameof( hex ) );

		var normalized = hex.Trim().TrimStart( '#' );

		if ( normalized.Length != 6 )
			throw new ArgumentException( "Hex color must be in RRGGBB format.", nameof( hex ) );

		return normalized;
	}

	private static byte ToByte( float value )
	{
		return (byte)Math.Clamp( (int)value, 0, 255 );
	}
}

public static class Vector3ColorExtensions
{
	public static Color HexToColor( this string hex )
	{
		return ColorUtils.FromHex( hex );
	}

	public static Vector3 HexToRgbVector3( this string hex )
	{
		return ColorUtils.HexToRgbVector3( hex );
	}

	public static Color ToRgbColor( this Vector3 rgb )
	{
		return ColorUtils.FromRgb255( rgb );
	}

	public static uint ToRgbUInt( this Vector3 rgb )
	{
		return ColorUtils.ToRgbUInt( rgb );
	}
}
