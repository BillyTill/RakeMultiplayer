# Changelog

## 1.1.25-alpha.1-Main - 2026-11-20

The server now enforces the player-Rake attack cooldowns (20 s hunter, 10 s animal,
50 s camera) instead of the old 1 s rate limit, so a modified client can no longer
swing every second. A broken camera is removed from the host's monitor list as well,
so five occupied slots always mean five buttons.

A phase change now runs after the frame instead of replacing the level in the middle of
the tick. Ammo and sprung traps survive the transition instead of resetting, and the
Rake keeps its health between scenes - it used to be healed back to the session value at
every nightfall, and every trap came back armed. The world snapshot is applied once per
level from a single entry point, so cameras and traps cannot be lost to hook ordering.

The trailer's "skip to the next hunt" button now advances the phase for everybody: the
host queues the transition, a client asks the host with PhaseSkipRequest. Before, a
client's button did nothing (its local level load was refused) and the host's button
changed the level behind the phase clock's back.

Protocol 3: a client introduces itself with a handshake before the host hands out any
world state, so a mismatched build is refused before it can load a level. The host now
announces level changes by scene name (the index is still stale right after LoadLevel in
this engine), which fixes the client lagging one phase behind. A joining player now also
receives dead players and the remaining camera-box count, respawn points keep clear of
the Rake, a sprung vanilla trap is spent, and bullets no longer damage animals twice.

The AI Rake no longer camps a downed hunter: a landed hit switches its task straight to
Flee, it ignores the same hunter for a few seconds after they get up, and it cannot hit
the same person twice within nine seconds. Before, it stood next to the ragdoll and swung
again the moment the hunter stood up.

Audit fixes (see docs/audit/2026-11-20-mechanics-vs-implementation.md): single player no
longer has its cameras, bait or traps hijacked by the multiplayer patches; the host
validates a client's shot profile against the weapon it reports holding (protocol 4);
world state is held back until the handshake validates the build; a late joiner receives
real camera state, dead players and the camera-box remainder; a sprung vanilla trap stays
sprung across a phase change; the respawn point keeps a full 25 m from the Rake; shotgun
pellets are sampled in a cone instead of a square; a lethal hit that cannot start the death
flow is retried instead of leaving the body at 1 HP; console teleport and simulated hits
are host-only.

## 1.1.23-alpha.1-Main - 2026-11-18

A level load refused on a client is no longer reported to the host as a real level
change (Harmony ran the postfix anyway).

## 1.1.22-alpha.1-Main - 2026-11-18

A client that changes level on its own is pulled back to the host level instead of
sitting on a black screen; the phase endpoint reports server/clientOnly.

## 1.1.21-alpha.1-Main - 2026-11-18

Installer: removes stale files from older builds, refuses to run while Rake is open.

## 1.1.20-alpha.1-Main - 2026-11-18

Protocol version 2: a client from an older build is refused with a readable message
instead of connecting and skipping the phase on its own side.

## 1.1.19-alpha.1-Main - 2026-11-18

Items picked up stay gone after a level change instead of reappearing at dawn.

## 1.1.18-alpha.1-Main - 2026-11-18

Skipping the phase from a client now asks the host (PhaseSkipRequest) instead of
loading a level locally, which left that client on a black screen.

## 1.1.17-alpha.1-Main - 2026-11-18

At night the Rake charges when it is spotted instead of running away, and the pause
between tasks is 20-35 s instead of 60-90.

## 1.1.16-alpha.1-Main - 2026-11-18

The Rake starts the day deeper in the forest (200+ m from the hunters instead of on
the camp doorstep), and the last two nights last 30 minutes.

## 1.1.15-alpha.1-Main - 2026-11-18

The Rake no longer slides in the idle animation: the animator speed follows the body
when the combat state is Idle (day patrol and fleeing).

## 1.1.14-alpha.1-Main - 2026-11-18

AI panel says what the Rake is actually doing (patrol/flee/hunt) instead of a bare
combat state that read as "standing still".

## 1.1.13-alpha.1-Main - 2026-11-18

Daytime Rake resumes patrolling right after a flee; the F7 AI panel highlights the Rake
while it is open.

## 1.1.12-alpha.1-Main - 2026-11-18

Daytime Rake never teleports: it outruns the hunter (9+ m/s vs 8) and keeps running
away from 60 m until 90 m, then patrols again.

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
