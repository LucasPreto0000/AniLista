[CmdletBinding()]
param(
  [ValidateSet('none', 'core', 'ui', 'all')]
  [string]$Test = 'none',
  [switch]$Install,
  [switch]$Force
)

$ErrorActionPreference = 'Stop'
$projectRootPath = Split-Path -Parent $PSScriptRoot
$outputFolderPath = Join-Path $projectRootPath 'bin'
$buildClock = [Diagnostics.Stopwatch]::StartNew()

$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
  $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) {
  throw 'Compilador do .NET Framework nao encontrado.'
}

$references = @(
  'System.dll', 'System.Core.dll', 'System.Drawing.dll',
  'System.Windows.Forms.dll', 'System.Net.Http.dll',
  'System.Runtime.Serialization.dll', 'System.Web.Extensions.dll'
) | ForEach-Object { '/r:' + $_ }
$compilerSignature = (Get-FileHash -LiteralPath $compilerPath -Algorithm SHA256).Hash
New-Item -ItemType Directory -Path $outputFolderPath -Force | Out-Null
$webViewFolder = & (Join-Path $PSScriptRoot 'restore-webview2.ps1')
$webViewFiles = @(Get-ChildItem -LiteralPath $webViewFolder -Filter '*.dll' -Recurse -File)
$references += @('Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll') | ForEach-Object { '/r:' + (Join-Path $webViewFolder $_) }

function Get-InputSignature {
  param([string[]]$Paths, [string[]]$Options)
  $parts = @($compilerSignature) + $Options
  foreach ($inputFilePath in ($Paths | Sort-Object -Unique)) {
    $parts += $inputFilePath.Substring($projectRootPath.Length)
    $parts += (Get-FileHash -LiteralPath $inputFilePath -Algorithm SHA256).Hash
  }
  $sha = [Security.Cryptography.SHA256]::Create()
  try {
    return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($parts -join "`n")))).Replace('-', '')
  } finally {
    $sha.Dispose()
  }
}

function Build-Target {
  param([string]$Name, [string[]]$Sources, [string[]]$Inputs, [string[]]$Options)
  $targetPath = Join-Path $outputFolderPath ($Name + '.exe')
  $stampPath = Join-Path $outputFolderPath ($Name + '.build.json')
  $signature = Get-InputSignature -Paths ($Sources + $Inputs + $PSCommandPath) -Options ($Options + $references)
  $stamp = $null
  if (Test-Path -LiteralPath $stampPath) {
    try { $stamp = Get-Content -LiteralPath $stampPath -Raw | ConvertFrom-Json } catch { $stamp = $null }
  }
  if (-not $Force -and $stamp -and $stamp.input -eq $signature -and (Test-Path -LiteralPath $targetPath)) {
    if ($stamp.output -eq (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash) {
      Write-Host "$Name atualizado; compilacao reaproveitada."
      return $targetPath
    }
  }
  Write-Host "Compilando $Name..."
  & $compilerPath /nologo /optimize+ "/out:$targetPath" @Options @references @Sources | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar $Name." }
  @{
    input = $signature
    output = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
  } | ConvertTo-Json | Set-Content -LiteralPath $stampPath -Encoding UTF8
  return $targetPath
}

function Run-Tests {
  param([string]$Name)
  $sourcePath = Join-Path $projectRootPath ("tests\$Name.cs")
  $testPath = Build-Target -Name $Name -Sources @($sourcePath) -Inputs @($appPath) -Options @('/target:exe', "/r:$appPath")
  & $testPath | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "$Name falhou." }
}

Push-Location -LiteralPath $projectRootPath
try {
  $sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRootPath 'source') -Filter '*.cs' -Recurse -File | Select-Object -ExpandProperty FullName)
  $iconPath = Join-Path $projectRootPath 'AniLista.ico'
  $manifestPath = Join-Path $projectRootPath 'source\app.manifest'
  $cursors = @(Get-ChildItem -LiteralPath (Join-Path $projectRootPath 'source\assets\cursors') -File | Where-Object { $_.Extension -in @('.cur', '.ani') })
  $resources = @($cursors | ForEach-Object { '/resource:' + $_.FullName + ',AniLista.Cursors.' + $_.Name })
  $resources += @($webViewFiles | ForEach-Object { '/resource:' + $_.FullName + ',AniLista.WebView2.' + $_.FullName.Substring($webViewFolder.Length + 1).Replace('\', '.') })
  $appPath = Build-Target -Name 'AniLista' -Sources $sources -Inputs (@($iconPath, $manifestPath, (Join-Path $PSScriptRoot 'restore-webview2.ps1')) + @($cursors.FullName) + @($webViewFiles.FullName)) -Options (@('/target:winexe', "/win32icon:$iconPath", "/win32manifest:$manifestPath") + $resources)
  Copy-Item -LiteralPath (Join-Path $projectRootPath 'AniLista.exe.config') -Destination ($appPath + '.config') -Force

  if ($Test -in @('core', 'all')) { Run-Tests -Name 'CoreTests'; Run-Tests -Name 'AnitsuTests' }
  if ($Test -in @('ui', 'all')) { Run-Tests -Name 'InteractionTests' }

  if ($Install) {
    $installedPath = Join-Path $projectRootPath 'AniLista.exe'
    $runningApp = @(Get-Process AniLista -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $installedPath })
    if ($runningApp.Count -gt 0) { throw 'Feche a janela do AniLista antes de usar -Install. O resultado esta em bin\AniLista.exe.' }
    Copy-Item -LiteralPath $appPath -Destination $installedPath -Force
    Write-Host 'AniLista.exe da raiz atualizado.'
  }
  Write-Host ('Pronto em {0:N2} s. Resultado: {1}' -f $buildClock.Elapsed.TotalSeconds, $appPath)
} finally {
  Pop-Location
}
