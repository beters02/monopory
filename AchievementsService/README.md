# Rent Rush Achievements Service

Authoritative achievement and cosmetic unlock API for the game.

## Current State

- Uses Postgres when `ConnectionStrings:AchievementsPostgres`, `Achievements:PostgresConnectionString`, or `ACHIEVEMENTS_POSTGRES` is configured.
- Falls back to an in-memory repository when no Postgres connection string is configured.
- If a Postgres connection string is configured but Postgres is unreachable during local startup, the service logs a warning and falls back to in-memory storage for that process.
- `/auth/steam/session` validates Steam Web API session tickets with `ISteamUserAuth/AuthenticateUserTicket`.
- Development auth fallback is controlled by `Steam:AllowDevAuth`; it defaults to enabled only in development and accepts the test token `dev-token`.

## Endpoints

- `POST /auth/steam/session`
- `GET /players/{steamId}/achievements`
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
  },
  "Steam": {
    "AppId": 4745160,
    "WebApiKey": "<set with env/user-secrets instead of committing>",
    "AllowDevAuth": false
  }
}
```

For game standalone builds, set:

```text
achievements_backend_url http://localhost:5199
```

Optional test-only overrides:

```text
achievements_access_token <token>
achievements_auth_token <steam-web-api-ticket-hex>
achievements_auth_ticket_target_steam_id 0
achievements_dev_steam_id 123456
achievements_service_name RentRushService
```

For real Steam ticket validation, configure the backend before launch:

```powershell
$env:Steam__AppId="4745160"
$env:Steam__WebApiKey="<your-steam-web-api-key>"
```

## Smoke Testing

Run the backend with the local development profile:

```powershell
dotnet run --project AchievementsService\AchievementsService.csproj --launch-profile AchievementsService
```

If you run without the launch profile, enable dev auth explicitly:

```powershell
$env:Steam__AllowDevAuth="true"
$env:Steam__AppId="4745160"
dotnet run --project AchievementsService\AchievementsService.csproj --urls http://localhost:5199
```

Dev auth is only for local smoke tests. Production should set `Steam:AllowDevAuth=false`.
