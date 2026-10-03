[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$rootPath = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1') -Test core
$sdkPath = & (Join-Path $PSScriptRoot 'restore-webview2.ps1')
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$targetPath = Join-Path $rootPath 'bin\AnitsuEmbeddedSmoke.exe'
& $compilerPath /nologo /target:exe "/out:$targetPath" '/r:System.dll' '/r:System.Core.dll' '/r:System.Windows.Forms.dll' '/r:System.Drawing.dll' "/r:$rootPath\bin\AniLista.exe" "/r:$sdkPath\Microsoft.Web.WebView2.Core.dll" (Join-Path $rootPath 'tests\AnitsuEmbeddedSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar teste WebView2.' }
& $targetPath
if ($LASTEXITCODE -ne 0) { throw 'Teste WebView2 integrado falhou.' }
