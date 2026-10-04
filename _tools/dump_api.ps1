# Dumps type/member metadata from Assembly-CSharp.dll using reflection-only loading.
# Output goes to an ASCII-only temp path to avoid codepage mangling.
$ErrorActionPreference = 'Continue'

$Dll = 'D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll'
$OutDir = Join-Path $env:TEMP 'qud_api'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Out = Join-Path $OutDir 'api_types.txt'

$asm = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($Dll)

$types = $null
try {
    $types = $asm.GetTypes()
}
catch [System.Reflection.ReflectionTypeLoadException] {
    $types = $_.Exception.Types | Where-Object { $_ -ne $null }
    Write-Host "ReflectionTypeLoadException; recovered $($types.Count) types"
}

$sb = New-Object System.Text.StringBuilder

function SafeName($t) {
    if ($null -eq $t) { return '<null>' }
    try { return $t.FullName } catch { return '<unloadable>' }
}

foreach ($t in $types) {
    if ($null -eq $t) { continue }
    $kind = if ($t.IsInterface) { 'interface' } elseif ($t.IsEnum) { 'enum' } elseif ($t.IsValueType) { 'struct' } else { 'class' }
    $base = SafeName $t.BaseType
    $ifs = @()
    try { foreach ($i in $t.GetInterfaces()) { $ifs += (SafeName $i) } } catch { }
    [void]$sb.AppendLine("=== TYPE $kind $(SafeName $t) : $base :: $($ifs -join ', ')")
    try {
        $flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
        foreach ($m in $t.GetMembers($flags)) {
            $ms = ''
            try { $ms = $m.ToString() } catch { $ms = '<err>' }
            if ($m.MemberType -eq 'Field') {
                [void]$sb.AppendLine("    F $ms")
            }
            elseif ($m.MemberType -eq 'Method') {
                [void]$sb.AppendLine("    M $ms")
            }
            elseif ($m.MemberType -eq 'Constructor') {
                [void]$sb.AppendLine("    C $ms")
            }
            else {
                [void]$sb.AppendLine("    $($m.MemberType) $ms")
            }
        }
    }
    catch { [void]$sb.AppendLine("    <member error: $($_.Exception.Message)>") }
}

[System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "types=$($types.Count) chars=$($sb.Length) -> $Out"
