using System;
using System.Threading.Tasks;
using Sandbox.Internal;

public class MonopolyApp : Component
{
	public class GameClosedAttribute : EventAttribute
	{
		public GameClosedAttribute() : base( "scene.stop" ) { }
	}

	public static bool IsStandalone = false;
	public static bool IsDebugEnabled = false;
	public static bool GameLaunchedWithDebugConvar = false;

	[ConVar( "achievements_steam_app_id" )]
	public static string AchievementsSteamAppId { get; set; } = Sandbox.Services.RentRushService.DefaultSteamAppId.ToString();

	[ConVar( "achievements_forkbox_ticket_identity" )]
	public static string AchievementsForkboxTicketIdentity { get; set; } = Sandbox.Services.RentRushService.DefaultForkboxTicketIdentity;

	protected override void OnAwake()
	{
//do if standalone
		IsStandalone = true;
		Sandbox.Services.RentRushService.TestInit();
		Log.Info( $"Application AppId: {Application.AppId}" );
		Log.Info( "Steam-backed local achievement service enabled." );
//do endif

		var debugConvarParsed = bool.TryParse( ConsoleSystem.GetValue( "debug" ), out bool debugConvar );
		GameLaunchedWithDebugConvar = debugConvar;
		if ( debugConvarParsed && debugConvar )
			IsDebugEnabled = true;

		Log.Info( $"IsStandalone: {IsStandalone}" );
	}

	[GameClosed]
	public void OnGameClosed()
	{
		Log.Info( "Game Closed" );
		AchievementServices.Shutdown();
		Networking.Disconnect();
	}

	[ConCmd( "achievements_auth_diagnostics" )]
	private static void PrintAchievementsAuthDiagnostics( Connection connection )
	{
//do if standalone
		_ = PrintAchievementsAuthDiagnosticsAsync();
//do else
/*
		Log.Warning( "Achievements auth diagnostics are only available in standalone builds." );
*/
//do endif
	}

	[ConCmd( "achment_debug" )]
	private static void RunAchievementsDebug( Connection connection )
	{
		Log.Info( $"Application AppId: {Application.AppId}" );
		Log.Info( $"Configured Steam AppId: {GetSteamAppId()}" );
		ConsoleSystem.Run( "achievements_auth_diagnostics" );
	}

//do if standalone
	private static async Task PrintAchievementsAuthDiagnosticsAsync()
	{
		try
		{
			Log.Info( await Sandbox.Services.RentRushService.GetForkboxAuthDiagnosticsAsync( AchievementsForkboxTicketIdentity ) );
			var state = await AchievementServices.Achievements.GetMyStateAsync();
			Log.Info( $"localAchievementState steamId={state?.SteamId ?? 0}; achievements={state?.Achievements?.Count ?? 0}; unlocks={state?.UnlockedCosmeticIds?.Count ?? 0}" );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Failed to print achievements diagnostics: {exception.Message}" );
		}
	}

	private static uint GetSteamAppId()
	{
		return uint.TryParse( AchievementsSteamAppId, out var appId ) && appId != 0
			? appId
			: Sandbox.Services.RentRushService.DefaultSteamAppId;
	}
//do else
/*
	private static uint GetSteamAppId()
	{
		return Sandbox.Services.RentRushService.DefaultSteamAppId;
	}
*/
//do endif

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null )
			return;

		var hud = Scene.Camera.Hud;
		hud.DrawText( new TextRendering.Scope( "Hello!", Color.Red, 32 ), Screen.Width * 0.5f );
	}
}
