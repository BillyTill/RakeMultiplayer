# Tests

## Compile

```powershell
dotnet build "RakeMod\Tests\RakeMultiplayer.Tests.csproj" -c Release
```

The project targets `net472` / `x86` because the mod and `UnityEngine.dll` are 32-bit.
It references the game's managed assemblies from `Rake\Rake_Data\Managed`.

## Run the pure tests

`dotnet test` does not work in this environment: the pinned NUnit adapter cannot enumerate
the installed .NET 10 runtimes. Use the small reflection runner instead:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "RakeMod\Tests\run-tests.ps1"
```

It runs `PacketSerializationTests` (86 tests, no Unity runtime required) and returns a
non-zero exit code on failure.

## Unity-dependent fixtures

`NetworkedRakeAITests` and `NetworkedCameraTests` construct `GameObject`s and need the
Unity player. Run the gameplay checks in-game instead:

```powershell
Rake.exe -batchmode -nographics -autotest   # host, arena, projectiles
Rake.exe -batchmode -nographics -aitest     # stalking cycle
Rake.exe -batchmode -nographics -deathtest  # death, loot, corpse, respawn
Rake.exe -batchmode -nographics -nctest     # noclip
```

Each writes `Rake\batchtest-result.json` with `{test, checks, failures}` and exits with
code 1 if any check failed (watchdog: 180 s).
