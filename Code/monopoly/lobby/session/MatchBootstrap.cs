using Sandbox;

public sealed class MatchBootstrap
{
	public MatchConfig Config { get; set; } = new();
	public bool HasConfig { get; set; }
	public bool AutoStartGame { get; set; }
	public int StartingPlayerCount { get; set; }
	public List<LobbyPlayer> StartingPlayers { get; set; } = new();
	public bool HasLoadedGame { get; set; }
	public GameSaveFile LoadedGame { get; set; }
	public List<LoadedSeatAssignment> LoadedSeatAssignments { get; set; } = new();
	public string LoadedSourceSaveId { get; set; } = "";

	public static MatchBootstrap Current { get; private set; } = new();

	public static void PrepareLobby( MatchConfig config )
	{
		Current = new MatchBootstrap
		{
			Config = CloneConfig( config ),
			HasConfig = true,
			AutoStartGame = false,
			StartingPlayerCount = 0,
			StartingPlayers = new()
		};
	}

	public static void PrepareGame( MatchConfig config, IReadOnlyList<LobbyPlayer> startingPlayers )
	{
		var players = ClonePlayers( startingPlayers );
		var clonedConfig = CloneConfig( config );

		if ( clonedConfig.RandomizeTurnOrder )
			ShufflePlayers( players );

		Current = new MatchBootstrap
		{
			Config = clonedConfig,
			HasConfig = true,
			AutoStartGame = true,
			StartingPlayerCount = players.Count,
			StartingPlayers = players
		};
	}

	public static void PrepareLoadedGame( GameSaveFile save, IReadOnlyList<LoadedSeatAssignment> seatAssignments, string sourceSaveId )
	{
		var config = MatchConfigSchema.Deserialize( save?.Snapshot?.MatchConfigSnapshot ?? "" );
		var startingPlayers = BuildLoadedPlayers( save, seatAssignments );

		Current = new MatchBootstrap
		{
			Config = config,
			HasConfig = true,
			AutoStartGame = true,
			StartingPlayerCount = startingPlayers.Count,
			StartingPlayers = startingPlayers,
			HasLoadedGame = true,
			LoadedGame = save,
			LoadedSeatAssignments = seatAssignments?.ToList() ?? new(),
			LoadedSourceSaveId = sourceSaveId ?? save?.Summary?.SaveId ?? ""
		};
	}

	public static void Clear()
	{
		Current = new MatchBootstrap();
	}

	public static MatchConfig CloneConfig( MatchConfig source )
	{
		return MatchConfigSchema.Clone( source );
	}

	private static List<LobbyPlayer> ClonePlayers( IReadOnlyList<LobbyPlayer> players )
	{
		if ( players is null )
			return new();

		return players
			.Where( player => player is not null && player.OwnerId != 0 )
			.Select( player => new LobbyPlayer
			{
				OwnerId = player.OwnerId,
				Name = player.Name,
				IsReady = player.IsReady,
				IsLocal = player.IsLocal,
				SelectedPieceId = PieceCatalog.GetByIdOrDefault( player.SelectedPieceId ).Id,
				SelectedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( player.SelectedDiceSkinId ).Id
			} )
			.ToList();
	}

	private static List<LobbyPlayer> BuildLoadedPlayers( GameSaveFile save, IReadOnlyList<LoadedSeatAssignment> assignments )
	{
		var assignmentBySeat = assignments?
			.Where( assignment => assignment is not null )
			.ToDictionary( assignment => assignment.SeatIndex, assignment => assignment )
			?? new Dictionary<int, LoadedSeatAssignment>();

		return save?.Snapshot?.Players?
			.Where( player => player is not null && player.OwnerId != 0 )
			.OrderBy( player => player.SeatIndex )
			.Select( player =>
			{
				assignmentBySeat.TryGetValue( player.SeatIndex, out var assignment );
				var ownerId = assignment?.AssignedOwnerId > 0 ? assignment.AssignedOwnerId : player.OwnerId;
				var name = !string.IsNullOrWhiteSpace( assignment?.AssignedPlayerName ) ? assignment.AssignedPlayerName : player.PlayerName;

				return new LobbyPlayer
				{
					OwnerId = ownerId,
					Name = string.IsNullOrWhiteSpace( name ) ? "Player" : name,
					IsReady = true,
					SelectedPieceId = PieceCatalog.GetByIdOrDefault( player.SelectedPieceId ).Id,
					SelectedDiceSkinId = DiceSkinCatalog.GetByIdOrDefault( player.SelectedDiceSkinId ).Id
				};
			} )
			.ToList() ?? new();
	}

	private static void ShufflePlayers( List<LobbyPlayer> players )
	{
		if ( players is null || players.Count <= 1 )
			return;

		for ( var i = players.Count - 1; i > 0; i-- )
		{
			var swapIndex = Game.Random.Int( 0, i );
			(players[i], players[swapIndex]) = (players[swapIndex], players[i]);
		}
	}
}
