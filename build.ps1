# Packages the app and verifies the result.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1
#   powershell -ExecutionPolicy Bypass -File build.ps1 -BumpCache
#   powershell -ExecutionPolicy Bypass -File build.ps1 -App
#
# Always builds pool-score-tracker.zip - the browser / installed-PWA version.
#
# -BumpCache first increments the service worker cache name in sw.js. Do that
# whenever the app's code has changed, otherwise browsers that already installed
# the app keep serving the copy they cached.
#
# -App also builds windows-app\publish\PoolScore.exe, the standalone
# Windows app. That app is native C# and draws its own scoreboard, so the web
# files below are nothing to do with it - rebuild it when anything under
# windows-app\ changes, and ignore the cache name entirely.
#
# The file list below is deliberately explicit, so nothing gets shipped by
# accident. If you add a file the app needs, add it here too - the verify step
# fails the build if anything shippable is missing from the list.

[CmdletBinding()]
param(
  [switch]$BumpCache,
  [switch]$App
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = $PSScriptRoot
$zipName = 'pool-score-tracker.zip'
$folderInZip = 'pool-score-tracker'

$files = @(
  'install-or-update.cmd',
  'index.html',
  'app.js',
  'styles.css',
  'sw.js',
  'manifest.webmanifest',
  'server.mjs',
  'README.txt',
  'assets/icon.svg',
  'assets/icon-192.png',
  'assets/icon-512.png',
  'assets/qrcode.js'
)

# Present in the folder but never in the zip: runtime state, dev config, the
# build script itself and the archive it produces.
$notShipped = @('session.json', 'build.ps1', '.gitignore', '.gitattributes', $zipName)

# The standalone Windows app is its own deliverable, built by -App below.
$notShippedFolders = @('windows-app/', '.git/')

function Get-DiskPath([string]$relative) {
  return (Join-Path $root $relative.Replace('/', '\'))
}

# ----------------------------- cache version ------------------------------

$swPath = Get-DiskPath 'sw.js'
$swText = [System.IO.File]::ReadAllText($swPath)
$versionMatch = [regex]::Match($swText, 'pool-score-tracker-v(\d+)')
if (-not $versionMatch.Success) { throw 'Could not find the cache name in sw.js' }
$version = [int]$versionMatch.Groups[1].Value

if ($BumpCache) {
  $next = $version + 1
  $swText = $swText.Replace("pool-score-tracker-v$version", "pool-score-tracker-v$next")
  [System.IO.File]::WriteAllText($swPath, $swText)
  "cache name bumped: v$version -> v$next"
  $version = $next
} else {
  "cache name: v$version  (pass -BumpCache if the app's code changed)"
}

# -------------------------------- package ---------------------------------

$missing = @()
foreach ($f in $files) {
  if (-not (Test-Path (Get-DiskPath $f))) { $missing += $f }
}
if ($missing.Count -gt 0) { throw ("Missing files: " + ($missing -join ', ')) }

$zipPath = Join-Path $root $zipName
$tmpPath = Join-Path $env:TEMP ('pool-score-tracker-build-' + [System.Guid]::NewGuid().ToString('N') + '.zip')

$archive = [System.IO.Compression.ZipFile]::Open($tmpPath, 'Create')
try {
  foreach ($f in $files) {
    # Entry names must use forward slashes. .NET's CreateFromDirectory writes
    # backslashes on Windows, which Mac/Linux unzip turns into filenames like
    # "pool-score-tracker\app.js" instead of a folder.
    $entry = "$folderInZip/$f"
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Get-DiskPath $f), $entry, 'Optimal') | Out-Null
  }
} finally {
  $archive.Dispose()
}

# --------------------------------- verify ---------------------------------
# The new archive is checked while it is still in the temp folder, so a failed
# build leaves the previous zip untouched rather than replacing it with a bad one.

$problems = @()
$shipped = @()
$prefix = "$folderInZip/"

$check = [System.IO.Compression.ZipFile]::OpenRead($tmpPath)
try {
  foreach ($e in $check.Entries) {
    $shipped += $e.FullName
    if ($e.FullName.Contains('\')) { $problems += "backslash in entry name: $($e.FullName)"; continue }
    if (-not $e.FullName.StartsWith($prefix)) { $problems += "entry outside $folderInZip/: $($e.FullName)"; continue }

    $relative = $e.FullName.Substring($prefix.Length)
    $disk = Get-DiskPath $relative
    if (-not (Test-Path $disk)) { $problems += "not on disk: $relative"; continue }

    $stream = $e.Open()
    $buffer = New-Object System.IO.MemoryStream
    try {
      $stream.CopyTo($buffer)
      $buffer.Position = 0
      $zipHash = (Get-FileHash -InputStream $buffer -Algorithm SHA256).Hash
    } finally {
      $stream.Close()
      $buffer.Close()
    }
    if ($zipHash -ne (Get-FileHash -Path $disk -Algorithm SHA256).Hash) {
      $problems += "content differs from the working copy: $relative"
    }
  }
} finally {
  $check.Dispose()
}

# Anything in the folder that the app would need but the list forgot.
$onDisk = Get-ChildItem $root -Recurse -File |
  Where-Object { -not $_.FullName.Contains('\.claude\') } |
  ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\', '/') } |
  Where-Object { $notShipped -notcontains $_ } |
  Where-Object { -not $_.EndsWith('.zip') } |
  Where-Object { $item = $_; -not ($notShippedFolders | Where-Object { $item.StartsWith($_) }) }

foreach ($f in $onDisk) {
  if ($shipped -notcontains ($prefix + $f)) { $problems += "in the folder but not in the zip - add it to `$files: $f" }
}

# --------------------------------- report ---------------------------------

""
if ($problems.Count -gt 0) {
  [System.IO.File]::Delete($tmpPath)
  "FAILED - $zipName left as it was:"
  $problems | ForEach-Object { "   $_" }
  exit 1
}

if ([System.IO.File]::Exists($zipPath)) { [System.IO.File]::Delete($zipPath) }
[System.IO.File]::Move($tmpPath, $zipPath)

$size = (Get-Item $zipPath).Length
"$zipName - $size bytes, $($files.Count) files, cache v$version"
"verified: every entry matches the working copy, forward slashes throughout,"
"          nothing the app needs left out"

# --------------------------- standalone windows app -----------------------

if ($App) {
  ""
  "building the standalone Windows app..."
  $project = Join-Path $root 'windows-app\PoolScoreTracker.csproj'
  $publish = Join-Path $root 'windows-app\publish'

  & dotnet publish $project -c Release -o $publish -v quiet --nologo
  if ($LASTEXITCODE -ne 0) { "FAILED - dotnet publish returned $LASTEXITCODE"; exit 1 }

  $exe = Join-Path $publish 'PoolScore.exe'
  if (-not (Test-Path $exe)) { "FAILED - no exe was produced"; exit 1 }

  # bin/obj are ~120 MB of rebuildable output and this folder syncs to OneDrive,
  # so only the finished exe is kept.
  foreach ($junk in @('windows-app\bin', 'windows-app\obj')) {
    $path = Join-Path $root $junk
    if (Test-Path $path) { Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue }
  }

  "PoolScore.exe - $((Get-Item $exe).Length) bytes, single self-contained file"
}
