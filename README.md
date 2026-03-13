# Riftward

A server-side world border mod for [Vintage Story](https://vintagestory.at/) that acts like an invisible wall — players are stopped at the border edge instead of being teleported away. Developed for the **Vintage Story Civilizations (VSC)** server.

## Features

- **Wall-style enforcement** — players hit the border and stop. No punishing teleports, no deaths, no lost boats or mounts.
- **Square and circle borders** — circle mode supports elliptical shapes with independent X/Z radii.
- **Translocator protection** — blocks translocators from generating exits outside the border, and prevents players from using translocators that point out of bounds (with a warning message).
- **Configurable warning messages** — players see "You have reached the edge of this world." when they hit the wall, with a cooldown to prevent spam.
- **New player grace period** — configurable delay before enforcement kicks in for freshly joined players, preventing edge cases during chunk loading.

## How It Works

Vintage Story is client-authoritative for movement — the client sends position packets and the server trusts them. Traditional approaches that poll player positions on a timer (e.g., every 2 seconds via `OnGameTick`) have two problems: they waste CPU checking every tick just to decrement a timer, and they allow players to overshoot the border by up to the poll interval before being corrected.

Riftward takes a different approach. It uses [Harmony](https://github.com/pardeike/Harmony) to patch `ServerUdpNetwork.HandlePlayerPosition` — the method that processes incoming player position packets on the server. A postfix runs after each packet is applied, checks if the resulting position is outside the border, and if so, clamps it to the border edge using `TeleportToDouble` (the only way to authoritatively override client position in VS). This means:

- **Zero wasted ticks** — the check only runs when there's actual movement data to process.
- **Immediate correction** — the position is fixed before the next game frame sees it.
- **No overshoot** — there's no polling window where a player can be out of bounds.

Translocator protection uses two additional Harmony patches:
- A **transpiler** on `BlockEntityStaticTranslocator.OnServerGameTick` replaces the vanilla `IsValidPos` check with a border-aware version, preventing exit points from generating outside the border.
- A **prefix** on `BlockEntityTeleporterBase.OnEntityCollide` blocks players from stepping into translocators whose target is out of bounds.

## Installation

1. Download the latest release zip.
2. Place it in your server's `Mods/` folder.
3. Restart the server.
4. Edit `ModConfig/riftward.json` to configure.

## Configuration

`riftward.json` is generated on first run in your server's `ModConfig/` folder:

| Setting | Default | Description |
|---|---|---|
| `BorderShape` | `"Square"` | `"Square"` or `"Circle"` (circle supports elliptical with independent X/Z) |
| `WorldRadiusChunks` | `null` | Simple radius — same value for X and Z (1 chunk = 32 blocks) |
| `WorldRadiusChunksXZ` | `null` | Independent X/Z radius in chunks. Takes priority over `WorldRadiusChunks` |
| `WorldCenter` | world spawn | Border center in block coordinates |
| `WorldBorderPaddingBlocks` | `8` | Wall sits this many blocks inside the radius to prevent chunk edge issues |
| `PreventFarTranslocatorExits` | `true` | Block translocators from targeting outside the border |
| `NewPlayerGraceSeconds` | `60` | Seconds after joining before enforcement starts |
| `ShowWarningMessage` | `true` | Show a message when players hit the border |
| `WarningCooldownSeconds` | `3.0` | Minimum seconds between warning messages per player |

If neither `WorldRadiusChunks` nor `WorldRadiusChunksXZ` is set, the radius defaults to half the map size.

## Building from Source

Requires .NET 8 SDK and a Vintage Story installation.

1. Update the `VSInstall` path in `BetterWorldBorder.csproj` to point to your VS install directory.
2. `dotnet build -c Release`
3. Output is in `bin/Release/Mods/mod/` — zip the contents for distribution.

## Compatibility

- **Vintage Story 1.21.0+**
- Server-side only — no client installation needed.
- Compatible with teleportation mods (`/home`, `/tpa`, etc.).
- Only one world border mod should be active at a time.

## License

MIT
