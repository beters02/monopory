public static class AchievementsBackendUrlConVar
{
	public const string Name = "achievements_backend_url";

	[ConVar( Name )]
	public static string Value { get; set; } = "";
}

public static class AchievementsAccessTokenConVar
{
	public const string Name = "achievements_access_token";

	[ConVar( Name )]
	public static string Value { get; set; } = "";
}

public static class AchievementsAuthServiceNameConVar
{
	public const string Name = "achievements_auth_service_name";

	[ConVar( Name )]
	public static string Value { get; set; } = Sandbox.Services.RentRushService.DefaultSboxAuthServiceName;
}

public static class AchievementsSteamworksAuthEnabledConVar
{
	public const string Name = "achievements_steamworks_auth_enabled";

	[ConVar( Name )]
	public static bool Value { get; set; } = true;
}

public static class AchievementsSteamAppIdConVar
{
	public const string Name = "achievements_steam_app_id";

	[ConVar( Name )]
	public static string Value { get; set; } = Sandbox.Services.RentRushService.DefaultSteamAppId.ToString();
}

public static class AchievementsSteamworksTicketIdentityConVar
{
	public const string Name = "achievements_steamworks_ticket_identity";

	[ConVar( Name )]
	public static string Value { get; set; } = Sandbox.Services.RentRushService.DefaultSteamworksTicketIdentity;
}
