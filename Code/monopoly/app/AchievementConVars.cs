using System.Runtime.CompilerServices;

#pragma warning disable CA2255

public static class AchievementsBackendUrlConVar
{
	public const string Name = "achievements_backend_url";
	public static readonly GameConVar<string> ConVar = new( Name, () => Value, value => Value = value );

	[ModuleInitializer]
	public static void Register()
	{
		GameConVars.Register( ConVar );
	}

	[ConVar( Name )]
	public static string Value { get; set; } = "";
}

public static class AchievementsAccessTokenConVar
{
	public const string Name = "achievements_access_token";
	public static readonly GameConVar<string> ConVar = new( Name, () => Value, value => Value = value );

	[ModuleInitializer]
	public static void Register()
	{
		GameConVars.Register( ConVar );
	}

	[ConVar( Name )]
	public static string Value { get; set; } = "";
}

public static class AchievementsAuthServiceNameConVar
{
	public const string Name = "achievements_auth_service_name";
	public static readonly GameConVar<string> ConVar = new( Name, () => Value, value => Value = value );

	[ModuleInitializer]
	public static void Register()
	{
		GameConVars.Register( ConVar );
	}

	[ConVar( Name )]
	public static string Value { get; set; } = Sandbox.Services.RentRushService.DefaultSboxAuthServiceName;
}

public static class AchievementsSteamworksAuthEnabledConVar
{
	public const string Name = "achievements_steamworks_auth_enabled";
	public static readonly GameConVar<bool> ConVar = new( Name, () => Value, value => Value = value );

	[ModuleInitializer]
	public static void Register()
	{
		GameConVars.Register( ConVar );
	}

	[ConVar( Name )]
	public static bool Value { get; set; } = true;
}

public static class AchievementsSteamAppIdConVar
{
	public const string Name = "achievements_steam_app_id";
	public static readonly GameConVar<string> ConVar = new( Name, () => Value, value => Value = value );

	[ModuleInitializer]
	public static void Register()
	{
		GameConVars.Register( ConVar );
	}

	[ConVar( Name )]
	public static string Value { get; set; } = Sandbox.Services.RentRushService.DefaultSteamAppId.ToString();
}

public static class AchievementsSteamworksTicketIdentityConVar
{
	public const string Name = "achievements_steamworks_ticket_identity";
	public static readonly GameConVar<string> ConVar = new( Name, () => Value, value => Value = value );

	[ModuleInitializer]
	public static void Register()
	{
		GameConVars.Register( ConVar );
	}

	[ConVar( Name )]
	public static string Value { get; set; } = Sandbox.Services.RentRushService.DefaultSteamworksTicketIdentity;
}
