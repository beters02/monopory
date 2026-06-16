using System;
using Sandbox;

public sealed class BoardDefinition
{
	public string Id { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public List<SpaceDef> Spaces { get; set; } = new();
	public List<CardDef> ChanceCards { get; set; } = new();
	public List<CardDef> CommunityChestCards { get; set; } = new();
	public RailroadDef RailroadData { get; set; } = new();
	public UtilityDef UtilityData { get; set; } = new();
	public BoardLayoutDefinition Layout { get; set; } = new();
	public BoardThemeDefinition Theme { get; set; } = new();

	public int SpaceCount => Spaces?.Count ?? 0;
	public int GoSpaceIndex => Layout?.GoSpaceIndex ?? 0;
	public int JailSpaceIndex => Layout?.JailSpaceIndex ?? 0;

	public BoardDefinition Clone()
	{
		return new BoardDefinition
		{
			Id = Id,
			DisplayName = DisplayName,
			Spaces = Spaces?.Select( CloneSpace ).ToList() ?? new(),
			ChanceCards = ChanceCards?.Select( CloneCard ).ToList() ?? new(),
			CommunityChestCards = CommunityChestCards?.Select( CloneCard ).ToList() ?? new(),
			RailroadData = CloneRailroad( RailroadData ),
			UtilityData = CloneUtility( UtilityData ),
			Layout = Layout?.Clone() ?? new(),
			Theme = Theme?.Clone() ?? new()
		};
	}

	private static SpaceDef CloneSpace( SpaceDef space )
	{
		if ( space is null )
			return null;

		return new SpaceDef
		{
			Index = space.Index,
			Key = space.Key,
			DisplayName = space.DisplayName,
			Type = space.Type,
			Price = space.Price,
			BaseRent = space.BaseRent,
			OneHouseRent = space.OneHouseRent,
			TwoHouseRent = space.TwoHouseRent,
			ThreeHouseRent = space.ThreeHouseRent,
			FourHouseRent = space.FourHouseRent,
			HotelRent = space.HotelRent,
			ColorGroup = space.ColorGroup,
			TaxAmount = space.TaxAmount,
			TextScale = space.TextScale,
			IsCorner = space.IsCorner
		};
	}

	private static CardDef CloneCard( CardDef card )
	{
		if ( card is null )
			return null;

		return new CardDef
		{
			Key = card.Key,
			Title = card.Title,
			Description = card.Description,
			Deck = card.Deck,
			Action = card.Action,
			Weight = card.Weight,
			Amount = card.Amount,
			TargetSpaceIndex = card.TargetSpaceIndex,
			RelativeSpaces = card.RelativeSpaces,
			CollectGo = card.CollectGo,
			ResolveDestination = card.ResolveDestination,
			HouseAmount = card.HouseAmount,
			HotelAmount = card.HotelAmount,
			GambleType = card.GambleType
		};
	}

	private static RailroadDef CloneRailroad( RailroadDef source )
	{
		return source is null
			? new RailroadDef()
			: new RailroadDef
			{
				Price = source.Price,
				OneOwnedRent = source.OneOwnedRent,
				TwoOwnedRent = source.TwoOwnedRent,
				ThreeOwnedRent = source.ThreeOwnedRent,
				FourOwnedRent = source.FourOwnedRent
			};
	}

	private static UtilityDef CloneUtility( UtilityDef source )
	{
		return source is null
			? new UtilityDef()
			: new UtilityDef
			{
				Price = source.Price,
				OneOwnedMultiplier = source.OneOwnedMultiplier,
				BothOwnedMultiplier = source.BothOwnedMultiplier
			};
	}
}

public sealed class BoardLayoutDefinition
{
	public List<int> CornerIndexes { get; set; } = new() { 0, 10, 20, 30 };
	public List<float> SpaceLengthWeights { get; set; } = new();
	public float CornerSizePercent { get; set; } = 15f;
	public float TrackDepthPercent { get; set; } = 15f;
	public int GoSpaceIndex { get; set; } = 0;
	public int JailSpaceIndex { get; set; } = 10;
	public int FreeParkingSpaceIndex { get; set; } = 20;
	public int GoToJailSpaceIndex { get; set; } = 30;

	public BoardLayoutDefinition Clone()
	{
		return new BoardLayoutDefinition
		{
			CornerIndexes = CornerIndexes?.ToList() ?? new(),
			SpaceLengthWeights = SpaceLengthWeights?.ToList() ?? new(),
			CornerSizePercent = CornerSizePercent,
			TrackDepthPercent = TrackDepthPercent,
			GoSpaceIndex = GoSpaceIndex,
			JailSpaceIndex = JailSpaceIndex,
			FreeParkingSpaceIndex = FreeParkingSpaceIndex,
			GoToJailSpaceIndex = GoToJailSpaceIndex
		};
	}

	public static BoardLayoutDefinition Classic( int spaceCount = 40 )
	{
		return new BoardLayoutDefinition
		{
			CornerIndexes = new() { 0, 10, 20, 30 },
			SpaceLengthWeights = Enumerable.Repeat( 1f, Math.Max( spaceCount, 0 ) ).ToList(),
			GoSpaceIndex = 0,
			JailSpaceIndex = 10,
			FreeParkingSpaceIndex = 20,
			GoToJailSpaceIndex = 30
		};
	}

	public bool IsCornerIndex( int spaceIndex )
	{
		return CornerIndexes?.Contains( spaceIndex ) == true;
	}

	public int GetCornerOrder( int spaceIndex )
	{
		return CornerIndexes?.IndexOf( spaceIndex ) ?? -1;
	}

	public int GetSideIndex( int spaceIndex, int spaceCount )
	{
		if ( spaceCount <= 0 )
			return 0;

		var corners = GetNormalizedCorners( spaceCount );
		if ( spaceIndex <= corners[1] )
			return 0;

		if ( spaceIndex <= corners[2] )
			return 1;

		if ( spaceIndex <= corners[3] )
			return 2;

		return 3;
	}

	public int GetFirstRegularIndexForSide( int sideIndex, int spaceCount )
	{
		var corners = GetNormalizedCorners( spaceCount );
		return sideIndex switch
		{
			0 => corners[0] + 1,
			1 => corners[1] + 1,
			2 => corners[2] + 1,
			_ => corners[3] + 1
		};
	}

	public int GetLastRegularIndexForSide( int sideIndex, int spaceCount )
	{
		var corners = GetNormalizedCorners( spaceCount );
		return sideIndex switch
		{
			0 => corners[1] - 1,
			1 => corners[2] - 1,
			2 => corners[3] - 1,
			_ => spaceCount - 1
		};
	}

	private int[] GetNormalizedCorners( int spaceCount )
	{
		var fallback = new[] { 0, 10, 20, 30 };
		var source = CornerIndexes is { Count: >= 4 } ? CornerIndexes : fallback.ToList();
		return source
			.Take( 4 )
			.Select( index => Math.Clamp( index, 0, Math.Max( spaceCount - 1, 0 ) ) )
			.OrderBy( index => index )
			.ToArray();
	}
}

public sealed class BoardThemeDefinition
{
	public string BoardEdgeColor { get; set; } = "";
	public string CenterColor { get; set; } = "";
	public string SpaceColor { get; set; } = "";
	public string BorderColor { get; set; } = "";
	public string TextColor { get; set; } = "";
	public Dictionary<ColorGroup, string> ColorGroupColors { get; set; } = new();
	public string ChestIconColor { get; set; } = "";
	public string GoColor { get; set; } = "";
	public string FreeParkingColor { get; set; } = "";
	public string GoToJailColor { get; set; } = "";

	public BoardThemeDefinition Clone()
	{
		return new BoardThemeDefinition
		{
			BoardEdgeColor = BoardEdgeColor,
			CenterColor = CenterColor,
			SpaceColor = SpaceColor,
			BorderColor = BorderColor,
			TextColor = TextColor,
			ColorGroupColors = ColorGroupColors?.ToDictionary( entry => entry.Key, entry => entry.Value ) ?? new(),
			ChestIconColor = ChestIconColor,
			GoColor = GoColor,
			FreeParkingColor = FreeParkingColor,
			GoToJailColor = GoToJailColor
		};
	}
}
