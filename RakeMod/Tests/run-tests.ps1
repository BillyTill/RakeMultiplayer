$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "Runner\RakeMultiplayer.TestRunner.csproj"

& dotnet build $project -c Release --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "test build failed" }

$exe = Join-Path $root "Runner\bin\Release\net472\RakeMultiplayer.TestRunner.exe"
& $exe PacketSerializationTests
exit $LASTEXITCODE
