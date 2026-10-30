[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$petRoot = Split-Path $PSScriptRoot -Parent
$petOutput = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path $petOutput -Parent) -Force | Out-Null
$petCompiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
if (-not (Test-Path -LiteralPath $petCompiler)) { $petCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe' }
$petIcon = Join-Path $petRoot 'assets/branding/app.ico'
$petLogo = Join-Path $petRoot 'assets/branding/app-256.png'
$petManifest = Join-Path $petRoot 'native/app.manifest'
$petInputs = @('Launcher.cs','Dashboard.cs','AssemblyInfo.cs') | ForEach-Object { Join-Path $petRoot ('native/' + $_) }
& $petCompiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/win32icon:' + $petIcon) ('/win32manifest:' + $petManifest) ('/resource:' + $petIcon + ',Dafeiyu.AppIcon') ('/resource:' + $petLogo + ',Dafeiyu.AppLogo') ('/out:' + $petOutput) $petInputs
if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
Copy-Item -LiteralPath (Join-Path $petRoot 'native/app.config') -Destination ($petOutput + '.config')
Write-Output $petOutput
