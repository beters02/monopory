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
	public float TextScale = MonopolySpaceSettings.DefaultScale;

	private int Quadrant {get; set;}

	public int GetQuadrant()
	{
		if (Quadrant == 0 && !IsCorner)
		{
			if (Index == 0 || Index == 10 || Index == 20 || Index == 30)
			{
				Quadrant = 0; // Corners don't have a quadrant
				IsCorner = true;
			} else if(Index < 10)
			{
				Quadrant = 1;
			} else if (Index < 20)
			{
				Quadrant = 2;
			} else if (Index < 30)
			{
				Quadrant = 3;
			} else if (Index < 40)
			{
				Quadrant = 4;
			}
		}

		return Quadrant;
	}

	public bool IsCorner {get; set;} = false;
}
