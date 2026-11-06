# Rake Multiplayer Mod - Developer Report

## Project Overview

This document summarizes the implementation of a cooperative multiplayer mod for Rake (2015) by Konsordo. The mod adds 2-4 player co-op support to the original single-player horror game using reverse engineering techniques.

## Game Analysis

### Unity Version
- **Engine**: Unity 5.x (2015 era)
- **Runtime**: .NET Framework 2.0/3.5 (v2.0.50727)
- **Scripting Backend**: Mono

### Key Classes Identified (via ILSpy decompilation)

| Class | Purpose | Location |
|-------|---------|----------|
| `FPSController` | Main player controller, health, inventory, state | Assembly-CSharp.dll |
| `PlayerMovement` | Movement, stamina, footstep sounds | Assembly-CSharp.dll |
| `CameraMouseLook` | Camera rotation, mouse look | Assembly-CSharp.dll |
| `Enemy_AI` (Rake) | Monster AI, patrol, hunt, attack states | Assembly-CSharp.dll |
| `Inventory` / `Inventory_item` | Item system | Assembly-CSharp.dll |
| `CameraComputer` / `RecordCamera_obj` | Surveillance camera system | Assembly-CSharp.dll |
| `WeaponManager` | Weapons, shooting | Assembly-CSharp.dll |
| `DetectionTriggerRake` | Rake detection triggers | Assembly-CSharp.dll |
| `InformationUI` | Objectives, hints | Assembly-CSharp.dll |
| `SaveGameSystem` | Save/load | Assembly-CSharp.dll |
| `NotificationsManager` | Event system | Assembly-CSharp.dll |

## Architecture Decisions

### Network Framework: Mirror
**Reasoning**: 
- High-level API built on Unity's LLAPI
- Good for modding (works with existing GameObjects)
- Supports both TCP (Telepathy) and UDP (KCP) transports
- Active community and documentation
- Compatible with Unity 5.x+

### Authority Model
| System | Authority | Reason |
|--------|-----------|--------|
| Player Movement | Client-Authoritative + Server Reconciliation | Responsive controls, no input lag |
| Player Animation | Client-Authoritative (synced via SyncVars) | Visual consistency |
| Rake AI | Server-Authoritative | Critical for horror - prevents desync/cheating |
| Items | Server-Authoritative (ownership) | Prevents duplication |
| Cameras | Server-Authoritative | Shared world state |

### Mod Loader: BepInEx 5.x
**Reasoning**:
- Standard for Unity game modding
- Built-in Harmony support
- Plugin system with config management
- Works with Unity 5.x (Mono)

### Patching: Harmony 2.x
**Reasoning**:
- Runtime patching without modifying original DLLs
- Supports prefix/postfix/transpiler
- Minimal performance overhead

## Implementation Summary

### Core Components Created

```
Source/RakeMultiplayer/
├── Core/
│   ├── RakeMultiplayerPlugin.cs       # Main BepInEx plugin entry
│   ├── RakeNetworkManager.cs          # Custom NetworkManager
│   ├── PlayerSpawner.cs               # Player/Rake/Camera spawning
│   ├── MultiplayerSceneManager.cs     # Scene management
│   ├── GameEntryPatch.cs              # Game initialization hooks
│   └── AssemblyInfo.cs                # Assembly metadata
├── Components/
│   ├── NetworkedPlayer.cs             # Player sync (position, rotation, anim, health, etc.)
│   ├── NetworkedRakeAI.cs             # Server-authoritative Rake AI sync
│   ├── NetworkedItem.cs               # Item pickup/drop/use sync
│   └── NetworkedCameraSystem.cs       # Camera placement/viewing sync
├── Networking/
│   └── NetworkDiscovery.cs            # LAN discovery, extended NetworkManager
├── Patches/
│   ├── GamePatches.cs                 # Core Harmony patches
│   └── AdditionalPatches.cs           # Camera, Weapon, UI, Save patches
└── UI/
    └── MultiplayerMenu.cs             # Lobby, connection UI
```

### Key Features Implemented

1. **Player Synchronization**
   - Position (20 Hz), Rotation (30 Hz), Camera Rotation (30 Hz)
   - Animator parameters (10 Hz): Speed, Grounded, Crouch, Run, FloatSpeed
   - Health, Stamina, Flashlight state
   - Player state machine (Full, Freezed, Disabled, Dead, Inventory, CameraMode)
   - Interpolation for smooth remote player movement

2. **Rake AI Synchronization**
   - Server-authoritative AI decisions
   - Position/rotation sync (15 Hz)
   - State sync (Patrol, Haunt, PlayerHount, Danger, Escape, CameraBreak, Attack, Killing, Dead)
   - Target player sync
   - Damage/death handling
   - Battery drain for cameras triggering Rake

3. **Item System**
   - Pickup (ownership transfer)
   - Drop (physics sync)
   - Use (consumables)
   - Camera placement (special mechanic)
   - Unique item handling

4. **Camera System**
   - Place cameras in world
   - View cameras (switch player camera)
   - Battery drain
   - Break cameras (triggers Rake)
   - Sync camera states across clients

5. **Networking**
   - Host/Client architecture
   - LAN discovery (Mirror Discovery)
   - Direct IP connection
   - Steam P2P ready (config option)
   - Configurable port (default 7777)
   - Max players 2-4

6. **UI**
   - Main menu integration
   - Lobby with player list
   - Connection status
   - IP copy button
   - Settings (player name, max players, Steam P2P)

7. **Harmony Patches**
   - FPSController: Skip Update for remote players, sync damage/death/flashlight
   - PlayerMovement: Skip for remote, sync freeze state
   - Enemy_AI: Skip Update on clients, sync state changes
   - CameraMouseLook: Skip for remote players
   - WeaponManager: Sync shooting for Rake detection
   - InformationUI: Broadcast objectives
   - SaveGameSystem: Disable in multiplayer
   - Scene loading: Redirect through NetworkManager

## Sync Rates & Performance

| Data | Rate | Method |
|------|------|--------|
| Player Position | 20 Hz | SyncVar + Command |
| Player Rotation | 30 Hz | SyncVar + Command |
| Camera Rotation | 30 Hz | SyncVar + Command |
| Animator Params | 10 Hz | SyncVar + Command |
| Health/Stamina | On Change | SyncVar + Command |
| Flashlight | On Change | SyncVar + Command |
| Rake Position | 15 Hz | SyncVar |
| Rake State | 10 Hz | SyncVar |
| Rake Target | On Change | SyncVar |
| Items | On Action | Command/RPC |

## Known Issues & Limitations

1. **Phantom Collisions**: Remote players have CharacterController disabled to prevent blocking, but this means they can walk through each other
   - *Mitigation*: Could add capsule colliders with trigger for visual feedback

2. **Rake Desync at High Ping**: Server-authoritative helps, but >200ms ping causes visible position correction
   - *Mitigation*: Increased interpolation speed, client-side prediction for movement

3. **Camera View Limitation**: Only one player can view a camera at a time
   - *Design Decision*: Matches single-player behavior

4. **No Save System**: Multiplayer sessions don't persist
   - *Reason*: Complex to sync save state across clients

5. **Host Migration**: Not implemented
   - *Future Work*: Implement host migration using Mirror's built-in support

6. **Steam P2P**: Config option exists but not fully implemented
   - *Requires*: Steamworks.NET integration

## Building & Deployment

### Prerequisites
- Visual Studio 2022 / Rider
- .NET Framework 4.7.2 Developer Pack
- Rake game installed (for reference assemblies)

### Build Steps
1. Open `Source/RakeMultiplayer/RakeMultiplayer.csproj`
2. Update reference paths in `.csproj` to match game installation
3. Build Release configuration
4. Copy output to `Rake/BepInEx/plugins/`

### Dependencies (place in BepInEx/plugins/)
- `0Harmony.dll` (Harmony 2.x)
- `Mirror.dll` (Mirror 80+)
- `Telepathy.dll` (included with Mirror)
- `BepInEx.dll` (BepInEx core)
- `BepInEx.Harmony.dll` (BepInEx Harmony support)

### Installation
Run `InstallMod.bat` as Administrator, or manually:
1. Install BepInEx 5.x for Unity 5.x
2. Copy mod DLL to `BepInEx/plugins/`
3. Copy dependencies to `BepInEx/plugins/`
4. Launch game

## Testing Checklist

- [ ] Two clients can connect (LAN)
- [ ] Two clients can connect (Online via port forward)
- [ ] Player movement syncs correctly
- [ ] Camera rotation syncs correctly
- [ ] Animations play on remote players
- [ ] Flashlight toggles sync
- [ ] Stamina/Health sync
- [ ] Rake AI behaves identically on all clients
- [ ] Rake attacks correct player
- [ ] Damage syncs (player and Rake)
- [ ] Item pickup/drop/use syncs
- [ ] Camera placement/viewing works
- [ ] Disconnect handling works
- [ ] No crashes in single-player (backward compatibility)
- [ ] Performance: 60 FPS with 4 players

## Future Improvements

1. **Host Migration**: Implement seamless host migration
2. **Voice Chat**: Integrate Steam Voice or Vivox
3. **Anti-Cheat**: Basic validation for player position/speed
4. **Mod Configuration UI**: In-game config menu
5. **Spectator Mode**: For dead players
6. **Custom Maps**: Support for community maps
7. **VR Support**: If original game gets VR mod
8. **Linux/Mac**: Test with Proton/Wine

## File Structure for Distribution

```
RakeMultiplayer_v1.0.0.zip
├── README.md
├── InstallMod.bat
├── BuildMod.bat
├── DEVELOPER_REPORT.md
├── BepInEx/
│   ├── plugins/
│   │   ├── RakeMultiplayer.dll
│   │   ├── 0Harmony.dll
│   │   ├── Mirror.dll
│   │   └── Telepathy.dll
│   └── config/
│       └── com.rake.multiplayer.cfg
└── Source/ (optional - for developers)
```

## Contact & Support

- GitHub Issues for bug reports
- Discussions for questions
- Discord for real-time help (if available)

---

**Version**: 1.0.0  
**Date**: 2024  
**Target Game**: Rake (2015) by Konsordo  
**Unity Version**: 5.x