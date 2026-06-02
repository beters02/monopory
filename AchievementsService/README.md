# Rent Rush Achievements Service

Authoritative achievement and cosmetic unlock API for the game.

## Current State

- Runs with an in-memory repository so the API can be exercised without external packages.
- `schema.sql` defines the Postgres tables expected by the production repository.
- `/auth/steam/session` keeps the auth contract in place, but the Steam ticket verification call is intentionally still a TODO until Steamworks app credentials are configured.

## Endpoints

- `POST /auth/steam/session`
- `GET /players/{steamId}/achievements`
- `GET /me/achievements`
- `POST /me/achievement-events`
- `GET /me/unlocks`
- `POST /me/cosmetics/selection`

Use `Authorization: Bearer <token>` for `/me/*` routes.
