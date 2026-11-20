# Аудит: реализация против описания механик (2026-11-20)

## 0. Статус исправлений (2026-11-20, вторая половина дня)

Правки внесены в код, сборка и тесты зелёные (`368 passed, 0 failed`), двухпроцессная
проверка на протоколе 4 пройдена (хост + клиент, скип фазы синхронен, инварианты 0).

**Исправлено полностью — critical/major:**
N1 (SP: камеры/приманка/капканы больше не перехватываются), N2 (`WorldPersistence.Clear`
и на клиенте), N3 (сервер сверяет `DamageType` с оружием в руках, `PlayerState.WeaponId`,
протокол 4), N6 (`Stun` при `_playerControlled` не трогает автомат AI), N7 (`ForceSendState`
при позднем входе), N8 (`RestoreState` не отбрасывает broken-камеру по квоте),
N9/N15 (`Triggered` читается из `Trap_proj.bSetup`), N10 (пустой ящик помечается
consumed), N11 (`_awaitingHandshake` взводится до отправок, мир буферизуется и
фильтруется), N12 (`tp`/`rake hit` только у хоста), N13 (капкан в SP раньше проверки роли).

**Исправлено — minor:** N4/N5 (документы `GAME_MECHANICS.md` §6.1 и `rake-ai-flow.md`
приведены к Flee после удара и числам 9/2.5/7 с), N14 (async-загрузка блокируется
клиенту), N16 (зазор респавна 25 м), N17 (дробины в круге, а не квадрате; трассер тоже),
N18 (ретрай смерти вместо 1 HP), N19 (id 3 — планшет — выпадает), N20 (F12 вместо
второго F9), N23/N24 (слом камеры на клиенте снимается; джойнер не получает свой
`PlayerConnected` дважды).

**Частично:** N10 — остаток частично израсходованного ящика по-прежнему не переносится
через фазу (нужен id, устойчивый к пересозданию сцены); N19 — бинокль (id 7) не имеет
Item-класса и не выпадает — поведение задокументировано, класса нет; N8 — сервер
по-прежнему не может проверить смерть без клиентского HP, поэтому «непроверенная»
смерть теперь просто не даёт лута.

**Открыто (minor/info):** счётчик слотов камер в HUD (N21), мёртвые `PacketType`
(`FireRequest`, `WeaponSwitch`, `ReconnectRequest` и др.), `Invariants.Reset()`,
устаревшие формулировки §10 в `MECHANICS_INTERACTIONS.md`.

## 1. Краткое резюме

- **Всего находок:** 34
- **Подтверждено при перепроверке:** 34 (100%)
- **Critical:** 1
- **Major:** 12
- **Minor:** 18
- **Info:** 3

Распределение по доменам: Session/Phase/Persistence — 5, Player/Damage/Death/Respawn — 5, Rake AI — 5, items-cameras-traps-doors — 5, networking — 5, UI/HUD — 4, «Одиночная игра, инварианты, статика» — 5.

Все 34 находки подтверждены. Критическая одна: в одиночной игре не работают камеры (и приманка) из-за порядка guard'ов в `GamePatches.cs`. Основная масса major-находок связана с авторитетностью на сервере и с рассинхроном состояния предметов; часть major/minor — это устаревшие документы, где код уже ушёл вперёд.

## 2. Таблица подтверждённых находок

Нумерация сквозная: сначала critical, затем major, minor и info.

| # | Severity | Правило | Домен | Суть (1 строка) | Правка |
|---|----------|---------|-------|-----------------|--------|
| N1 | critical | §11 | Одиночная игра, инварианты, статика | В SP `SetupCamera`/`SetUnsetupState` уходят в клиентскую ветку с `return false` — камеры не ставятся и не ломаются | Добавить `if (!isConnected) return true;` в `SetupCamera`/`SetUnsetupState`/`AddFoodPoint` и не форсить `CheckSetup` в SP |
| N2 | major | §1 / I-ARCH-4 | Session/Phase/Persistence | `WorldPersistence.Clear()` зовётся только на хосте — у клиента `_inventories` переживают матч | Вызывать `WorldPersistence.Clear()` и на клиенте при старте нового матча |
| N3 | major | §7 (I-DMG-1) | Player, Damage, Death, Respawn | Клиент сам выбирает `DamageType`, а сервер жёстко наносит 200 dmg — ваншот охотника | Сверять `DamageType` с последним оружием стрелка и отклонять несовпадающий выстрел |
| N4 | major | §6.1 п.4 | Rake: AI, роль, темп атак | Документ обещает Flee при обнаружении, код даёт Chase (Flee — лишь после урона) | Обновить §6.1 п.4 и rake-ai-flow §2: «замечен» = Chase, Flee только после урона/удара; внести в §11 |
| N5 | major | I-RAKE-9 | Rake: AI, роль, темп атак | rake-ai-flow описывает после удара Chase с кулдауном 5 с, код — Flee и 9/2.5/7 с | Переписать rake-ai-flow §2/§3/§7: после удара — Flee, далее 9 с / 2.5 с / 7 с |
| N6 | major | I-ROLE-4 | Rake: AI, роль, темп атак | `Stun()` переводит в `Stunned` и при `_playerControlled`, `stateTimer` стоит — после возврата тела лишний полный стан | При `_playerControlled` не трогать state machine в `Stun()`: стан отдавать только `RakePlayerController` и обнулять `stateTimer` при возврате |
| N7 | major | I-ITEM-2 | items-cameras-traps-doors | При входе камера получает `CameraSpawn`, сбрасывающий `battery=100`/`IsBroken=false`, а дельта не досылает состояние | На входе слать `CameraSync` с `IsBroken`/`Battery`, а не `CameraSpawn` |
| N8 | major | §5 | items-cameras-traps-doors | `RestoreState` отбрасывает broken-камеру при 5 usable, а `ApplySetupVisuals` снова кладёт её в `CameraManager` | В `RestoreState` восстанавливать broken без проверки квоты и без `ApplySetupVisuals` |
| N9 | major | I-RAKE-3 | items-cameras-traps-doors | `WorldPersistence` пишет `Triggered=false`, не читая `Trap_proj.bSetup` — сработавший капкан снова взведён | Читать `Trap_proj.bSetup` при сохранении: `Triggered = !bSetup` |
| N10 | major | I-ITEM-3 | items-cameras-traps-doors | Пустой ящик убирается из `_networkedItems` без `_consumedItemIds.Add` — после смены фазы возвращается полным | Персистить `count` ящика и добавлять id в `_consumedItemIds` при `count == 0` |
| N11 | major | I-NET-6 | networking | `_awaitingHandshake` взводится после `PlayerConnected`/дверей/`ItemDestroyed` — непроверенный старый билд получает состояние мира | Ставить `_awaitingHandshake` до любых `Send` и гейтить `PlayerConnected`/`RakeOwner`/двери по handshake |
| N12 | major | I-UI-4 | UI/HUD | `DebugConsole` двигает позицию локально без `isServer` и минует `ServerTeleportPlayer`; rake hit — самоповреждение | Отправлять tp/rake hit запросом хосту и применять только в `RakeNetworkManager` (как rake stun) |
| N13 | major | §11 | Одиночная игра, инварианты, статика | Guard `isServer` стоит перед `isConnected`: в SP `Trap_proj` не срабатывает и не наносит урон | Поставить `if (!isConnected) return true;` первым, до проверки `isServer` |
| N14 | minor | §1 (клиент не меняет уровень) / I-PHASE-12 | Session/Phase/Persistence | Async-патч `LoadLevelAsync` не блокирует клиента и не рассылает уровень — загрузка мимо синхронизации | Добавить в async-патч блокировку клиента и рассылку уровня, как в `LoadLevel` |
| N15 | minor | I-PHASE-1 | Session/Phase/Persistence | `Triggered = false` для `trapObjects` хардкодится — капкан после рассвета снова взведён | Читать признак срабатывания из `Trap_proj.bSetup` вместо хардкода `false` |
| N16 | minor | I-DEATH-4 (§8 п.4, п.6) | Player, Damage, Death, Respawn | После NavMesh порог защиты от Рейка 25 м подменён 15 м | Сравнивать с полным `MinRespawnDistanceFromRake` (25 м), а не с `0.6×` порога |
| N17 | minor | §7 | Player, Damage, Death, Respawn | Дробины сэмплируются в квадрате, а не в круге 4.5° | Сэмплировать точку в круге (`Random.insideUnitCircle * radius`), а не в квадрате |
| N18 | minor | §8 п.1 (I-DEATH-1) | Player, Damage, Death, Respawn | При смертельном уроне ставится 1 HP вместо повтора смерти | Если `BeginLocalDeath` не сработал, повторить смерть, а не возвращать 1 HP |
| N19 | minor | I-DEATH-2 (§8 п.3) | Player, Damage, Death, Respawn | id 3 (Tablet) и 7 (Binocular) не выпадают из трупа | Добавить `TabletComputer_item` и `Binocular_item` (id 3 и 7) в `ItemTypeForWeapon` |
| N20 | minor | I-RAKE-10 | Rake: AI, роль, темп атак | `RegisterHit` гейтится по локальному `IsActive`, а не по владельцу роли | Гейтить `RegisterHit` по владельцу роли: у человека писать только `_lastHitTimes`, без Flee и `_disengageUntil` |
| N21 | minor | I-RAKE-4 | Rake: AI, роль, темп атак | §15 обещает капкану «+ урон», в коде только `Stun` | Убрать «+ урон» из строки §15: стан 3 с без урона |
| N22 | minor | I-RAKE-2 | items-cameras-traps-doors | `ApplyCameraSync` применяет `IsBroken` без `ApplyUnsetupVisuals` — камера остаётся в `CameraManager` | В `ApplyCameraSync` при `IsBroken` вызывать `ApplyUnsetupVisuals`, как в `BreakCamera` |
| N23 | minor | I-ITEM-2 | networking | Снимок позднего входа не содержит живых HP, а документ помечает I-ITEM-2 «сделано 20.11» | Добавить `PlayerHealthMap` в снимок позднего входа и исправить статус I-ITEM-2 в §10.4 |
| N24 | minor | I-NET-4 (§9) | networking | `ExitToMainMenu` при обрыве хоста только в `Active`; в Waiting/GameOver клиент остаётся в мёртвом лобби | Возвращать в меню при обрыве хоста в любом состоянии |
| N25 | minor | I-NET-2 | networking | Повторный `PlayerConnected` (`IsYou=false`) завышает `GetKnownPlayerCount` на 1 | Заменить повторный пакет на отдельный пакет имени либо не анонсировать дважды |
| N26 | minor | I-UI-2 | UI/HUD | Бар HP Рейка опрашивает `CurrentHealth`, `OnSessionStateChanged` не подписан никем | Подписать бар на `OnSessionStateChanged` и брать `session.RakeHealth` |
| N27 | minor | I-UI-1 | UI/HUD | В HUD нет счётчика слотов камер «4/5» | Вывести в HUD счётчик из `GetTotalUsableCameraCount()`/`MaxCameras` |
| N28 | minor | §10 | UI/HUD | На одном F9 два обработчика дампов: оружие и иерархия | Развести на разные клавиши (F9 — оружие, F12 — иерархия) либо слить дампы |
| N29 | minor | I-ARCH-7 | Одиночная игра, инварианты, статика | Invariants проверяет только «сцена ≥ 1», нет «фаза == сцена» и id игрока из пакета | Добавить проверки фазы против сцены и id из пакета и вызывать из переходов |
| N30 | minor | I-ARCH-4 | Одиночная игра, инварианты, статика | `Invariants.Counts` статичен, `Reset()` нигде не зовётся — нарушения копятся между матчами | Вызывать `Invariants.Reset()` при старте и завершении матча в `SessionManager` |
| N31 | minor | I-RAKE-3 | Одиночная игра, инварианты, статика | Документ числит I-RAKE-3 в «осталось», хотя `bSetup` уже сбрасывается в `GamePatches` | Обновить §10.4 п.6 и строку 598: I-RAKE-3 выполнен (bSetup сбрасывается после срабатывания) |
| N32 | info | I-PHASE-10 | Session/Phase/Persistence | `_phaseTimer` считается по старому `_nightNumber` до его обновления; клиент таймер не читает | Обновлять `_nightNumber` из пакета до расчёта `_phaseTimer` |
| N33 | info | §10.3 | Session/Phase/Persistence | Документ называет `SavedRakeHealth` мёртвым кодом, а он читается в `ApplySavedRakeHealth` | Убрать из §10.3 утверждение о мёртвом коде `SavedRakeHealth` |
| N34 | info | I-NET-3 | networking | `ReconnectRequest`/`ReconnectConfirm` без `case` в `Deserialize` — мёртвые значения enum | Реализовать реконнект либо удалить `ReconnectRequest`/`ReconnectConfirm` из `PacketType` |

## 3. Critical и Major

### N1. §11 — камеры и приманка не работают в одиночной игре (critical)

- **Правило:** §11
- **Домен:** Одиночная игра, инварианты, статика
- **Что не так (evidence):** `GamePatches.cs:515,538-540`: при `netCam != null` и `isServer=false` камера уходит в клиентскую ветку с `return false`; `AddFoodPoint` (624-637) так же блокирует приманку в SP.
- **Почему это важно:** в одиночной игре `isServer` всегда `false`, а проверка `isServer` стоит раньше `isConnected`. Поэтому установка и снятие камер в SP попадают в клиентскую ветку и не выполняются — ключевой ночной инструмент недоступен, та же ошибка порядка guard'ов блокирует приманку. Для поддерживаемого одиночного режима это ломает основной цикл, поэтому severity critical.
- **Предлагаемая правка:** добавить `if (!isConnected) return true;` в `SetupCamera`/`SetUnsetupState`/`AddFoodPoint` и не форсить `CheckSetup` в SP.
- **Note проверяющего:** подтверждено: в SP `SetupCamera`/`SetUnsetupState` уходят в клиентскую ветку с `return false` (`GamePatches.cs:538-540, 577-579`) — камеры не ставятся и не ломаются; `AddFoodPoint` (624-637) в билде не вызывается.

### N2. §1 / I-ARCH-4 — состояние клиента переживает матч (major)

- **Правило:** §1 / I-ARCH-4
- **Домен:** Session/Phase/Persistence
- **Что не так (evidence):** `Core/SessionManager.cs:101` — `WorldPersistence.Clear()` вызывается только из `StartSession`, а `StartSession` — только на хосте (`UI/MultiplayerMenu.cs:778`). У клиента `_inventories` переживают матч.
- **Почему это важно:** новый матч на клиенте начинается с инвентарём и прочими сохранёнными статиками предыдущего матча. Это нарушает изоляцию матчей: возможны «призрачные» предметы, расхождение с хостом и обход правил персистентности.
- **Предлагаемая правка:** вызывать `WorldPersistence.Clear()` и на клиенте при старте нового матча.
- **Note проверяющего:** `Clear()` есть только в `SessionManager.cs:101` (`StartSession`, лишь хост); клиентский `HandleClientStartGame` (`RakeNetworkManager.Session.cs:239`) его не зовёт — статики `WorldPersistence.cs:35` переживают матч.

### N3. §7 (I-DMG-1) — клиент выбирает профиль урона (major)

- **Правило:** §7 (I-DMG-1)
- **Домен:** Player, Damage, Death, Respawn
- **Что не так (evidence):** `RakeNetworkManager.Players.cs:139` `damageType = ... packet.DamageType`; `:151` `amount = 200; pellets = 1;` — профиль оружия и урон выбирает клиент.
- **Почему это важно:** сервер доверяет клиентскому `DamageType` и жёстко наносит 200 единиц по типу из пакета. Любой клиент может ваншотить охотника и обходить баланс оружия — это прямая дыра в авторитетности урона.
- **Предлагаемая правка:** сверять `DamageType` с последним оружием стрелка и отклонять несовпадающий выстрел.
- **Note проверяющего:** `DamageType` из пакета (`RakeNetworkManager.Players.cs:139`) выбирает профиль (`:151`, 200 dmg) без сверки с оружием стрелка; `Amount`/`Pellets` игнорируются, но 200 dmg ваншотит охотника.

### N4. §6.1 п.4 — «замечен» трактуется как Chase, а не Flee (major)

- **Правило:** §6.1 п.4
- **Домен:** Rake: AI, роль, темп атак
- **Что не так (evidence):** `NetworkedRakeAI.cs:1537-1538` — `LogInfo("[AI] Spotted while watching - charging")`; `TransitionToState(RakeState.Chase)`; то же в `UpdateStealth` (`1717-1720`).
- **Почему это важно:** поведение ИИ разошлось с описанием: по документу Рейк должен убегать, увидев игрока, а по коду переходит в Chase. Тесты, баланс и ожидания игроков опираются на неверную механику; судя по тому, что Flee срабатывает после урона, текущее поведение осознанное — править нужно документ.
- **Предлагаемая правка:** обновить §6.1 п.4 и rake-ai-flow §2: «замечен» = Chase, Flee только после урона/удара; внести в §11.
- **Note проверяющего:** §6.1 п.4 (`GAME_MECHANICS.md:120`) обещает Flee по взгляду, но `NetworkedRakeAI.cs:1535-1539` и `1717-1721` дают Chase; Flee — только после урона/удара (`3369-3381`, `620-623`); `rake-ai-flow.md:52` тоже устарел.

### N5. I-RAKE-9 — после удара в документе Chase и 5 с, в коде Flee и 9/2.5/7 с (major)

- **Правило:** I-RAKE-9
- **Домен:** Rake: AI, роль, темп атак
- **Что не так (evidence):** `docs/rake-ai-flow.md:63` `Attack --> Chase: "удар нанесён — кулдаун 5 с"`; `:85` `F --> G["кулдаун 5 с, снова Chase"]`; `:146` «Один удар, затем `Chase`».
- **Почему это важно:** документ описывает несуществующий переход и тайминги. Настройка темпа атак по rake-ai-flow даёт неверные ожидания и тесты; расхождение затрагивает §2/§3/§7.
- **Предлагаемая правка:** переписать rake-ai-flow §2/§3/§7: после удара — Flee, далее 9 с / 2.5 с / 7 с, а не Chase с 5 с.
- **Note проверяющего:** `NetworkedRakeAI.cs:611-624,1492,1953`: после удара — сразу Flee и 9/2.5/7 с, а `rake-ai-flow.md:63,85,146` всё ещё «Chase + 5 с».

### N6. I-ROLE-4 — стан игрока-Рейка задевает ИИ-state (major)

- **Правило:** I-ROLE-4
- **Домен:** Rake: AI, роль, темп атак
- **Что не так (evidence):** `NetworkedRakeAI.cs:2830-2837` — `Stun()`: `TransitionToState(RakeState.Stunned)` выполняется и при `_playerControlled`; `stateTimer` стоит (`UpdateStateMachine` пропущен, `:385`).
- **Почему это важно:** при управлении Рейком игроком `Stun()` всё равно переводит состояние ИИ в `Stunned`, а `UpdateStateMachine` в это время не тикает. После возврата тела ИИ получает полный стан «в долг» — игрок теряет контроль на необоснованное время.
- **Предлагаемая правка:** при `_playerControlled` не трогать state machine в `Stun()`: стан отдавать только `RakePlayerController` и обнулять `stateTimer` при возврате.
- **Note проверяющего:** `Stun()` переводит в Stunned и при `_playerControlled`, а `UpdateStateMachine` пропущен (`:385`): `stateTimer` замирает, и после возврата тела ИИ получает лишний полный стан (`NetworkedRakeAI.cs:2830`).

### N7. I-ITEM-2 — `CameraSpawn` при входе сбрасывает состояние камеры (major)

- **Правило:** I-ITEM-2
- **Домен:** items-cameras-traps-doors
- **Что не так (evidence):** `NetworkedCameraSystem.cs:170` `_isPlaced) SendCameraSpawn();`; `:684-686` `_batteryLevel=100f; _isBroken=false`; `Session.cs:366` зовёт `BroadcastSpawn`, `CameraSync` неизменное не шлёт (`:760`).
- **Почему это важно:** при позднем входе/спавне клиент получает `CameraSpawn`, который обнуляет `battery` и снимает `IsBroken`, а дельта-синхронизация не пересылает неизменившееся состояние. Клиент видит сломанную камеру рабочей и может ей пользоваться — рассинхрон состояния предметов.
- **Предлагаемая правка:** на входе слать `CameraSync` с `IsBroken`/`Battery`, а не `CameraSpawn`, который сбрасывает состояние.
- **Note проверяющего:** `Session.cs:366` → `NetworkedCameraSystem.cs:170` шлёт `CameraSpawn`, `HandleCameraSpawn` (`:684-686`) ставит `battery=100`/`IsBroken=false`, дельта `:760` не досылает — сломанная камера у клиента целая.

### N8. §5 — восстановление broken-камеры ломает квоту (major)

- **Правило:** §5
- **Домен:** items-cameras-traps-doors
- **Что не так (evidence):** `NetworkedCameraSystem.cs:596` `if (!_isPlaced && !CanPlaceCamera()) return;` — broken-камера не восстановится при 5 usable; `:609` `ApplySetupVisuals();` — иначе попадёт в `CameraManager`.
- **Почему это важно:** порядок восстановления отбрасывает сломанную камеру, если usable-камер уже 5, а `ApplySetupVisuals` затем снова добавляет её в `CameraManager`. Сломанная камера возвращается в строй, ломая квоту и баланс камер.
- **Предлагаемая правка:** в `RestoreState` восстанавливать broken без проверки квоты и без `ApplySetupVisuals`.
- **Note проверяющего:** `NetworkedCameraSystem.cs:596` при 5 usable порядок restore отбрасывает broken-камеру, а `:609` `ApplySetupVisuals` снова кладёт её в `CameraManager` (`AddCamera`); фикс верен, клиент — `:695`.

### N9. I-RAKE-3 — сработавший капкан восстанавливается взведённым (major)

- **Правило:** I-RAKE-3
- **Домен:** items-cameras-traps-doors
- **Что не так (evidence):** `Core/WorldPersistence.cs:141-146` `_trapStates.Add(new TrapState { ... Triggered = false });` для `levelItems.trapObjects`, `bSetup` не читается — сработавший капкан снова взведён.
- **Почему это важно:** сохранение жёстко пишет `Triggered=false`, игнорируя фактическое `Trap_proj.bSetup`. После рассвета сработавший капкан снова взведён — бесконечное повторное использование и обход одноразовости, заявленной в I-RAKE-3.
- **Предлагаемая правка:** читать `Trap_proj.bSetup` при сохранении: `Triggered = !bSetup`.
- **Note проверяющего:** `WorldPersistence.cs:141-146` пишет `Triggered=false` без чтения `bSetup`, поэтому сработавший капкан (`bSetup=false`, `GamePatches.cs:782`) восстанавливается взведённым (`WorldPersistence.cs:177-179`).

### N10. I-ITEM-3 — пустой ящик возвращается полным (major)

- **Правило:** I-ITEM-3
- **Домен:** items-cameras-traps-doors
- **Что не так (evidence):** `Core/RakeNetworkManager.Items.cs:357-368` `if (!active) { _networkedItems.Remove(...)` без `_consumedItemIds.Add` (`:166`) — снимок `CaptureConsumedItems` (`:186`) его не удалит.
- **Почему это важно:** пустой ящик удаляется из `_networkedItems` без записи в `_consumedItemIds`. После смены фазы снимок не знает, что ящик израсходован, и возвращает его полным — лишние камеры и дублирование предметов.
- **Предлагаемая правка:** персистить `count` ящика и добавлять id в `_consumedItemIds` при `count == 0`.
- **Note проверяющего:** подтверждено: `Core/RakeNetworkManager.Items.cs:357-368` убирает пустой ящик из `_networkedItems` без `_consumedItemIds.Add` (`:166`) — после смены фазы он возвращается полным, давая лишние камеры.

### N11. I-NET-6 — handshake взводится после первых Send (major)

- **Правило:** I-NET-6
- **Домен:** networking
- **Что не так (evidence):** `RakeNetworkManager.cs:568` — `_awaitingHandshake` взводится уже после `PlayerConnected` (`:546`), дверей (`:551`) и `ItemDestroyed` (`:555`); комментарий `:55` «sends no world state».
- **Почему это важно:** непроверенный старый билд успевает получить `PlayerConnected`, двери, предметы и бродкасты `StartGame` до отбраковки. Комментарий «sends no world state» неверен — это окно и для несовместимых клиентов, и для чужих сборок.
- **Предлагаемая правка:** ставить `_awaitingHandshake` до любых `Send` и гейтить `PlayerConnected`/`RakeOwner`/двери по handshake.
- **Note проверяющего:** confirmed: `RakeNetworkManager.cs:568` arms after `:546-562` sends, so an unvalidated old build gets `PlayerConnected`/doors/items (and `StartGame` broadcasts) before rejection; comment `:55` false.

### N12. I-UI-4 — DebugConsole двигает позицию в обход сервера (major)

- **Правило:** I-UI-4
- **Домен:** UI/HUD
- **Что не так (evidence):** `DebugConsole.cs:274` `player.position = position;`; `:372` `fps.TakeDamage(damage,"RakeAttack")` — ни проверки `isServer`, ни серверного запроса; консоль есть у любого пира.
- **Почему это важно:** телепорт двигает позицию локально без `isServer` и минует `ServerTeleportPlayer`, а изменения реплицируются клиентским `PlayerStatePacket` — клиентская авторитетность позиции и чит-вектор. `rake hit` при этом лишь самоповреждение и на авторитетность не влияет.
- **Предлагаемая правка:** отправлять tp/rake hit запросом хосту и применять только в `RakeNetworkManager` (как rake stun).
- **Note проверяющего:** `DebugConsole.cs:274` tp двигает позицию локально без `isServer`, минуя `ServerTeleportPlayer`, и реплицируется клиентским `PlayerStatePacket`; `:372` — лишь самоповреждение, реакция идёт через ragdoll-запрос.

### N13. §11 — капканы не срабатывают в одиночной игре (major)

- **Правило:** §11
- **Домен:** Одиночная игра, инварианты, статика
- **Что не так (evidence):** `GamePatches.cs:762-764`: `if (!RakeMultiplayerPlugin.isServer) return false;` стоит раньше `if (!RakeMultiplayerPlugin.isConnected) return true; // single player: vanilla`; в SP `isServer=false` — `Trap_proj` не срабатывает.
- **Почему это важно:** guard `isServer` стоит перед проверкой `isConnected`, поэтому в одиночной игре префикс возвращает `false` и ванильный `Trap_proj` молчит. Капканы в SP фактически отключены — ломается базовая механика урона 35/100.
- **Предлагаемая правка:** поставить `if (!isConnected) return true;` первым, до проверки `isServer`.
- **Note проверяющего:** `GamePatches.cs:762`: guard `isServer` стоит перед `isConnected` (`:764`), в SP `isServer=false` → prefix возвращает `false`, ванильный `Trap_proj` молчит и не наносит урон 35/100.

## 4. Minor и Info

- **N14** · minor · §1 / I-PHASE-12 · Session/Phase/Persistence — async-патч `LoadLevelAsync` (`GameEntryPatch.cs:64-70`) не зовёт `BlockClientLevelLoad`/`NotifyLocalLevelLoad`; загрузка идёт мимо синхронизации. Правка: добавить блокировку клиента и рассылку уровня, как в `LoadLevel`. (Сейчас `LoadLevelAsync` в игре не вызывается — запас прочности.)
- **N15** · minor · I-PHASE-1 · Session/Phase/Persistence — `WorldPersistence.cs:145` хардкодит `Triggered=false` для `trapObjects` уровня; сработавший капкан снова взведён после рассвета. Правка: читать `Trap_proj.bSetup`.
- **N16** · minor · I-DEATH-4 · Player, Damage, Death, Respawn — `PlayerDeathSystem.cs:323` после NavMesh оставляет защиту 15 м вместо 25 м. Правка: сравнивать с полным `MinRespawnDistanceFromRake`.
- **N17** · minor · §7 · Player, Damage, Death, Respawn — `ProjectileSystem.cs:52-53` сэмплирует дробины в квадрате (до ~6.4°), а не в круге 4.5°; тот же баг в `BulletVisuals.cs:44-45`. Правка: `Random.insideUnitCircle * radius`.
- **N18** · minor · §8 п.1 / I-DEATH-1 · Player, Damage, Death, Respawn — `PlayerDeathPatch.cs:26-31` при смертельном уроне ставит 1 HP вместо повторного срабатывания смерти. Правка: повторить смерть, а не возвращать HP.
- **N19** · minor · I-DEATH-2 · Player, Damage, Death, Respawn — `PlayerDeathSystem.cs:411-421` не покрывает id 3/7 (Tablet/Binocular), то же в `Players.cs:787`. Правка: добавить `TabletComputer_item` (id 3) и `Binocular_item` (id 7).
- **N20** · minor · I-RAKE-10 · Rake: AI, роль, темп атак — `NetworkedRakeAI.cs:593` гейтит `RegisterHit` по локальному `IsActive`, а не по роли; на сервере при удалённом владельце ставится Flee и `_disengageUntil`. Правка: гейтить по владельцу роли.
- **N21** · minor · I-RAKE-4 · Rake: AI, роль, темп атак — `GAME_MECHANICS.md:352` обещает капкану «+ урон», в `NetworkedTrap.cs:95` только `rake.Stun(StunDuration)`. Правка: убрать «+ урон» из §15.
- **N22** · minor · I-RAKE-2 · items-cameras-traps-doors — `NetworkedCameraSystem.cs:657` применяет `IsBroken` без `ApplyUnsetupVisuals` (он есть лишь в `BreakCamera:515`/`HandleCameraDestroy:711`); камера остаётся в `CameraManager`. Правка: вызывать `ApplyUnsetupVisuals` при `IsBroken`.
- **N23** · minor · I-ITEM-2 · networking — `Session.cs:434-457` в снимке позднего входа только `PlayerDied` с `Health=0`, живых HP нет; `MECHANICS_INTERACTIONS.md:577` «сделано 20.11». Правка: добавить `PlayerHealthMap` и исправить статус.
- **N24** · minor · I-NET-4 · networking — `RakeNetworkManager.cs:471-475` выходит в меню только при `Active`; в Waiting/GameOver клиент остаётся в мёртвом лобби (спасают лишь LEAVE/RETURN). Правка: выходить при обрыве хоста в любом состоянии.
- **N25** · minor · I-NET-2 · networking — `Session.cs:356` повторно шлёт джойнеру его же `PlayerConnected` (`IsYou=false`); `Players.cs:626` добавляет свой id, `GetKnownPlayerCount` завышает счётчик на 1. Правка: отдельный пакет имени либо без двойного анонса.
- **N26** · minor · I-UI-2 · UI/HUD — `MultiplayerMenu.cs:416` берёт `NetworkedRakeAI.CurrentHealth`, подписчиков у `SessionManager.OnSessionStateChanged` (`:39`) нет. Правка: подписать бар на `OnSessionStateChanged` и брать `session.RakeHealth`.
- **N27** · minor · I-UI-1 · UI/HUD — `RuntimeUIBuilder.cs:1194-1214`/`MultiplayerMenu.cs:454-466` строят лишь Phase/Night/Timer; счётчика слотов камер «4/5» нет. Правка: вывести `GetTotalUsableCameraCount()`/`MaxCameras`.
- **N28** · minor · §10 · UI/HUD — `MultiplayerMenu.cs:260` и `:266` — два обработчика на одном `KeyCode.F9`; комментарий `:258` признаёт лишь прежнюю коллизию с F8. Правка: развести на разные клавиши или слить дампы.
- **N29** · minor · I-ARCH-7 · Одиночная игра, инварианты, статика — `Invariants.cs:62-65` проверяет только `level >= 1`; проверок «фаза == сцена» и «id игрока из пакета» нет (`LevelMatchesPhase` лишь гейтит `DayNightManager.cs:620`). Правка: добавить проверки и вызывать из переходов.
- **N30** · minor · I-ARCH-4 · Одиночная игра, инварианты, статика — `Invariants.cs:23` `Counts` статичен, `Reset()` (`:131`) нигде не зовётся; `StartSession`/`EndSession` (`SessionManager.cs:107,143`) его не зовут. Правка: вызывать `Invariants.Reset()` при старте и завершении матча.
- **N31** · minor · I-RAKE-3 · Одиночная игра, инварианты, статика — `MECHANICS_INTERACTIONS.md:575` «план» и `:598` «осталось», хотя `GamePatches.cs:768,782` уже реализуют одноразовость; в §10 (стр. 505) уже «OK». Правка: обновить §10.4 п.6 и строку 598 на «выполнено».
- **N32** · info · I-PHASE-10 · Session/Phase/Persistence — `DayNightManager.cs:602-603`: `_phaseTimer` считается по старому `_nightNumber` до обновления (Day1→Night2 даёт −1500 с), но клиент `PhaseTimer` не читает и пересчитывает следующим пакетом ≤1 с. Правка: обновлять `_nightNumber` до расчёта.
- **N33** · info · §10.3 · Session/Phase/Persistence — `MECHANICS_INTERACTIONS.md:554` называет `SavedRakeHealth` мёртвым кодом, но `WorldPersistence.cs:88` читает его в `ApplySavedRakeHealth`. Правка: убрать утверждение из §10.3.
- **N34** · info · I-NET-3 · networking — `PacketType.cs:31-32` (`ReconnectRequest=22`, `ReconnectConfirm=23`) не имеют `case` в `PacketSerializer.cs:30-76`; значения мёртвые и недостижимые. Правка: реализовать реконнект либо удалить типы пакетов.

## 5. Ошибки документации

Здесь собраны находки, где устарел сам документ, а не код. Править нужно описание, а не реализацию.

- **N4 — `GAME_MECHANICS.md:120` (§6.1 п.4) и `rake-ai-flow.md:52`.** Обещают Flee при обнаружении игрока; код (`NetworkedRakeAI.cs:1535-1539, 1717-1721`) даёт Chase, Flee — только после урона/удара (`:3369-3381, :620-623`). Обновить формулировку «замечен = Chase» и внести в §11.
- **N5 — `rake-ai-flow.md:63, :85, :146`.** «Chase + кулдаун 5 с» после удара; код — Flee и 9/2.5/7 с (`NetworkedRakeAI.cs:611-624, 1492, 1953`). Переписать §2/§3/§7.
- **N21 — `GAME_MECHANICS.md:352` (§15).** У капканов указан «+ урон»; `NetworkedTrap.cs:95` делает только `rake.Stun(StunDuration)`. Убрать «+ урон», оставить стан 3 с без урона (согласуется с I-RAKE-4 и §4, стр. 68).
- **N23 — `MECHANICS_INTERACTIONS.md:577`.** Статус I-ITEM-2 «сделано 20.11» неточен: снимок позднего входа (`Session.cs:434-457`) содержит только `PlayerDied(Health=0)`, живых HP нет. Это и правка кода (`PlayerHealthMap`), и правка статуса: §512 уже признаёт проблему, но строка 577 её скрывает.
- **N31 — `MECHANICS_INTERACTIONS.md:575` и `:598`.** I-RAKE-3 числится «план»/«осталось», хотя `GamePatches.cs:768,782` уже сбрасывают `bSetup`; в §10 (стр. 505) уже «OK». Привести строки к «выполнено».
- **N33 — `MECHANICS_INTERACTIONS.md:554` (§10.3).** `WorldPersistence.SavedRakeHealth` назван мёртвым кодом, но читается в `WorldPersistence.cs:88` (`ApplySavedRakeHealth`, §10.5). Удалить утверждение о мёртвом коде.

## 6. Что делать дальше

**1. Блокер — закрыть первым**
- **N1:** guard `!isConnected` в `SetupCamera`/`SetUnsetupState`/`AddFoodPoint` и убрать форс `CheckSetup` в SP. Без этого одиночный режим с камерами неиграбелен.

**2. Безопасность и авторитетность (высокий риск)**
- **N3:** серверная сверка `DamageType` с последним оружием стрелка.
- **N11:** `_awaitingHandshake` до любых `Send`; гейт `PlayerConnected`/двери/`RakeOwner` по handshake.
- **N12:** tp и rake hit перевести на серверные запросы, применять только в `RakeNetworkManager`.

**3. Целостность состояния мира (высокий/средний риск)**
- **N2:** `WorldPersistence.Clear()` и на клиенте при старте матча.
- **N6:** не трогать state machine при `_playerControlled`, обнулять `stateTimer`.
- **N7/N8/N22:** `CameraSync` вместо `CameraSpawn` при входе; restore broken без квоты и без `ApplySetupVisuals`; `ApplyUnsetupVisuals` при `IsBroken`.
- **N9/N15/N31:** читать `Trap_proj.bSetup` в сохранении и восстановлении; документ привести в соответствие.
- **N10:** персистить `count` ящика и писать `_consumedItemIds`.
- **N13:** guard `!isConnected` для `Trap_proj` в SP.
- **N23:** `PlayerHealthMap` в снимке позднего входа.

**4. Поведение, баланс и наблюдаемость (средний риск)**
- **N14, N16, N17, N18, N19, N20** — исправления по игроку/смерти/оружию/роли Рейка.
- **N24, N25** — сетевое поведение при обрыве и учёте игроков.
- **N26, N27, N28** — HUD и диагностика: бар HP, счётчик камер, развести F9.
- **N29, N30** — инварианты: добавить проверки и вызывать `Reset()`.
- **N32** — порядок `_nightNumber`/`_phaseTimer` (дёшево, попутно с N2/N14).

**5. Документация и мёртвый код (низкий риск, но обязательно вместе с кодом)**
- **N4, N5, N21, N23, N31, N33** — внести правки из раздела 5, чтобы документы не расходились с кодом после исправлений.
- **N34** — удалить недостижимые `ReconnectRequest`/`ReconnectConfirm` либо реализовать реконнект.

Порядок: сначала N1, затем авторитетность (N3/N11/N12), затем состояние мира (N2/N6–N10/N13/N23), после — балансные и UI-правки; документацию править в том же изменении, что и соответствующий код, иначе расхождение вернётся при следующем аудите.
