using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

	private void UpdateVisualTokens()
	{
		if ( MatchState != MatchLifecycleState.InGame )
		{
			ClearSpawnedTokens();
			return;
		}

		var activePlayers = GetLobbyPlayers();
		if ( HasVisualTokensForPlayers( activePlayers ) )
			return;

		SpawnTokensForPlayers( activePlayers );
	}

	private void SpawnTokensForPlayers( IReadOnlyList<PlayerState> activePlayers )
	{
		ClearSpawnedTokens();
		ClearExistingTokenObjects();

		if ( TokenPrefab is null )
		{
			Log.Warning( "GameController has no TokenPrefab assigned, so player tokens were not spawned." );
			return;
		}

		if ( Board is null )
			Board = Scene.GetAllComponents<Board>().FirstOrDefault();

		for ( var i = 0; i < activePlayers.Count; i++ )
		{
			var player = activePlayers[i];
			if ( player is null || !player.IsAssigned )
				continue;

			var tokenObject = TokenPrefab.Clone();
			tokenObject.Name = $"Token_{i + 1:00}";
			tokenObject.SetParent( GameObject );

			var token = tokenObject.Components.Get<PlayerToken>() ?? tokenObject.Components.Create<PlayerToken>();
			token.Board = Board;
			token.PlayerState = player;
			var colorIndex = player.ColorSlot >= 0 ? player.ColorSlot : i;
			var playerColor = Theme is not null ? Theme.GetPlayerColor( colorIndex ) : Color.White;
			token.ApplyPlayerColor( playerColor );

			if ( Board is not null )
				tokenObject.WorldPosition = Board.GetSpacePosition( player.SpaceIndex ) + Vector3.Up * token.HeightOffset;

			spawnedTokenObjects.Add( tokenObject );
		}
	}

	private bool HasVisualTokensForPlayers( IReadOnlyList<PlayerState> activePlayers )
	{
		for ( var i = spawnedTokenObjects.Count - 1; i >= 0; i-- )
		{
			if ( spawnedTokenObjects[i] is null || !spawnedTokenObjects[i].IsValid() )
				spawnedTokenObjects.RemoveAt( i );
		}

		if ( activePlayers is null || spawnedTokenObjects.Count != activePlayers.Count )
			return false;

		foreach ( var player in activePlayers )
		{
			if ( player is null || !player.IsAssigned )
				return false;

			var hasToken = spawnedTokenObjects.Any( tokenObject =>
			{
				var token = tokenObject?.Components.Get<PlayerToken>();
				return token is not null && token.PlayerState == player;
			} );

			if ( !hasToken )
				return false;
		}

		return true;
	}

	private void ClearExistingTokenObjects()
	{
		foreach ( var token in Scene.GetAllComponents<PlayerToken>().ToList() )
		{
			if ( token?.GameObject is null || token.GameObject == TokenPrefab )
				continue;

			token.GameObject.Destroy();
		}
	}

	private void ClearSpawnedTokens()
	{
		for ( var i = spawnedTokenObjects.Count - 1; i >= 0; i-- )
		{
			if ( spawnedTokenObjects[i] is not null )
				spawnedTokenObjects[i].Destroy();
		}

		spawnedTokenObjects.Clear();
	}
}
