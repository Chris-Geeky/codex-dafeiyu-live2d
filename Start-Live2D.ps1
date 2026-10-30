[CmdletBinding()]
param([ValidateSet(30,60)][int]$Fps = 60)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$petBundledNode = Join-Path $PSScriptRoot 'runtime/node.exe'
$petNode = if (Test-Path -LiteralPath $petBundledNode) { $petBundledNode } else { (Get-Command node -ErrorAction Stop).Source }
$petLauncher = Join-Path $PSScriptRoot 'launch.mjs'
$petPreflightJson = & $petNode $petLauncher preflight
$petPreflight = $petPreflightJson | ConvertFrom-Json
if (-not $petPreflight.ready -and $petPreflight.reason -ne 'running-without-debug-port') {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($petPreflight.message, 'Codex Live2D', 'OK', 'Information') | Out-Null
    exit 2
}
# Prefer the dispatch object of a running Explorer folder view. Unlike a fresh
# Shell.Application object, that object actually brokers creation in Explorer.
# https://devblogs.microsoft.com/oldnewthing/20131118-00/?p=2643
$petShell = New-Object -ComObject Shell.Application
$petArguments = ('"' + $petLauncher + '" start --fps ' + [string]$Fps + ' --wait-for-exit --open-pet')
try {
    $petBroker = $petShell
    $petWindows = $petShell.Windows()
    for ($petWindowIndex = 0; $petWindowIndex -lt $petWindows.Count; $petWindowIndex++) {
        $petWindow = $petWindows.Item($petWindowIndex)
        if ([IO.Path]::GetFileName($petWindow.FullName) -ieq 'explorer.exe' -and $null -ne $petWindow.Document.Application) {
            $petBroker = $petWindow.Document.Application
            break
        }
    }
    $petBroker.ShellExecute($petNode, $petArguments, $PSScriptRoot, 'open', 0)
} finally {
    [Runtime.InteropServices.Marshal]::ReleaseComObject($petShell) | Out-Null
}
