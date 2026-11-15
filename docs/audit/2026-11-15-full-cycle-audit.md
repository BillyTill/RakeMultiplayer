# Full-cycle audit — 2026-11-15

Four parallel audits over the whole session: connect -> lobby -> match -> day/night
transitions -> combat/death/respawn -> results -> back to the menu. Every finding below was
read from the source (`RakeMod/Source/RakeMultiplayer`), checked against the decompiled game
(`extracted/RakeProject/Scripts/Assembly-CSharp`) and, where possible, against the live log
(`Rake/BepInEx/LogOutput.log`).

Legend: **C** critical (breaks the match), **H** high, **M** medium, **L** low.
"Confirmed x2/x3" means independent audits reached the same finding.

## Status (fixed in the working tree, not committed yet)

| Id | Fix | Verified |
| --- | --- | --- |
| C1 | case-insensitive gameplay scene check in MultiplayerSceneManager, MapUI, MultiplayerMenu | log: [4.1] MapSettings ..., [4.6] Day phase: hunters respawned ...; MapPanel active=true |
| C2 | ServerReturnToLobby resets the session to Waiting; the overlay only arms at loadedLevel >= 1; the client keeps the session | match over armed once; RETURN TO LOBBY -> MainMenu_v2; re-hosting started a fresh Day 1 |
| C3 | ResetDeathState clears _playerHealth | code |
| C4 | client Disconnected inside a running match runs ExitToMainMenu | code |
| C5 | HandleServerRakeAttackRequest uses ServerGrabPlayer (host included) | code |
| H1 | Time.timeScale = 1 in GameEntryPatch.OnSceneChangeStart | code |
| H2 | phase clock on Time.unscaledDeltaTime | code |
| H3 | owner snapshot baseline reset on load and on owner change | code |
| H4 | the grab requires distance + line of sight before freezing the hunter | code |
| H5 | PlayerInputLock.SetMatchOver (re-applied every frame) + HUD hidden on the results screen | Control=false, canMove=false at match over |
| H6 | PlayerInputLock.ResetForScene() on every load | code |
| H7 | the free camera is disabled on every load | code |
| M1 | RakePlayerController.ApplyStun for a human Rake | code |
| M2 | a death without a spawner clamps the health instead of vanishing | code |
| M3 | host death calls NotifyHunterDied | code |
| M4 | _playerStates cleared on load | code |
| S1 | StartGamePacket uses ReadSafeString | code |
| S2 | loot list deduplicated per weapon type (partial: the server still trusts the list) | code |
| S3 | trap placement validated (finite position + within 12 m of the sender) | code |
| L1 | Rake HUD uses TotalNightsConfig | code |
| L2 | weapon dump moved to F9; F7/F8 are gameplay-only | code |
| L3 | lobby title no longer overwritten | code |
| L4 | display panel text matches the Apply button | code |
| L5 | teleports snap instead of lerping | code |
| L6 | not fixed: the enum values are unused, listed for completeness | - |


---

## C1. Scene names never match the gameplay check — `StartsWith("level")` is case sensitive

Confirmed x3. Scenes are `Level_1(Day)` .. `Level_5(Night)`; the code tests `"level"`.

| File | Line | Code |
| --- | --- | --- |
| `Core/MultiplayerSceneManager.cs` | 82 | `if (sceneName == gameScene \|\| sceneName.StartsWith("level"))` |
| `UI/MapUI.cs` | 96 | same pattern |
| `UI/MultiplayerMenu.cs` | 213 | same pattern |

Consequences (`OnGameSceneLoaded()` is dead code on every peer):

- `MapSettings.FindOrCreate` is never called -> `MapSettings.Current` stays null ->
  `DayNightManager.RespawnAtSafehouse` logs `[4.6] No safehouse or spawn point configured -
  skipping respawn` (log lines 303, 543, 779). No dawn safehouse teleport.
- `WorldPersistence.RestoreInventory` never runs -> **the whole inventory is lost on every
  day/night level reload** (log: `[Save] LoadGame skipped in multiplayer`, and no
  `[4.3] Inventory ...` line at all).
- `WorldPersistence.RestoreWorld`, `SpawnRake/SpawnItems` after load and the cycle re-sync
  never run.
- `MapUI` never shows the minimap and the F3 branch is unreachable.
- "A freshly loaded match closes the menu that started it" is dead code.

Log evidence: `[SceneManager] Scene changed to: Level_1(Day)` (5 occurrences),
`[DayNight] phase Night -> loading 'Level_1(Night)' (was 'Level_1(Day)')`; no `[4.1] MapSettings`
line anywhere.

Fix: `sceneName.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0`, or simply
`Application.loadedLevel >= 1` (which the file already uses elsewhere).

## C2. The results screen cannot be dismissed and a new match cannot be started

`UI/MultiplayerMenu.cs:322-338` (re-arm), `:165-181` (button),
`Core/RakeNetworkManager.cs:1511-1518` (`ServerReturnToLobby` only broadcasts `LevelChange(0)`).

1. Night 3 ends -> `SessionManager.EndSession(HuntersWin)` -> `_currentState = GameOver`;
   only `AbortSession` ever clears it (`Core/SessionManager.cs:156`).
2. Host clicks RETURN TO LOBBY -> `_matchOver = false`, load level 0.
3. Next `Update()`: `session.CurrentState == GameOver && !_matchOver` -> `_matchOver = true`,
   overlay back on top of the main menu.
4. The button only loads level 0 again: START GAME is unreachable, the process must be
   restarted. Clients are bricked the same way because their local `SessionManager` stays
   `GameOver` after `OnLeaveLobby`.

Fix: on RETURN TO LOBBY broadcast `SessionState.Waiting` (server) and apply it on clients;
arm `_matchOver` only while `Application.loadedLevel >= 1`.

## C3. Server health map survives a level change -> instant 9999 death

`Core/RakeNetworkManager.cs:2620` (`ResetDeathState` clears only the timers) and `:2600`
(`_playerHealth`), consumed at `:2566`.

Client dies near the end of a night -> `_playerHealth[C] = 0`, respawn pending (8 s) -> the
day transition reloads the level, `GameEntryPatch` clears `_dead` and the timers but not the
health map -> the client is alive in the new level with a stored HP of 0 -> the next trap or
PvP hit makes `ServerRegisterDamage` force a death (`PlayerDamagedPacket{Amount=9999}`).

Fix: clear the map in `ResetDeathState` and treat a missing entry as 100.

## C4. Host leaves -> the client is stranded in a frozen match

`Core/RakeNetworkManager.cs:342-345` (only `isConnected = false`),
`Core/SessionManager.cs:156` (`AbortSession` never tells clients),
`Components/NetworkedRakeAI.cs:400-405`.

Client sees no `Disconnected` handling: `SessionManager` still Active, `DayNightManager`
still running, `NetworkedRakeAI` returns at the `!isConnected` guard with the vanilla brain
already disabled -> the Rake freezes at its last snapshot, the HUD counts down forever,
no results screen. Only ESC -> EXIT TO MENU rescues the player.

Fix: on client disconnect run `MultiplayerMenu.ExitToMainMenu("host left")`.

## C5. A client-owned Rake grabs the host: the host is never frozen

`Core/RakeNetworkManager.cs:1806-1818` (`HandleServerRakeAttackRequest`, Kind 0) broadcasts a
`RakeGrabPacket`, which only clients receive; contrast `ServerGrabPlayer` (`:2325`) which also
runs the grab locally for the host.

Host = victim of a player-controlled Rake: no freeze, no camera turn, then 10 damage from
nowhere.

Fix: call `ServerGrabPlayer(packet.TargetPlayerId, rakePos, focus)` instead of the raw
broadcast.

## H1. Pausing across a level change leaves `Time.timeScale = 0`

`Core/RakeNetworkManager.cs:1499-1508` (client level change), `UI/PauseMenuSkin.cs:27-41`.

Client pauses (timeScale 0) -> host's night ends -> `LevelChange` -> `LoadLevel` recreates
`GamePauseMenu` with `isPaused = false`, so the plate hides but nothing restores the scale.
The new level looks alive but movement, the phase timer and the day/night cycle are frozen.

Fix: reset `Time.timeScale = 1` and the cursor in the load path (and in
`GameEntryPatch.OnSceneChangeStart`, which every load passes through).

## H2. The session clock runs on scaled time

`Core/DayNightManager.cs:173` (`_phaseTimer += Time.deltaTime`).

A host pressing ESC freezes the 300 s night for everybody: no `PhaseChangePacket`, client
Rake snapshots stop, countdown stops, with no indication why.

Fix: `Time.unscaledDeltaTime` for the phase timer.

## H3. Rake-owner anti-teleport baseline is not reset on a level change

`Core/RakeNetworkManager.cs:1732-1741` (`_lastRakeOwnerPosition/_lastRakeOwnerTime`), no reset
in `PrepareForSceneLoad` (`:1342`) or `SetRakeOwner` (`:1683`).

Client owns the Rake in `Level_1(Day)` at A -> phase reload -> the owner publishes from B
(hundreds of metres away) -> `Distance(B,A) > 30*dt` -> the packet is dropped before
`ApplyOwnerSnapshot` and before the relay. The baseline is not updated on rejection, so the
Rake stands still for everyone else until `30*dt > |B-A|` (~17 s for 500 m).

Fix: reset the baseline in `PrepareForSceneLoad` and when the owner changes; snap the first
snapshot after either event.

## H4. The grab is issued before the hit is known to be possible

`Components/NetworkedRakeAI.cs:1595` (grab on Attack entry) vs `:1615-1625` (strike gated by
distance, front and line of sight).

Rake enters Attack at <= 3 m behind a thin wall/fence -> the hunter is frozen for 1.3 s + 0.5 s,
`clearLine` fails so no damage lands, `_attackCooldown = 5` and Chase immediately re-enters
Attack: the hunter is held ~1.8 s out of every 5 s and never hit.

Fix: run the reach/LOS test before `BeginGrab`; abort to Chase with a short cooldown instead.

## H5. The results overlay does not stop the hunt

`UI/MultiplayerMenu.cs:322-338`.

`GameOver` only sets `_matchOver`, hides the menu and unlocks the cursor: `Time.timeScale`
stays 1, the player keeps control, `RakeHudUI` keeps drawing under the black overlay and the
world keeps simulating (`[Menu] Match over: THE HUNTERS SURVIVED` right after a level reload
in the log).

Fix: freeze input and hide the HUD while the overlay is up.

## H6. `PlayerInputLock` latches across a level change

`Core/DebugConsole.cs:1016-1023` (state machine), `Core/PlayerRagdoll.cs:396` (SetRagdoll
without a matching release when the ragdoll is destroyed by a scene load).

Die -> level changes while the ragdoll exists -> the ragdoll object is destroyed, `OnDestroy`
never calls `ReleaseRagdoll` -> `_ragdoll` stays true and `_applied` stays true, so `Apply()`
returns immediately and the next match's F1 menu / console no longer freeze the player.

Fix: reset the lock state from `GameEntryPatch` on scene load; release from
`PlayerRagdoll.OnDestroy`.

## H7. The debug free camera survives scene loads

`Core/DebugFreeCamera.cs:53-54` (`HideFlags.HideAndDontSave`), `:79-90`, `:118-125`.

Console -> `debug camera on` -> phase change -> the camera is not destroyed, stays enabled at
`depth + 1` while the new player cameras are enabled again: two full-cost cameras and doubled
WASD/mouse. `_disabledCameras` points at destroyed objects, so restoring the player cameras
does nothing.

Fix: disable the free camera from a scene-change hook.

## M1. Client-owned Rake ignores traps and stun

`Components/NetworkedTrap.cs:92-98` -> `NetworkedRakeAI.Stun` (`:2424`) sets
`currentState = Stunned`, but `UpdateStateMachine` is skipped while `_playerControlled`
(`:362-385`) and `RakePlayerController.Update` keeps moving the body; the trap is consumed
anyway.

Fix: give the controller a stun lockout to honour (like `_damageLockout`).

## M2. A death that arrives before `PlayerSpawner.LocalPlayer` exists is swallowed

`Core/PlayerDeathSystem.cs:53-57` returns false and `Patches/PlayerDeathPatch.cs:24` still
suppresses the vanilla path.

Lethal hit during a load -> no ragdoll, no loot, no respawn, negative health, "Game over"
suppressed -> the hunter is effectively unkillable for the rest of the match.

Fix: schedule a respawn and clamp the health instead of returning silently.

## M3. "Rake wins" is not evaluated when the host is the last hunter to die

`Core/PlayerDeathSystem.cs:93-97` (host path only broadcasts + schedules; `NotifyHunterDied`
is only called from `ApplyDeath`, which the host never runs for itself) vs
`Core/RakeNetworkManager.cs:2528` for a client death.

Fix: call `SessionManager.NotifyHunterDied(playerId)` on the host death path too.

## M4. Level change while a client was never seen -> stale player state is authoritative

`Core/RakeNetworkManager.cs:1342-1359` clears items/cameras/traps but not `_playerStates` or
`_playerHealth`, consumed at `:974` (pickup proximity) and `:2444` (late-join positions).

Fix: clear both in `PrepareForSceneLoad`.

## S1. `StartGamePacket` can allocate ~2 GB on the host

`Networking/StartGamePacket.cs:30` uses `reader.ReadString()`; every other string uses
`PacketSerializer.ReadSafeString` (`Networking/PacketSerializer.cs:89`).

Any peer that can connect can send a length prefix of `FF FF FF FF 07` and make the 32-bit
host allocate ~2 GB before reading a byte, repeatably.

Fix: `ReadSafeString` plus a length cap.

## S2. The loot list from a client is whitelisted but never verified

`Core/RakeNetworkManager.cs:2533` (`SanitizeDroppedWeapons`) -> `PlayerDeathSystem.DropItems`.

`PlayerDied{Health=0, WeaponIds=[6,6,6,6,6,6,6,6]}` spawns eight sniper rifles; the player is
respawned 8 s later, so it repeats.

Fix: derive the drop list from the server's own `WeaponManager` view and reject the packet
unless the server-side health is <= 0.

## S3. Trap placement from clients is unvalidated

`Core/RakeNetworkManager.cs:1206-1219` (trap) vs `:1235-1241` (bait, validated).

`MapMarker{MarkerType=1, Position=<anywhere>}` spawns an armed trap anywhere, including
under other players, in the safehouse or off-map.

Fix: mirror the bait checks (world bounds + distance to the sender's server-side proxy).

## L1. Rake HUD hard-codes "/ 3"

`UI/RakeHudUI.cs:421` uses `" / 3"` while the hunter HUD uses `TotalNightsConfig`.

## L2. Two hotkeys leak into the menu and F8 is double-bound

`UI/PerfStats.cs:110` (F8) and `Core/WeaponDiagnostics` (F8 again); `UI/RakeStatusPanel.cs:25`
(F7). Both work in the main menu and over the mod menu.

## L3. Lobby title is overwritten every frame

`UI/MultiplayerMenu.cs:556` writes "Host Lobby"/"Client Lobby", `:818` rewrites "LOBBY".

## L4. Display panel says "applied immediately" but applies only on Apply & save

`UI/RuntimeUIBuilder.cs:416` vs `:479-484`, `UI/DisplaySettings.cs:89-110`.

## L5. Teleports lerp instead of snapping

`Core/RakeNetworkManager.cs:2194` uses `ApplyPosition` although `ApplyPositionImmediate`
(`Components/NetworkedPlayer.cs:411`) exists; the test arena slides instead of appearing.

## L6. Dead protocol values

`Networking/PacketType.cs:21-32` declares 12 types with no class and no `Deserialize` case
(`FireRequest`, `Flashlight`, `PlayerInput*`, `WeaponState/Switch/Ack`, `Reconnect*`).
Nothing sends them today, but `Components/NetworkedItem.cs:87-114` still writes legacy bytes
(`21`/`22`) that fall into that hole.

---

## Checked and found correct

- All 37 live packets in `Networking/Packets.cs` have symmetric `Write`/`Read`; the id scheme
  matches Telepathy's counter and the Steam transport's `_nextConn`.
- No duplicate packet-handler registration: `ReregisterHandlers` is a no-op and dispatch is a
  `switch`.
- Late join sends `SessionStatePacket` + `PhaseChangePacket` and the Rake snaps in via the
  15 Hz `RakeStatePacket`.
- One hit per grab pacing, stun expiry, damage dedup (`IsDead`/`IsDown`), loot replication and
  the respawn camera/input restore all behave as designed.
- Every clickable canvas has a `GraphicRaycaster` and an `EventSystem` exists in level 0 and
  in all level scenes.
- `LoadingScreen` closes correctly after a synchronous `LoadLevel`.
- Menu/pause/loading canvases are created once in `RakeMultiplayerPlugin.Awake`, so they are
  not duplicated per scene.

## Follow-up (reported after the audit, also fixed)

**The old pause menu flashed for a frame.** GamePauseMenu.Update activates its panel on
ESC and the mod only hid it on the next frame, so the vanilla black menu rendered once.
Fixed with a Postfix on GamePauseMenu.Update that deactivates the panel in the same frame
(Patches/InputGatePatches.cs), plus PauseMenuSkin.Show() deactivating it directly.

**The camera followed the cursor while paused and on the results screen.** CameraMouseLook
keeps accumulating the mouse axis: with Time.timeScale = 0 the smoothing factor is zero but
the target rotation still grows, so the view snapped when the game resumed; on the results
screen Control = false alone was not enough because FPSController.SetPlayerDisableState
re-enables the component on respawn. PlayerInputLock now has a dedicated SetPause flag and
disables the CameraMouseLook component itself while any lock is active, restoring the
previous enabled/Control state on release.

Also: the Rake health bar was re-shown every frame by UpdateRakeHealthBar() after the results
overlay hid it; it now returns early while the match-over overlay is up.
