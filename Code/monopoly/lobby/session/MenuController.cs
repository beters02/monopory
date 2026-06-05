using Sandbox;
using Sandbox.Engine.Settings;
using Sandbox.Network;
using Sandbox.Rendering;
using System;
using System.Threading.Tasks;

public sealed class MenuController : Component
{
	public MatchConfig Config { get; set; } = new();
	private bool isOpeningLobby;

	protected override void OnStart()
	{
		GameAssets.PrewarmUiAssets();
		SteamInviteBridge.Register( Scene );
		HandleLaunchArguments();
	}

	public bool TryOpenLobby()
	{
		if ( isOpeningLobby )
			return false;

		_ = OpenLobbyAsync();
		return true;
	}

	private async Task OpenLobbyAsync()
	{
		isOpeningLobby = true;

		try
		{
			await ResetNetworkingBeforeHostingAsync();

			NetworkSession.ClearRejoinWindow();

			var hostedConfig = MatchBootstrap.CloneConfig( Config );
			MatchBootstrap.PrepareLobby( hostedConfig );

			Networking.CreateLobby( new LobbyConfig
			{
				Name = "Monopory Lobby",
				MaxPlayers = Math.Max( hostedConfig.MaxPlayers, hostedConfig.MinPlayers ),
				Privacy = LobbyPrivacy.FriendsOnly,
				DestroyWhenHostLeaves = false
			} );

			LoadLobbyScene();
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to open lobby: {exception.Message}" );
		}
		finally
		{
			isOpeningLobby = false;
		}
	}

	private static async Task ResetNetworkingBeforeHostingAsync()
	{
		NetworkSession.ClearRejoinWindow();

		if ( !Networking.IsActive )
			return;

		Log.Info( "Disconnecting existing network session before hosting lobby." );
		Networking.Disconnect();

		var deadline = Time.Now + 2f;
		while ( Networking.IsActive && Time.Now < deadline )
		{
			await System.Threading.Tasks.Task.Delay( 100 );
		}

		await System.Threading.Tasks.Task.Delay( 250 );
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

