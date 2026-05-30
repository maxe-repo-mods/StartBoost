# StartBoost

Customize your run start with configurable level, currency, equipment, and player upgrades.

R.E.P.O. BepInEx mod. Host-side only.

## Features

- Override the starting level (1-50) to begin mid-run or continue from a checkpoint
- Add extra starting currency to your run budget
- Spawn extra carts (standard or small/pocket variants) at the start of each level
- Grant starting player upgrades (health, stamina, speed, strength, range, throw, extra jump, launch, and more)
- Spawn starting inventory items automatically on first join
- Add extra charging station charges (batteries) at run start
- Supports late joiners (upgrades and inventory applied automatically)

## Installation

Requires [BepInEx 5.x](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/).

Place `StartBoost.dll` in `BepInEx/plugins/`.

Configuration file is generated at `BepInEx/config/maxenterme.StartBoost.cfg` on first launch.

## Configuration

Edit `BepInEx/config/maxenterme.StartBoost.cfg` to customize your run start.

### Level and Economy

| Section | Key | Default | Range | Description |
|---------|-----|---------|-------|-------------|
| Level | StartLevel | 0 | 0-50 | Override the starting level (0 = no override, 1-50 = start at that level). Sets levelsCompleted so the game treats you as if you reached that level |
| Economy | StartCurrency | 0 | 0-100000 | Extra starting currency added at the beginning of a run |

### Items and Carts

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| Items | ExtraCarts | 0 | Number of extra carts to spawn at the start of each level (0-5) |
| Items | UseSmallCarts | false | Use small (pocket) carts instead of medium carts |
| Items | ExtraBatteries | 0 | Number of extra charging station charges added at run start (0-20) |
| Inventory | Slot1 | (empty) | Item asset name for inventory slot 1 (e.g., 'Item Gun', 'Item Tracker'). Empty = none. Only given on first join |
| Inventory | Slot2 | (empty) | Item asset name for inventory slot 2. Empty = none. Only given on first join |
| Inventory | Slot3 | (empty) | Item asset name for inventory slot 3. Empty = none. Only given on first join |

### Player Upgrades

Applied to all players at the start of each level (and to late joiners automatically).

| Section | Key | Default | Range | Description |
|---------|-----|---------|-------|-------------|
| Upgrades | Health | 0 | 0-100 | Starting Health upgrade level |
| Upgrades | Stamina | 0 | 0-100 | Starting Stamina upgrade level |
| Upgrades | Speed | 0 | 0-100 | Starting Speed upgrade level |
| Upgrades | Strength | 0 | 0-100 | Starting Strength upgrade level |
| Upgrades | Range | 0 | 0-100 | Starting Range upgrade level |
| Upgrades | ExtraJump | 0 | 0-100 | Starting ExtraJump upgrade level |
| Upgrades | Launch | 0 | 0-100 | Starting Launch upgrade level |
| Upgrades | CrouchRest | 0 | 0-100 | Starting Crouch Rest upgrade level |
| Upgrades | Wings | 0 | 0-100 | Starting Tumble Wings upgrade level |
| Upgrades | Throw | 0 | 0-100 | Starting Throw Strength upgrade level |
| Upgrades | TumbleClimb | 0 | 0-100 | Starting Tumble Climb upgrade level |
| Upgrades | MapPlayerCount | 0 | 0-100 | Starting Map Player Count upgrade level |
| Upgrades | DeathHeadBattery | 0 | 0-100 | Starting Death Head Battery upgrade level |

## Build

```bash
dotnet build -c Release
```

The compiled DLL will be available at:
```
bin/Release/netstandard2.1/StartBoost.dll
```


## AI Disclosure

This mod was developed with the assistance of AI (Claude by Anthropic). All code has been reviewed and tested by the developer.

## License

MIT
