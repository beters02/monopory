using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Sandbox;

public sealed class Board : Component
{
	private static readonly Regex SpaceCardTextTokenRegex = new( @"\{space_(\d+)\}", RegexOptions.IgnoreCase | RegexOptions.Compiled );
	private static Board instance;
	public static Board Instance => instance;
	private GameController GameRef;
	private Logger logger = new("Board");
	private Logger boardSpace = new("BoardSpace");

	[Property] public List<BoardSpace> Spaces { get; set; } = new();
	public List<SpaceDef> SpaceDefs { get; private set; } = new();
	public RailroadDef RailroadData { get; private set; } = new();
	public UtilityDef UtilityData { get; private set; } = new();

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
	[Property] public GameObject HousePrefab { get; set; }
	[Property] public GameObject HotelPrefab { get; set; }
	[Property] public Vector3 HouseImprovementPrefabScale { get; set; } = Vector3.One;
	[Property] public Vector3 HotelImprovementPrefabScale { get; set; } = Vector3.One;
	[Property] public float ImprovementPrefabZOffset { get; set; } = 0.9f;
	[Property] public float ImprovementPrefabEdgeInset { get; set; } = 2.12f;
	[Property] public float ImprovementPrefabSideInset { get; set; } = 2.23f;
	[Property] public float ImprovementPrefabSpacing { get; set; } = 3.52f;
	[Property] public WorldPanel BoardWorldPanel {get; set;}

	public List<CardDef> ChanceCards { get; private set; } = new();
	public List<CardDef> CommunityChestCards { get; private set; } = new();
	
	public static bool UseProceduralBoardPanelStatic => Instance.UseProceduralBoardPanel;

	[Property, Change("OnDebugEnabledChanged")] public bool DebugEnabled {get; set;} = false;
	private void OnDebugEnabledChanged(bool _, bool newValue) => logger.SetEnabled(newValue);

	private float lastProcBoardWorldScale;
	private float lastProcRefPanelSize;
	private string lastCursorType;
	private Vector2? lastPointerHoverPosition;
	private string appliedBoardSpaceNamesSnapshot = "";

	// Component

	protected override void OnStart()
	{
		instance = this;

		if ( MonopolyApp.IsDebugEnabled() )
			DebugEnabled = true;

		lastProcBoardWorldScale = ProceduralBoardWorldScale;
		lastProcRefPanelSize = ProceduralReferencePanelSize;

#if STANDALONE
		if (DebugEnabled && !MonopolyApp.IsGameLaunchedWithDebugConvar())
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
		EnsureBoardSpaceNameConfigApplied();
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
				HousePrefab,
				HotelPrefab,
				HouseImprovementPrefabScale,
				HotelImprovementPrefabScale,
				ImprovementPrefabZOffset,
				ImprovementPrefabEdgeInset,
				ImprovementPrefabSideInset,
				ImprovementPrefabSpacing
			);
		}
	}

	private void LoadBoardDefinitions()
	{
		SpaceDefs = BoardData.CreateSpaceDefs();
		appliedBoardSpaceNamesSnapshot = "";
		EnsureBoardSpaceNameConfigApplied( true );
		RailroadData = BoardData.CreateRailroadDefs();
		UtilityData = BoardData.CreateUtilityDefs();
	}

	private void EnsureBoardSpaceNameConfigApplied( bool force = false )
	{
		if ( SpaceDefs is null || SpaceDefs.Count == 0 )
			return;

		var snapshot = GetCurrentBoardSpaceNamesSnapshot();
		if ( !force && string.Equals( appliedBoardSpaceNamesSnapshot, snapshot, StringComparison.Ordinal ) )
			return;

		BoardSpaceNameConfig.ApplySnapshot( SpaceDefs, snapshot );
		appliedBoardSpaceNamesSnapshot = snapshot;
		RefreshSpaceDefinitions();
		LoadCardDefinitions();
		RefreshProceduralBoardPanel();
	}

	private string GetCurrentBoardSpaceNamesSnapshot()
	{
		EnsureGameControllerRef();

		var snapshot = GameRef?.Config?.BoardSpaceNamesSnapshot ?? "";
		if ( !string.IsNullOrWhiteSpace( snapshot ) )
			return snapshot;

		var bootstrap = MatchBootstrap.Current;
		return bootstrap?.HasConfig == true ? bootstrap.Config?.BoardSpaceNamesSnapshot ?? "" : "";
	}

	private void RefreshSpaceDefinitions()
	{
		if ( Spaces is null )
			return;

		foreach ( var space in Spaces )
		{
			if ( space is null )
				continue;

			space.EnsureDef( GetSpaceDef( space.Index ) );
		}
	}

	private void LoadCardDefinitions()
	{
		ChanceCards = CardData.CreateChanceCards();
		CommunityChestCards = CardData.CreateCommunityChestCards();

		ResolveCardTextTokens( ChanceCards );
		ResolveCardTextTokens( CommunityChestCards );
	}

	private void ResolveCardTextTokens( IEnumerable<CardDef> cards )
	{
		if ( cards is null )
			return;

		foreach ( var card in cards )
		{
			if ( card is null )
				continue;

			card.Title = ResolveCardTextTokens( card.Title );
			card.Description = ResolveCardTextTokens( card.Description );
		}
	}

	private string ResolveCardTextTokens( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) || SpaceDefs is null || SpaceDefs.Count == 0 )
			return text ?? "";

		return SpaceCardTextTokenRegex.Replace( text, match =>
		{
			if ( !int.TryParse( match.Groups[1].Value, out var spaceIndex ) )
				return match.Value;

			spaceIndex = GameController.NormalizeSpaceIndex( spaceIndex );
			var spaceName = SpaceDefs.ElementAtOrDefault( spaceIndex )?.DisplayName;
			if ( string.IsNullOrWhiteSpace( spaceName ) )
				return match.Value;

			return Regex.Replace( spaceName, @"\s+", " " ).Trim();
		} );
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
		EnsureGameControllerRef();

		if ( !Input.Pressed( "Attack1" ) )
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
			.WithTag( "board_space" )
			.Run();
	}

	private void UpdateSpaceHoverCursor()
	{
		var traceResult = GetSelectionMouseTraceResult();
		var isHoveringSpace = IsHoveringSelectableSpace( traceResult );
		var desiredCursor = isHoveringSpace ? "pointer" : "default";
		const float PointerLatchRadius = 4f;

		// Some click frames briefly miss ray hits even when hovering the same space.
		// Preserve prior pointer state near the last confirmed pointer-hover location.
		if ( desiredCursor is null &&
			lastPointerHoverPosition.HasValue &&
			(Mouse.Position - lastPointerHoverPosition.Value).Length <= PointerLatchRadius &&
			string.Equals( lastCursorType, "pointer", StringComparison.Ordinal ) )
		{
			desiredCursor = "pointer";
		}

		Mouse.CursorType = desiredCursor;

		if ( string.Equals( desiredCursor, "pointer", StringComparison.Ordinal ) && isHoveringSpace )
			lastPointerHoverPosition = Mouse.Position;
		
		lastCursorType = desiredCursor;
	}

	private bool IsHoveringSelectableSpace( SceneTraceResult? traceResult )
	{
		EnsureGameControllerRef();

		if ( !traceResult.HasValue || !traceResult.Value.Hit || traceResult.Value.GameObject is null )
			return false;

		if ( GameRef is null || !GameRef.CanLeaveCurrentSelectedSpace() )
			return false;

		bool FoundBoardSpace = TryGetBoardSpaceFromGameObject( traceResult.Value.GameObject, out var space );
		if ( !FoundBoardSpace || space is null )
			return false;

		SpaceDef def = GetSpaceDef( space.Index );

		if ( def is null )
			return false;

		return GameRef.ShouldShowSelectedSpaceCard( def.Type );
	}

	private void EnsureGameControllerRef()
	{
		if ( GameRef is null )
			GameRef = Scene?.GetAllComponents<GameController>().FirstOrDefault();
	}

	private static bool TryGetBoardSpaceFromGameObject( GameObject gameObject, out BoardSpace space )
	{
		space = null;

		if ( gameObject is null )
			return false;

		space = gameObject.GetComponent<BoardSpace>();

		return space is not null;
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
