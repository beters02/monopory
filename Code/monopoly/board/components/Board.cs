using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Sandbox;

public sealed class Board : Component
{
	private static Board instance;
	public static Board Instance => instance;
	private GameController GameRef;
	private Logger logger = new("Board");
	private Logger boardSpace = new("BoardSpace");

	[Property] public List<BoardSpace> Spaces { get; set; } = new();
	public List<SpaceDef> SpaceDefs { get; private set; } = new();

	[Property] public Vector3 HitboxSize { get; set; } = new Vector3( 96f, 96f, 12f );
	[Property] public bool UseWorldPanelLabels { get; set; } = false;
	[Property, Change(nameof(OnUseProceduralBoardPanelChanged))] public bool UseProceduralBoardPanel { get; set; } = false;
	[Property] public Sandbox.ui.BoardPanel ProceduralBoardPanel { get; set; }
	[Property] public float ProceduralBoardHalfSize { get; set; } = 43f;
	[Property] public float ProceduralCornerSize { get; set; } = 13f;
	[Property] public float ProceduralRegularSpaceLength { get; set; } = 8f;
	[Property] public float ProceduralSpaceZOffset { get; set; } = 0f;
	[Property] public float ProceduralReferencePanelSize { get; set; } = 2000f;
	[Property] public float ProceduralBoardWorldScale { get; set; } = 1f;
	[Property] public Model HouseModel { get; set; }
	[Property] public Model HotelModel { get; set; }
	[Property] public Vector3 ImprovementModelScale { get; set; } = Vector3.One;
	[Property] public float ImprovementModelZOffset { get; set; } = 0.9f;
	[Property] public WorldPanel BoardWorldPanel {get; set;}

	public List<CardDef> ChanceCards { get; private set; } = new();
	public List<CardDef> CommunityChestCards { get; private set; } = new();
	
	public static bool UseProceduralBoardPanelStatic => Instance.UseProceduralBoardPanel;

	[Property, Change("OnDebugEnabledChanged")] public bool DebugEnabled {get; set;} = false;
	private void OnDebugEnabledChanged(bool _, bool newValue) => logger.SetEnabled(newValue);

	private float lastProcBoardWorldScale;
	private float lastProcRefPanelSize;

	// Component

	protected override void OnStart()
	{
		instance = this;

		var debugConvarParsed = bool.TryParse(ConsoleSystem.GetValue( "debug" ), out bool debugConvar);
		if (debugConvarParsed && debugConvar)
			DebugEnabled = true;

		lastProcBoardWorldScale = ProceduralBoardWorldScale;
		lastProcRefPanelSize = ProceduralReferencePanelSize;

		#if STANDALONE
		if (DebugEnabled && !debugConvar)
		{
			DebugEnabled = false;
			Log.Warning("Game was published with Board DebugEnabled!");
		}
		#endif

		logger.SetEnabled(DebugEnabled);

		GameRef = Scene.GetAllComponents<GameController>().FirstOrDefault();

		LoadBoardDefinitions();
		LoadCardDefinitions();
		RefreshProceduralBoardPanel();
		InitSpaces();
	}

	protected override void OnUpdate()
	{
		DrawAllHitboxesDebug();
		UpdateSpaceHoverCursor();
		UpdateLocalSpaceSelection();
		UpdateSpaceImprovements();

		if (lastProcBoardWorldScale != ProceduralBoardWorldScale || lastProcRefPanelSize != ProceduralReferencePanelSize)
		{
			UpdateSpacesPosAndSize();
		}
	}

	private void OnUseProceduralBoardPanelChanged( bool _, bool __ )
	{
		RefreshProceduralBoardPanel();
	}

	private void RefreshProceduralBoardPanel()
	{
		ProceduralBoardPanel ??= Scene.GetAllComponents<Sandbox.ui.BoardPanel>().FirstOrDefault();
		BoardWorldPanel ??= Scene.GetAllComponents<WorldPanel>().FirstOrDefault();
		ProceduralBoardPanel?.StateHasChanged();
	}

	private Vector3 GetProceduralSpacePosition( Rect spaceRect )
	{
		return BoardMath.RectPercentToLocal( spaceRect, GetBoardWorldSize(), ProceduralSpaceZOffset );
	}

	private Vector3 GetProceduralSpaceSize( Rect rect )
	{
		return BoardMath.RectPercentToColliderSize( rect, GetBoardWorldSize() );
	}

	private float GetBoardWorldSize()
	{
		var baseBoardSize = ProceduralBoardHalfSize * 2f;

		if ( BoardWorldPanel is not null && BoardWorldPanel.PanelSize.x > 0f )
		{
			baseBoardSize *= BoardWorldPanel.PanelSize.x / ProceduralReferencePanelSize;
		}

		return baseBoardSize * ProceduralBoardWorldScale;
	}

	private void UpdateSpacesPosAndSize()
	{
		foreach ( var space in Spaces )
		{
			if ( space?.Def is null )
				continue;

			Rect spaceRect = BoardMath.GetSpaceRect( space.Def.Index, ProceduralBoardPanel );
			space.GameObject.LocalPosition = GetProceduralSpacePosition( spaceRect );
			space.SetHitboxOverride( GetProceduralSpaceSize( spaceRect ) );
		}

		lastProcBoardWorldScale = ProceduralBoardWorldScale;
		lastProcRefPanelSize = ProceduralReferencePanelSize;
	}

	// Spaces & Defs initializing
	private void InitSpaces()
	{

		if (UseProceduralBoardPanel)
		{
			Spaces = new();
			foreach ( var def in SpaceDefs.OrderBy( space => space.Index ) )
			{
				Rect spaceRect = BoardMath.GetSpaceRect( def.Index, ProceduralBoardPanel );
				var spaceObject = new GameObject( true, $"Space_{def.Index:00}_{def.Key}" );
				spaceObject.SetParent( GameObject );
				spaceObject.LocalPosition = GetProceduralSpacePosition( spaceRect );
				spaceObject.LocalRotation = Rotation.Identity;
				spaceObject.LocalScale = Vector3.One;

				var boardSpace = spaceObject.Components.Create<BoardSpace>();
				boardSpace.Index = def.Index;
				boardSpace.SetHitboxOverride( GetProceduralSpaceSize( spaceRect ) );

				Spaces.Add( boardSpace );
			}
		}

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

			if (!UseProceduralBoardPanel)
			{
				ApplySpaceModifications(space);
				TryCreateLabel(space);
			}
			
		}
	}

	private void ApplySpaceModifications(BoardSpace space)
	{
		var quad = GetSpaceQuadrant( space.Index );
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
		//if (UseProceduralBoardPanel)
		//	return;
		
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
		bool didNormalize = GameController.TryNormalizeSpaceIndex(index, out int normalizedSpaceIndex);

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
		bool didNormalize = GameController.TryNormalizeSpaceIndex(index, out int normalizedSpaceIndex);

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

	public static int GetSpaceQuadrant( int index )
	{
		return index switch
		{
			10 or 20 or 30 or 40 => 0,
			< 10 => 1,
			< 20 => 2,
			< 30 => 3,
			_ => 4
		};
	}

	public static int GetSpaceQuadrantIncludeCorners( int index )
	{
		return index switch
		{
			< 10 => 1,
			< 20 => 2,
			< 30 => 3,
			_ => 4
		};
	}

	public static Vector3 GetHitboxSize(int SpaceIndex)
	{
		var isCorner =
			SpaceIndex == 0 ||
			SpaceIndex == 10 ||
			SpaceIndex == 20 ||
			SpaceIndex == 30;

		var cornHbs = SpaceLayoutSettings.CornerHitboxSize;
		var regHbs = SpaceLayoutSettings.HitboxSize;

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
		str += $", SpaceDef: {def?.DisplayName} (Quad: {GetSpaceQuadrant( def.Index )} Index: {def?.Index}, Type: {def?.Type})";
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
