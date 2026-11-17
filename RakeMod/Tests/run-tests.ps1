param(
    [string[]] $Filter = @()
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "Runner\RakeMultiplayer.TestRunner.csproj"

& dotnet build $project -c Release --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "test build failed" }

$exe = Join-Path $root "Runner\bin\Release\net472\RakeMultiplayer.TestRunner.exe"

# No filter = the whole suite. Only the summary line is printed here; run the exe
# directly to see individual results.
$output = & $exe @Filter 2>&1
$code = $LASTEXITCODE

$output | Where-Object { $_ -notmatch '^(PASS|UNITY|IGNORE|WARN) ' } | ForEach-Object { $_ }

exit $code
