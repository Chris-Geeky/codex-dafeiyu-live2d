[CmdletBinding()]
param([string]$OutputDirectory = '', [string]$NodeVersion = '24.21.0')
$ErrorActionPreference = 'Stop'
$petRoot = Split-Path $PSScriptRoot -Parent
if ($NodeVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a numeric Node.js release version.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $petRoot 'dist' }
$petBuildDir = [IO.Path]::GetFullPath($OutputDirectory)
$petVersion = (Get-Content (Join-Path $petRoot 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
$petName = 'codex-dafeiyu-live2d-v' + $petVersion + '-win-x64'
$petDestination = Join-Path $petBuildDir $petName
$petArchive = Join-Path $petBuildDir ($petName + '.zip')
if ((Test-Path -LiteralPath $petDestination) -or (Test-Path -LiteralPath $petArchive)) { throw 'Output already exists. Choose a fresh -OutputDirectory; existing files are never deleted.' }
$petCache = Join-Path $petRoot '.downloads'
New-Item -ItemType Directory -Path $petCache -Force | Out-Null
$petNodeName = 'node-v' + $NodeVersion + '-win-x64'
$petNodeZip = Join-Path $petCache ($petNodeName + '.zip')
$petChecksums = Join-Path $petCache ('node-v' + $NodeVersion + '-SHASUMS256.txt')
$petBaseUrl = 'https://nodejs.org/dist/v' + $NodeVersion + '/'
function Get-PetDownload([string]$Url,[string]$Target) {
    & curl.exe --fail --location --silent --show-error --retry 2 --max-time 240 --output $Target $Url
    if ($LASTEXITCODE -ne 0) { throw ('Download failed: ' + $Url) }
}
Get-PetDownload ($petBaseUrl + 'SHASUMS256.txt') $petChecksums
$petPattern = '^([a-fA-F0-9]{64})\s+' + [regex]::Escape($petNodeName + '.zip') + '$'
$petMatch = [regex]::Match((Get-Content -LiteralPath $petChecksums -Raw), $petPattern, [Text.RegularExpressions.RegexOptions]::Multiline)
if (-not $petMatch.Success) { throw 'Official Node.js archive checksum not found.' }
if (-not (Test-Path -LiteralPath $petNodeZip)) { Get-PetDownload ($petBaseUrl + $petNodeName + '.zip') $petNodeZip }
$petActualHash = (Get-FileHash -LiteralPath $petNodeZip -Algorithm SHA256).Hash
if ($petActualHash -ine $petMatch.Groups[1].Value) { throw 'Node.js archive checksum mismatch. Cached archive was not used.' }
New-Item -ItemType Directory -Path $petDestination | Out-Null
foreach ($petFile in @('package.json','assets-manifest.json','cli.mjs','cdp.mjs','renderer.mjs','launch.mjs','settings.mjs','core-runtime.mjs','setup.mjs','README.md','LICENSE','NOTICE.md','Start-Live2D.cmd','Start-Live2D.ps1','Restore-Pet.cmd','Restore-Pet.ps1')) {
    Copy-Item -LiteralPath (Join-Path $petRoot $petFile) -Destination $petDestination
}
# Keep the beginner guide readable in Windows Notepad, including older versions.
$petGuide = [IO.File]::ReadAllText((Join-Path $petRoot 'START-HERE.txt'),[Text.Encoding]::UTF8)
$petGuide = $petGuide -replace '\r?\n', "`r`n"
[IO.File]::WriteAllText((Join-Path $petDestination 'START-HERE.txt'),$petGuide,[Text.UTF8Encoding]::new($true))
$petDocs = Join-Path $petDestination 'docs'
New-Item -ItemType Directory -Path $petDocs | Out-Null
Copy-Item -LiteralPath (Join-Path $petRoot 'docs/control-panel.png') -Destination $petDocs
foreach ($petFolder in @('assets','third-party')) { Copy-Item -LiteralPath (Join-Path $petRoot $petFolder) -Destination $petDestination -Recurse }
$petRuntimeDir = Join-Path $petDestination 'runtime'
New-Item -ItemType Directory -Path $petRuntimeDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$petZip = [IO.Compression.ZipFile]::OpenRead($petNodeZip)
try {
    # Extract only these two named entries. This also avoids legacy Windows
    # long-path failures in the much larger npm/node_modules directory.
    foreach ($petEntryName in @('node.exe','LICENSE')) {
        $petEntry = $petZip.GetEntry($petNodeName + '/' + $petEntryName)
        if ($null -eq $petEntry) { throw ('Official archive entry not found: ' + $petEntryName) }
        $petTargetName = if ($petEntryName -eq 'LICENSE') { 'Node-LICENSE.txt' } else { $petEntryName }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($petEntry, (Join-Path $petRuntimeDir $petTargetName))
    }
} finally { $petZip.Dispose() }
$petLauncherExe = Join-Path $petDestination 'CodexDafeiyu.exe'
& (Join-Path $PSScriptRoot 'Build-Launcher.ps1') -OutputPath $petLauncherExe
& (Join-Path $petRuntimeDir 'node.exe') (Join-Path $petDestination 'cli.mjs') verify
if ($LASTEXITCODE -ne 0) { throw 'Portable asset verification failed.' }
# This mode checks files only. It never starts, stops, or connects to Codex.
$petSelfTest = Start-Process -FilePath $petLauncherExe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($petSelfTest.ExitCode -ne 0) { throw 'Native launcher self-test failed.' }
Compress-Archive -LiteralPath $petDestination -DestinationPath $petArchive -CompressionLevel Optimal
$petZipHash = (Get-FileHash -LiteralPath $petArchive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($petArchive + '.sha256'),($petZipHash + '  ' + [IO.Path]::GetFileName($petArchive) + "`n"),[Text.UTF8Encoding]::new($false))
Write-Output $petArchive
Write-Output ('SHA256 ' + $petZipHash)
