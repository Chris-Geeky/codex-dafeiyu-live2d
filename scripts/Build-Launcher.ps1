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
$petInputs = @('Launcher.cs','Dashboard.cs','AppearanceDialog.cs','AppearanceWebDialog.cs','NativeDragGuard.cs','AssemblyInfo.cs') | ForEach-Object { Join-Path $petRoot ('native/' + $_) }
$petAppearance = Join-Path $petRoot 'appearance-catalog.json'
$petWebView = Join-Path $petRoot 'assets/runtime/webview2'
$petPreview = Join-Path $petRoot 'docs/appearance-panel.png'
& $petCompiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/r:' + (Join-Path $petWebView 'Microsoft.Web.WebView2.Core.dll')) ('/r:' + (Join-Path $petWebView 'Microsoft.Web.WebView2.WinForms.dll')) ('/win32icon:' + $petIcon) ('/win32manifest:' + $petManifest) ('/resource:' + $petIcon + ',Dafeiyu.AppIcon') ('/resource:' + $petLogo + ',Dafeiyu.AppLogo') ('/resource:' + $petAppearance + ',Dafeiyu.AppearanceCatalog') ('/resource:' + $petPreview + ',Dafeiyu.AppearancePreview') ('/out:' + $petOutput) $petInputs
if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
Copy-Item -LiteralPath (Join-Path $petRoot 'native/app.config') -Destination ($petOutput + '.config')
foreach($petLibrary in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) { Copy-Item -LiteralPath (Join-Path $petWebView $petLibrary) -Destination (Join-Path (Split-Path $petOutput -Parent) $petLibrary) }
Write-Output $petOutput
