using System;
using Sandbox;

public sealed class SpaceDef
{
	public int Index { get; set; }
	public string Key { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public SpaceType Type { get; set; }

	// Property Data
	public int Price { get; set; }
	public int BaseRent { get; set; }
	public int OneHouseRent { get; set; }
	public int TwoHouseRent { get; set; }
	public int ThreeHouseRent { get; set; }
	public int FourHouseRent { get; set; }
	public int HotelRent { get; set; }

	public ColorGroup ColorGroup { get; set; } = ColorGroup.None;
	public string ColorGroupClass => ColorGroups.ToCssClass( ColorGroup );

	// Tax spaces
	public int TaxAmount { get; set; }

	// Text info
	public float TextScale = SpaceLayoutSettings.DefaultScale;

	public bool IsCorner {get; set;} = false;
}
