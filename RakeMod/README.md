# Rake Multiplayer Mod

Cooperative multiplayer mod for **Rake (2015)** by Konsordo. Adds 2–4 player co-op with a server-authoritative Rake AI, a full three-night day/night loop, daytime preparation mechanics (cameras, bait, traps, map) and persistent progression.

**Current version: 1.1.0** — Phases 1–4 complete (Day/Night loop, 5-phase Rake AI, Daytime Mechanics, Persistence).

---

## Features

### Session & Day/Night (Phase 1)
- Server-authoritative **Day/Night cycle** with configurable phase durations
- **3-night session** state machine with victory/defeat conditions
- Synchronized lighting, fog and a HUD (phase, night counter, timer)
- Session state survives scene changes (`DayNightManager` + `SessionManager`)

### Rake AI (Phase 2)
- Full **5-phase Rake AI** shared by all players:
  - **Follow** — silent stalking, prioritizes isolated hunters
  - **Stealth** — crouched approach, 2× damage, LOS detection
  - **Chase** — direct pursuit
  - **Blocking** — 80% frontal damage reduction for 2–3 s
  - **Feigned Retreat** — retreats to the cave, then flanks
- 15 Hz state sync (`RakeStatePacket`), server-authoritative damage

### Daytime Mechanics (Phase 3)
- **Pig cameras / surveillance cameras**
  - Place, pick up and monitor cameras
  - **Maximum 5 cameras deployed at once** (server-enforced, matching the original game)
  - Battery drain, breakage, Rake alert on break (`QuestTriggerPacket`)
  - Monitor view auto-switches away from broken/picked-up cameras
- **Pig-camera**: mount a camera on a trapped pig; it shares the 5-camera quota
- **Bait**: lures the nearest animal (server-side animal AI)
- **Traps**: server-authoritative trap zones; a triggered trap **stuns the Rake for 3 seconds**
- **Minimap** (toggle **M**): cameras = green, traps = red, bait = yellow, safehouse = blue, cave = red
- **Remote players**: coloured third-person proxies (first-person hands, weapons and cameras are disabled on remote clones)

### Persistence (Phase 4)
- Per-scene `MapSettings` auto-discovery (spawns, cameras, traps, cave, safehouse)
- Per-night spawn rotation
- Inventory, camera/trap state and Rake health persist between nights
- Day-phase **safehouse respawn** for all hunters

---

## Requirements

- **Game**: Rake (2015), original Steam release
- **OS**: Windows 10/11
- **BepInEx**: 5.x (Mono / Unity 5.x build)
- **.NET Framework**: 3.5 profile (Unity 5 Mono). The mod is built as `net35` to match BepInEx/Harmony.

---

## Installation

### Automatic

1. Copy the mod folder into your Rake installation (`Steam/steamapps/common/Rake/`).
2. Run `InstallMod.bat` as Administrator (installs into `BepInEx/plugins`).
3. Launch the game through Steam.

### Manual

1. Install **BepInEx 5.x** for Unity 5.x and run the game once to generate folders.
2. Copy `RakeMultiplayer.dll` (and `Telepathy.dll` if not already present) into `Rake/BepInEx/plugins/`.
3. Launch the game.

---

## Usage

### Host

1. Launch Rake.
2. Open the multiplayer menu (main menu) → **Host Game** → **LAN (Direct IP)**.
3. Share your IP; press **Start Game** once everyone joined.

### Join

1. Multiplayer menu → **Join Game**.
2. Enter the host IP and port (default **7777**) → **Connect**.

### In game

| Key | Action |
|-----|--------|
| **M** | Toggle minimap |
| **F1** | Toggle multiplayer menu |
| **E** | Interact (including camera setup/pickup) |
| **I** | Inventory |
| **F** | Flashlight |

---

## Configuration

`BepInEx/config/com.rake.multiplayer.cfg`:

```ini
[General]
Enabled = true

[Network]
Address = 127.0.0.1
Port = 7777
MaxPlayers = 4
IsServer = false
UseSteamP2P = false
SteamAppId = 480
GameSceneName = level1
```

Day/night durations and camera battery drain are runtime fields on
`DayNightManager` / `NetworkedCamera` (defaults: 300 s day, 300 s night, 5 %/min battery).

---

## Sync rates & authority

| System | Rate | Authority |
|--------|------|-----------|
| Player position/rotation | 20 Hz | Client → server → others |
| Player animation | 10 Hz | Client |
| Rake state | 15 Hz | **Server** |
| Camera state | 1 Hz (delta) | **Server** |
| Camera setup/pickup | event | **Server** (quota validated) |
| Pigs/animals | 10 Hz (delta) | **Server** |
| Traps | event | **Server** |
| Bait placement | event | **Server** |
| Day/Night + session | 1 Hz + events | **Server** |

---

## Known issues / limitations

1. **Remote-player trap damage** — traps damage the host's local player and animals; remote players are not damaged because player health is not yet server-authoritative.
2. **Pig-camera placement is optimistic** — the requesting client applies the visual immediately; the server still enforces the 5-camera quota and corrects the authoritative pig state.
3. **Bait visuals** — only the placing client sees the bait projectile; other clients see its map marker and synchronized pig behaviour.
4. **Steam P2P** is not implemented (LAN/direct IP only).
5. **Save games** — multiplayer progression is per-session; the game's own save system is not synchronized.
6. High ping (>200 ms) can cause minor position differences; the state protocol is last-write-wins.
7. Remote players are shown as simple coloured humanoid proxies - the game ships no third-person body model.

---

## Building from source

```powershell
cd RakeMod
dotnet build Source/RakeMultiplayer/RakeMultiplayer.csproj -c Release
```

Output: `RakeMod/Source/RakeMultiplayer/bin/Release/net35/RakeMultiplayer.dll`

> The project targets **net35** because Rake runs Unity 5.0 Mono (mscorlib 2.0 / System.Core 3.5).
> Building against net40 produces a DLL that fails to load some types at runtime (TypeLoadException).

Reference paths in the `.csproj` point at `D:\Rake Multiplayer\Rake`; adjust them for your installation.

### Tests

```powershell
cd test
dotnet build NetworkedCamera.Tests.csproj
./bin/Debug/net40/NetworkedCamera.Tests.exe --noresult
```

215 headless tests cover packet serialization, camera/trap/pig logic, quotas,
persistence and integration/stress simulations (3-night loop, 5% loss, 200 ms jitter).

---

## Technical details

- **Networking**: Telepathy (TCP), custom packet protocol (`PacketType` 1–32)
- **Mod loader**: BepInEx 5.x
- **Patching**: Harmony 2.x
- **Key classes**: `RakeNetworkManager`, `NetworkedPlayer`, `NetworkedRakeAI`,
  `NetworkedCamera`, `NetworkedPig`, `NetworkedTrap`, `DayNightManager`,
  `SessionManager`, `MapSettings`, `WorldPersistence`, `MapUI`

---

## Credits

- **Original game**: Konsordo (Nikita Subachev)
- **Mod framework**: BepInEx Team
- **Patching**: Harmony (pardeike)
- **Transport**: Telepathy (vis2k)

## License

For educational purposes only. Not affiliated with Konsordo or the original Rake developers.
