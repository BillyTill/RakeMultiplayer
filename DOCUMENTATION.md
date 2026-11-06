# Rake Multiplayer — Technical Documentation

> **Mod for Rake (2015, Unity 5.0.0f4)** — LAN co-op via Telepathy TCP transport  
> **Status:** LAN-testable (Stage 3.5 complete)  
> **Repository:** https://github.com/BillyTill/RakeMultiplayer

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture Decisions](#architecture-decisions)
3. [Network Model](#network-model)
4. [Code Structure](#code-structure)
5. [Key Components](#key-components)
6. [Packet Protocol](#packet-protocol)
7. [Harmony Patches](#harmony-patches)
8. [Installation & Build](#installation--build)
9. [LAN Testing Guide](#lan-testing-guide)
10. [Known Issues & Roadmap](#known-issues--roadmap)

---

## Overview

### What This Mod Does
- Adds **2-player LAN co-op** to the single-player horror game *Rake (2015)*
- Uses **Telepathy** (raw TCP, port 7777) for networking — no Mirror at runtime
- **Server-authoritative** Rake AI, items, cameras; **client-authoritative** player movement with server reconciliation
- **Backward compatible** — single-player works unchanged when mod is installed but not used

### Target Environment
| Component | Version |
|-----------|---------|
| Game | Rake (2015), Unity 5.0.0f4 |
| Runtime | Mono 3.0 (CLR 3.5), x86 |
| Mod Loader | BepInEx 5.4.23.2 (UnityMono x86) |
| Harmony | HarmonyX 2.9.0.0 (via `0Harmony.dll`) |
| Transport | Telepathy 1.0.266.0 (TCP) |
| Target Framework | .NET Framework 4.0 (`net40`) |

---

## Architecture Decisions

### Why Telepathy (not Mirror)?
- Mirror adds ~200KB+ overhead and complex abstractions
- Game is Unity 5.x / Mono 3.0 — Mirror's newer APIs break on old runtime
- Telepathy is lightweight, raw `byte[]` packets, no reflection overhead
- We implement our own packet serializer, message routing, connection management

### Authority Model

| System | Authority | Sync Rate | Notes |
|--------|-----------|-----------|-------|
| **Player Position** | Client-auth + server reconciliation | 20 Hz | Client sends, server broadcasts, clients interpolate |
| **Player Rotation** | Client-auth | 30 Hz | Camera + body yaw |
| **Player Animation** | Client-auth | 10 Hz | Animator parameters (Speed, Run, etc.) |
| **Player Health/Stamina** | Server-auth | On change | Server validates damage, broadcasts |
| **Flashlight** | Client-auth | On change | Toggle sync |
| **Rake AI** | **Server only** | 15 Hz pos / 10 Hz state | Clients run `NetworkedRakeAI` (interpolation only) |
| **Items (pickup/destroy)** | Server only | Event-based | Deterministic network IDs via rounded coords |
| **Cameras/Quest Triggers** | Server only | Event-based | Stage 3.6+ |

### Why `net40` not `net472`?
- Unity 5.0 uses **Mono 3.0** (CLR 3.5 era)
- `net472` requires CLR 4.0+ — causes `TypeLoadException` / `MissingMethodException` at runtime
- `net40` matches the game's actual runtime capabilities

### Single-Player Compatibility
- Mod **does nothing** until user presses **F1 → Host/Join**
- No patches fire, no network init, no object spawns in single-player
- `Enabled = true` by default for patches, but `InitializeNetworking()` only via UI buttons

---

## Network Model

### Transport: Telepathy TCP
- **Port:** 7777 (hardcoded, configurable in future)
- **Protocol:** Length-prefixed frames (Telepathy default)
- **Message format:** Custom binary serializer (see `PacketSerializer.cs`)
- **Polling:** `Telepathy.Server.GetNextMessage()` / `Client.GetNextMessage()` — no events

### Connection Flow
```
HOST (Server)                    CLIENT
   │                                │
   ├─ F1 → Host Game → Start Game ──┤
   │   Telepathy.Server.Start(7777) │
   │   RakeNetworkManager.StartServer()    │
   │                                │
   │                            F1 → Join Game → LAN → IP → Connect
   │                            Telepathy.Client.Connect(ip, 7777)
   │                                │
   ├─ OnConnected (id=2) ──────────►│ PlayerConnectedPacket(IsYou=true, YourId=2)
   │                                │ PlayerConnectedPacket(IsYou=false, YourId=1)
   │                                │
   ├─ StartGamePacket(scene="level1") ► LoadScene("level1")
   │                                │
   │   Both: PlayerSpawner processes queue
   │   Host: RegisterLocalPlayer(existing FPSController) → PlayerId=1
   │   Client: SpawnRemotePlayer(1) + RegisterLocalPlayer(existing) → PlayerId=2
```

### Scene Management
- **GameSceneName** config default: `"level1"` (must match game's Build Settings)
- Host sends `StartGamePacket` with scene name
- Both sides load via `SceneManager.LoadScene()`
- `MultiplayerSceneManager` handles post-load player spawning

---

## Code Structure

```
RakeMod/
├── Source/RakeMultiplayer/
│   ├── Core/                    # Entry points, spawning, network manager
│   │   ├── RakeMultiplayerPlugin.cs      # BepInEx plugin, config, logging
│   │   ├── PlayerSpawner.cs              # Local/remote player spawning
│   │   ├── GameEntryPatch.cs             # FPSController.Start hook → local player init
│   │   ├── RakeNetworkManager.cs         # Singleton: SendToServer, SendToAllClients, polling
│   │   └── MultiplayerSceneManager.cs    # Scene load coordination
│   ├── Components/              # MonoBehaviours attached to networked objects
│   │   ├── NetworkedPlayer.cs            # Player sync, SetAsLocal/RemotePlayer
│   │   ├── NetworkedRakeAI.cs            # Rake interpolation, state machine
│   │   ├── NetworkedItem.cs              # Item network ID, pickup sync
│   │   └── NetworkedCameraSystem.cs      # Camera/quest sync (stub)
│   ├── Networking/              # Packet definitions & serialization
│   │   ├── PacketSerializer.cs           # BinaryReader/Writer helpers
│   │   ├── PacketType.cs                 # Enum: 1=PlayerState, 2=RakeState, ...
│   │   ├── NetworkPacket.cs              # Base class with PlayerId, Timestamp
│   │   ├── Packets.cs                    # All packet structs
│   │   └── StartGamePacket.cs            # Scene transition
│   ├── Patches/                 # Harmony patches (11 total)
│   │   ├── GamePatches.cs                # Weapon, Item, Destroy patches
│   │   └── AdditionalPatches.cs          # FPSController.Awake (add NetworkedPlayer)
│   └── UI/
│       ├── MultiplayerMenu.cs            # F1 runtime UI (Canvas, sortingOrder=32000)
│       └── RuntimeUIBuilder.cs           # UI helpers
├── Plugins/                     # Runtime dependencies (copied to BepInEx/plugins)
│   ├── Telepathy.dll
│   ├── 0Harmony.dll (HarmonyX 2.9.0)
│   ├── BepInEx.dll, BepInEx.Harmony.dll
│   ├── UnityEngine.CoreModule.dll (type forwarder shim)
│   └── Mirror.* (build-time only, not used at runtime)
├── CoreModuleShim/              # Source for UnityEngine.CoreModule.dll
├── BuildMod.bat                 # dotnet build net40 Release
├── InstallMod.bat               # Copies DLLs to game/BepInEx/plugins
└── RakeMultiplayer.csproj       # TargetFramework=net40, LangVersion=latest
```

---

## Key Components

### 1. `RakeMultiplayerPlugin.cs` — Plugin Entry
- `Awake()`: Creates `MultiplayerMenu` (DontDestroyOnLoad), registers config
- `Update()`: Polls `RakeNetworkManager.Instance.PollEvents()` every frame
- Config: `Port`, `GameSceneName`, `Enabled` (patches on/off)
- Static `Log` (ManualLogSource) for all mod logging

### 2. `PlayerSpawner.cs` — Player Lifecycle
```csharp
// Host: uses EXISTING scene FPSController (tagged "Player")
RegisterLocalPlayer(FPSController instance) → NetworkedPlayer.SetAsLocalPlayer()

// Client: spawns remote player from prefab
SpawnRemotePlayer(playerId, spawnPos) → Instantiate(playerPrefab)
    → SetAsRemotePlayer() (disables weapCam, weapon viewmodels, input)
    → DEBUG CAPSULE fallback if model missing
```
- `_pendingRemoteSpawns` queue handles spawn-before-network-ready
- `playerPrefab` assigned via Inspector or auto-found at runtime

### 3. `NetworkedPlayer.cs` — Player Sync & View Model Control
```csharp
// Local player (host or client's own)
SetAsLocalPlayer():
    isLocalPlayer = true
    enabled = true
    PlayerId = 1 (host) or assigned via PlayerConnectedPacket

// Remote player (other players)
SetAsRemotePlayer():
    isLocalPlayer = false
    fps.enabled = false          // Disables PlayerMovement, CameraMouseLook
    pm.enabled = false
    cml.enabled = false
    wm.enabled = false
    rifle.enabled = false
    sniper.enabled = false
    // CRITICAL: Hide view model (hands/weapon)
    weapCam.enabled = false
    foreach weapon in WeaponManager.playerWeapons: weapon.gameObject.SetActive(false)
    // Animator stays ENABLED for animation sync
```
- `FixedUpdate()`: Sends `PlayerStatePacket` (pos, rot, anim, health, stamina, flashlight) at 20/30/10 Hz
- `OnPlayerStateReceived()`: Interpolates remote transform, applies animator params

### 4. `NetworkedRakeAI.cs` — Server-Authoritative Rake
```csharp
// On SERVER (host): Enemy_AI runs normally, NetworkedRakeAI reads state & broadcasts
// On CLIENTS: Enemy_AI DISABLED, NetworkedRakeAI interpolates pos/rot/state
void Update() {
    if (RakeMultiplayerPlugin.IsServer) {
        // Read from Enemy_AI, send RakeStatePacket
    } else {
        // Interpolate toward last received state
        if (!IsInitialized) { DisableEnemyAI(); IsInitialized = true; }
    }
}
```
- Sync: Position (15 Hz), Rotation (15 Hz), State (10 Hz), Health (on change)
- States: `Idle`, `Patrol`, `Chase`, `Attack`, `Dead`

### 5. `RakeNetworkManager.cs` — Message Routing
- Singleton, wraps Telepathy `Server`/`Client`
- `PollEvents()` called from `Plugin.Update()`
- Routes packets by `PacketType` to handlers
- `SendToServer(pkt)`, `SendToAllClients(pkt)`, `SendToClient(id, pkt)`

### 6. `MultiplayerMenu.cs` — Runtime UI (F1 Toggle)
- Canvas `sortingOrder = 32000` (above game UI)
- States: `MainMenu` → `HostLobby`/`ClientLobby` → `InGame`
- `_waitingForConnection` delays `ShowLobby()` until `isConnected`
- `_startGameSent` guard prevents duplicate `StartGame` broadcasts
- `_knownPlayerIds` tracks lobby count on client

---

## Packet Protocol

All packets inherit `NetworkPacket { byte Type; int PlayerId; uint Timestamp; }`

| Type | Name | Direction | Payload | Rate |
|------|------|-----------|---------|------|
| 1 | `PlayerStatePacket` | Client→Server, Server→All | Pos, Rot, AnimSpeed, Health, Stamina, Flashlight | 20/30/10 Hz |
| 2 | `RakeStatePacket` | Server→All | Pos, Rot, State, Health | 15/10 Hz |
| 3 | `PlayerConnectedPacket` | Server→Client | PlayerId, SpawnPos, IsYou, YourId | On connect |
| 4 | `PlayerDisconnectedPacket` | Server→All | PlayerId | On disconnect |
| 5 | `ItemPickupPacket` | Client→Server | ItemNetId, RequesterId | On pickup attempt |
| 6 | `ItemSpawnPacket` | Server→All | ItemNetId, Pos, Name, Count | On item spawn |
| 7 | `ItemDestroyedPacket` | Server→All | ItemNetId | On pickup confirmed |
| 8 | `StartGamePacket` | Server→All | SceneName | Host clicks Start |
| 9 | `DamagePacket` | Client→Server | TargetNetId, Amount, Type | On hit |
| 10 | `FlashlightPacket` | Client→All | On/Off | On toggle |
| 11 | `CameraSyncPacket` | Server→All | CameraId, Pos, Rot, Active | Stage 3.6+ |
| 12 | `QuestTriggerPacket` | Server→All | TriggerId, Activated | Stage 3.6+ |

### Deterministic Item Network IDs
```csharp
// NetworkedItem.GenerateNetworkId()
string key = $"{Mathf.RoundToInt(pos.x)}_{Mathf.RoundToInt(pos.y)}_{Mathf.RoundToInt(pos.z)}_{item.name}";
int netId = key.GetHashCode();  // Same on host & client
```

### Serialization (`PacketSerializer.cs`)
- `WriteVector3`, `ReadVector3` (3 floats)
- `WriteQuaternion`, `ReadQuaternion` (4 floats)
- `WritePacket`, `ReadPacket` — switches on `PacketType`
- Tolerance for float comparison: `0.001f`

---

## Harmony Patches (11 Total)

| Patch | Target | Purpose |
|-------|--------|---------|
| `FPSControllerStartPatch` | `FPSController.Start` | `GameEntryPatch.Postfix` → init local NetworkedPlayer |
| `FPSControllerAwakePatch` | `FPSController.Awake` | `AdditionalPatches.Postfix` → `AddComponent<NetworkedPlayer>()` |
| `FPSControllerUpdatePatch` | `FPSController.Update` | Block `CameraRander(false)` on remote (prevents camera disable) |
| `PlayerMovementUpdatePatch` | `PlayerMovement.Update` | Early return if `!isLocalPlayer` (handled by component enable) |
| `CameraMouseLookUpdatePatch` | `CameraMouseLook.Update` | Early return if `!bControl` (disabled on remote) |
| `CameraComputerUpdatePatch` | `CameraComputer.Update` | Block interaction raycast on remote |
| `DetectionTriggerRakePatch` | `DetectionTriggerRake.OnTriggerEnter` | Only server processes Rake detection |
| `RifleWeaponUpdatePatch` | `Rifle_weapon.Update` | **Prefix**: `if (!isLocalPlayer) return false;` — blocks fire on remote |
| `SniperRifleWeaponUpdatePatch` | `SniperRifle_weapon.Update` | Same as rifle |
| `ItemPickupPatch` | `Item.PickupItem` | **Prefix**: Client sends `ItemPickupRequest`, blocks local `Destroy` |
| `DestroyPatch` | `Object.Destroy` | **Prefix**: Blocks `Destroy` on `Item` components on clients |

### Weapon Fire Blocking (Critical)
```csharp
// RifleWeaponUpdatePatch.Prefix / SniperRifleWeaponUpdatePatch.Prefix
[HarmonyPrefix]
static bool Prefix(Rifle_weapon __instance) {
    var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
    if (netPlayer != null && !netPlayer.isLocalPlayer) return false; // BLOCK
    return true; // ALLOW local player
}
```
- `Fire()` called from `Update()` on `Input.GetButtonDown("Fire1")`
- Only local player's `Update` runs → only local player fires
- `Event_PlayerFire` posted once per successful raycast (subscribed by `Enemy_AI` for danger state)

### Item Pickup (Server-Authoritative)
```csharp
// ItemPickupPatch.Prefix
static bool Prefix(Item __instance) {
    if (RakeMultiplayerPlugin.IsServer) return true; // Server processes normally
    // Client: send request, block local pickup
    RakeNetworkManager.Instance.SendToServer(new ItemPickupRequestPacket { ItemNetId = netId });
    return false;
}
```
- Server validates, broadcasts `ItemDestroyedPacket`, gives weapon/ammo
- Clients receive destroy packet → `NetworkedItem.OnDestroyed()` → `Destroy(gameObject)`

---

## Installation & Build

### Prerequisites
- .NET SDK 6.0+ (builds `net40` via targeting pack)
- Game installed at `D:\Rake Multiplayer\Rake\` (or update paths in scripts)

### Build
```cmd
cd RakeMod
BuildMod.bat
```
Output: `Source/RakeMultiplayer/bin/Release/net40/RakeMultiplayer.dll`

### Install
```cmd
InstallMod.bat
```
Copies to `Rake/BepInEx/plugins/`:
- `RakeMultiplayer.dll`
- `Telepathy.dll`
- `0Harmony.dll`
- `BepInEx.dll`, `BepInEx.Harmony.dll`
- `UnityEngine.CoreModule.dll` (shim)

### BepInEx Config (`Rake/BepInEx/config/BepInEx.cfg`)
```ini
[Preloader]
PatcherAssembly = BepInEx.Harmony.dll   ; Enables HarmonyX
```

### Doorstop Config (`Rake/doorstop_config.ini`)
```ini
[config]
enabled = true
target_assembly = BepInEx.Core.dll
```

---

## LAN Testing Guide

### Prerequisites
- Two machines (or two windows on same machine) on same LAN
- Firewall: allow **port 7777 TCP** inbound/outbound
- Both run same game version + mod

### Host (Machine A)
1. Launch `Rake.exe`
2. Press **F1** → **Multiplayer Menu** opens
3. Click **"Host Game (LAN)"** → status: "Hosting on port 7777..."
4. Click **"Start Game"** → loads `level1`
5. Console: `[3.3] Host Lobby created`, `[3.3] Hosting on port 7777`

### Client (Machine B)
1. Launch `Rake.exe`
2. Press **F1** → **Multiplayer Menu**
3. Click **"Join Game"** → **"LAN"**
4. Enter Host IP (e.g., `192.168.1.50`) → **Connect**
5. Status: "Connecting..." → "Connected!" → Lobby shows 2 players
6. Host clicks **Start Game** → Client auto-loads `level1`

### Expected In-Game
| Element | Host Sees | Client Sees |
|---------|-----------|-------------|
| **Own Player** | Normal FPS (hands, gun, movement) | Normal FPS |
| **Other Player** | **Cyan capsule** (DebugCapsule), no hands/gun, smooth interpolation | Same |
| **Rake** | Normal AI, synced position/state/HP | Interpolated, no local AI |
| **Items** | Pickup → weapon/ammo added, item destroyed | Same (server-authoritative) |
| **Shooting** | Works, damages Rake | Works, damages Rake (server validates) |
| **Flashlight** | Toggle syncs | Toggle syncs |

### Verify in Logs (`BepInEx/LogOutput.log`)
**Host:**
```
[3.3] Spawned remote player 2 at (4, 0, 0)
[3.5] Spawned DEBUG CAPSULE for remote player!
[3.5] Disabled camera: Main Camera
[3.5] Disabled weapCam (weapon view model camera)
[3.5] Disabled weapon view model: Rifle_weapon
[3.5] Player 2 State: Pos=(...), Health=100
```

**Client:**
```
[3.3] Client received PlayerConnected: playerId=1, IsYou=false
[3.3] Spawned remote player 1 at ...
[3.5] Spawned DEBUG CAPSULE for remote player!
[3.3] Client received PlayerConnected: playerId=2, IsYou=true
[3.3] Local player assigned ID: 2
[3.5] Disabled weapCam (weapon view model camera)  -- for remote player 1
```

---

## Known Issues & Roadmap

### Current (Stage 3.5 — LAN Testable)
- ✅ Player movement/look/shoot sync
- ✅ Rake AI server-authoritative sync
- ✅ Item pickup/destroy sync (deterministic IDs)
- ✅ Flashlight toggle sync
- ✅ Debug capsule for remote player visibility
- ✅ View model properly hidden on remote players
- ✅ Single-player backward compatible
- ✅ Build passes, batchmode tests pass (11/11 patches, 7/7 SerTest)

### Stage 3.6 — Polish (Next)
- [ ] **Proper body model** for remote players (replace capsule)
- [ ] **Camera sync** (surveillance cameras, objective cameras)
- [ ] **Quest trigger sync** (objective completion, door triggers)
- [ ] **Stamina sync** (already in packet, need UI sync)
- [ ] **Death/respawn handling**
- [ ] **Disconnect handling** (cleanup, reconnect UI)
- [ ] **High ping compensation** (interpolation tuning, extrapolation)

### Stage 4 — Online/Steam
- [ ] Steam P2P transport (SteamNetworkingSockets)
- [ ] Direct IP connect (NAT traversal)
- [ ] Lobby browser (Steam matchmaking)
- [ ] Persistent sessions (rejoin)

### Known Bugs
1. **GameSceneName mismatch** — if game's actual scene isn't `"level1"`, client fails to load. Fix: detect scene name at runtime or make configurable.
2. **Rake interpolation jitter** at >100ms ping — need better smoothing (cubic spline or dead reckoning).
3. **Item race condition** — simultaneous pickup by both players: server picks first, second gets "already taken" hint. Works but could be smoother.
4. **No reconnection** — disconnect = back to main menu.

---

## Decompiled Reference

Full decompiled `Assembly-CSharp.dll` available in `Decompiled_Source/` (187 files).
Key files for mod development:
- `FPSController.cs` — Player root, cameras, `CameraRander()`, `TakeDamage()`
- `PlayerMovement.cs` — Movement, stamina, animator params
- `CameraMouseLook.cs` — Mouse look, `bControl` flag
- `WeaponManager.cs` — `playerWeapons` list (view models), `changeWeapon()`
- `Rifle_weapon.cs` / `SniperRifle_weapon.cs` — View model scripts, `Fire()`, muzzle flash fields
- `Enemy_AI.cs` — Rake state machine, `TakeDamage()`, `SetDangerState()`
- `Item.cs` / `Medkit_item.cs` — Pickup base + examples
- `Inventory.cs` — UI inventory

See `Decompiled_Source/ARCHITECTURE_NOTES.md` for detailed architecture analysis.

---

## Credits & References

- **Telepathy** — visit UG for TCP transport
- **BepInEx 5** — Mod loader
- **HarmonyX** — Runtime patching
- **ILSpy** — Decompilation
- **Game:** *Rake (2015)* by Conscience

---

## License

This mod is for educational/reverse-engineering purposes only.  
Not affiliated with original game developers.

---

*Last updated: 2026-11-06 (Stage 3.5 complete)*