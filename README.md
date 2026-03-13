# Riftward

A server-side world border mod for [Vintage Story](https://vintagestory.at/). Instead of teleporting players who cross the border, it just stops them at the edge like an invisible wall. Built for the **Vintage Story Civilizations (VSC)** server.

## Features

- **Wall-style border** - you walk into it, you stop. No teleporting across the map, no dying, no losing your boat.
- **Square and circle shapes** - circle mode can be elliptical with separate X and Z radii.
- **Translocator blocking** - translocators won't generate exits outside the border, and players can't use ones that point out of bounds.
- **Warning messages** - tells you when you've hit the edge. Has a cooldown so it doesn't spam you.
- **Grace period** - new players get a configurable window before enforcement kicks in, so they don't get stuck while chunks are still loading.

## How It Works

VS is client-authoritative for movement. The client sends position packets and the server just trusts them. The usual way to enforce a border is polling player positions on a timer with `OnGameTick`, but that wastes cycles on every tick and lets players overshoot the border between checks.

Riftward uses [Harmony](https://github.com/pardeike/Harmony) to patch `ServerUdpNetwork.HandlePlayerPosition` directly. This is the method that processes incoming position packets from clients. A postfix runs right after each packet is applied, checks if the player ended up outside the border, and if so clamps them back to the edge with `TeleportToDouble` (the only way to authoritatively override client position in VS).

This means the check only fires when there's an actual position update to process, the correction happens before the next game frame, and there's no polling window where a player can be out of bounds.

Translocator protection uses two more Harmony patches:
- A transpiler on `BlockEntityStaticTranslocator.OnServerGameTick` swaps the vanilla `IsValidPos` check with a border-aware one, so exit points can't generate outside the border.
- A prefix on `BlockEntityTeleporterBase.OnEntityCollide` stops players from stepping into translocators that point outside the border.

## Installation

1. Download the latest release zip.
2. Drop it in your server's `Mods/` folder.
3. Restart the server.
4. Edit `ModConfig/riftward.json` to your liking.

## Configuration

`riftward.json` gets generated on first run in your server's `ModConfig/` folder:

| Setting | Default | Description |
|---|---|---|
| `BorderShape` | `"Square"` | `"Square"` or `"Circle"` (circle supports elliptical with independent X/Z) |
| `WorldRadiusChunks` | `null` | Single radius for both X and Z (1 chunk = 32 blocks) |
| `WorldRadiusChunksXZ` | `null` | Separate X/Z radius in chunks. Overrides `WorldRadiusChunks` if set |
| `WorldCenter` | world spawn | Border center in block coordinates |
| `WorldBorderPaddingBlocks` | `8` | Pulls the wall this many blocks inside the radius to avoid chunk edge weirdness |
| `PreventFarTranslocatorExits` | `true` | Stop translocators from targeting outside the border |
| `NewPlayerGraceSeconds` | `60` | Seconds after joining before enforcement starts |
| `ShowWarningMessage` | `true` | Show a message when players hit the border |
| `WarningCooldownSeconds` | `3.0` | Min seconds between warning messages per player |

If you don't set either radius option, it defaults to half the map size.

## Building from Source

Requires .NET 8 SDK and a Vintage Story installation.

1. Update the `VSInstall` path in `Riftward.csproj` to point to your VS install.
2. `dotnet build -c Release`
3. Output lands in `bin/Release/Mods/mod/`. Zip the contents for distribution.

## Compatibility

- Vintage Story 1.21.0+
- Server-side only, no client install needed.
- Works fine alongside teleport mods (`/home`, `/tpa`, etc.).
- Don't run two world border mods at the same time.

## License

MIT
