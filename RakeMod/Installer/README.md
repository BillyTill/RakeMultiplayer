# Rake Multiplayer — установщик

`RakeMultiplayerSetup.exe` ставит мод на **чистую** копию игры Rake (2015). Ничего не нужно
распаковывать вручную.

## Что ставится

- **BepInEx 5.4.23.2 (x86)** — загрузчик плагинов под 32-битный Unity 5:
  `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, папка `BepInEx\core`.
- **Мод**: `BepInEx\plugins\RakeMultiplayer.dll`.
- **Telepathy.dll** — транспорт (TCP, порт 7777).
- **Ассеты модели**: `RakePlayerModel.bin`, `RakePlayerRifle.bin`, `RakePlayerSkin.png`,
  `rifle.png`.
- **Деинсталлятор** `Uninstall_RakeMultiplayer.exe` в папке игры.

Конфиг мода (`BepInEx\config\...`) не входит в установщик: при первом запуске BepInEx
создаст его с дефолтами, а существующий конфиг не перезаписывается.

## Как пользоваться

1. Запустить `RakeMultiplayerSetup.exe` (SmartScreen может предупредить — «Подробнее» →
   «Выполнить всё равно», файл без цифровой подписи).
2. Указать папку, где лежат `Rake.exe` и `Rake_Data` (установщик попробует найти сам:
   рядом с собой, по реестру, в типовых путях).
3. «Установить» → «Запустить игру».
4. В игре **F1** — меню мультиплеера.

Тихий режим для скриптов:

```
RakeMultiplayerSetup.exe /silent "D:\Games\Rake"
```

Код возврата: `0` — успех, `1` — ошибка.

## Удаление

В папке игры появляется `Uninstall_RakeMultiplayer.exe` (это копия установщика в режиме
удаления). Запустить → «Удалить мод».

- Удаляет файлы BepInEx и мода; если в `BepInEx\plugins` не осталось других плагинов —
  сносит папку `BepInEx` целиком (вместе с конфигом и логами).
- Файлы самой игры (`Rake.exe`, `Rake_Data`) не трогает.
- После удаления деинсталлятор удаляет сам себя.

Тихий режим (без пути берёт папку, в которой лежит сам):

```
Uninstall_RakeMultiplayer.exe /uninstall "D:\Games\Rake"
```

## Игра по сети

- Хост: F1 → **Host Game** (порт 7777).
- Клиент: F1 → IP хоста + порт 7777 → Connect.
- Radmin VPN / Hamachi работают: в поле IP вводится VPN-адрес хоста (`26.x.x.x` /
  `25.x.x.x`). На хосте нужно разрешить входящий TCP 7777 в брандмауэре Windows.
- До 4 игроков; хост авторитетен.

## Сборка установщика

`build.ps1` собирает payload из `RakeMod\BepInEx.zip` и текущих файлов в
`Rake\BepInEx\plugins`, затем компилирует `Installer.cs` штатным `csc.exe` из .NET Framework.
Требуется только Windows с .NET Framework 4.x (на 10/11 уже есть).
