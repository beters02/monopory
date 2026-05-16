using Sandbox;
using System;

public static class SceneFlow
{
	private static string pendingScenePath = "";
	private static DateTime pendingSceneRequestedAtUtc = DateTime.MinValue;
	private const float PendingSceneLoadWindowSeconds = 2f;

	public static void LoadMenu( Scene scene )
	{
		if ( ShouldSkipLoad<MenuController>( scene, ScenePaths.Menu ) )
			return;

		LoadScene( scene, ScenePaths.Menu );
	}

	public static void LoadLobby( Scene scene )
	{
		if ( ShouldSkipLoad<LobbyController>( scene, ScenePaths.Lobby ) )
			return;

		LoadScene( scene, ScenePaths.Lobby );
	}

	public static void LoadGame( Scene scene )
	{
		if ( ShouldSkipLoad<GameController>( scene, ScenePaths.Game ) )
			return;

		LoadScene( scene, ScenePaths.Game );
	}

	private static void LoadScene( Scene scene, string scenePath )
	{
		MarkSceneLoadRequested( scenePath );
		Log.Info( $"[SceneFlow] Loading {scenePath}" );
		scene.LoadFromFile( scenePath );
	}

	private static bool ShouldSkipLoad<T>( Scene scene, string scenePath ) where T : Component
	{
		var targetCount = GetComponentCount<T>( scene );
		var duplicateRequest = IsDuplicateLoadRequest( scenePath );

		Log.Info(
			$"[SceneFlow] Request path={scenePath} target={typeof( T ).Name} " +
			$"host={Networking.IsHost} active={Networking.IsActive} " +
			$"scene-null={scene is null} target-count={targetCount} " +
			$"pending={pendingScenePath} pending-age={GetPendingSceneAgeSeconds():0.00}" );

		if ( scene is null )
			return true;

		if ( duplicateRequest )
		{
			Log.Info( $"[SceneFlow] Skip {scenePath}; duplicate load request." );
			return true;
		}

		if ( targetCount > 0 )
		{
			Log.Info( $"[SceneFlow] Skip {scenePath}; {typeof( T ).Name} already exists." );
			return true;
		}

		return false;
	}

	private static int GetComponentCount<T>( Scene scene ) where T : Component
	{
		return scene is null ? 0 : scene.GetAllComponents<T>().Count();
	}

	private static bool IsDuplicateLoadRequest( string scenePath )
	{
		var pendingAge = GetPendingSceneAgeSeconds();

		return pendingScenePath == scenePath &&
			pendingAge >= 0f &&
			pendingAge <= PendingSceneLoadWindowSeconds;
	}

	private static void MarkSceneLoadRequested( string scenePath )
	{
		pendingScenePath = scenePath;
		pendingSceneRequestedAtUtc = DateTime.UtcNow;
	}

	private static float GetPendingSceneAgeSeconds()
	{
		if ( pendingSceneRequestedAtUtc == DateTime.MinValue )
			return float.PositiveInfinity;

		return (float)(DateTime.UtcNow - pendingSceneRequestedAtUtc).TotalSeconds;
	}
}
