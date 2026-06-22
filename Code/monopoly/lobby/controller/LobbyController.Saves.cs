using Sandbox;
using System;

public sealed partial class LobbyController
{
	public bool TryLoadSavedGame( GameSaveSummary saveSummary, IReadOnlyList<LoadedSeatAssignment> assignments, out string message )
	{
		message = "";

		if ( !Networking.IsHost || !IsLocalEffectiveHost )
		{
			message = "Only the host can load saved games.";
			return false;
		}

		if ( saveSummary is null || string.IsNullOrWhiteSpace( saveSummary.SaveId ) )
		{
			message = "Choose a save to load.";
			return false;
		}

		var save = GameSaveService.ReadSave( saveSummary.SaveId );
		if ( save is null )
		{
			message = "Could not read that save.";
			return false;
		}

		var resolvedAssignments = ResolveSeatAssignments( save, assignments );
		MatchBootstrap.PrepareLoadedGame( save, resolvedAssignments, save.Summary.SaveId );
		GameSaveService.SetLastLoadedSaveId( save.Summary.SaveId );
		StagedLoadedSaveName = save.Summary.DisplayName;
		StagedLoadedGameIdentifier = save.Summary.GameIdentifier;
		message = $"Loaded {save.Summary.DisplayName} into the lobby. Press Start Game when everyone is ready.";
		return true;
	}

	private void ClearStagedLoadedGame()
	{
		if ( !Networking.IsHost )
			return;

		StagedLoadedSaveName = "";
		StagedLoadedGameIdentifier = "";
		if ( MatchBootstrap.Current?.HasLoadedGame == true )
			MatchBootstrap.Clear();
	}

	public IReadOnlyList<LoadedSeatAssignment> BuildDefaultSeatAssignments( GameSaveSummary saveSummary )
	{
		var save = GameSaveService.ReadSave( saveSummary?.SaveId ?? "" );
		return ResolveSeatAssignments( save, null );
	}

	private IReadOnlyList<LoadedSeatAssignment> ResolveSeatAssignments( GameSaveFile save, IReadOnlyList<LoadedSeatAssignment> proposedAssignments )
	{
		var proposedBySeat = proposedAssignments?
			.Where( assignment => assignment is not null )
			.ToDictionary( assignment => assignment.SeatIndex, assignment => assignment )
			?? new Dictionary<int, LoadedSeatAssignment>();

		var connectionsBySteamId = GetConnections().ToDictionary( connection => connection.SteamId, connection => connection );
		return save?.Snapshot?.Players?
			.Where( player => player is not null && player.OwnerId != 0 )
			.OrderBy( player => player.SeatIndex )
			.Select( player =>
			{
				if ( proposedBySeat.TryGetValue( player.SeatIndex, out var proposed ) && proposed.AssignedOwnerId > 0 )
				{
					return new LoadedSeatAssignment
					{
						SeatIndex = player.SeatIndex,
						AssignedOwnerId = proposed.AssignedOwnerId,
						AssignedPlayerName = string.IsNullOrWhiteSpace( proposed.AssignedPlayerName ) ? player.PlayerName : proposed.AssignedPlayerName,
						KeepDisconnected = proposed.KeepDisconnected
					};
				}

				connectionsBySteamId.TryGetValue( player.OwnerId, out var connection );
				return new LoadedSeatAssignment
				{
					SeatIndex = player.SeatIndex,
					AssignedOwnerId = player.OwnerId,
					AssignedPlayerName = connection?.Name ?? player.PlayerName,
					KeepDisconnected = connection is null
				};
			} )
			.ToList() ?? new();
	}
}
