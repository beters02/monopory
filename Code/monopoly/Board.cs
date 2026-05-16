using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Sandbox;

public sealed class Board : Component
{
	private static Board instance;
	public static Board Instance => instance;
	private MonopolyGame GameRef;
	private Logger logger = new("Board");
	private Logger boardSpace = new("BoardSpace");

	[Property] public List<BoardSpace> Spaces { get; set; } = new();
	public List<SpaceDef> SpaceDefs { get; private set; } = new();

	[Property] public Vector3 HitboxSize { get; set; } = new Vector3( 96f, 96f, 12f );
	[Property] public GameObject BoardPlaneGameObject {get; set;}
	[Property] public bool UseWorldPanelLabels { get; set; } = false;
	[Property] public Model HouseModel { get; set; }
	[Property] public Model HotelModel { get; set; }
	[Property] public Vector3 ImprovementModelScale { get; set; } = Vector3.One;
	[Property] public float ImprovementModelZOffset { get; set; } = 0.9f;

	public List<CardDef> ChanceCards { get; private set; } = new();
	public List<CardDef> CommunityChestCards { get; private set; } = new();

	[Property, Change("OnDebugEnabledChanged")] public bool DebugEnabled {get; set;} = false;
	private void OnDebugEnabledChanged(bool _, bool newValue) => logger.SetEnabled(newValue);

	// Component

	protected override void OnStart()
	{
		instance = this;

		var debugConvarParsed = bool.TryParse(ConsoleSystem.GetValue( "debug" ), out bool debugConvar);
		if (debugConvarParsed && debugConvar)
			DebugEnabled = true;

		#if STANDALONE
		if (DebugEnabled && !debugConvar)
		{
			DebugEnabled = false;
			Log.Warning("Game was published with Board DebugEnabled!");
		}
		#endif

		logger.SetEnabled(DebugEnabled);

		GameRef = Scene.GetAllComponents<MonopolyGame>().FirstOrDefault();

		LoadBoardDefinitions();
		LoadCardDefinitions();
		InitSpaces();
	}

	protected override void OnUpdate()
	{
		DrawAllHitboxesDebug();
		UpdateSpaceHoverCursor();
		UpdateLocalSpaceSelection();
		UpdateSpaceImprovements();
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

	private void ApplySpaceModifications(BoardSpace space)
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

	private void ColliderSizeMod1or3(BoardSpace space) => space.ModifyColliderSize(0.5f);
	private void ColliderSizeMod2or4(BoardSpace space) => space.ModifyColliderSize(1f);
	private void ColliderSizeModNon0(BoardSpace space) => space.ModifyColliderSize(0f, 3f);

	private void UpdateSpaceImprovements()
	{
		if ( GameRef is null )
			return;

		foreach ( var space in Spaces )
		{
			if ( space?.Def is null || space.Def.Type != SpaceType.Property )
				continue;

			space.SetImprovementVisuals(
				GameRef.GetImprovementCount( space.Index ),
				HouseModel,
				HotelModel,
				ImprovementModelScale,
				ImprovementModelZOffset
			);
		}
	}

	private void LoadBoardDefinitions()
	{
		SpaceDefs = BoardData.CreateSpaceDefs();
	}

	private void LoadCardDefinitions()
	{
		ChanceCards = CardData.CreateChanceCards();
		CommunityChestCards = CardData.CreateCommunityChestCards();
	}


	private void TryCreateLabel(BoardSpace space)
	{
		if (!ShouldCreateLabel(space))
			return;

		if ( UseWorldPanelLabels )
			space.CreateWorldPanelLabel();
		else
			space.CreateLabel();
	}
	private bool ShouldCreateLabel( BoardSpace space )
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
	public BoardSpace GetSpace( int index )
	{
		int originalIndex = index;

		boardSpace.Info("RETRIEVING SPACE FOR INDEX: " + index);
		bool didNormalize = MonopolyGame.TryNormalizeSpaceIndex(index, out int normalizedSpaceIndex);

		if (didNormalize)
		{
			boardSpace.Info($"NORMALIZED SPACE INDEX {index} -> {normalizedSpaceIndex}");
			index = normalizedSpaceIndex;
		}

		if ( Spaces.Count == 0 )
		{
			boardSpace.Error("COULD NOT RETRIEVE SPACE. SPACES LIST IS EMPTY.");
			GameRef?.ForceEndGameFromException(new InvalidOperationException("No available board spaces."));
			return null;
		}

		if ( index < 0 || index >= Spaces.Count )
		{
			boardSpace.Error($"COULD NOT RETRIEVE SPACE. SPACES[{index}] IS OUT OF RANGE. SPACES.COUNT: {Spaces.Count}.");
			GameRef?.ForceEndGameFromException(new InvalidOperationException($"Board space index {index} is out of range."));
			return null;
		}

		var space = Spaces[index];
		if ( space is null )
		{
			boardSpace.Error($"COULD NOT RETRIEVE SPACE. SPACES[{index}] RETURNED NULL. SPACES.COUNT: {Spaces.Count}.");
			GameRef?.ForceEndGameFromException(new InvalidOperationException($"Board space index {index} is null."));
			return null;
		}

		string succMsg = $"SUCCESSFULLY RETRIEVED SPACE FROM INDEX: {originalIndex}";
		if (didNormalize)
			succMsg += $"->{normalizedSpaceIndex}->{space.DisplayName}";
		else
			succMsg += $"->{space.DisplayName}";
		
		boardSpace.Info(succMsg);
		return space;
	}

	public SpaceDef GetSpaceDef(int index)
	{
		int originalIndex = index;

		boardSpace.Info("RETRIEVING SPACE DEF FOR INDEX: " + index);
		bool didNormalize = MonopolyGame.TryNormalizeSpaceIndex(index, out int normalizedSpaceIndex);

		if (didNormalize)
		{
			boardSpace.Info($"NORMALIZED SPACE DEF INDEX {index} -> {normalizedSpaceIndex}");
			index = normalizedSpaceIndex;
		}

		if ( SpaceDefs.Count == 0 )
		{
			boardSpace.Error("COULD NOT RETRIEVE SPACE DEF. SPACEDEFS LIST IS EMPTY.");
			GameRef?.ForceEndGameFromException(new InvalidOperationException("No available board space definitions."));
			return null;
		}

		if ( index < 0 || index >= SpaceDefs.Count )
		{
			boardSpace.Error($"COULD NOT RETRIEVE SPACE DEF. SPACEDEFS[{index}] IS OUT OF RANGE. SPACEDEFS.COUNT: {SpaceDefs.Count}.");
			GameRef?.ForceEndGameFromException(new InvalidOperationException($"Board space definition index {index} is out of range."));
			return null;
		}

		var spaceDef = SpaceDefs[index];
		if ( spaceDef is null )
		{
			boardSpace.Error($"COULD NOT RETRIEVE SPACE DEF. SPACEDEFS[{index}] RETURNED NULL. SPACEDEFS.COUNT: {SpaceDefs.Count}.");
			GameRef?.ForceEndGameFromException(new InvalidOperationException($"Board space definition index {index} is null."));
			return null;
		}

		string succMsg = $"SUCCESSFULLY RETRIEVED SPACE DEF FROM INDEX: {originalIndex}";
		if (didNormalize)
			succMsg += $"->{normalizedSpaceIndex}->{spaceDef.DisplayName}";
		else
			succMsg += $"->{spaceDef.DisplayName}";

		boardSpace.Info(succMsg);
		return spaceDef;
	}

	public static SpaceDef GetSpaceDefStatic(int index)
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
		if ( initialTraceResult == null )
			return;

		SceneTraceResult traceResult = initialTraceResult.Value;
		if ( DebugEnabled )
			DebugOverlay.Trace( traceResult, 5f, true );

		HandleTraceResult( traceResult, out BoardSpace space, out SpaceDef _ );

		if ( GameRef is null || space is null )
			return;
		
		if ( !GameRef.CanLeaveCurrentSelectedSpace() )
		{
			SpaceDef def = GetSpaceDef( GameRef.LocalSelectedSpaceIndex );
			GameRef.ShowLocalPopup(
				"Decision required",
				$"Buy or auction {def.DisplayName} before closing this card.",
				PopupKind.Warning,
				true,
				3f
			);
			return;
		}
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
			traceResult.Value.GameObject.Components.TryGet<BoardSpace>( out _ );

		Mouse.CursorType = isHoveringSpace ? "pointer" : null;
	}

	private void HandleTraceResult(SceneTraceResult traceResult, out BoardSpace foundSpace, out SpaceDef foundSpaceDef)
	{
		foundSpace = null;
		foundSpaceDef = null;

		// start building debug string
		var str = $"Hit: {traceResult.Hit}, Object: {traceResult.GameObject?.Name}";
		
		if ( !traceResult.Hit || traceResult.GameObject is null )
		{
			logger.Info(str + " (no hit)");
			return;
		}

		if ( !traceResult.GameObject.Components.TryGet<BoardSpace>( out var space ) )
		{
			logger.Info(str + " (not a space)");
			return;
		}

		if (traceResult.Collider is BoxCollider)
		{
			var trc = traceResult.Collider as BoxCollider;
			str += $", Collider Center: {trc.Center}, Collider Size: {trc.Scale}";
		}

		var def = GetSpaceDef(space.Index);
		str += $", SpaceDef: {def?.DisplayName} (Quad: {def?.GetQuadrant()} Index: {def?.Index}, Type: {def?.Type})";
		logger.Info(str);
		
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
