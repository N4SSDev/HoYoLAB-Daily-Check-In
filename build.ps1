# Compilation. Aucune dependance : csc.exe fait partie de Windows.
#   .\build.ps1          -> compile l'application dans out\
#   .\build.ps1 -Test    -> compile et execute la suite de tests
[CmdletBinding()]
param([switch]$Test)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out  = Join-Path $root 'out'
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) { throw "csc.exe introuvable : $csc" }
if (-not (Test-Path $out)) { New-Item -ItemType Directory $out | Out-Null }

# Assemblies du .NET Framework, toutes livrees avec Windows.
$refs = @(
  '/r:System.dll'
  '/r:System.Core.dll'
  '/r:System.Web.Extensions.dll'   # JavaScriptSerializer
  '/r:System.Windows.Forms.dll'    # fenetre + notification
  '/r:System.Drawing.dll'
  '/r:Microsoft.CSharp.dll'      # liaison tardive COM (dynamic)
)

function Invoke-Csc {
  param([string[]]$Sources, [string]$Target, [string]$OutFile, [string[]]$Extra = @())
  $args = @('/nologo', "/target:$Target", '/optimize+', '/platform:x64',
            '/warnaserror+', '/nowarn:1701,1702', "/out:$OutFile") + $refs + $Extra + $Sources
  $log = & $csc @args 2>&1
  if ($LASTEXITCODE -ne 0) { $log | ForEach-Object { Write-Host $_ }; throw "compilation echouee" }
}

$src  = Get-ChildItem (Join-Path $root 'src') -Filter *.cs -Recurse | ForEach-Object FullName
$icon = Join-Path $root 'assets\icon.ico'
$exe  = Join-Path $out 'DailyCheckIn.exe'

# `winexe` : pas de console qui clignote quand la tache planifiee le lance.
$extra = @("/win32icon:$icon", '/define:RELEASE')
$sw = [Diagnostics.Stopwatch]::StartNew()
Invoke-Csc -Sources $src -Target 'winexe' -OutFile $exe -Extra $extra
$sw.Stop()

$size = (Get-Item $exe).Length
Write-Host ("build : {0} en {1} ms  ({2:N0} octets)" -f (Split-Path $exe -Leaf), $sw.ElapsedMilliseconds, $size)

if ($Test) {
  # @() force des tableaux : sans cela PowerShell concatene deux chaines.
  # /main designe le point d'entree, ce qui evite d'exclure Program.cs dont
  # d'autres fichiers dependent.
  $testSrc = @(Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object FullName) + @($src)
  $testExe = Join-Path $out 'Tests.exe'
  Invoke-Csc -Sources $testSrc -Target 'exe' -OutFile $testExe -Extra @('/main:DailyCheckIn.Tests')
  Write-Host ''
  & $testExe
  if ($LASTEXITCODE -ne 0) { throw "tests en echec" }
}

