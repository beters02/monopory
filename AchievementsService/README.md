# Rent Rush Achievements Service

Authoritative achievement and cosmetic unlock API for the game.

## Current State

- Uses Postgres when `ConnectionStrings:AchievementsPostgres`, `Achievements:PostgresConnectionString`, or `ACHIEVEMENTS_POSTGRES` is configured.
- Falls back to an in-memory repository when no Postgres connection string is configured.
- If a Postgres connection string is configured but Postgres is unreachable during local startup, the service logs a warning and falls back to in-memory storage for that process.
- `/auth/forkbox/session` validates a `Forkbox.Steamworks.SteamUser` auth token and returns a short-lived bearer token.
- `/auth/steamworks/session` remains as a compatibility alias for older local scripts.
- `/auth/sbox/session` validates a Facepunch s&box auth token and returns a short-lived bearer token.
- The game currently tries Forkbox SteamUser first, then s&box auth with `Sandbox.Services.Auth.GetToken("sbox-network-storage")`.
- `/auth/device/session` remains as a local fallback when s&box auth is unavailable.
- External Steamworks DLL/client tickets are no longer required for internal achievements.

## Endpoints

- `POST /auth/device/session`
- `POST /auth/forkbox/session`
- `POST /auth/steamworks/session`
- `POST /auth/sbox/session`
- `GET /players/{playerId}/achievements`
- `GET /me/achievements`
- `POST /me/achievement-events`
- `GET /me/unlocks`
- `POST /me/cosmetics/selection`

Use `Authorization: Bearer <token>` for `/me/*` routes.

## Local Config Example

```json
{
  "ConnectionStrings": {
    "AchievementsPostgres": "Host=localhost;Port=5432;Database=rentrush_achievements;Username=postgres;Password=postgres"
  }
}
```

For game standalone builds, set:

```text
achievements_backend_url http://localhost:5199
achievements_steamworks_auth_enabled true
achievements_steam_app_id 4745160
achievements_steamworks_ticket_identity RentRushAchievements
achievements_auth_service_name sbox-network-storage
```

Optional override:

```text
achievements_access_token <token>
```

## Smoke Testing

Run the backend with the local development profile:

```powershell
dotnet run --project AchievementsService\AchievementsService.csproj --launch-profile AchievementsService
```

Run with Postgres:

```powershell
$env:ACHIEVEMENTS_POSTGRES="Host=localhost;Port=5432;Database=rentrush_achievements;Username=postgres;Password=postgres"
$env:Steam__AppId="590830"
$env:Steam__TicketAppIds="590830,480,4745160"
$env:Steam__WebApiKey="<your Steam Web API key>"
dotnet run --project AchievementsService\AchievementsService.csproj --launch-profile AchievementsService
```

Forkbox auth tokens are validated through `https://api.steampowered.com` by default. Override with `Steam__WebApiBaseUrl` only when testing a different Steam Web API host.
