# Rent Rush

Rent Rush is a multiplayer digital board game built with
[s&box](https://sbox.game/). The project includes the game client, UI, board
and gameplay systems, assets, localization, editor tooling, and an optional
achievements service.

## Project structure

- `Code/` contains the C# game and UI code.
- `Editor/` contains s&box editor extensions.
- `Assets/` contains scenes, models, materials, sounds, and textures.
- `Localization/` contains localized game text.
- `ProjectSettings/` contains s&box project configuration.
- `AchievementsService/` contains the optional achievement and cosmetic unlock
  API. See its own README for configuration and local development instructions.

## Running the game

1. Install s&box through Steam and make sure you can open the s&box editor.
2. Clone this repository into your local s&box projects or addons directory.
3. Open `monopory.sbproj` in the s&box editor.
4. Open the startup scene at `Assets/scenes/menu.scene`, then run the project.

The main project targets the .NET version supplied by the current s&box
toolchain. Generated project files and build outputs should not be committed.

## Licensing

Copyright (c) 2026 Bryce Peters.

This repository uses separate licenses for code and game content:

- **Code:** Original source code, scripts, and project tooling are licensed
  under the [GNU General Public License version 3](LICENSE), GPL-3.0-only.
  You may study, use, modify, and redistribute this code, including portions
  of it, under the GPL's terms. Distributed versions must preserve the
  notices, disclose their corresponding source, state changes, and use the
  same license.
- **Game content:** Original assets, artwork, models, textures, animation,
  audio, writing, localization, and Rent Rush branding are **not** licensed
  under the GPL. They remain all rights reserved under the
  [Rent Rush Game Content License](ASSETS-LICENSE.md).
- **Third-party material:** Dependencies and third-party assets remain subject
  to their own licenses and notices.

You may use or learn from the GPL-licensed code, but you may not redistribute
the complete Rent Rush game with its protected content or branding. The GPL
does permit someone to build and distribute a different game from the code if
they follow the GPL and replace the protected content and branding.
