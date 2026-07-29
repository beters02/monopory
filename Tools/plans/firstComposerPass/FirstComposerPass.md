# First Composer Pass Implementation Plan

## Summary

Implement the requested UI/command features, then fix the three investigated issues:
card weighting clarity, widescreen camera framing, and trade negotiation target state.

Primary systems touched:
`GameCommandManager`, `GameController` partials, lobby/game HUD Razor components,
`GameCamera`, card data/deck logic, trade UI, stats/game-over overlay.

## Feature Changes

- Token camera hint:
  Add a visible HUD notification when `GameCamera.IsTokenCameraActive` is true:
  “Hold Tab to show cursor.” Use existing keybind name `"Score"` because
  `PlayerToken.ShouldReleaseTokenMouseForTokenUi` already checks `Input.Down("Score")`.

- Commands:
  Add commands in `GameCommandManager.cs`:
  `kick_player {playerName} {forceAbandon=false}` as `[HostCmd]`.
  `ping_player {playerName}` as regular `[ConCmd]`.
  `game_phase` as regular `[ConCmd]`, returning current `GameController.Phase`.

- Kick behavior:
  Add host-only kick helpers on lobby/game controllers.
  Resolve target by name via existing player resolution patterns.
  `forceAbandon=false` disconnects/marks player disconnected and starts normal abandon timer.
  `forceAbandon=true` immediately finalizes abandon/removal using existing abandon flow.
  Never allow kicking self unless `forceAbandon=true` and host confirms.

- Lobby kick UI:
  In `LobbyMenu.razor`, host sees a right-side `Kick` button per non-host player card.
  After kick succeeds and player is disconnected, replace button with `Abandon`.
  Both buttons call existing popup confirmation before invoking controller/command helper.
  Preserve current ready pill/player detail layout.

- Player ping:
  Extend `SteamProfilePopupCard` usage from `PropertyOwnershipPanel.razor`
  with a `Ping` button.
  Add a synced/local ping marker on `PlayerToken`: pulse ring/beam above target piece,
  color-matched to player, auto-expiring after ~4 seconds.
  `ping_player` and profile button call same `GameController.PingPlayer(...)`.

- Auction winner popup:
  In `GameController.Auctions.FinishAuction`, replace winner-only popup with
  `SendGlobalPopupToAll("Auction won", "{winner} won {property} for ${bid}.", ...)`.
  Keep table chat and owned-set popups unchanged.

- Post-match stats:
  Replace/extend `SessionOverlay` game-over panel with a post-match stats page.
  Reuse `PropertyStatsModal` data sources plus fun summary cards:
  winner, richest finish, most rent earned, most landed-on property,
  biggest cash swing/loss, most properties, dice totals, gamble result summary.
  Keep host/non-host Back To Lobby and Leave Lobby controls visible.

- Top-right player cards:
  Increase `PropertyOwnershipPanel` card height.
  Add avatar area using `SteamFriendsBridge.GetProfileEntry(...).AvatarTexture`
  only when `MonopolyApp.IsStandalone()`.
  Non-standalone fallback: simple question-mark placeholder or new `GameAssets`
  placeholder field for later assignment.

## Bug Fixes

- Card weight verification:
  Current deck expansion already treats `Weight` as copy count:
  `ReshuffleCardDrawPile` adds each card `Math.Max(card.Weight, 1)` times.
  Gamble appears often because both Chance and Community Chest gamble cards use
  `Weight = 2`, and Community Chest reuses key `chance_gamble_coin_flip`.
  Fix plan:
  make Community Chest key unique, e.g. `chest_gamble_coin_flip`;
  decide desired frequency by data, likely set gamble `Weight = 1` in both decks;
  update `view_deck` output to include card key and weight/copy count for debugging.

- Widescreen board/dice framing:
  `GameCamera` uses fixed `Distance` for board mode and dice distance based only
  on two dice points. Add a generic fit-distance helper using vertical FOV,
  horizontal FOV from `Screen.Width / Screen.Height`, and board/dice bounds.
  For `Board` mode, compute board bounds from all `Board.Spaces.TokenPosition`
  plus padding, then use max(required width distance, required height distance).
  For default dice resolution, include both dice bounds with a wider padding and
  clamp to min distance so dice never leave screen on ultrawide/aspect extremes.

- Trade negotiation wrong player:
  Root cause: `TradeComposerModal` keeps `LocalReceiverPlayerIndex`; it can override
  parent `ComposerReceiverPlayerIndex` after opening negotiate/edit flows.
  Fix plan:
  remove child-owned receiver state or reset it whenever `ReceiverPlayerIndex`,
  `IsOpen`, `CanSelectReceiver`, or `Title` changes.
  For locked negotiate/edit modes, `EffectiveReceiverPlayerIndex` must always equal
  `ReceiverPlayerIndex`, and dropdown must not invoke `OnSelectReceiver`.
  Add validation in `TradePanel.SendTrade`: negotiated trade receiver must be the
  original opposite party before sending.

## Test Plan

- Build: run `dotnet build`.
- Commands:
  verify `help` lists new commands; run `game_phase`, `ping_player`, and
  host-only `kick_player` success/failure paths.
- Lobby:
  host sees Kick/Abandon buttons; clients do not; confirmations gate actions.
- Ping:
  profile button and command produce same marker on correct token, then expire.
- Auction:
  all players receive auction-won popup; no-bid auctions remain unchanged.
- Camera:
  verify Board mode at 16:9, 21:9, 32:9; entire board visible.
  verify default dice resolution at same aspect ratios; both dice remain visible.
- Trade:
  create, edit, negotiate trades among 3+ players; receiver name and sent target
  remain correct after reopening different trades.
- Deck:
  `view_deck chance/chest` shows expected expanded copy counts;
  gamble count matches configured weights.

## Assumptions

- `Tab` maps to existing `"Score"` input binding.
- “Kick” means disconnect/mark disconnected first, not instant abandon.
- “Abandon” means immediate existing abandon finalization.
- Post-match stats can reuse existing synced match history/stat dictionaries.
- Avatar loading uses existing `SteamFriendsBridge` cache and standalone guard.
