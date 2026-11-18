# Changelog

## 1.1.11-alpha.1-Main - 2026-11-18

Daytime Rake: patrol, run away when a hunter comes closer than 60 m, stop at 90 m,
patrol again. Day skip keeps the phase and the level in sync (verified on a solo host).

## 1.1.10-alpha.1-Main - 2026-11-17

Daytime Rake walks away from hunters instead of teleporting, and roams the forest
instead of standing still.

## 1.1.9-alpha.1-Main - 2026-11-17

A client can no longer load a level on its own (the host owns level changes); the
host logs its countdown every 30 seconds so "phase did not change" is visible.

## 1.1.8-alpha.1-Main - 2026-11-17

Client no longer spawns a proxy for itself or for the Rake owner; a client cannot
start or advance the day/night cycle; one Connected event per client; host-as-Rake
announces the Rake position instead of the world origin.

## 1.1.7-alpha.1-Main - 2026-11-17

Install integrity: the installer writes a SHA-256 manifest and the mod verifies it,
refusing multiplayer on a mixed or modified install. Fixed the extra hunter model when
a player owns the Rake, and a phase change that could stop before the level load.

## 1.1.6-alpha.1-Main - 2026-11-17

Reliability pass: per-match state, one scene reset path, protocol version and a
reject for incompatible builds, late joiners get dropped loot and markers, and the
scene test runner now actually runs every test.

## 1.1.5-alpha.1-Main - 2026-11-15

Full-cycle audit fixes: day/night levels are recognised again (safehouse respawn,
inventory restore, minimap), the results screen can be dismissed and the match
restarted, pause no longer flashes the vanilla menu or drifts the camera.

## 1.1.4-alpha.1-Main - 2026-11-15

Rake attacks are grabs again (the hunter is held, the camera turns, one hit per
grab), the pause menu buttons work, and leaving to the main menu cleans the session.

## 1.1.3-alpha.1-Main - 2026-11-15

Fixes the Rake AI standing still next to the hunters during the day, and stops a
per-frame exception in the F8 overlay after a level change.

## 1.1.2-alpha.1-Main - 2026-11-15

Rebuild of the 1.1 line from the **Main** branch. This is the build that ships the
graphics presets and the phase/climbing fixes; it does not contain the optics or
target-refresh work from the other branch.

## 1.1.1-alpha.1 - 2026-11-15

First public alpha of Rake Multiplayer.

- One Rake and up to three hunters, with a lobby and Rake role selection.
- Host-authoritative AI, damage, loot, doors, traps, cameras and phase state.
- LAN (TCP) and Steam lobby support.
- New physical-paper interface built over the vanilla menu room: main menu,
  host/join, Steam browser, lobby, settings, display settings and loading screen.
- Display & graphics settings: resolution, window mode, refresh rate, quality,
  V-Sync and anti-aliasing, saved and re-applied on launch.
- HUD in the top-right corner and a raised Rake health bar.
- The Rake now starts on forest ground instead of the level's staging platform during the day.
- Steam lobby worldwide search and null-safe lobby rows.
- New Rake HUD: ribbed wound health, scratched moon phase ring, ability sockets, torn-ring sense markers.
- Rake first-person camera: 105° field of view bound to the head bone, so the walk and
  run animation is the head bob instead of a synthetic sway.
- Hits: blood creeps in from the screen edges and a film-grain layer pulses for a
  second, on top of the HUD.
- Development watchdog no longer closes a normal session after 180 seconds.
- First-person camera now follows the animated head bone (the skeleton keeps updating while the body is hidden).
- First person: the eye sits slightly in front of the head so the Rake body can stay visible without the camera clipping into it.
- HUD reshaped: torn plate edges, crooked panels and tilted ability sockets.
- Target markers: bigger, with a dark backing, red for players and traps, and always oriented away from the screen centre.
- Session phase now follows the loaded level: a Day scene starts on Day 1 with a sun icon instead of claiming night.
- Pause menu restyled: torn plate, bone type, ember continue button; vanilla panel hidden, its actions kept.
- Climbing reworked: an object-shaped probe no longer grabs the Rake's own body, the grip
  stops flickering on round trunks, releasing the keys keeps it hanging, ledges are pulled
  over, Space always lets go and the wall cannot be re-grabbed for two seconds.
- First person while climbing hides the body, so the raised arms no longer fill the lens.
- Frame statistics overlay on `F8`: frame, CPU, GPU and render time in milliseconds,
  visible triangles, draw calls and objects.
- New `[Graphics] TerrainShadows` config option (default `false`): disables the terrain
  shadow pass on every level and re-applies it after a graphics preset change.
- Graphics presets (off / light / balanced / aggressive) in `[Graphics] Preset` and in
  Display & Graphics; balanced gives about 1.6x FPS on forest levels.
- Day/night timer now loads the matching level instead of only changing the sky.
- Clients follow the session cycle, so the HUD shows the real phase and countdown.
- Climbing recovers from a degenerate wall direction or a stale release timer instead
  of silently refusing every wall.
