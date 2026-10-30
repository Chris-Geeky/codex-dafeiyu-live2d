$ErrorActionPreference = 'Stop'
$petBundledNode = Join-Path $PSScriptRoot 'runtime/node.exe'
$petNode = if (Test-Path -LiteralPath $petBundledNode) { $petBundledNode } else { (Get-Command node -ErrorAction Stop).Source }
& $petNode (Join-Path $PSScriptRoot 'launch.mjs') restore
