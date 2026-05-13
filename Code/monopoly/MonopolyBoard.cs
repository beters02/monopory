using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Sandbox;

public sealed class MonopolyBoard : Component
{
	private static MonopolyBoard instance;
	public static MonopolyBoard Instance => instance;
	private MonopolyGame GameRef;
	private DebugHelper debugHelper = new();

	[Property] public List<MonopolySpace> Spaces { get; set; } = new();
	public List<MonopolySpaceDef> SpaceDefs { get; private set; } = new();

	[Property] public Vector3 HitboxSize { get; set; } = new Vector3( 96f, 96f, 12f );
	[Property] public GameObject BoardPlaneGameObject {get; set;}
	[Property] public bool UseWorldPanelLabels { get; set; } = false;

	[Property, Change("OnDebugEnabledChanged")] public bool DebugEnabled {get; set;} = false;
	private void OnDebugEnabledChanged(bool _, bool newValue) => debugHelper.SetEnabled(newValue);

	// Component

	protected override void OnStart()
	{
		instance = this;
		debugHelper.SetEnabled(DebugEnabled);

		GameRef = Scene.GetAllComponents<MonopolyGame>().FirstOrDefault();

		LoadBoardDefinitions();
		InitSpaces();
	}

	protected override void OnUpdate()
	{
		DrawAllHitboxesDebug();
		UpdateSpaceHoverCursor();
		UpdateLocalSpaceSelection();
	}

	// Spaces & Defs initializing
	private void InitSpaces()
	{
		foreach ( var space in Spaces )
		{
			space.EnsureHitbox();
			space.SetHitboxEnabled(true);
			space.EnsureDef(GetSpaceDef(space.Index));

			if (space.Def == null)
			{
				Log.Warning($"Space at index {space.Index} has no definition! Skipping label creation and modifications.");
				continue;
			}

			ApplySpaceModifications(space);
			TryCreateLabel(space);
		}
	}

	private void ApplySpaceModifications(MonopolySpace space)
	{
		var quad = space.Def.GetQuadrant();
		if (quad == 0)
			space.ModifyColliderSize(1f, 1f);
		else if (quad == 1)
		{
			ColliderSizeModNon0(space);
			ColliderSizeMod1or3(space);
		}
		else if (quad == 2)
		{
			ColliderSizeModNon0(space);
			ColliderSizeMod2or4(space);
		}
		else if (quad == 3)
		{
			ColliderSizeModNon0(space);
			ColliderSizeMod1or3(space);
		} else if (quad == 4)
		{
			space.ModifyColliderCenter(0f, 1.5f);
			ColliderSizeModNon0(space);
			ColliderSizeMod2or4(space);
		}
	}

	private void ColliderSizeMod1or3(MonopolySpace space) => space.ModifyColliderSize(0.5f);
	private void ColliderSizeMod2or4(MonopolySpace space) => space.ModifyColliderSize(1f);
	private void ColliderSizeModNon0(MonopolySpace space) => space.ModifyColliderSize(0f, 3f);
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
				DisplayName = "de_nuke", // Harvey Milk Blvd
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 2,
				OneHouseRent = 10,
				TwoHouseRent = 30,
				ThreeHouseRent = 90,
				FourHouseRent = 160,
				HotelRent = 250,
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
				Key = "property_brown_1", // Chiraq
				DisplayName = "de_miraq",
				Type = SpaceType.Property,
				Price = 60,
				BaseRent = 4,
				OneHouseRent = 20,
				TwoHouseRent = 60,
				ThreeHouseRent = 180,
				FourHouseRent = 320,
				HotelRent = 450,
				ColorGroup = "brown"
			},
			new()
			{
				Index = 4,
				Key = "tax_income",
				DisplayName = "Plug\nTax",
				Type = SpaceType.Tax,
				TaxAmount = 200
			},
			new()
			{
				Index = 5,
				Key = "railroad_0",
				DisplayName = "Season Railroad",
				Type = SpaceType.Railroad,
				Price = 200,
				BaseRent = 100
			},
			new()
			{
				Index = 6,
				Key = "property_light_blue_0",
				DisplayName = "CSGOWild.com", // CSGO Blackjack
				Type = SpaceType.Property,
				Price = 100,
				BaseRent = 6,
				OneHouseRent = 30,
				TwoHouseRent = 90,
				ThreeHouseRent = 270,
				FourHouseRent = 400,
				HotelRent = 550,
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
				DisplayName = "CSGO\nRoll\n.com",
				Type = SpaceType.Property,
				Price = 100,
				BaseRent = 6,
				OneHouseRent = 30,
				TwoHouseRent = 90,
				ThreeHouseRent = 270,
				FourHouseRent = 400,
				HotelRent = 550,
				ColorGroup = "light_blue"
			},
			new()
			{
				Index = 9,
				Key = "property_light_blue_2",
				DisplayName = "CSGO\nBlackjack\n.com", // wild
				Type = SpaceType.Property,
				Price = 120,
				BaseRent = 8,
				OneHouseRent = 40,
				TwoHouseRent = 100,
				ThreeHouseRent = 300,
				FourHouseRent = 450,
				HotelRent = 600,
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
				DisplayName = "Bryce's Skunky Dungeon",
				Type = SpaceType.Property,
				Price = 140,
				BaseRent = 10,
				OneHouseRent = 50,
				TwoHouseRent = 150,
				ThreeHouseRent = 450,
				FourHouseRent = 625,
				HotelRent = 750,
				ColorGroup = "pink"
			},
			new()
			{
				Index = 12,
				Key = "utility_0",
				DisplayName = "Kickapoo Casino",
				Type = SpaceType.Utility,
				Price = 60,
				BaseRent = 2,
			},
			new()
			{
				Index = 13,
				Key = "property_pink_1",
				DisplayName = "Fitz' FN FREEHAND",
				Type = SpaceType.Property,
				Price = 140,
				BaseRent = 10,
				OneHouseRent = 50,
				TwoHouseRent = 150,
				ThreeHouseRent = 450,
				FourHouseRent = 625,
				HotelRent = 750,
				ColorGroup = "pink"
			},
			new()
			{
				Index = 14,
				Key = "property_pink_2",
				DisplayName = "Brycen's Goon Cave",
				Type = SpaceType.Property,
				Price = 160,
				BaseRent = 12,
				OneHouseRent = 60,
				TwoHouseRent = 180,
				ThreeHouseRent = 500,
				FourHouseRent = 700,
				HotelRent = 900,
				ColorGroup = "pink"
			},
			new()
			{
				Index = 15,
				Key = "railroad_1",
				DisplayName = "Train Railroad",
				Type = SpaceType.Railroad,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 16,
				Key = "property_orange_0",
				DisplayName = "Yodie-Land",
				Type = SpaceType.Property,
				Price = 180,
				BaseRent = 14,
				OneHouseRent = 70,
				TwoHouseRent = 200,
				ThreeHouseRent = 550,
				FourHouseRent = 750,
				HotelRent = 950,
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
				DisplayName = "Section 80",
				Type = SpaceType.Property,
				Price = 180,
				BaseRent = 14,
				OneHouseRent = 70,
				TwoHouseRent = 200,
				ThreeHouseRent = 550,
				FourHouseRent = 750,
				HotelRent = 950,
				ColorGroup = "orange"
			},
			new()
			{
				Index = 19,
				Key = "property_orange_2",
				DisplayName = "The Liqo Sto",
				Type = SpaceType.Property,
				Price = 200,
				BaseRent = 16,
				OneHouseRent = 80,
				TwoHouseRent = 220,
				ThreeHouseRent = 600,
				FourHouseRent = 800,
				HotelRent = 100,
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
				DisplayName = "Landon's Room",
				Type = SpaceType.Property,
				Price = 220,
				BaseRent = 18,
				OneHouseRent = 90,
				TwoHouseRent = 250,
				ThreeHouseRent = 700,
				FourHouseRent = 875,
				HotelRent = 1050,
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
				DisplayName = "Caden's Cockhouse",
				Type = SpaceType.Property,
				Price = 220,
				BaseRent = 18,
				OneHouseRent = 90,
				TwoHouseRent = 250,
				ThreeHouseRent = 700,
				FourHouseRent = 875,
				HotelRent = 1050,
				ColorGroup = "red"
			},
			new()
			{
				Index = 24,
				Key = "property_red_2",
				DisplayName = "Luke's Law",
				Type = SpaceType.Property,
				Price = 240,
				BaseRent = 20,
				OneHouseRent = 100,
				TwoHouseRent = 300,
				ThreeHouseRent = 750,
				FourHouseRent = 925,
				HotelRent = 1100,
				ColorGroup = "red"
			},
			new()
			{
				Index = 25,
				Key = "railroad_2",
				DisplayName = "Cobblestone Railroad",
				Type = SpaceType.Railroad,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 26,
				Key = "property_yellow_0",
				DisplayName = "The Co-Op",
				Type = SpaceType.Property,
				Price = 260,
				BaseRent = 22,
				OneHouseRent = 110,
				TwoHouseRent = 330,
				ThreeHouseRent = 800,
				FourHouseRent = 975,
				HotelRent = 1150,
				ColorGroup = "yellow"
			},
			new()
			{
				Index = 27,
				Key = "property_yellow_1",
				DisplayName = "Ethan's Dirty Den",
				Type = SpaceType.Property,
				Price = 260,
				BaseRent = 22,
				OneHouseRent = 110,
				TwoHouseRent = 330,
				ThreeHouseRent = 800,
				FourHouseRent = 975,
				HotelRent = 1150,
				ColorGroup = "yellow"
			},
			new()
			{
				Index = 28,
				Key = "utility_1",
				DisplayName = "Riverwind Casino",
				Type = SpaceType.Utility,
				Price = 60,
				BaseRent = 2
			},
			new()
			{
				Index = 29,
				Key = "property_yellow_2",
				DisplayName = "Shrine Auditorium",
				Type = SpaceType.Property,
				Price = 280,
				BaseRent = 24,
				OneHouseRent = 120,
				TwoHouseRent = 360,
				ThreeHouseRent = 850,
				FourHouseRent = 1025,
				HotelRent = 1200,
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
				DisplayName = "Chance",
				Type = SpaceType.Property,
				Price = 300,
				BaseRent = 26,
				OneHouseRent = 130,
				TwoHouseRent = 390,
				ThreeHouseRent = 900,
				FourHouseRent = 1105,
				HotelRent = 1275,
				ColorGroup = "green"
			},
			new()
			{
				Index = 32,
				Key = "property_green_1",
				DisplayName = "Troop",
				Type = SpaceType.Property,
				Price = 300,
				BaseRent = 26,
				OneHouseRent = 130,
				TwoHouseRent = 390,
				ThreeHouseRent = 900,
				FourHouseRent = 1105,
				HotelRent = 1275,
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
				DisplayName = "Blayze's Giant Balls",
				Type = SpaceType.Property,
				Price = 320,
				BaseRent = 28,
				OneHouseRent = 150,
				TwoHouseRent = 450,
				ThreeHouseRent = 1000,
				FourHouseRent = 1200,
				HotelRent = 1400,
				ColorGroup = "green"
			},
			new()
			{
				Index = 35,
				Key = "railroad_3",
				DisplayName = "Cache Railroad",
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
				DisplayName = "The Mos Eisley Cantina",
				Type = SpaceType.Property,
				Price = 350,
				BaseRent = 35,
				OneHouseRent = 175,
				TwoHouseRent = 500,
				ThreeHouseRent = 1100,
				FourHouseRent = 1300,
				HotelRent = 1500,
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
				DisplayName = "The Grand Casino",
				Type = SpaceType.Property,
				Price = 400,
				BaseRent = 50,
				OneHouseRent = 200,
				TwoHouseRent = 600,
				ThreeHouseRent = 1400,
				FourHouseRent = 1700,
				HotelRent = 1500,
				ColorGroup = "dark_blue"
			},
		};
	}
	private void TryCreateLabel(MonopolySpace space)
	{
		if (!ShouldCreateLabel(space))
			return;

		if ( UseWorldPanelLabels )
			space.CreateWorldPanelLabel();
		else
			space.CreateLabel();
	}
	private bool ShouldCreateLabel( MonopolySpace space )
	{
		switch ( space.Def.Type )
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

	// Space Helpers
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

	public static MonopolySpaceDef GetSpaceDefStatic(int index)
	{
		return instance.GetSpaceDef(index);
	}

	public Vector3 GetSpacePosition( int index )
	{
		var space = GetSpace( index );
		return space?.TokenPosition ?? Vector3.Zero;
	}

	public static Vector3 GetHitboxSize(int SpaceIndex)
	{
		var isCorner =
			SpaceIndex == 0 ||
			SpaceIndex == 10 ||
			SpaceIndex == 20 ||
			SpaceIndex == 30;

		var cornHbs = MonopolySpaceSettings.CornerHitboxSize;
		var regHbs = MonopolySpaceSettings.HitboxSize;

		if (isCorner)
			return cornHbs;

		if (GetQuadFromIndex(SpaceIndex) == 1 || GetQuadFromIndex(SpaceIndex) == 3)
			return new Vector3(regHbs.y, regHbs.x, regHbs.z);

		return regHbs;
	}

	public static int GetQuadFromIndex(int Index)
	{
		if (Index == 0 || Index == 10 || Index == 20 || Index == 30)
		{
			return 0;
		} else if(Index < 10)
		{
			return 1;
		} else if (Index < 20)
		{
			return 2;
		} else if (Index < 30)
		{
			return 3;
		}
		return 4;
	}

	// Local Space Selection
	private void UpdateLocalSpaceSelection()
	{
		if ( !Input.Pressed( "attack1" ) )
			return;

		SceneTraceResult? initialTraceResult = GetSelectionMouseTraceResult();
		if (initialTraceResult == null)
			return;

		SceneTraceResult traceResult = initialTraceResult.Value;
		if (DebugEnabled)
			DebugOverlay.Trace( traceResult, 5f, true );

		HandleTraceResult(traceResult, out MonopolySpace space, out MonopolySpaceDef _);

		if ( GameRef is null || space is null )
			return;
		
		GameRef.SelectSpace( space.Index );
	}

	private SceneTraceResult? GetSelectionMouseTraceResult()
	{
		var camera = Scene.Camera;
		if ( camera is null )
			return null;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		return Scene.Trace.Ray( ray, 5000f )
			.UseHitboxes()
			.HitTriggers()
			.Run();
	}

	private void UpdateSpaceHoverCursor()
	{
		var traceResult = GetSelectionMouseTraceResult();
		var isHoveringSpace =
			traceResult.HasValue &&
			traceResult.Value.Hit &&
			traceResult.Value.GameObject is not null &&
			traceResult.Value.GameObject.Components.TryGet<MonopolySpace>( out _ );

		Mouse.CursorType = isHoveringSpace ? "pointer" : null;
	}

	private void HandleTraceResult(SceneTraceResult traceResult, out MonopolySpace foundSpace, out MonopolySpaceDef foundSpaceDef)
	{
		foundSpace = null;
		foundSpaceDef = null;

		// start building debug string
		var str = $"Hit: {traceResult.Hit}, Object: {traceResult.GameObject?.Name}";
		
		if ( !traceResult.Hit || traceResult.GameObject is null )
		{
			debugHelper.LogInfo(str + " (no hit)");
			return;
		}

		if ( !traceResult.GameObject.Components.TryGet<MonopolySpace>( out var space ) )
		{
			debugHelper.LogInfo(str + " (not a space)");
			return;
		}

		if (traceResult.Collider is BoxCollider)
		{
			var trc = traceResult.Collider as BoxCollider;
			str += $", Collider Center: {trc.Center}, Collider Size: {trc.Scale}";
		}

		var def = GetSpaceDef(space.Index);
		str += $", SpaceDef: {def?.DisplayName} (Quad: {def?.GetQuadrant()} Index: {def?.Index}, Type: {def?.Type})";
		debugHelper.LogInfo(str);
		
		foundSpace = space;
		foundSpaceDef = def;
	}

	// DEBUG
	private void DrawAllHitboxesDebug()
	{
		if (!DebugEnabled)
			return;
		
		foreach ( var space in Spaces )
		{
			if ( space is null )
				continue;

			var ind = space.Index;
			DebugOverlay.Box(
				space.GetColliderCenter() ?? Vector3.Zero,
				space.GetColliderSize() ?? GetHitboxSize(ind),
				Color.Green,
				0f,
				default,
				true
			);
		}
	}
}
