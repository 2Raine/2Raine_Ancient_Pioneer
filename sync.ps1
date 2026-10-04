# 2Raine_Ancient_Pioneer —— 工作区 <-> 游戏模组目录 双向同步
#
# 为什么要这个脚本
# ----------------
# 游戏只从它自己的 Mods 目录加载模组，而源码要放进 git 仓库。所以两边各有一份，
# 必须有一个明确的"谁是准的"。
#
#   push  = 以【工作区】为准，覆盖游戏目录   （改完代码要走这个，然后重启游戏）
#   pull  = 以【游戏目录】为准，覆盖工作区   （在游戏目录里临时试改过之后用）
#   diff  = 只报告差异，不动任何文件          （不确定该往哪边同步时先跑这个）
#
# 绝不复制 *.dll / *.pdb：那是游戏编译 Scripts\*.cs 之后写到
# %USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\ModAssemblies\ 的产物，
# 不是源码。仓库里也不该有它们（见 .gitignore）。
#
# 用法：
#   pwsh -File sync.ps1 diff
#   pwsh -File sync.ps1 push
#   pwsh -File sync.ps1 pull

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('diff', 'push', 'pull', 'status')]
    [string]$Action = 'diff'
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- 路径
$RepoRoot = $PSScriptRoot
$Source   = Join-Path $RepoRoot 'mod\Toncihana_Elemental'

$GameMods = Join-Path $env:USERPROFILE 'AppData\LocalLow\Freehold Games\CavesOfQud\Mods'
$Target   = Join-Path $GameMods 'Toncihana_Elemental'

# 这些永远不同步：它们是编译产物或编辑器杂物，不是源码
$ExcludePatterns = @('*.dll', '*.pdb', '*.mdb', '*.log', '*.bak', '*.orig', '*.swp', '*~')

function Test-Excluded([string]$Name) {
    foreach ($p in $ExcludePatterns) {
        if ($Name -like $p) { return $true }
    }
    return $false
}

function Get-SourceFiles([string]$Root) {
    if (-not (Test-Path $Root)) { return @() }
    Get-ChildItem $Root -Recurse -File |
        Where-Object { -not (Test-Excluded $_.Name) } |
        ForEach-Object { $_.FullName.Substring($Root.Length + 1) }
}

# ---------------------------------------------------------------- 前置检查
if (-not (Test-Path $Source)) {
    Write-Host "找不到工作区源码目录：" -ForegroundColor Red
    Write-Host "  $Source"
    exit 1
}
if (-not (Test-Path $Target)) {
    Write-Host "找不到游戏模组目录：" -ForegroundColor Red
    Write-Host "  $Target"
    Write-Host "（模组还没被游戏加载过？先确认它装在 $GameMods 下）"
    exit 1
}

Write-Host "工作区  : $Source"
Write-Host "游戏目录: $Target"
Write-Host ""

# ---------------------------------------------------------------- 比对
$srcFiles = Get-SourceFiles $Source
$dstFiles = Get-SourceFiles $Target

$onlyInSource = @($srcFiles | Where-Object { $dstFiles -notcontains $_ })
$onlyInTarget = @($dstFiles | Where-Object { $srcFiles -notcontains $_ })

$changed = @()
foreach ($rel in ($srcFiles | Where-Object { $dstFiles -contains $_ })) {
    $a = Join-Path $Source $rel
    $b = Join-Path $Target $rel
    $ha = (Get-FileHash $a -Algorithm SHA256).Hash
    $hb = (Get-FileHash $b -Algorithm SHA256).Hash
    if ($ha -ne $hb) { $changed += $rel }
}

# 不在任何一边出现的"孤儿"产物，只提示不处理
$artifacts = @()
if (Test-Path $Target) {
    $artifacts = Get-ChildItem $Target -Recurse -File |
        Where-Object { Test-Excluded $_.Name } |
        ForEach-Object { $_.FullName.Substring($Target.Length + 1) }
}

# ---------------------------------------------------------------- 报告
$total = $onlyInSource.Count + $onlyInTarget.Count + $changed.Count
if ($total -eq 0) {
    Write-Host "两边一致，没有需要同步的源码。" -ForegroundColor Green
} else {
    if ($onlyInSource.Count) {
        Write-Host "只在【工作区】有（$($onlyInSource.Count)）：" -ForegroundColor Yellow
        $onlyInSource | ForEach-Object { Write-Host "  + $_" }
    }
    if ($onlyInTarget.Count) {
        Write-Host "只在【游戏目录】有（$($onlyInTarget.Count)）：" -ForegroundColor Yellow
        $onlyInTarget | ForEach-Object { Write-Host "  - $_" }
    }
    if ($changed.Count) {
        Write-Host "内容不同（$($changed.Count)）：" -ForegroundColor Yellow
        $changed | ForEach-Object { Write-Host "  ~ $_" }
    }
}
Write-Host ""
if ($artifacts.Count) {
    Write-Host "游戏目录里的产物（脚本不碰、git 也不收）：" -ForegroundColor DarkGray
    $artifacts | ForEach-Object { Write-Host "  . $_" -ForegroundColor DarkGray }
    Write-Host ""
}

if ($Action -eq 'diff' -or $Action -eq 'status') {
    if ($total -gt 0) {
        Write-Host "要同步请选一边：push（工作区 -> 游戏）或 pull（游戏 -> 工作区）"
    }
    exit 0
}

if ($total -eq 0) { exit 0 }

# ---------------------------------------------------------------- 同步
if ($Action -eq 'push') {
    $from = $Source
    $to   = $Target
    Write-Host "push：工作区 -> 游戏目录" -ForegroundColor Cyan
} else {
    $from = $Target
    $to   = $Source
    Write-Host "pull：游戏目录 -> 工作区" -ForegroundColor Cyan
}

$copied = 0
foreach ($rel in (Get-SourceFiles $from)) {
    $a = Join-Path $from $rel
    $b = Join-Path $to $rel
    $dir = Split-Path $b -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

    $need = $true
    if (Test-Path $b) {
        if ((Get-FileHash $a -Algorithm SHA256).Hash -eq (Get-FileHash $b -Algorithm SHA256).Hash) {
            $need = $false
        }
    }
    if ($need) {
        Copy-Item $a $b -Force
        Write-Host "  -> $rel"
        $copied++
    }
}

Write-Host ""
Write-Host "复制了 $copied 个文件。" -ForegroundColor Green

# 反向独有的文件不会被删除，只提示 —— 静默删除源码是危险的
$leftover = if ($Action -eq 'push') { $onlyInTarget } else { $onlyInSource }
if ($leftover.Count) {
    Write-Host ""
    Write-Host "注意：下面这些文件在目标端没有对应来源，脚本没有删除它们：" -ForegroundColor Yellow
    $leftover | ForEach-Object { Write-Host "  ? $_" }
    Write-Host "确认不需要之后请手工删除。"
}

if ($Action -eq 'push') {
    Write-Host ""
    Write-Host "改了 .cs 必须【完全重启】游戏才会重新编译。" -ForegroundColor Yellow
    Write-Host "只改 XML 可以在游戏里 wish reload。" -ForegroundColor Yellow
}
