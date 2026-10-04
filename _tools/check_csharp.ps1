# Type-check the Toncihana mod's C# against the real game assemblies.
# The game compiles the mod itself at runtime; this exists only to surface API mistakes early.
$ErrorActionPreference = 'Continue'

$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$managed = 'D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed'
$scripts = Join-Path $env:USERPROFILE 'AppData\LocalLow\Freehold Games\CavesOfQud\Mods\Toncihana_Elemental\Scripts'

# Output must not be keyed on $env:TEMP: Reasonix redefines TEMP per session
# (...\Temp\reasonix-session-tmp-<id>), where qud_api does not exist, so Out-File
# fails on a missing directory. LOCALAPPDATA\Temp is the same path under both hosts,
# and we create it rather than assume it is already there.
$qapi = Join-Path (Join-Path $env:LOCALAPPDATA 'Temp') 'qud_api'
if (-not (Test-Path $qapi)) { New-Item -ItemType Directory -Force -Path $qapi | Out-Null }

$out = Join-Path $qapi 'toncihana_check.dll'

# System.dll / System.Core.dll are already pulled in implicitly by csc from the framework
# directory; referencing the game's copies as well trips CS1703 (duplicate identity).
$refs = @(
    'Assembly-CSharp.dll',
    '0Harmony.dll',
    'UnityEngine.dll',
    'UnityEngine.CoreModule.dll',
    'netstandard.dll',
    'Newtonsoft.Json.dll'
) | ForEach-Object { Join-Path $managed $_ } | Where-Object { Test-Path $_ }

$files = Get-ChildItem $scripts -Filter *.cs | ForEach-Object { $_.FullName }

$cscArgs = @('/nologo', '/target:library', '/langversion:5', ('/out:' + $out))
foreach ($r in $refs) { $cscArgs += ('/reference:' + $r) }
$cscArgs += $files

Write-Host "compiling $($files.Count) files with $($refs.Count) references"
$cscArgs += '/utf8output'
$log = Join-Path $qapi 'csc_out.txt'
& "$fw\csc.exe" $cscArgs 2>&1 | Out-File -FilePath $log -Encoding utf8
Write-Host "exit=$LASTEXITCODE  log=$log"
