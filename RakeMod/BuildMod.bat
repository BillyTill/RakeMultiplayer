@echo off
:: Build script for Rake Multiplayer Mod

echo ========================================
echo Building Rake Multiplayer Mod
echo ========================================
echo.

set PROJECT_PATH=%~dp0Source\RakeMultiplayer\RakeMultiplayer.csproj
set BUILD_CONFIG=Release

echo [INFO] Project: %PROJECT_PATH%
echo [INFO] Configuration: %BUILD_CONFIG%
echo.

:: Check for dotnet
dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] .NET SDK not found. Please install .NET 6.0+ SDK
    pause
    exit /b 1
)

echo [INFO] .NET SDK version: 
dotnet --version

:: Restore packages
echo [INFO] Restoring NuGet packages...
dotnet restore "%PROJECT_PATH%"
if %errorLevel% neq 0 (
    echo [ERROR] Package restore failed
    pause
    exit /b 1
)

:: Build
echo [INFO] Building...
dotnet build "%PROJECT_PATH%" -c %BUILD_CONFIG% --no-restore
if %errorLevel% neq 0 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)

echo.
echo [OK] Build successful!
echo Output: %~dp0Source\RakeMultiplayer\bin\%BUILD_CONFIG%\net40\RakeMultiplayer.dll
echo.

:: Copy to game folder for testing
set GAME_PATH=D:\Rake Multiplayer\Rake
if exist "%GAME_PATH%\Rake.exe" (
    echo [INFO] Copying to game folder for testing...
    mkdir "%GAME_PATH%\BepInEx\plugins" 2>nul
    copy /Y "%~dp0Source\RakeMultiplayer\bin\%BUILD_CONFIG%\net40\RakeMultiplayer.dll" "%GAME_PATH%\BepInEx\plugins\"
    copy /Y "%~dp0Source\RakeMultiplayer\bin\%BUILD_CONFIG%\net40\RakeMultiplayer.pdb" "%GAME_PATH%\BepInEx\plugins\" 2>nul
    echo [OK] Copied to game folder
) else (
    echo [WARN] Game folder not found at %GAME_PATH%
)

echo.
pause