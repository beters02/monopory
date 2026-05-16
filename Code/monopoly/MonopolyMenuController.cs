using Sandbox;
using Sandbox.Network;
using Sandbox.Rendering;
using System;

public sealed class MonopolyMenuController : Component
{
	[Property] public MonopolyGameConfig Config { get; set; } = new();



	protected override void OnStart()
	{
		MonopolySteamInviteBridge.Register( Scene );
		HandleLaunchArguments();
	}

	public bool TryOpenLobby()
	{
		if ( Networking.IsActive )
			Networking.Disconnect();

		Networking.CreateLobby( new LobbyConfig
		{
			Name = "Monopory Lobby",
			MaxPlayers = Math.Max( Config.MaxPlayers, Config.MinPlayers ),
			Privacy = LobbyPrivacy.FriendsOnly,
			DestroyWhenHostLeaves = false
		} );

		LoadLobbyScene();
		return true;
	}

	private void HandleLaunchArguments()
	{
		if ( ConsoleSystem.GetValue("debug") == "True" )
			Log.Info("Game was launched with +Debug.");
	}

	[Rpc.Broadcast]
	private void LoadLobbyScene()
	{
		MonopolySceneFlow.LoadLobby( Scene );
	}
}
