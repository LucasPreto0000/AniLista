$ErrorActionPreference = 'Stop'
$dependencyFolder = Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\dependencies'
$version = '1.0.4258.31'
$packagePath = Join-Path $dependencyFolder "webview2.$version.nupkg"
$expectedHash = '1E5195C2FC8FFC85A25C053C2B9ED59A0E7D9216370D630EC4C9C7E001EEB05472D3FBC3023288B5750854D5E25D76AAE022DF499C8D522735C0E2259CF2EC3D'
New-Item -ItemType Directory -Path $dependencyFolder -Force | Out-Null
if (-not (Test-Path -LiteralPath $packagePath)) {
  Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $packagePath
}
if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash -ne $expectedHash) { throw 'Pacote WebView2 com hash inesperado.' }
$targetFolder = Join-Path $dependencyFolder "WebView2-$version"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
  foreach ($pair in @(
    @('lib/net462/Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.Core.dll'),
    @('lib/net462/Microsoft.Web.WebView2.WinForms.dll', 'Microsoft.Web.WebView2.WinForms.dll'),
    @('runtimes/win-x64/native/WebView2Loader.dll', 'x64\WebView2Loader.dll'),
    @('runtimes/win-x86/native/WebView2Loader.dll', 'x86\WebView2Loader.dll')
  )) {
    $targetFile = Join-Path $targetFolder $pair[1]
    New-Item -ItemType Directory -Path (Split-Path $targetFile -Parent) -Force | Out-Null
    [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($pair[0]), $targetFile, $true)
  }
} finally { $zip.Dispose() }
Write-Output $targetFolder
