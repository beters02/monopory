using Sandbox;
using Sandbox.Engine.Settings;
using Sandbox.Network;
using Sandbox.Rendering;
using System;

public sealed class MenuController : Component
{
	public MatchConfig Config { get; set; } = new();

	protected override void OnStart()
	{
		GameAssets.PrewarmUiAssets();
		SteamInviteBridge.Register( Scene );
		Config = MatchConfigSchema.CreateDefault();
		HandleLaunchArguments();
		LoadingState.HideAfterSceneReady();
	}

	public bool TryOpenLobby()
	{
		NetworkSession.ClearRejoinWindow();
		//if ( Networking.IsActive )
		//	Networking.Disconnect();

		var hostedConfig = MatchBootstrap.CloneConfig( Config );
		MatchBootstrap.PrepareLobby( hostedConfig );

		if (!Networking.IsActive)
		{
			Networking.CreateLobby( new LobbyConfig
			{
				Name = "Monopory Lobby",
				MaxPlayers = Math.Max( hostedConfig.MaxPlayers, hostedConfig.MinPlayers ),
				Privacy = LobbyPrivacy.FriendsOnly,
				DestroyWhenHostLeaves = false
			} );
		}
		

		LoadLobbyScene();
		return true;
	}

	private void HandleLaunchArguments()
	{
		if ( ConsoleSystem.GetValue("debug") == "True" )
			Log.Info("Game was launched with +Debug.");
	}

	private void LoadLobbyScene()
	{
		SceneFlow.LoadLobby( Scene );
	}
}

