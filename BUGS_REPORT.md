# 🐛 BUGS REPORT — RakeMultiplayer
*Сгенерировано на основе аудита Working Dir + ZIP "Fixed" версии*

---

## 🔴 КРИТИЧЕСКИЕ (блокируют геймплей / ломают архитектуру)

### 1. [Networking] DamageRequestPacket без `ShooterPosition`
- **Файл:** `Packets.cs` (ZIP: 131-155, Working Dir: ✅ исправлено)
- **Суть:** Нет поля позиции стрелка → сервер не может заставить Рейка среагировать на выстрел (`Event_PlayerFire`).
- **Последствие:** Рейк не уходит в Danger state при стрельбе.
- **Фикс в Working Dir:** Добавлено поле `Vector3 ShooterPosition` + Write/Read.

---

### 2. [Items] ItemPickupPatch возвращает `true` на клиенте
- **Файл:** `GamePatches.cs` (ZIP: 193, Working Dir: ✅ исправлено)
- **Суть:** Клиент выполняет локальный `PickupItem` + шлёт запрос. Сервер тоже даёт предмет → дублирование / рассинхрон инвентаря.
- **Фикс в Working Dir:** На клиенте `return false` (блокируем локальный пикап), только `ClientSend`.

---

### 3. [Combat] Нет патчей синхронизации выстрелов
- **Файл:** `GamePatches.cs` (ZIP: отсутствуют, Working Dir: ✅ исправлено)
- **Суть:** Нет `RifleWeaponFireSyncPatch` / `SniperRifleWeaponFireSyncPatch`. Выстрелы не отправляются на сервер.
- **Фикс в Working Dir:** Добавлены Postfix-патчи на `Update`, проверяют `Input.GetButtonDown("Fire1")`, читают `caster` через рефлексию, шлют `DamageRequestPacket` с `ShooterPosition`.

---

### 4. [Rake AI] NetworkedRakeAI читает несуществующее поле `currentState`
- **Файл:** `NetworkedRakeAI.cs` (ZIP: 123, 162, Working Dir: то же)
- **Суть:** В `Enemy_AI` нет поля `currentState` (это enum внутри класса или другой name). Рефлексия возвращает null → состояние всегда Idle.
- **Нужно:** Найти настоящее поле состояния в декомпилированном `Enemy_AI.cs` (возможно `state`, `currentStateEnum`, `aiState` и т.д.).

---

### 5. [Rake AI] В `ApplySyncData` нет интерполяции — прямой телепорт
- **Файл:** `NetworkedRakeAI.cs` (ZIP: 151-152, Working Dir: то же)
- **Суть:** `transform.position = position` каждый пакет (15Hz). На клиенте Рейк дергается/телепортится.
- **Нужно:** Добавить буфер интерполяции (как в `NetworkedPlayer`: `targetPosition` + `Lerp` в `Update`).

---

### 6. [Items] DestroyPatch патчит `UnityEngine.Object.Destroy` глобально
- **Файл:** `GamePatches.cs` (ZIP: 200-220, Working Dir: то же)
- **Суть:** Harmony Prefix на `Object.Destroy(Object)` ловит **все** уничтожения в игре. Риск: сломать Unity, пропустить перегрузку `Destroy(Object, float)`.
- **Последствие:** Краши, утечки памяти, неожиданное поведение.
- **Нужно:** Патчить только `Item.PickupItem` / `Item.DestroyItem` (есть ли такой метод) или использовать флаг `IsApplyingNetworkDestroy` без глобального патча.

---

### 7. [Combat] Нет пакетов синхронизации аммо / перезарядки / смены оружия
- **Файлы:** `Packets.cs`, `PacketType.cs`, `RakeNetworkManager.cs`, `GamePatches.cs`
- **Суть:** Нет `WeaponAmmoPacket`, `WeaponReloadPacket`, `WeaponSwitchPacket`. На клиентах аммо рассинхронизируется мгновенно.
- **Нужно:** Добавить типы пакетов + сериализацию + обработку + патчи на `WeaponManager.AddAmmo` / `Rifle_weapon.Reload` / смену оружия.

---

### 8. [Rake AI] DetectionTriggerPatch = TODO — серверный Рейк не видит игроков
- **Файл:** `GamePatches.cs` (ZIP: 81-100, Working Dir: то же)
- **Суть:** `OnTriggerEnter` только логирует TODO. Серверный `Enemy_AI` не получает уведомления о детекте игроков.
- **Последствие:** Рейк не начинает преследование, не реагирует на игроков в радиусе.
- **Нужно:** Вызвать `enemyAI.OnPlayerDetected()` или похожий метод (проверить в `Enemy_AI.cs`).

---

### 9. [Networking] PlayerDisconnectedPacket использует неверный PlayerId
- **Файл:** `RakeNetworkManager.cs` (ZIP: 347, Working Dir: то же)
- **Суть:** `var packet = new PlayerDisconnectedPacket { PlayerId = connectionId };` — должен быть `connectionId + 1` (Host=1, Clients=connectionId+1).
- **Последствие:** Клиенты не удаляют отключившегося игрока (ID не совпадает).

---

### 10. [Items] Реестр предметов не очищается на дисконнект / StopHost
- **Файл:** `RakeNetworkManager.cs` (ZIP: `StopHost` 183-184, `Disconnect` 197, Working Dir: то же)
- **Суть:** `_networkedItems` не очищается → утечка памяти, дубликаты ID при рехосте, "призрачные" предметы.
- **Нужно:** `_networkedItems.Clear()` в `StopHost`, `Disconnect`, `OnServerClientDisconnected`.

---

### 11. [Networking] Нет лимитов размера пакета / строки
- **Файл:** `PacketSerializer.cs` (ZIP: 15-41, Working Dir: то же)
- **Суть:** Читает `reader.ReadString()` без лимита → OOM через огромную строку в `DamageType` / `ItemType`.
- **Нужно:** `MAX_PACKET_SIZE = 64KB`, `MAX_STRING_LENGTH = 256`, проверка в `Deserialize`.

---

### 12. [Networking] Нет rate limiting на сервере
- **Файл:** `RakeNetworkManager.cs` (ZIP: `ProcessServerMessages`, Working Dir: то же)
- **Суть:** Клиент может спамить пакетами любой частотой → DoS, перегруз CPU.
- **Нужно:** Бюджет сообщений в секунду на коннекшн (например, 100 msg/s), дроп/бан при превышении.

---

### 13. [Networking] Обратная совместимость протокола — нет версионирования
- **Файл:** `PacketSerializer.cs`, `PacketType.cs`
- **Суть:** Любое изменение пакета ломает совместимость версий мода.
- **Нужно:** Добавить `ProtocolVersion` в handshake / первый пакет.

---

## 🟠 ВАЖНЫЕ (ломят фичи / дают плохой UX)

### 14. [Player Sync] Health/Stamina/Flashlight — нет полей в PlayerStatePacket
- **Файл:** `Packets.cs` (ZIP: `PlayerStatePacket` 11-40, Working Dir: то же)
- **Суть:** В `NetworkedPlayer` есть `ApplyHealth`/`ApplyStamina`/`ApplyFlashlight` (в ZIP они реализованы, в Working Dir — нет), но в пакете полей нет → не синхронизируются.
- **Нужно:** Добавить `Health`, `Stamina`, `Flashlight` в `PlayerStatePacket` + Write/Read + отправку в `SendPlayerState`.

---

### 15. [Player Sync] Жёстко зашитый lerp factor (15f) без адаптации к пингу
- **Файл:** `NetworkedPlayer.cs` (ZIP: 85, 89, Working Dir: то же)
- **Суть:** `Time.deltaTime * 15f` — при пинге >100ms даёт джиттер / overshoot.
- **Нужно:** Интерполяция с буфером (2-3 пакета), адаптивный `lerpFactor` на основе RTT.

---

### 16. [Rake AI] RakeStatePacket.State — 1 байт не вмещает все флаги Enemy_AI
- **Файл:** `Packets.cs` (ZIP: 46-75), `NetworkedRakeAI.cs` (enum `RakeState`)
- **Суть:** В `Enemy_AI` есть флаги: `bDanger`, `bPlayerHount`, `bHaunt`, `bHungry`, `bStunned`, `bDead`, `bPatrol`, `bSearch`... Enum 0-6 не покрывает комбинации.
- **Нужно:** Bitmask (byte/ushort) вместо enum, или расширенный enum с комбо-состояниями.

---

### 17. [Rake AI] Bullet damage type не триггерит Danger state
- **Файл:** `RakeNetworkManager.cs` (ZIP: `HandleServerDamageRequest` 427-448, Working Dir: то же)
- **Суть:** В `Enemy_AI` проверка `damageType == "SniperShot" || damageType == "TrapDamage"` для входа в Danger. Обычные пули (`"Bullet"`) игнорируются.
- **Нужно:** Либо на сервере менять `DamageType` на `"SniperShot"` для любого выстрела, либо патчить `Enemy_AI.TakeDamage` чтобы `"Bullet"` тоже давал Danger.

---

### 18. [Combat] Фейковый shooter для Event_PlayerFire — хрупкий хак
- **Файл:** `RakeNetworkManager.cs` (Working Dir: `HandleServerDamageRequest` ~419-440)
- **Суть:** Создаём `new GameObject("FakeShooter")`, вешаем `MonoBehaviour`, вызываем рефлексию. GC alloc на каждый выстрел, может утекать при исключении.
- **Нужно:** Либо добавить в `Enemy_AI` публичный метод `ForceDangerState(Vector3 position)`, либо использовать существующий `NotificationsManager` с нормальным компонентом.

---

### 19. [Items] Недетерминистические Item ID (в ZIP есть djb2, но всё равно по позиции)
- **Файл:** `RakeNetworkManager.cs` (ZIP: 834-851), `GameEntryPatch.cs` (ZIP: 86-91)
- **Суть:** ID = хеш от `Round(pos) + name`. Если предметы одинаковые и рядом — коллизия. При физике/движении позиции могут расходиться хост/клиент.
- **Нужно:** Регистрация при старте с последовательными ID (1,2,3...) по уникальному `instanceID` или пути в сцене.

---

### 20. [Items] Сервер доверяет `packet.PlayerId` из ItemPickupPacket
- **Файл:** `RakeNetworkManager.cs` (ZIP: 509-512, Working Dir: то же)
- **Суть:** Клиент может подставить чужой `PlayerId` → предмет выдастся другому игроку.
- **Нужно:** Использовать авторитетный `connectionId + 1` как `PickerId`.

---

### 21. [Items] WeaponManager отключён на удалённых игроках — AddWeapon может не сработать
- **Файл:** `NetworkedPlayer.cs` (ZIP: 130-131), `RakeNetworkManager.cs` (ZIP: `HandleServerItemPickup` пытается дать оружие)
- **Суть:** На сервере хост выдаёт предмет локальному игроку (OK), но для клиентов — ищет `WeaponManager` на удалённом префабе, где он `enabled = false`. Методы могут не работать.
- **Нужно:** Включить `WeaponManager` на сервере для выдачи, или вызывать внутренние методы напрямую.

---

### 22. [Networking] StopHost / Disconnect не шлют грациозные дисконнекты
- **Файл:** `RakeNetworkManager.cs` (ZIP: 176-199)
- **Суть:** Просто закрывают сокет. Клиенты узнают об отключении по таймауту TCP (секунды/минуты).
- **Нужно:** Перед `_server.Stop()` / `_client.Disconnect()` слать `PlayerDisconnectedPacket` / специальный пакет "ServerShutdown".

---

### 23. [Networking] Устаревший `Application.LoadLevel` (deprecated)
- **Файл:** `RakeNetworkManager.cs` (ZIP: 580, 585)
- **Суть:** `UnityEngine.Application.LoadLevel` deprecated since 2018. Использовать `SceneManager.LoadScene`.

---

### 24. [Core] RunSerializationTest выполняется в продакшн-билде
- **Файл:** `RakeNetworkManager.cs` (Working Dir: есть вызов в `Awake`/`Start`, ZIP: не проверял)
- **Суть:** Тест сериализации логируется в консоль, аллоцирует память при каждом запуске.
- **Нужно:** Обернуть в `#if DEBUG` или убрать.

---

### 25. [Core] Дублирование `isServer`: статическое поле + ConfigEntry
- **Файл:** `RakeMultiplayerPlugin.cs`, `RakeNetworkManager.cs` (ZIP: 77-79, 107-109 — комментарий говорит, что ConfigEntry удалён, но код может остаться)
- **Суть:** Два источника истины → рассинхрон.
- **Нужно:** Оставить только `RakeMultiplayerPlugin.isServer` (static bool).

---

## 🟡 СРЕДНИЕ (код-качество / техдолг)

### 26. [Player Sync] Пустые патчи в GamePatches.cs (FPSControllerUpdatePatch, PlayerMovementPatch)
- **Файл:** `GamePatches.cs` (ZIP: закомментированы, Working Dir: могут быть активны)
- **Суть:** Патчи есть, логики нет — мусор, создаёт ложное впечатление работы.

---

### 27. [Player Sync] SetAsRemotePlayer — 90 строк ручного отключения компонентов
- **Файл:** `NetworkedPlayer.cs` (ZIP: 115-210)
- **Суть:** Хрупко, легко забыть новый компонент. Лучше: конфиг-лист типов для отключения, или отдельный префаб "RemotePlayer" без лишнего.

---

### 28. [Rake AI] Кэш через `FindObjectOfType` раз в секунду
- **Файл:** `RakeNetworkManager.cs` (ZIP: 156-162)
- **Суть:** `FindObjectOfType` дорогой, даже раз в секунду лишний. Лучше: событие спавна Рейка / подписка на `OnEnable`.

---

### 29. [Networking] Рефлексия в горячих путях (DamageRequest, FireVisuals)
- **Файл:** `RakeNetworkManager.cs` (ZIP: 450-492, 712-800)
- **Суть:** `GetField`/`GetMethod` каждый пакет → GC alloc, медленно.
- **Нужно:** Закешировать `FieldInfo`/`MethodInfo` в `static readonly` при первом использовании.

---

### 30. [Items] HandleClientItemDestroyed: `Remove` после `Destroy` может не сработать
- **Файл:** `RakeNetworkManager.cs` (ZIP: 691-694)
- **Суть:** `UnityEngine.Object.Destroy(item.gameObject)` асинхронно, но `Remove` сразу. Если `Destroy` отложен — ключ остаётся.
- **Нужно:** Сначала `Remove`, потом `Destroy`.

---

### 31. [Networking] Inconsistent naming: `_playerStates` (key=connectionId) vs `_knownPlayerIds` (key=PlayerId)
- **Файл:** `RakeNetworkManager.cs`
- **Суть:** Легко перепутать и получить баг с несоответствием ID.

---

### 32. [Core] NetworkDiscovery.cs.bak — мусорный файл в репозитории
- **Файл:** Корень проекта
- **Суть:** Бэкап в рабочей директории — признак незавершённой работы.

---

## 🟢 НИЗКИЕ (полировка)

### 33. [Combat] Хардкод урона в патчах выстрелов (100 / 200)
- **Файл:** `GamePatches.cs` (Working Dir: `RifleWeaponFireSyncPatch`, `SniperRifleWeaponFireSyncPatch`)
- **Суть:** Урон должен браться из данных оружия (`WeaponManager` / `Rifle_weapon.damage`).

---

### 34. [Combat] FireVisualsPacket не содержит ShooterId для хоста (хотя есть в пакете)
- **Файл:** `RakeNetworkManager.cs` (ZIP: 712-800) — ищет `PlayerSpawner.Instance.TryGetRemotePlayer(packet.ShooterId)`
- **Суть:** На хосте `ShooterId=1` (хост), но хост не в `_remotePlayers` → визуалов у хоста не будет.
- **Нужно:** Отдельная ветка для хоста — найти локального игрока.

---

### 35. [Player Sync] DebugCapsule спавнится для каждого удалённого игрока (визуальный мусор)
- **Файл:** `PlayerSpawner.cs` (ZIP: 96-102)
- **Суть:** `GameObject.CreatePrimitive(PrimitiveType.Capsule)` — остаётся в игре, мешает визуально.

---

### 36. [Core] Отсутствует обработка смены сцены (씬-менеджмент)
- **Файл:** `MultiplayerSceneManager.cs` (ZIP: есть, но минимальный)
- **Суть:** При загрузке игровой сцены нужно реинициализировать спавнеры, реестры, кэши.

---

### 37. [Core] Нет автоматического реконнекта / миграции хоста
- **Суть:** Если хост уходит — сессия ломается полностью.

---

### 38. [Networking] Telepathy — TCP only, нет UDP для игрового трафика
- **Суть:** Позиции/инпут через TCP → задержки, head-of-line blocking. Для 20Hz позиций нужен UDP.

---

### 39. [Items] Нет синхронизации дверей / камер / SetupCamera
- **Файл:** `Decompiled_Source/Door_obj.cs`, `CameraComputer.cs`
- **Суть:** Взаимодействия не синхронизируются вовсе.

---

### 40. [Combat] Нет синхронизации анимаций стрельбы / перезарядки на удалённых игроках
- **Суть:** `Animator.SetBool("Fire", true)` только локально.

---

---

## 📋 ИТОГОВАЯ ТАБЛИЦА

| Категория | 🔴 Критично | 🟠 Важно | 🟡 Средне | 🟢 Низко | Всего |
|-----------|-------------|----------|-----------|----------|-------|
| Networking | 5 | 2 | 4 | 2 | 13 |
| Player Sync | 0 | 3 | 2 | 2 | 7 |
| Rake AI | 3 | 2 | 1 | 0 | 6 |
| Items | 3 | 2 | 1 | 1 | 7 |
| Combat | 2 | 1 | 1 | 3 | 7 |
| Core/Arch | 0 | 1 | 2 | 1 | 4 |
| **ВСЕГО** | **13** | **11** | **11** | **9** | **44** |

---

## 🎯 ПРИОРИТЕТНЫЙ ПЛАН ФИКСОВ

### Phase 1 — Critical Gameplay (1-2 дня)
1. ✅ `ShooterPosition` в `DamageRequestPacket` — **СДЕЛАНО в Working Dir**
2. ✅ `ItemPickupPatch: return false` на клиенте — **СДЕЛАНО в Working Dir**
3. ✅ Патчи синхронизации выстрелов — **СДЕЛАНО в Working Dir**
4. Исправить `currentState` рефлексию в `NetworkedRakeAI` (найти реальное поле в `Enemy_AI`)
5. Добавить интерполяцию в `NetworkedRakeAI.ApplySyncData`
6. Убрать глобальный `DestroyPatch` → безопасная альтернатива
7. Починить `PlayerDisconnectedPacket.PlayerId = connectionId + 1`
8. Очистка `_networkedItems` в `StopHost/Disconnect`
9. Добавить `MAX_PACKET_SIZE` + `MAX_STRING_LENGTH` в `PacketSerializer`

### Phase 2 — Core Sync (3-5 дней)
10. Добавить Health/Stamina/Flashlight в `PlayerStatePacket` + реализовать в Working Dir (взять из ZIP)
11. Расширить `RakeStatePacket.State` → bitmask для всех флагов `Enemy_AI`
12. Реализовать `DetectionTriggerPatch` (уведомление серверного AI)
13. Исправить `"Bullet"` damage type → триггерит Danger state
14. Добавить пакеты Ammo/Reload/Switch + обработку
15. Интерполяция с буфером в `NetworkedPlayer` (адаптивная под пинг)

### Phase 3 — Polish & Security (1-2 недели)
16. Rate limiting на сервере
17. Протокольное версионирование
18. Детерминистская регистрация предметов (sequential ID)
19. Валидация входящих данных (позиция, скорость, ID)
20. Грейсфул дисконнект / ServerShutdown пакет
21. Замена `Application.LoadLevel` → `SceneManager.LoadScene`
22. Убрать `RunSerializationTest` из продакшна
23. Закешировать рефлексию в `static readonly`
24. Удалить DebugCapsule, пустые патчи, `.bak` файлы
25. UDP транспорт (замена Telepathy или надстройка)

---

## 📁 ФАЙЛЫ ДЛЯ ПРОВЕРКИ (Decompiled_Source)
- `Enemy_AI.cs` — найти реальные поля состояния/здоровья/методы TakeDamage/Event_PlayerFire
- `Rifle_weapon.cs` / `SniperRifle_weapon.cs` — поля `caster`, `muzzleFire`, `fireSound`, `muzzlePos`, `SFX`, `currentAmmo`, `bCanFire`, `bSight`
- `WeaponManager.cs` — `AddWeapon`, `AddAmmo`, `CheckWeapon`, `weaponsAmmo`, `playerWeapons`
- `Item.cs` / `Medkit_item.cs` / `Rifle_item.cs` / `RifleAmmo_item.cs` — логика пикапа
- `Door_obj.cs` / `CameraComputer.cs` / `SetupCamera.cs` — взаимодействия
- `PlayerMovement.cs` — стамина, бег, анимации
- `FPSController.cs` — `weapCam`, `Health`, `flashlight`, `TakeDamage` (есть ли?)

---

*Отчёт сгенерирован автоматически на основе аудита двух версий кодовой базы.*