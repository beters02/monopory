using Sandbox;
using Sandbox.Network;
using Sandbox.Rendering;
using System;

public sealed class MenuController : Component
{
	[Property] public MatchConfig Config { get; set; } = new();



	protected override void OnStart()
	{
		SteamInviteBridge.Register( Scene );
		HandleLaunchArguments();
	}

	public bool TryOpenLobby()
	{
		if ( !Networking.IsHost )
			return false;

		var hostedConfig = MatchBootstrap.CloneConfig( Config );
		MatchBootstrap.PrepareLobby( hostedConfig );

		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new LobbyConfig
			{
				Name = "Monopory Lobby",
				MaxPlayers = Math.Max( hostedConfig.MaxPlayers, hostedConfig.MinPlayers ),
				Privacy = LobbyPrivacy.FriendsOnly,
				DestroyWhenHostLeaves = true
			} );
		}

		LoadLobbyScene();
		return true;
	}

	[Rpc.Host]
	public void RequestOpenLobby()
	{
		TryOpenLobby();
	}

	private void HandleLaunchArguments()
	{
		if ( ConsoleSystem.GetValue("debug") == "True" )
			Log.Info("Game was launched with +Debug.");
	}

	[Rpc.Broadcast]
	private void LoadLobbyScene()
	{
		SceneFlow.LoadLobby( Scene );
	}
}
