# Rake Multiplayer Mod

Developer notes for the **1.1.0-alpha.1** cooperative mod.

## Build

```powershell
dotnet build RakeMod/Source/RakeMultiplayer/RakeMultiplayer.csproj -c Release
```

Output: `RakeMod/Source/RakeMultiplayer/bin/Release/net35/RakeMultiplayer.dll`.

The plugin targets **net35 / x86** and references the assemblies from
`Rake/Rake_Data/Managed`. It is a BepInEx 5 plugin; Harmony patches are applied at
startup.

## Tests

```powershell
powershell -ExecutionPolicy Bypass -File RakeMod/Tests/run-tests.ps1
```

Packet/runtime smoke checks can also be run from the game:

```text
Rake.exe -batchmode -nctest
Rake.exe -batchmode -deathtest
Rake.exe -batchmode -autotest
```

## Installer

`RakeMod/Installer/build.ps1` builds the plugin, packs BepInEx + the plugin + Steam
native DLLs into `payload.pak`, and compiles `RakeMultiplayerSetup.exe`.

## UI notes

- `UI/RuntimeUIBuilder.cs` builds the menu: card buttons, paper forms, display
  settings and the HUD.
- `UI/MenuSkin.cs` generates the paper/plate textures and loads the embedded
  camera/reel sprites.
- `UI/MenuBackdrop.cs` keeps the vanilla menu room visible while hiding its
  screen-space buttons.
- `UI/LoadingScreen.cs` is the standalone loading overlay.
- `UI/DisplaySettings.cs` saves resolution / window / refresh / quality settings in
  `PlayerPrefs`.

## Development bridge (MCP)

Launch with `-rakemcp` (or enable `Diagnostics/GameBridge`) to open a local control
bridge on `127.0.0.1:8765`. The MCP server lives in `tools/game-mcp`; it exposes
state, UI tree, screenshots, menu navigation, button clicks and input fields.
This is a development tool and is disabled by default.

## Game data note

The test copy disables the Unity Configuration dialog by setting
`PlayerSettings.displayResolutionDialog = 0` in `Rake_Data/mainData`
(backup: `mainData.bak`). The patch script is
`tools/disable_resolution_dialog.py`.
