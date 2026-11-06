# Rake Multiplayer Mod

Cooperative multiplayer mod for Rake (2015) by Konsordo. Adds 2-4 player co-op support to the original single-player horror game.

## Features

- **2-4 Player Co-op**: Play through the entire game with friends
- **Full Player Sync**: Position, rotation, animations, camera, stamina, health, flashlight
- **Server-Authoritative Rake AI**: The monster behaves identically for all players
- **Item Synchronization**: Batteries, cameras, weapons, quest items
- **Camera System**: Place and view surveillance cameras cooperatively
- **LAN & Online Play**: Direct IP connection or Steam P2P (planned)
- **Host Migration**: Automatic host migration on disconnect (planned)

## Requirements

- **Game**: Rake (2015) - Original Steam release
- **OS**: Windows 10/11 (64-bit)
- **.NET Framework**: 4.7.2 or later
- **BepInEx**: 5.x (included in installation)

## Installation

### Automatic Installation (Recommended)

1. Download the latest release from the Releases page
2. Extract the zip file to your Rake game folder:
   ```
   Steam/steamapps/common/Rake/
   ```
3. Run `InstallMod.bat` as Administrator
4. Launch the game through Steam

### Manual Installation

1. Install BepInEx 5.x for Unity 5.x:
   - Download BepInEx 5.x (Unity 5.x version) from https://github.com/BepInEx/BepInEx/releases
   - Extract to your Rake game folder
   - Run the game once to generate folders

2. Copy mod files:
   - Copy `RakeMultiplayer.dll` to `Rake/BepInEx/plugins/`
   - Copy `Mirror.dll` to `Rake/BepInEx/plugins/`
   - Copy `0Harmony.dll` to `Rake/BepInEx/plugins/`

3. Configure (optional):
   - Edit `Rake/BepInEx/config/com.rake.multiplayer.cfg`

## Usage

### Hosting a Game

1. Launch Rake
2. Click "Multiplayer" in the main menu
3. Click "Host Game"
4. Share your IP address with friends (shown in lobby)
5. Click "Start Game" when everyone has joined

### Joining a Game

1. Launch Rake
2. Click "Multiplayer" in the main menu
3. Click "Join Game"
4. Enter the host's IP address and port (default 7777)
5. Click "Connect"

### LAN Play

- Host clicks "Host Game" → "LAN"
- Other players on same network will see the game in server browser
- Or use direct IP with local IP address

### Online Play (Steam P2P)

- Requires Steam to be running
- Host clicks "Host Game" → "Online (Steam)"
- Friends can join via Steam invite or "Join Game" → "Steam Friends"

## Configuration

Edit `BepInEx/config/com.rake.multiplayer.cfg`:

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
```

## Controls

- **F1**: Toggle multiplayer menu (in-game)
- **Tab**: Show player list
- **F**: Flashlight (synced)
- **I**: Inventory (synced)
- **E**: Interact/Pickup (synced)

## Known Issues

1. **Phantom Collisions**: Remote players may occasionally block movement - fixed in v1.0.1
2. **Rake Desync**: High ping (>200ms) may cause slight position differences - interpolation helps
3. **Camera View**: Only one player can view a camera at a time
4. **Save Games**: Multiplayer sessions don't save - play in one session

## Building from Source

### Prerequisites

- Visual Studio 2022 or JetBrains Rider
- .NET Framework 4.7.2 Developer Pack
- Unity 5.x (for reference assemblies)

### Build Steps

1. Open `Source/RakeMultiplayer/RakeMultiplayer.csproj`
2. Update reference paths in `.csproj` to match your game installation
3. Build in Release configuration
4. Output will be in `Source/RakeMultiplayer/bin/Release/`
5. Copy to `Rake/BepInEx/plugins/`

## Technical Details

### Architecture

- **Networking**: Mirror (high-level HLAPI)
- **Transport**: Telepathy (TCP) / KCP (UDP) / Steam P2P (planned)
- **Mod Loader**: BepInEx 5.x
- **Patching**: Harmony 2.x
- **Authority Model**: 
  - Player movement: Client-authoritative with server reconciliation
  - Rake AI: Server-authoritative
  - Items: Server-authoritative ownership

### Key Classes

- `RakeMultiplayerPlugin`: Main entry point
- `RakeNetworkManager`: Custom NetworkManager
- `NetworkedPlayer`: Player synchronization
- `NetworkedRakeAI`: Server-authoritative monster AI
- `NetworkedItem`: Item pickup/drop/use sync
- `NetworkedCameraSystem`: Camera placement/viewing

### Sync Rates

- Player Position: 20 Hz
- Player Rotation: 30 Hz
- Animations: 10 Hz
- Rake Position: 15 Hz
- Rake State: 10 Hz

## Troubleshooting

### "Mod not loading"
- Check BepInEx console (tilde ` key)
- Verify `RakeMultiplayer.dll` is in `BepInEx/plugins/`
- Check `BepInEx/LogOutput.log` for errors

### "Can't connect"
- Verify port 7777 is open (TCP/UDP)
- Check firewall/antivirus
- Try LAN mode first
- Ensure both players have same game version

### "Rake not moving"
- Host must be the one running the AI
- Check `NetworkedRakeAI` is on the Rake object
- Verify NavMesh is baked in the scene

### "Items not syncing"
- Items must have `NetworkedItem` component
- Prefabs must be registered in NetworkManager
- Check `NetworkIdentity` on item prefabs

## Credits

- **Original Game**: Konsordo (Nikita Subachev)
- **Networking**: Mirror Networking (vis2k)
- **Mod Framework**: BepInEx Team
- **Patching**: Harmony (pardeike)
- **Transport**: Telepathy (vis2k)

## License

This mod is for educational purposes only. Not affiliated with Konsordo or the original Rake developers.

## Support

- Issues: GitHub Issues
- Discussions: GitHub Discussions
- Discord: [Link if available]

---

**Version 1.0.0** - Initial release