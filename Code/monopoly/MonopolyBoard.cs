using Sandbox;

public sealed class MonopolyBoard : Component
{
	[Property] public List<MonopolySpace> Spaces { get; set; } = new();
	public List<MonopolySpaceDef> SpaceDefs { get; private set; } = new();

	protected override void OnStart()
	{
		LoadBoardDefinitions();

		foreach ( var space in Spaces )
		{
			var def = GetSpaceDef( space.Index );

			if ( def == null )
			{
				Log.Info("def for " + space.Index + " is null.");
				continue;
			}
				
			if ( ShouldCreateLabel( def.Type ) )
			{
				space.CreateLabel( def );
			}
		}
	}

	private bool ShouldCreateLabel( SpaceType type )
	{
		switch ( type )
		{
			case SpaceType.CommunityChest:
			case SpaceType.Go:
			case SpaceType.Chance:
			case SpaceType.FreeParking:
			case SpaceType.GoToJail:
				return false;

			default:
				return true;
		}
	}

	private void LoadBoardDefinitions()
	{
		SpaceDefs = new()
		{
			new()
			{
				Index = 0,
				Key = "go",
				DisplayName = "GO",
				Type = SpaceType.Go
			},
			new()
			{
				Index = 1,
				Key = "property_brown_0",
				DisplayName = "Brown 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "brown"
			},
			new()
			{
				Index = 2,
				Key = "chest_0",
				DisplayName = "Community Chest",
				Type = SpaceType.CommunityChest,
			},
			new()
			{
				Index = 3,
				Key = "property_brown_1",
				DisplayName = "Brown 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "brown"
			},
			new()
			{
				Index = 4,
				Key = "tax_income",
				DisplayName = "Income Tax",
				Type = SpaceType.Tax,
				TaxAmount = 200
			},
			new()
			{
				Index = 5,
				Key = "railroad_0",
				DisplayName = "Railroad 0",
				Type = SpaceType.Railroad,
				Price = 200,
				BaseRent = 100
			},
			new()
			{
				Index = 6,
				Key = "property_light_blue_0",
				DisplayName = "Light Blue 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "light_blue"
			},
			new()
			{
				Index = 7,
				Key = "chance_0",
				DisplayName = "Chance",
				Type = SpaceType.Chance
			},
			new()
			{
				Index = 8,
				Key = "property_light_blue_1",
				DisplayName = "Light Blue 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "light_blue"
			},
			new()
			{
				Index = 9,
				Key = "property_light_blue_2",
				DisplayName = "Light Blue 2",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "light_blue"
			},
			new()
			{
				Index = 10,
				Key = "jail",
				DisplayName = "Jail",
				Type = SpaceType.Jail
			},
			new()
			{
				Index = 11,
				Key = "property_pink_0",
				DisplayName = "Pink 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "pink"
			},
			new()
			{
				Index = 12,
				Key = "utility_0",
				DisplayName = "Utility 0",
				Type = SpaceType.Utility,
				Price = 60,
				BaseRent = 2,
			},
			new()
			{
				Index = 13,
				Key = "property_pink_1",
				DisplayName = "Pink 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "pink"
			},
			new()
			{
				Index = 14,
				Key = "property_pink_2",
				DisplayName = "Pink 2",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "pink"
			},
			new()
			{
				Index = 15,
				Key = "railroad_1",
				DisplayName = "Railroad 1",
				Type = SpaceType.Railroad,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 16,
				Key = "property_orange_0",
				DisplayName = "Orange 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "orange"
			},
			new()
			{
				Index = 17,
				Key = "chest_1",
				DisplayName = "Community Chest",
				Type = SpaceType.CommunityChest
			},
			new()
			{
				Index = 18,
				Key = "property_orange_1",
				DisplayName = "Orange 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "orange"
			},
			new()
			{
				Index = 19,
				Key = "property_orange_2",
				DisplayName = "Orange 2",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "orange"
			},
			new()
			{
				Index = 20,
				Key = "free_parking",
				DisplayName = "Free Parking",
				Type = SpaceType.FreeParking
			},
			new()
			{
				Index = 21,
				Key = "property_red_0",
				DisplayName = "Red 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "red"
			},
			new()
			{
				Index = 22,
				Key = "chance_1",
				DisplayName = "Chance",
				Type = SpaceType.Chance
			},
			new()
			{
				Index = 23,
				Key = "property_red_1",
				DisplayName = "Red 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "red"
			},
			new()
			{
				Index = 24,
				Key = "property_red_2",
				DisplayName = "Red 2",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "red"
			},
			new()
			{
				Index = 25,
				Key = "railroad_2",
				DisplayName = "Railroad 2",
				Type = SpaceType.Railroad,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 26,
				Key = "property_yellow_0",
				DisplayName = "Yellow 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "yellow"
			},
			new()
			{
				Index = 27,
				Key = "property_yellow_1",
				DisplayName = "Yellow 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "yellow"
			},
			new()
			{
				Index = 28,
				Key = "utility_1",
				DisplayName = "Utility 1",
				Type = SpaceType.Utility,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 29,
				Key = "property_yellow_2",
				DisplayName = "Yellow 2",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "yellow"
			},
			new()
			{
				Index = 30,
				Key = "go_to_jail",
				DisplayName = "Go to Jail",
				Type = SpaceType.GoToJail
			},
			new()
			{
				Index = 31,
				Key = "property_green_0",
				DisplayName = "Green 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "green"
			},
			new()
			{
				Index = 32,
				Key = "property_green_1",
				DisplayName = "Green 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "green"
			},
			new()
			{
				Index = 33,
				Key = "chest_2",
				DisplayName = "Community Chest",
				Type = SpaceType.CommunityChest
			},
			new()
			{
				Index = 34,
				Key = "property_green_2",
				DisplayName = "Green 2",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "green"
			},
			new()
			{
				Index = 35,
				Key = "railroad_3",
				DisplayName = "Railroad 3",
				Type = SpaceType.Railroad,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 36,
				Key = "chance_2",
				DisplayName = "Chance",
				Type = SpaceType.Chance
			},
			new()
			{
				Index = 37,
				Key = "property_dark_blue_0",
				DisplayName = "Dark Blue 0",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "dark_blue"
			},
			new()
			{
				Index = 38,
				Key = "tax_luxury",
				DisplayName = "Luxury Tax",
				Type = SpaceType.Tax,
				TaxAmount = 200
			},
			new()
			{
				Index = 39,
				Key = "property_dark_blue_1",
				DisplayName = "Dark Blue 1",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				ColorGroup = "dark_blue"
			},
		};
	}


	public MonopolySpace GetSpace( int index )
	{
		if ( Spaces.Count == 0 || Spaces.Count - 1 < index)
			return null;

		index = ((index % Spaces.Count) + Spaces.Count) % Spaces.Count;
		return Spaces[index];
	}

	public MonopolySpaceDef GetSpaceDef(int index)
	{
		if ( Spaces.Count == 0 || Spaces.Count - 1 < index)
			return null;

		index = ((index % Spaces.Count) + Spaces.Count) % Spaces.Count;
		return SpaceDefs[index];
	}

	public Vector3 GetSpacePosition( int index )
	{
		var space = GetSpace( index );
		return space?.TokenPosition ?? Vector3.Zero;
	}
}