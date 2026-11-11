@echo off
:: Rake Multiplayer Mod - Automatic Installer
:: Run as Administrator for best results

echo ========================================
echo Rake Multiplayer Mod Installer v1.0.0
echo ========================================
echo.

:: Check if running as admin
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [OK] Running as Administrator
) else (
    echo [WARN] Not running as Administrator - some operations may fail
    echo        Right-click and "Run as Administrator" for best results
)
echo.

:: Find Rake installation
set RAKE_PATH=""
if exist "D:\Rake Multiplayer\Rake\Rake.exe" (
    set RAKE_PATH=D:\Rake Multiplayer\Rake
) else if exist "C:\Program Files (x86)\Steam\steamapps\common\Rake\Rake.exe" (
    set RAKE_PATH=C:\Program Files (x86)\Steam\steamapps\common\Rake
) else if exist "C:\Program Files\Steam\steamapps\common\Rake\Rake.exe" (
    set RAKE_PATH=C:\Program Files\Steam\steamapps\common\Rake
) else if exist "D:\Steam\steamapps\common\Rake\Rake.exe" (
    set RAKE_PATH=D:\Steam\steamapps\common\Rake
) else if exist "E:\Steam\steamapps\common\Rake\Rake.exe" (
    set RAKE_PATH=E:\Steam\steamapps\common\Rake
)

if "%RAKE_PATH%"=="" (
    echo [ERROR] Could not find Rake installation automatically.
    echo Please ensure Rake is installed via Steam.
    echo.
    set /p RAKE_PATH=Enter Rake installation path manually: 
    if not exist "%RAKE_PATH%\Rake.exe" (
        echo [ERROR] Invalid path: %RAKE_PATH%
        pause
        exit /b 1
    )
)

echo [OK] Found Rake at: %RAKE_PATH%
echo.

:: Create BepInEx folder structure
echo [INFO] Setting up BepInEx...
mkdir "%RAKE_PATH%\BepInEx\plugins" 2>nul
mkdir "%RAKE_PATH%\BepInEx\patchers" 2>nul
mkdir "%RAKE_PATH%\BepInEx\config" 2>nul
mkdir "%RAKE_PATH%\BepInEx\core" 2>nul

:: Copy mod files
echo [INFO] Copying mod files...
set MOD_SOURCE=%~dp0Source\RakeMultiplayer\bin\Release\net35
if not exist "%MOD_SOURCE%\RakeMultiplayer.dll" (
    set MOD_SOURCE=%~dp0..\RakeMod\Source\RakeMultiplayer\bin\Release\net35
)
if not exist "%MOD_SOURCE%\RakeMultiplayer.dll" (
    set MOD_SOURCE=%~dp0RakeMultiplayer.dll
)

if exist "%MOD_SOURCE%\RakeMultiplayer.dll" (
    copy /Y "%MOD_SOURCE%\RakeMultiplayer.dll" "%RAKE_PATH%\BepInEx\plugins\"
    echo [OK] Copied RakeMultiplayer.dll
) else (
    echo [WARN] RakeMultiplayer.dll not found in build output
    echo        Please build the project first or copy manually
)

:: Check for dependencies
echo [INFO] Checking dependencies...

:: BepInEx core files (should be installed separately)
if not exist "%RAKE_PATH%\BepInEx\core\BepInEx.dll" (
    echo [WARN] BepInEx core not found!
    echo        Please install BepInEx 5.x for Unity 5.x first:
    echo        https://github.com/BepInEx/BepInEx/releases
    echo        Download: BepInEx_UnityIL2CPP_x64_5.x.x.zip (or UnityMono for older versions)
)

:: Copy Harmony if available
if exist "%MOD_SOURCE%\0Harmony.dll" (
    copy /Y "%MOD_SOURCE%\0Harmony.dll" "%RAKE_PATH%\BepInEx\plugins\"
    echo [OK] Copied 0Harmony.dll
) else if exist "%MOD_SOURCE%\Harmony.dll" (
    copy /Y "%MOD_SOURCE%\Harmony.dll" "%RAKE_PATH%\BepInEx\plugins\"
    echo [OK] Copied Harmony.dll
) else (
    echo [WARN] Harmony not found - required for patching
    echo        Download from: https://github.com/pardeike/Harmony/releases
)

:: Copy Mirror if available
if exist "%MOD_SOURCE%\Mirror.dll" (
    copy /Y "%MOD_SOURCE%\Mirror.dll" "%RAKE_PATH%\BepInEx\plugins\"
    echo [OK] Copied Mirror.dll
) else (
    echo [WARN] Mirror.dll not found - required for networking
    echo        Download from: https://github.com/vis2k/Mirror/releases
)

:: Copy Telepathy transport if available
if exist "%MOD_SOURCE%\Telepathy.dll" (
    copy /Y "%MOD_SOURCE%\Telepathy.dll" "%RAKE_PATH%\BepInEx\plugins\"
    echo [OK] Copied Telepathy.dll
)

:: Create default config
echo [INFO] Creating default config...
if not exist "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg" (
    echo [General] > "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo Enabled = true >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo. >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo [Network] >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo Address = 127.0.0.1 >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo Port = 7777 >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo MaxPlayers = 4 >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo IsServer = false >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo UseSteamP2P = false >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo SteamAppId = 480 >> "%RAKE_PATH%\BepInEx\config\com.rake.multiplayer.cfg"
    echo [OK] Created default config
) else (
    echo [OK] Config already exists
)

:: Verify installation
echo.
echo ========================================
echo Installation Summary
echo ========================================
echo Game Path: %RAKE_PATH%
echo.
echo Files in BepInEx/plugins:
dir /b "%RAKE_PATH%\BepInEx\plugins" 2>nul
echo.
echo Next Steps:
echo 1. Ensure BepInEx 5.x is installed (see warnings above)
echo 2. Download missing dependencies (Harmony, Mirror, Telepathy)
echo 3. Launch Rake.exe through Steam
echo 4. Press tilde (~) to open BepInEx console
echo 5. Look for "[RakeMultiplayer] Loaded successfully"
echo.
echo For LAN play: Allow Rake.exe through Windows Firewall
echo For Online play: Port forward 7777 (TCP/UDP) or use Steam P2P
echo.
pause