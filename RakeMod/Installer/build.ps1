$ErrorActionPreference = "Stop"

$root = "D:\Rake Multiplayer"
$installer = Join-Path $root "RakeMod\Installer"
$staging = Join-Path $installer "payload"

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $root "RakeMod\BepInEx.zip"), $staging)

$pluginsSrc = Join-Path $root "Rake\BepInEx\plugins"
$pluginsDst = Join-Path $staging "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $pluginsDst | Out-Null

$files = @(
    "RakeMultiplayer.dll",
    "Telepathy.dll",
    "RakePlayerModel.bin",
    "RakePlayerRifle.bin",
    "RakePlayerSkin.png",
    "rifle.png"
)

foreach ($f in $files) {
    $src = Join-Path $pluginsSrc $f
    if (!(Test-Path $src)) { throw "missing $src" }
    Copy-Item $src $pluginsDst -Force
}

# Steam (Spacewar app 480): native DLLs + appid next to Rake.exe
$steamSrc = Join-Path $root "RakeMod\libs\steam\x86"
Copy-Item (Join-Path $steamSrc "steam_api.dll") $staging -Force
Copy-Item (Join-Path $steamSrc "CSteamworks.dll") $staging -Force
[IO.File]::WriteAllText((Join-Path $staging "steam_appid.txt"), "480")

$pak = Join-Path $installer "payload.pak"
$all = Get-ChildItem $staging -Recurse -File

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([Text.Encoding]::ASCII.GetBytes("RMPK"))
$bw.Write([int]$all.Count)

foreach ($f in $all) {
    $rel = $f.FullName.Substring($staging.Length + 1).Replace('\', '/')
    $name = [Text.Encoding]::UTF8.GetBytes($rel)
    $data = [IO.File]::ReadAllBytes($f.FullName)
    $bw.Write($name.Length)
    $bw.Write($name)
    $bw.Write($data.Length)
    $bw.Write($data)
}

$bw.Flush()
[IO.File]::WriteAllBytes($pak, $ms.ToArray())
$bw.Close()

"payload entries: $($all.Count), size: $([math]::Round((Get-Item $pak).Length / 1MB, 2)) MB"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out = Join-Path $installer "RakeMultiplayerSetup.exe"

$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/out:$out",
    "/resource:$pak,payload.pak",
    "/r:System.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    (Join-Path $installer "Installer.cs")
)

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed with $LASTEXITCODE" }

"built: $out ($([math]::Round((Get-Item $out).Length / 1MB, 2)) MB)"
