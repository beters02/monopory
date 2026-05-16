using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
{

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

			if ( Board is not null )
				tokenObject.WorldPosition = Board.GetSpacePosition( player.SpaceIndex ) + Vector3.Up * token.HeightOffset;

			spawnedTokenObjects.Add( tokenObject );
		}
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
