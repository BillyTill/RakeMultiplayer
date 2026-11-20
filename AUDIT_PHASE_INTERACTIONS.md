# Аудит §10 MECHANICS_INTERACTIONS.md — пункты 1–5 (I-PHASE-1/3/6, I-TICK-1/2)

Репозиторий: `D:\Rake Multiplayer`, мод: `RakeMod\Source\RakeMultiplayer`, BepInEx/Harmony, Unity 5, x86, host-authoritative.
Аудит выполнен по фактическому коду (только чтение), затем по тем же пунктам внесены
исправления: часть (I-PHASE-1, I-PHASE-6, I-TICK-1) — в код, часть (I-PHASE-3,
I-TICK-2) — не внесена и явно помечена ниже (см. §2 «Журнал исправлений»).

---

## 1. Вердикты по коду ДО исправлений

### 1.1. I-PHASE-1 — ВЕРДИКТ: PARTIAL

Требование: переход фазы сохраняет камеры и их состояние, капканы (поставленные и сработавшие), инвентарь каждого игрока, потреблённые предметы мира, HP Рейка.

Что реально работает:

- Камеры: `Core\WorldPersistence.cs:98-107` — состояние снимается (`SaveWorld`, :46-59) и восстанавливается (`RestoreWorld`), причём только для камер, которые нашлись в новой сцене по тому же `CameraId`:
  `WorldPersistence.cs:101` — `if (!NetworkedCamera.TryGetCamera(state.CameraId, out var cam) || cam == null) continue;`
  `CameraId` детерминированно назначается по порядку (позиция, имя): `Core\RakeNetworkManager.Cameras.cs:112-146` (`RegisterCameras`), поэтому сценовые камеры переживают переход.
- Потреблённые предметы: `Patches\GameEntryPatch.cs:160` (`WorldPersistence.SaveConsumedItems()`) перед выгрузкой сцены и `Core\GameEntryPatch.cs:75` (`WorldPersistence.ApplyConsumedItems()`) после загрузки.

Что сломано:

1. **Инвентарь не сохраняется никогда.** `WorldPersistence.SaveInventory` вызывается только из `Core\MultiplayerSceneManager.cs:199`, внутри `LoadGameScene()` — а этот метод нигде не вызывается (меню грузит уровень напрямую: `UI\MultiplayerMenu.cs:755` — `UnityEngine.Application.LoadLevel(1);`). `RestoreInventory` (`MultiplayerSceneManager.cs:150`) при пустом словаре выходит на `WorldPersistence.cs:158`: `if (!_inventories.TryGetValue(player.PlayerId, out var ammo)) return;` — то есть после каждой смены фазы патроны остаются дефолтными.
2. **HP Рейка не сохраняется, а подменяется константой.** `SaveWorld` пишет `WorldPersistence.cs:88` — `_rakeHealth = SessionManager.Instance.RakeHealth;`, но `SavedRakeHealth` (`WorldPersistence.cs:179`) не читается нигде. Вместо этого `Core\DayNightManager.cs:265` вызывает `ApplyPersistentRakeHealth()` (там же :435-448), который берёт `SessionManager.Instance.RakeHealth` (:439) и делает `rake.ApplyHealth(health)` (:444). А `SessionManager.RakeHealth` на хосте равен константе: `Core\SessionManager.cs:103` — `_rakeHealth = 100f;` (обновляется только при смерти Рейка: `Patches\GamePatches.cs:847` — `SessionManager.Instance?.UpdateRakeHealth(0f);`). Итог: на каждом переходе в ночь HP Рейка принудительно ставится 100 (лечение или почти смерть), а не переносится.
   Дополнительно: `ApplyPersistentRakeHealth` вызывается ДО `LoadLevelForPhase` (`DayNightManager.cs:265` против :288), то есть применяется к Рейку старой сцены, который через кадр уничтожается.
3. **Капканы теряют состояние «сработавший» и размножаются.** `WorldPersistence.cs:25-29` (`TrapState`) хранит только `Position`/`Radius`; `WorldPersistence.cs:61-69` не сохраняет `IsTriggered`/`IsArmed`. Плюс `WorldPersistence.cs:71-84` на каждом сохранении добавляет ещё и ванильные капканы (`levelItems.trapObjects`), которые в новой сцене снова существуют, а зоны `NetworkedTrap` на их позициях уже восстановлены → каждый переход добавляет дубликаты (ограничение `NetworkedTrap.MaxTraps = 20`, `Components\NetworkedTrap.cs:16,59`), а сработавший капкан возвращается взведённым.

К чему приводит: после рассвета/заката игрок теряет патроны, Рейк получает HP=100 (каждый раз «лечится» или наоборот почти умирает), сработавшие капканы снова ловят, а число капканов растёт от фазы к фазе.

### 1.2. I-PHASE-3 — ВЕРДИКТ: PARTIAL

Требование: восстановление строго в порядке сцена → `WorldPersistence.Restore` → спавн игроков → AI → HUD.

- В `Core\MultiplayerSceneManager.cs:129-171` порядок обратный: :144 `RakeNetworkManager.Instance?.EnsureRemotePlayersSpawned();` (спавн игроков) стоит РАНЬШЕ :147 `WorldPersistence.RestoreWorld();`, затем :150 `RestoreInventory`, :155 `SpawnRake()`, :156 `SpawnItems()`.
- Есть второй, более ранний путь инициализации: Harmony-патч `Core\GameEntryPatch.cs:14-18` на `FPSController.Start`, который в :75 вызывает `ApplyConsumedItems`, в :83 — `RestoreWorld()` (только хост), в :93 — `EnsureRemotePlayersSpawned()`. Снимок одноразовый (`WorldPersistence.cs:119` — `_cameraStates.Clear();`), поэтому порядок фактически определяется тем, кто первым вызвал `RestoreWorld` (на хосте — `GameEntryPatch` во время загрузки сцены, на клиенте — корутина `MultiplayerSceneManager` на следующем кадре).
- HUD создаётся до какого-либо мира и живёт глобально: `Core\RakeMultiplayerPlugin.cs:200-203` (`DontDestroyOnLoad`, `menuObj.AddComponent<UI.MultiplayerMenu>();`), HUD Рейка — `Components\RakePlayerController.cs:345` (`_hudUi = UI.RakeHudUI.Create(this);`). Значения HUD при этом опрашиваются каждый кадр (`UI\MultiplayerMenu.cs:425` `UpdateHUD()`, :384 `UpdateRakeHealthBar()`), а не снимаются один раз, поэтому «чужих значений» на практике нет — но заявленный порядок «HUD после мира» не выполняется и не проверяется.

К чему приводит: удалённые игроки спавнятся в мир, который ещё не восстановлен; при изменении порядка вызовов часть состояний камер/капканов теряется молча (второй `RestoreWorld` — no-op).

### 1.3. I-PHASE-6 — ВЕРДИКТ: PARTIAL

Требование: смена фазы = `LoadLevel`; `MatchState.ResetCombat` оживляет всех; фаза не наносит урон и не лечит.

- `LoadLevel` на смене фазы действительно есть: `Core\DayNightManager.cs:288` → :374 `Application.LoadLevel(target);`.
- **`MatchState.ResetCombat` на смене уровня не вызывается вообще.** `Core\MatchState.cs:94-100` (`DeadPlayers/PlayerHealth/RespawnDue/RespawnScheduled.Clear()`) вызывается только из `MatchState.Begin` (:62) и `MatchState.End` (:82) — grep по `ResetCombat` даёт только эти места и тест.
- Оживание происходит побочным эффектом: `Core\SceneScope.cs:68` (`"PlayerDeath"` → `PlayerDeathSystem.ResetForScene`) и `Core\PlayerDeathSystem.cs:45` — `if (MatchState.IsActive) MatchState.Current.DeadPlayers.Clear();`; плюс `Core\GameEntryPatch.cs:69` → `RakeNetworkManager.ResetDeathState()` (`Core\RakeNetworkManager.Players.cs:859-876`) очищает карты здоровья/респавна. `SetLevel` живёт в `Update` (`Core\RakeNetworkManager.cs:229`).
- «Не лечит» нарушено: `DayNightManager.cs:265` + :444 принудительно ставит HP Рейка (см. п.1.1 п.2) — это лечение на границе фазы.

К чему приводит: смерть фазу не переживает только пока отрабатывают `SceneScope`/`FPSController.Start`; при любом сценарии без них (наблюдатель, игрок-Рейк без `FPSController`) трупы и таймеры респавна уедут в новую сцену, а Рейк на рассвете/ночью лечится.

### 1.4. I-TICK-1 — ВЕРДИКТ: VIOLATED

Требование: переход фазы не в середине тика — таймер проверяется, переход ставится в очередь и исполняется после репликации.

- `Core\DayNightManager.cs:217-220` — прямо в `Update`:
  `if (_phaseTimer >= phaseDuration)` / `TransitionPhase();`
- `TransitionPhase` (:226) сразу рассылает фазу (:254 `BroadcastPhaseChange();`) и сразу грузит уровень: :288 `LoadLevelForPhase(_currentPhase);` → :374 `Application.LoadLevel(target);` — синхронно, внутри того же `Update`.
- Очереди/отложенности нет ни в каком виде (поля-флага/корутины/`Action` нет). Обработка пакетов и репликация — в другом MonoBehaviour (`Core\RakeNetworkManager.cs:220-255`, `ProcessServerMessages`/`ProcessClientMessages`/`TickRespawns`), Script Execution Order для них не задан, то есть `LoadLevel` может произойти до обработки пакетов этого кадра.

К чему приводит: тик рвётся посередине — часть кадра доигрывается уже в новой сцене на реестрах, очищенных `SceneScope.ResetAll` (`Patches\GameEntryPatch.cs:165`, `Core\SceneScope.cs:81-107`), теряются пакеты и «полуприменённые» состояния камер/капканов/смертей.

### 1.5. I-TICK-2 — ВЕРДИКТ: VIOLATED

Требование: сначала применяются все попадания тика, потом считаются смерти.

- Урон применяется по одному попаданию с немедленной смертью: `Core\ProjectileSystem.cs:196-204` —
  `ProjectileSystem.ApplyingImpact = true;` / `rake.TakeDamage(damage, _damageType);`
  смерть считается тут же, в постфиксе `Patches\GamePatches.cs:830-859` (`if (health <= 0f)` на :841 → `BroadcastRakeDied`, `ShowRakeDeath`).
- Остальные попадания того же тика отбрасываются: `ProjectileSystem.cs:215` — `if (player.IsDead) return;`; для Рейка второй выстрел того же кадра бьёт уже мёртвый/уничтожаемый объект.
- Учёта «сначала всё, потом смерти» нет: поиск `pendingDamage|DamageQueue|_damageQueue|deferred` по `Source` даёт 0 совпадений. Для игроков `ServerRegisterDamage` вызывается уже ПОСЛЕ удара: `Core\RakeNetworkManager.Players.cs:963-964` (`fps.TakeDamage(...)` затем `ServerRegisterDamage(...)`), а смерть по HP форсируется в :814-817.

К чему приводит: дробовик, попавший тремя дробинами в одного игрока, засчитывает только первую; часть урона «теряется», учёт HP на сервере расходится с фактическим — ровно сценарий «мёртвый не успел».

---

## 2. Журнал исправлений (внесено в код)

### 2.1. I-PHASE-1 — HP Рейка

- `Core\WorldPersistence.cs` — `SaveWorld` теперь пишет **живой** HP Рейка (`NetworkedRakeAI.CurrentHealth`), а не устаревшее значение из `SessionManager`; добавлено свойство `HasSavedRakeHealth`.
- `Core\GameEntryPatch.cs` — после загрузки сцены и создания `NetworkedRakeAI` (сразу после `RegisterSceneObjects`) сохранённый HP применяется к Рейку новой сцены (`WorldPersistence.SavedRakeHealth`), вместо применения к Рейку старой сцены.
- `Core\DayNightManager.cs` — `ApplyPersistentRakeHealth` берёт HP из `WorldPersistence` (реальный, снятый перед выгрузкой), а `SessionManager.RakeHealth` используется только как фолбэк, когда сохранённого HP нет.

### 2.2. I-PHASE-1 — инвентарь

- `Patches\GameEntryPatch.cs` — в `OnSceneChangeStart` (до выгрузки сцены, на каждом пире) сохраняется инвентарь локального игрока: `WorldPersistence.SaveInventory(...)`. Теперь `RestoreInventory` в `MultiplayerSceneManager.OnGameSceneLoaded` получает реальные данные, а не пустой словарь.
- Ограничение (честно): переносятся патроны (`WeaponManager.weaponsAmmo`). Сами стволы в руках перенести нельзя — `WeaponManager.playerWeapons` хранит инстансы сцены; это отдельная задача, в файле отмечено как известное ограничение.

### 2.3. I-PHASE-1 — капканы

- `Core\WorldPersistence.cs`: в `TrapState` добавлено `Triggered`; `SaveWorld` сохраняет `IsTriggered`; ванильные капканы (`levelItems.trapObjects`) больше не добавляются повторно, если на этой позиции уже есть зона `NetworkedTrap` (дедупликация по расстоянию 0.5 м) — дубликаты от фазы к фазе прекращены.
- `Components\NetworkedTrap.cs`: добавлен `RestoreTriggeredState(bool)` — сработавший капкан после восстановления остаётся потраченным (`IsArmed = false`, `IsTriggered = true`) и это реплицируется.

### 2.4. I-PHASE-3 — порядок — **внесено интегратором (20.11.2026)**

- `Core/WorldPersistence.cs`: добавлен единый идемпотентный вход
  `EnsureWorldRestored(where)` — снапшот применяется ровно один раз на загруженный
  уровень, кто бы ни вызвал первым; `ApplySavedRakeHealth(where)` с таким же гейтом.
- `Core/GameEntryPatch.cs`: вместо `ApplyConsumedItems()` + `RestoreWorld()` вызывает
  `EnsureWorldRestored`.
- `Core/MultiplayerSceneManager.cs`: `EnsureWorldRestored` вызывается ПЕРВЫМ, затем
  инвентарь, затем спавн Рейка/предметов, и только потом `EnsureRemotePlayersSpawned`
  (раньше прокси спавнились до восстановления мира). Ранний `EnsureRemotePlayersSpawned`
  из `OnSceneChanged` убран.
- Проверено в игре: снапшот применяется по одному разу на уровень, HP Рейка переносится.

### 2.5. I-PHASE-6 — ResetCombat на границе уровня

- `Core\RakeNetworkManager.Players.cs` (`ResetDeathState`): добавлен явный вызов
  `MatchState.Current.ResetCombat()` — документированный механизм оживания теперь
  действительно исполняется на смене уровня (идемпотентно к уже очищаемым картам
  здоровья/респавна). Внесено.

### 2.6. I-TICK-1 — переход после тика

- `Core\DayNightManager.cs`: таймер больше не грузит уровень из `Update`. При истечении
  фазы ставится флаг `_phaseTransitionPending` и запускается корутина
  `TransitionAfterTick`, которая ждёт `WaitForEndOfFrame()` (тик, включая репликацию
  `RakeNetworkManager.Update`, уже доигран) и только затем вызывает `TransitionPhase`.
- `TransitionPhase` разделён на обёртку с защитой от повторного входа
  (`_phaseTransitionRunning`, try/finally) и `TransitionPhaseCore`. `SkipPhase()`
  (дев-инструмент) проходит через ту же очередь. Внесено.

### 2.7. I-TICK-2 — урон до смерти — **НЕ ВНЕСЕНО**

`Core\TickDamageQueue.cs` не создан, `ProjectileSystem.cs` не изменён,
`RakeNetworkManager.Update`/`PrepareForSceneLoad` не тронуты. Урон по-прежнему
применяется сразу с немедленной смертью цели; остаток дроби по умершему
отбрасывается. Открытый пункт (MECHANICS_INTERACTIONS §10.4, п.7).

---

## 3. Известные ограничения после правок (не чинилось осознанно)

1. HUD (`MultiplayerMenu`/`RuntimeUIBuilder`, `RakeHudUI`) создаётся глобально и до мира. Перестраивать его жизненный цикл — отдельный риск; сейчас он опрашивает состояние каждый кадр, поэтому устаревших значений не показывает.
2. Инвентарь: переносятся только патроны; список оружия в руках не сериализуется (нужны префабы и согласованные id).
3. `SessionManager.RakeHealth` на хосте по-прежнему не обновляется в реальном времени (обновляется только при смерти) — из-за этого `SessionStatePacket.RakeHealth` у клиентов может быть неактуален, хотя на перенос HP через фазу это больше не влияет.
4. Сборка и тесты выполнены интегратором после правок: `dotnet build -c Release` —
   0 ошибок, `run-tests.ps1` — 368 passed, 0 failed. В живой игре правки не проверялись.

## 4. Самое опасное из найденного (по убыванию риска)

1. HP Рейка на границе фазы: `ApplyHealth(SessionManager.RakeHealth)` с константой 100 — Рейк каждый переход лечится/обнуляется (I-PHASE-1 и I-PHASE-6 одновременно).
2. Переход фазы синхронно внутри `Update` (`LoadLevel` посреди тика, реестры уже сброшены `SceneScope`) — рваный тик и потерянные пакеты (I-TICK-1).
3. Инвентарь не сохранялся на смене фазы (`SaveInventory` недостижим) — игрок каждый рассвет/закат терял патроны (I-PHASE-1).
