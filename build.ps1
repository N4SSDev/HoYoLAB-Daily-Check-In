[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out  = Join-Path $root 'out'
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) { throw "csc.exe introuvable : $csc" }
if (-not (Test-Path $out)) { New-Item -ItemType Directory $out | Out-Null }

$refs = @(
  '/r:System.dll'
  '/r:System.Core.dll'
  '/r:System.Web.Extensions.dll'
  '/r:System.Windows.Forms.dll'
  '/r:System.Drawing.dll'
  '/r:Microsoft.CSharp.dll'
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

$extra = @("/win32icon:$icon", '/define:RELEASE')
$sw = [Diagnostics.Stopwatch]::StartNew()
Invoke-Csc -Sources $src -Target 'winexe' -OutFile $exe -Extra $extra
$sw.Stop()

$size = (Get-Item $exe).Length
Write-Host ("build : {0} en {1} ms  ({2:N0} octets)" -f (Split-Path $exe -Leaf), $sw.ElapsedMilliseconds, $size)
