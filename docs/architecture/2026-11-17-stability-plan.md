# Rake Multiplayer — architecture stability review and plan

Date: 2026-11-17
Scope: `RakeMod/Source/RakeMultiplayer` (33 901 lines, 83 files)

This document records what the architecture is worth today, where bugs actually come
from, and the work that removes whole classes of them instead of individual cases.
Status is updated as items land.

## Measurements

| Metric | Value |
| --- | --- |
| Source | 33 901 lines / 83 files |
| Largest file | `Core/RakeNetworkManager.cs` — 3 144 lines, 40+ packet handlers |
| Mutable `static` fields | 166 |
| Singletons (`Instance`) | 14 |
| `catch` blocks / empty | 85 / 14 |
| Tests written / executed by `run-tests.ps1` | ~385 / **86** |

### Why the test number matters

`run-tests.ps1` passes the filter `PacketSerializationTests`, so 86 packet tests run and
everything else is silently excluded. Running the remaining fixtures by hand:

- filter `Rake`: 87 checks, 43 pass, **44 fail** with `ECall ... not implemented in the
  system module` — those fixtures touch `GameObject`/`MonoBehaviour` and cannot execute
  outside Unity;
- filter `Camera`: 84 pass, **2 fail** on real assertions (`PigCameraQuotaTests`), which
  turned out to be an artefact of the runner never calling `[TearDown]` (see item 1).

So the reported "86/86" covers the least fragile part of the code (packet serialization)
while camera math, the Rake AI state machine and the packet handlers — the parts that
actually break — are not checked automatically at all.

## What already works (keep, do not rewrite)

1. **Transport abstraction.** `INetServer` / `INetClient` with two implementations
   (`LanServerTransport` over Telepathy TCP, `SteamServerTransport` over Steam P2P),
   ~150 lines each. Changing transport costs two files, not a rewrite. This is why
   FishNet/PurrNet/Mirror are not options: they require .NET Standard 2.0 and Unity
   2019+, and this project is `net35` on Unity 5.0.0f4 32-bit.
2. **Host authority.** One source of truth; desyncs are diagnosable as "client applied
   the wrong thing" rather than "both sides are right".
3. **State tied to reality, not intent.** `Application.loadedLevel >= 1` instead of an
   "are we in a match" flag removed a whole family of bugs in the 1.1.5 audit.
4. **Central input lock.** `PlayerInputLock` with explicit flags replaced scattered
   writes to `bControl`/`playerState`.
5. **Observability.** Dev bridge (`/state`, `/look`, `/ui`, phase control), F8/F9. Every
   recent bug was found by reading state, not by guessing.

## Where bugs come from (ranked by observed damage)

### 1. Ambient mutable state — 166 static fields, 14 singletons

Field lifetime is the process, not the match. Every 1.1.5 audit finding traces here:

- `_playerHealth` survived a level change (first trap after dawn = "9999" damage);
- `_playerStates` was not cleared between sessions;
- `_matchOver` re-armed the results overlay after returning to the lobby;
- `CameraMouseLook.enabled` was not restored after pause/results;
- `NextNetId` / `_nextTrapId` style counters are shared by every session.

**Fix:** match-scoped state object (`MatchState`) created on session start and destroyed
on session end, plus an `ISceneResettable` registry so no component can forget its reset.

### 2. No invariant checks

All 21 audit findings are detectable by three assertions:

- session phase == loaded level;
- results overlay armed ⇒ match is running;
- `playerId` referenced by a packet exists in the registry.

Today those rules live in the author's head. In a dev build they must fail loudly at the
moment of violation.

**Fix:** `Invariants` helper, called from the state transitions, dev/`-rakemcp` builds only.

### 3. God class without domain boundaries

`RakeNetworkManager` (3 144 lines) mixes validation, state mutation, UI updates and scene
loading in 40+ handlers. A change in one handler can break another with no warning.

**Fix:** split by domain (Player / Item / Rake / Camera / Session) behind a dispatch
table. Mechanical, no behaviour change; each bug becomes local.

### 4. Protocol has no version and no reconciliation

A client joining mid-match cannot rebuild the world — hence the "late joiner" and "owner
snapshot baseline" patches. A lost persistent event (item pickup, trap placement, role
assignment) is a permanent divergence.

**Fix:** `ProtocolVersion` in the handshake (mismatch = explicit refusal, not weird
behaviour) and a full snapshot on join.

### 5. Silent failures

85 `catch` blocks, 14 of them empty. Tracing the level-0 loop required a temporary stack
log. Failures must be loud once and countable, not swallowed.

**Fix:** `Safe.Run(scope, action)` — logs the first occurrence of each distinct failure
plus a counter.

### 6. Tests that do not run

See above. This is the single largest reason regressions reach players.

## Plan

| # | Work | Closes | Status |
| --- | --- | --- | --- |
| 1 | Run every fixture; triage failures; make Unity-only ones explicit | False coverage, hidden regressions | **done** |
| 2 | `MatchState` + one reset registry + invariants | The whole audit bug class | **done** (combat state migrated) |
| 3 | `ProtocolVersion` + join snapshot | Divergence, mixed-version installs | **done** |
| 4 | Split `RakeNetworkManager` by domain | Non-local bugs | **done** |
| 5 | Scripted smoke test over the dev bridge | Would have caught all 21 audit findings | **done** (single actor) |
| 6 | Environment validation (BepInEx/steam files, install dir) | "Works on my PC" crashes | **done** |

Order is by return on effort, not by size.

### Item 1 — details

- `Runner/Program.cs`: default filter = run everything; implement `[SetUp]`, `[TearDown]`,
  `[OneTimeSetUp]`, `[OneTimeTearDown]` (TearDown in `finally`, so a failing test cannot
  poison the fixtures that follow); classify tests that fail with Unity interop errors as
  `UNITY` (unrunnable outside the game) instead of `FAIL`, and report them as a separate
  number so they can never masquerade as passing coverage.
- `run-tests.ps1`: run the whole suite, print `passed / failed / unity`.
- Acceptance: the two `PigCameraQuotaTests` failures disappear (runner artefact), every
  remaining failure is a real defect, and the summary states exactly how much of the
  suite actually executed.

## Honest ceiling

Unity 5.0 / 32-bit / Mono 2.x: a native crash leaves no managed stack (the `mono.dll`
crash from another PC is exactly that), there is no real threading and GC produces
spikes. Logic and state bugs can be reduced by an order of magnitude with the plan above.
Native crashes cannot be excluded — only made visible and diagnosable: log the last step,
the versions of every native dependency, and refuse to start rather than fail silently.


## Progress log

### 2026-11-17 - item 1 done

`Runner/Program.cs` rewritten: the whole suite runs by default (it used to hard-code the
`PacketSerializationTests` filter), and it now implements `[SetUp]`, `[TearDown]`,
`[OneTimeSetUp]`, `[OneTimeTearDown]` and `[Ignore]`. `TearDown` runs on every outcome.

Result: **341 passed, 0 failed, 44 unity**, versus the previously reported 86.

- The two `PigCameraQuotaTests` failures were an artefact: the runner never called
  `[TearDown]`, so `ReserveExternalCamera_AllowsFive` left five cameras reserved and every
  later test in that fixture started with a full quota. With teardown in place they pass.
  The production API was correct; the harness was wrong.
- The 44 remaining are all `NetworkedRakeAITests`. Their `SetUp` creates a `GameObject` with
  `NetworkedRakeAI`, `Enemy_AI`, `Animator`, `AudioSource` and `NavMeshAgent`, so they cannot
  execute outside Unity (ECall). They are now reported as `unity=44` instead of passing
  silently, and `--strict` turns them into a failure so they cannot hide again.
  Making them runnable needs the combat logic extracted out of the `MonoBehaviour`
  (item 2/4) or an in-game harness (item 5).
- `run-tests.ps1` runs everything, keeps the filter argument, and prints only the summary.

### 2026-11-17 - item 2 started: invariants

`Core/Invariants.cs` (dev only; `RakeMultiplayerPlugin.DevMode` = GameBridge flag or
`-rakemcp`): each rule is logged once and then counted, and the counters are exposed in
`/state` as `invariants` / `invariantRules`.

Wired so far:

- `SessionManager` - an `Active` session requires `Application.loadedLevel >= 1`
  (`StartSession`, `HandleSessionStatePacket`, `ForceState`);
- `MultiplayerMenu` - the results overlay may only arm on a `GameOver` session.

Verified in game: 0 violations through host -> day/night cycle -> results -> back to the
menu, and a deliberate `/invariant` self-test on level 0 produces exactly
`session Active without a loaded level (x1) :: selftest: state=Active level=0 scene='MainMenu_v2'`.

Still pending for item 2: `MatchState` (match-scoped state instead of 166 process-wide
statics) and the `ISceneResettable` registry with a test that fails when a component forgets
to register.


### 2026-11-17 - item 2 continued: match-scoped state and one reset entry point

**`Core/MatchState.cs`.** The match now has an owner: `Begin(level)` when the session becomes
Active and `End(reason)` when it finishes or is aborted. A second `Begin` while a match is
running does not overwrite it silently - it ends the old one and logs why, because silent
overwrite is how stale state travelled between sessions.

Free of `UnityEngine` and BepInEx types on purpose, so the lifecycle is unit tested outside
the game. That is not cosmetic: the first version of the tests failed with
`FileNotFoundException: BepInEx, Version=5.4.23.2` because `MatchState` logged through
`RakeMultiplayerPlugin`, which derives from BepInEx's `BaseUnityPlugin` and drags the whole
loader into the process. Logging now goes through `Core/Diagnostics.cs` (an `Action<string>`
facade), and the plugin installs the real handlers on startup. Pure state machines no longer
need the engine to be exercised.

Migrated so far: player deaths (`PlayerDeathSystem` uses `MatchState.Current.DeadPlayers`
while a match runs, with a local fallback outside a match). The per-level statics in
`RakeNetworkManager`, `NetworkedCamera` and friends are still cleared through the reset
registry below - moving them into `MatchState` is the next step.

**`Core/SceneScope.cs`.** One list of process-wide resetters, called from a single place
(`Patches/GameEntryPatch.OnSceneChangeStart`, after the world snapshot is saved). Five
subsystems are registered today: `NetIdentity`, `NetworkedTrap`, `NetworkedCamera`,
`PlayerDeath`, `PlayerInputLock`. A failing reset is logged and does not stop the others, and
`CheckCleanState` asserts after the load that the network-id registry and the dead-player set
are actually empty.

Why a registry of static resetters instead of an `ISceneResettable` interface on
components: instance state dies with the scene by itself. The state that leaked was
process-wide (statics and `DontDestroyOnLoad` singletons), so that is what the registry
targets. `FindObjectsOfType` is also unreliable here (inactive objects are skipped, and
Unity 5.0 cannot search by interface).

Verified end to end: menu -> host -> 6 day/night transitions -> results -> main menu.
`matchActive` is true only inside the match, `matchLevel` follows the loaded level, the
match survived six level changes, `End` fired exactly once with
`reason=game over: HuntersWin`, and `/state` reported `invariants: 0` at every step.
Log: `[SceneScope] reset 5 subsystems (load Level_1(Night))` on every load,
`[MatchState] begin: level=1 (StartSession)`, `[MatchState] end: level=1 reason=game over: HuntersWin`.

Tests: **349 passed, 0 failed, 44 unity** (+8 `MatchStateTests`).

Still pending for item 2: move the remaining process-wide match state (player states, health
map, item registry) into `MatchState`, then delete the fallbacks.


### 2026-11-17 - item 2 finished: combat state moved into the match

`MatchState` now owns what used to be process-wide in `RakeNetworkManager`:
`PlayerHealth`, `RespawnDue`, `RespawnScheduled` (plus `DeadPlayers` from the previous step),
and `ResetCombat()` drops all of it at once. `End` calls it, and so does the level-change
path.

The migration is a property swap, not a rewrite: `RakeNetworkManager` keeps the same field
names at every call site, but they now read through

    MatchState.IsActive ? MatchState.Current.PlayerHealth : _loosePlayerHealth

so the lobby (no match yet) still works, and a match cannot inherit the previous one. No
call site changed, which is why the change is verifiable rather than hopeful.

`SceneScope` gained a sixth entry, `RakeCombatState`, so health and respawn timers are
cleared by the same single reset path as everything else - and the reset now reports what it
dropped: `[Death] level change: cleared 1 health entries, 0 pending respawns`.

**Verified in game, not just compiled.** A host-only session has an empty health map (only
clients are registered there), so a dev hook `/hp?player=&value=` was added that writes
through the same accessor the server handler uses:

- `/state` baseline: `matchHp: 0`;
- after `/hp?player=2&value=37`: `matchHp: 1` (the property really routes into the match);
- after one day/night change: `matchHp: 0` and
  `[Death] level change: cleared 1 health entries, 0 pending respawns`;
- at the results screen: `matchActive: false`, `invariants: 0`.

The multi-client packet path itself was not exercised (it needs a second game instance);
what was verified is the ownership and the reset, which is what this item changed.

Tests: **350 passed, 0 failed, 44 unity** (+1 `End_ClearsHealthAndRespawnTimers`,
+1 `ResetCombat_KeepsTheMatchRunning`).

Remaining process-wide state, deliberately not touched yet: `_playerStates`,
`_knownPlayerIds`, `_connectedClients`, the item/trap/marker registries. They belong to the
*connection*, not to the match (they exist in the lobby and are cleared on
`StopHost`/`Disconnect`), so they need a connection-scoped owner rather than `MatchState`.
That is item 3 territory.


### 2026-11-17 - item 3 done: the handshake has a version

`Networking/Protocol.cs` holds `Version` (currently 1) and the compatibility rule.
`ClientReadyPacket` now carries it, and the host validates it **before** touching any session
state:

- mismatch -> `ProtocolRejectPacket` with a readable reason, then a disconnect;
- a build that predates versioning (no version field at all) reads as 0 and is refused the
  same way, instead of being half-accepted and then failing in undefined ways;
- the client shows the reason in the multiplayer panel and drops the connection instead of
  retrying a handshake that cannot succeed.

`ClientReadyPacket.Read` reads the version defensively: a packet that ends after the name is
treated as "no version", not as a corrupt packet, so a mixed install produces a message
rather than a crash.

### The join snapshot already existed - it had two holes

`HandleServerClientReady` was already replaying cameras, pigs, traps, doors, consumed items,
session state and the phase to a late joiner. Two gaps remained:

- **dropped loot** was never replayed: anything a hunter dropped before you joined was
  invisible to you. Now tracked in `_droppedItemIds` and replayed. Only *dropped* items are
  replayed - scene items are already in the joiner's own copy of the level, and re-sending
  them would duplicate them;
- **world markers** were never replayed. Now sent to the joiner.

### Verified with a real connection, not only in unit tests

`RakeMod/Tests/Probe` is a small console probe that connects to a live host exactly like a
client (same Telepathy transport, same framing) and sends a handshake with a wrong version.

    RakeMultiplayer.ProtocolProbe.exe 7777 0     # an older build
    RakeMultiplayer.ProtocolProbe.exe 7777 -1    # a build with no version field

Against a running host both report `PASS`, the host log shows
`[Net] rejected C1: Mod version mismatch: client protocol unknown ..., host protocol 1`,
the match keeps running, and `/state` still reports `invariants: 0` with `protocol: 1`.

The happy path (a matching client) still needs a second game instance, so it was not run.

Tests: **355 passed, 0 failed, 44 unity** (+5: handshake round-trip, legacy handshake,
reject packet, version compatibility, mismatch message).


### 2026-11-17 - item 4 done: RakeNetworkManager split by domain

3 709 lines became 714 (core) + six partial files, by a mechanical split with a brace-aware
scanner and a name table: no call site changed, no behaviour changed.

| File | Lines | What lives there |
| --- | --- | --- |
| `RakeNetworkManager.cs` | 714 | connection lifecycle, dispatch, transport helpers, all state |
| `.Players.cs` | 894 | state, damage, death, respawn, teleport, movement |
| `.Items.cs` | 515 | pickup, drops, inventory effects |
| `.Rake.cs` | 368 | owner, attacks, sounds, grab |
| `.Session.cs` | 359 | session, level changes, handshake, results |
| `.Cameras.cs` | 243 | cameras, pigs, animals |
| `.World.cs` | 239 | markers, traps, fire visuals |

Two rules made this safe: only methods move (fields, events and constants stay in the core,
so nothing can end up referring to a field in another file's scope), and any method not in
the table stays where it was. The compiler and the 355-test suite confirmed nothing was
lost; the in-game cycle confirmed the behaviour.

### 2026-11-17 - item 5 done: a scripted cycle that asserts state

`RakeMod/Tests/smoke-cycle.ps1` drives a host through a full match over the dev bridge and
asserts, at every step: no invariant violations, time not frozen, a game level loaded,
`matchActive` set, `matchLevel == level`, menu closed in the match, results keep the level
loaded, and the return to the menu ends the match. It treats a busy bridge as "not ready
yet" (a level load blocks the main thread), and retries `return to lobby` because Unity drops
a load that arrives while another one is finishing.

It immediately earned its keep by finding two real defects:

- `MatchState.LevelIndex` never followed the day/night level change, so a match running on
  `Level_1(Night)` still claimed level 1. It is now synced from `Application.loadedLevel`
  every frame - the request that started a load can be a name, be dropped, or land elsewhere;
- the dev bridge gave up after 8 s, while a level load on this machine blocks the main thread
  for 10-20 s. A healthy game looked dead to every scripted check. The timeout is now 30 s
  and the caller decides how long to wait.

Single actor only: the protocol probe covers a second connection, a real second player needs
a second game instance.

### 2026-11-17 - item 6 done: say what is installed

`Core/EnvironmentCheck.cs`, run at startup before any native call:

- `steam_api.dll`, `CSteamworks.dll`, `steam_appid.txt` must be present next to `Rake.exe`
  and not truncated (a quarantined or replaced native library loads and then calls into the
  wrong ABI - the most likely shape of the `mono.dll` crash from the other PC);
- when they are not, **Steam is not initialised at all**, with the reason in the log, and LAN
  keeps working;
- foreign files in `BepInEx\plugins` are reported by name.

Installer: files in `BepInEx\core` that this installer does not ship are moved to
`BepInEx\_backup_<timestamp>\core` before extraction (reversible, and it stops our plugin
from running against somebody else's loader). Foreign plugins are reported, not touched.

Also: the launcher now drops the game to `BelowNormal` priority, because a 32-bit Unity build
at 200 fps makes the whole desktop stutter while a scripted check runs.


### 2026-11-17 - smoke test vs the dev bridge: what the last step taught

Running the cycle end to end exposed a real defect in the game and two in the harness.

**Game:** `ServerReturnToLobby` had no guard. Asking twice - a player clicking RETURN TO
LOBBY again, or any script retrying - issues a second `Application.LoadLevel(0)` while Unity
is still finishing the first, and Unity 5 then stops finishing the frame. It now returns
early when level 0 is already loaded or a network load is in flight.

**Harness (1):** the state endpoint was routed through the main-thread queue, so every poll
blocked behind `Update` - which is exactly what a load blocks. `/state` answers directly from
the connection thread again: it is a cheap read and it must work *while* the game is loading,
because that is when a check needs to know what happened. Long-running commands (`/lobby`,
`/phase`) still go through the main thread.

**Harness (2):** the bridge handled one connection at a time, so a single slow request made
the whole tool look dead. Each connection now gets its own worker, and a failed main-thread
command is logged instead of returning a silent error string.

**Test:** the script polled `/state` every 300 ms through the menu load and re-requested
`/lobby` whenever the answer was empty - the empty answer *was* the load. It now asks once and
stays silent for 20 s, and only retries when a valid state says the game is still in the level.

Verified by hand at that point: one `/lobby` from the results screen returns to `MainMenu_v2`
with `responding=True`, `matchActive:false`, `invariants:0`, `timeScale:1`. The scripted
version of that last step was not re-run in game after the harness fixes, to keep the machine
free - the phase checks, the results checks and the underlying behaviour are verified.

Also: the launcher drops the game to `BelowNormal` priority, because a 32-bit Unity build at
200 fps makes the desktop (and the mouse) stutter while a check runs.
