# Rake Game MCP

Local MCP server for the running Rake multiplayer mod. It proxies tool calls to
the in-game bridge that ships in `RakeMultiplayer.dll` (BepInEx).

## Start the game with the bridge

```text
Rake.exe -rakemcp
```

or enable it in `BepInEx/config/`:

```ini
[Diagnostics]
GameBridge = true
GameBridgePort = 8765
```

The bridge listens only on `127.0.0.1:8765`. Nothing is exposed to the network.

## MCP configuration

The workspace `opencode.json` already registers it:

```json
{
  "mcp": {
    "servers": {
      "rake-game": {
        "type": "local",
        "command": ["node", "D:/Rake Multiplayer/RakeMod/Tools/game-mcp/server.js"],
        "cwd": "D:/Rake Multiplayer"
      }
    }
  }
}
```

Reload OpenCode (`/reload`) or restart it. The tools appear as
`tools.rake_game_*` in Code Mode (or `rake_game_rake_state` as direct tools).

## Tools

| Tool | What it does |
|---|---|
| `rake_state` | Scene, menu open, active panel, resolution, FPS, network role |
| `rake_ui` | Live uGUI tree with names, rects, labels, buttons and inputs |
| `rake_screenshot` | Capture the game (UI included) and return a PNG |
| `rake_menu` | Open `main`, `host`, `join`, `lobby`, `settings`, `steam`, `browse`, or `close` |
| `rake_click` | Click a Button by GameObject name or label (e.g. `HostGameBtn`) |
| `rake_input` | Set and submit an InputField (e.g. `IPInput`) |

## Raw HTTP

The bridge is also usable without MCP:

```text
GET  /state
GET  /ui
GET  /screenshot
GET  /menu?screen=lobby
GET  /click?name=HostGameBtn
GET  /input?name=IPInput&value=192.168.1.20
GET  /close
```

## Batch-mode smoke test

The bridge works in `-batchmode` too (screenshots are black there, but state,
UI tree, menu navigation and clicks work):

```powershell
.\Rake.exe -batchmode -rakemcp
Invoke-WebRequest http://127.0.0.1:8765/state
Invoke-WebRequest 'http://127.0.0.1:8765/menu?screen=main'
Invoke-WebRequest 'http://127.0.0.1:8765/click?name=HostGameBtn'
```

This is a development tool. Leave `GameBridge` disabled for normal play.
