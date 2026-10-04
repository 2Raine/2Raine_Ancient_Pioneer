# Caves of Qud 模组制作 —— 开工前预检与交付前自检
#
# 这个脚本不是"检查代码"的，它检查的是【流程有没有被遵守】。
# 它无法判断我看没看懂教程，但它能把"该读的文件、该跑的校验、该看的日志"
# 全部摆到台面上，让跳过任何一步都变成显式的选择，而不是遗忘。
#
# 用法：
#   pwsh -File preflight.ps1 before    # 动手前：列出必读材料 + 打印其指纹
#   pwsh -File preflight.ps1 after     # 交付前：跑全部校验，输出结论
#   pwsh -File preflight.ps1 log       # 查看错误日志（最近的错误记录）
#   pwsh -File preflight.ps1 mistake "一句话描述这次错误"   # 记一次错误

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('before', 'after', 'log', 'mistake', 'help')]
    [string]$Action = 'help',

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Continue'

$WS   = 'D:\caves of qud 模组制作'
$REPO = Join-Path $WS '2Raine_Ancient_Pioneer'
$MOD  = Join-Path $REPO 'mod\Toncihana_Elemental'
$GAME = Join-Path $env:USERPROFILE 'AppData\LocalLow\Freehold Games\CavesOfQud'
$PY   = 'C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'
$TOOLS = Join-Path $WS '_tools'
$MISTAKES = Join-Path $WS '错误日志.md'

# 必读材料：动代码之前必须看过的东西
$Required = @(
    @{ Path = (Join-Path $WS 'AGENTS.md');
       Why  = '工作流与铁律（自动注入，但这里是权威副本）' },
    @{ Path = (Join-Path $WS 'Caves of Qud 模组制作入门指南.md');
       Why  = 'Wiki 整理教程，含大量更正标注' },
    @{ Path = (Join-Path $WS 'Qud机制数据库_使用说明.md');
       Why  = 'qud.py 的完整用法' },
    @{ Path = (Join-Path $WS 'Toncihana_制作笔记与调参参考.md');
       Why  = '本模组的设计记录与踩坑，改数值前必读' }
)

function Get-Fingerprint([string]$Path) {
    if (-not (Test-Path $Path)) { return '缺失' }
    $h = (Get-FileHash $Path -Algorithm SHA256).Hash.Substring(0, 12)
    $t = (Get-Item $Path).LastWriteTime.ToString('MM-dd HH:mm')
    $s = (Get-Item $Path).Length
    return "$h  $t  $($s)B"
}

function Show-Before {
    Write-Host ''
    Write-Host '════════════════════════════════════════════════════════════════' -ForegroundColor Cyan
    Write-Host '  开工前预检 —— 动代码之前必须完成的事' -ForegroundColor Cyan
    Write-Host '════════════════════════════════════════════════════════════════' -ForegroundColor Cyan
    Write-Host ''

    Write-Host '【1】必读材料（读完再动手）' -ForegroundColor Yellow
    foreach ($r in $Required) {
        $fp = Get-Fingerprint $r.Path
        Write-Host ("  [ ] {0}" -f (Split-Path $r.Path -Leaf))
        Write-Host ("      作用: {0}" -f $r.Why) -ForegroundColor DarkGray
        Write-Host ("      指纹: {0}" -f $fp) -ForegroundColor DarkGray
    }
    Write-Host ''

    Write-Host '【2】这次要用的机制，先查过吗' -ForegroundColor Yellow
    Write-Host '  [ ] 教程里搜过关键词 → 记下 文件:行号' -ForegroundColor Gray
    Write-Host '  [ ] qud.py 查过部件/事件/属性 → 记下命令与输出' -ForegroundColor Gray
    Write-Host '  [ ] 原版怎么做的 → 搜 qud_src，记下"多少处、写法是否一致"' -ForegroundColor Gray
    Write-Host '  [ ] 读过程序集源码里对应的方法体' -ForegroundColor Gray
    Write-Host ''
    Write-Host '  ⚠ 若以上任一条答不上来，就是"准备用推断代替查证"的信号。停下。' -ForegroundColor Red
    Write-Host ''

    Write-Host '【3】坐标/命名/结构类问题，先核对这三条铁律' -ForegroundColor Yellow
    Write-Host '  [ ] 区域是 80x25，左上角 (0,0)，直接 GetCell(x, y)，不写换算函数' -ForegroundColor Gray
    Write-Host '  [ ] 部件必须放在 XRL.World.Parts，XML 里写裸名（前缀是无条件拼接的）' -ForegroundColor Gray
    Write-Host '  [ ] <anatomy Category=> 必须是 BodyPartCategory 的合法值' -ForegroundColor Gray
    Write-Host ''

    Write-Host '【4】本轮是否有未经验证的推断' -ForegroundColor Yellow
    Write-Host '  [ ] 有 → 在回复里明确标注"未验证"，不要说得像事实' -ForegroundColor Gray
    Write-Host ''
}

function Show-After {
    Write-Host ''
    Write-Host '════════════════════════════════════════════════════════════════' -ForegroundColor Cyan
    Write-Host '  交付前自检' -ForegroundColor Cyan
    Write-Host '════════════════════════════════════════════════════════════════' -ForegroundColor Cyan
    Write-Host ''

    $pass = 0; $fail = 0

    # 1) 同步
    Write-Host '【1】仓库 → 游戏目录 同步' -ForegroundColor Yellow
    $diff = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $REPO 'sync.ps1') diff 2>&1 | Out-String
    if ($diff -match '两边一致') {
        Write-Host '  [OK] 两边一致' -ForegroundColor Green; $pass++
    } else {
        Write-Host '  [!!] 有差异 —— 先跑 sync.ps1 push，否则游戏跑的还是旧代码' -ForegroundColor Red
        ($diff -split "`n") | Where-Object { $_ -match '^\s+[+~-]' } | Select-Object -First 8 |
            ForEach-Object { Write-Host "       $_" -ForegroundColor DarkGray }
        $fail++
    }
    Write-Host ''

    # 2) 编译
    Write-Host '【2】C# 独立编译' -ForegroundColor Yellow
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $TOOLS 'check_csharp.ps1') 2>&1 | Out-String
    $log = Join-Path $env:TEMP 'qud_api\csc_out.txt'
    $errs = @()
    if (Test-Path $log) {
        $errs = Get-Content $log -Encoding UTF8 | Where-Object { $_ -match ': error ' }
    }
    if ($out -match 'exit=0' -and $errs.Count -eq 0) {
        Write-Host '  [OK] exit=0，0 个 error' -ForegroundColor Green; $pass++
    } else {
        Write-Host '  [!!] 编译未通过' -ForegroundColor Red
        $errs | Select-Object -First 10 | ForEach-Object { Write-Host "       $_" -ForegroundColor DarkGray }
        $fail++
    }
    Write-Host ''

    # 3) 两个校验器
    Write-Host '【3】XML / 引用校验' -ForegroundColor Yellow
    foreach ($t in @(@{f='validate_mod.py'; ok='all checks passed'; name='validate_mod'},
                     @{f='audit_references.py'; ok='no unresolved references found'; name='audit_references'})) {
        $r = & $PY (Join-Path $TOOLS $t.f) 2>&1 | Out-String
        if ($r -match [regex]::Escape($t.ok)) {
            Write-Host ("  [OK] {0}" -f $t.name) -ForegroundColor Green; $pass++
        } else {
            Write-Host ("  [!!] {0} 未通过" -f $t.name) -ForegroundColor Red
            ($r -split "`n") | Where-Object { $_ -match '!!|PROBLEM' } | Select-Object -First 8 |
                ForEach-Object { Write-Host "       $_" -ForegroundColor DarkGray }
            $fail++
        }
    }
    Write-Host ''

    # 4) 游戏日志
    Write-Host '【4】游戏日志（最近一次实测）' -ForegroundColor Yellow
    $plog = Join-Path $GAME 'Player.log'
    if (Test-Path $plog) {
        $age = (Get-Date) - (Get-Item $plog).LastWriteTime
        Write-Host ("  日志时间: {0}（{1:N0} 分钟前）" -f (Get-Item $plog).LastWriteTime, $age.TotalMinutes) -ForegroundColor DarkGray
        $mine = Get-Content $plog -Encoding UTF8 | Select-String -Pattern '\[Toncihana\]|MODERROR.*Storm-Caller'
        if ($mine) {
            $mine | Select-Object -Last 6 | ForEach-Object { Write-Host ("       " + $_.Line.Trim()) -ForegroundColor DarkGray }
        } else {
            Write-Host '  [--] 日志里没有本模组的输出（改完 .cs 是否完全重启过游戏？）' -ForegroundColor Yellow
        }
    }
    Write-Host ''

    # 5) git
    Write-Host '【5】未提交的改动' -ForegroundColor Yellow
    Push-Location $REPO
    $st = & git status --short 2>&1 | Out-String
    Pop-Location
    if ([string]::IsNullOrWhiteSpace($st)) {
        Write-Host '  [OK] 工作区干净' -ForegroundColor Green; $pass++
    } else {
        Write-Host '  [--] 有未提交改动（交付前应提交）' -ForegroundColor Yellow
        ($st -split "`n") | Where-Object { $_.Trim() } | Select-Object -First 8 |
            ForEach-Object { Write-Host "       $_" -ForegroundColor DarkGray }
    }
    Write-Host ''

    Write-Host '════════════════════════════════════════════════════════════════' -ForegroundColor Cyan
    if ($fail -eq 0) {
        Write-Host ("  结论：全部通过（{0} 项）" -f $pass) -ForegroundColor Green
    } else {
        Write-Host ("  结论：{0} 项通过，{1} 项未通过 —— 不要声称已完成" -f $pass, $fail) -ForegroundColor Red
    }
    Write-Host '════════════════════════════════════════════════════════════════' -ForegroundColor Cyan
    Write-Host ''
    if ($fail -gt 0) { exit 1 }
}

function Show-Mistake([string[]]$Words) {
    $text = ($Words -join ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        Write-Host '  用法: preflight.ps1 mistake "一句话描述这次错误"' -ForegroundColor Yellow
        return
    }
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm'
    if (-not (Test-Path $MISTAKES)) {
        $head = @"
# 错误日志

**规则：每次犯错，立刻在这里追加一条。** 不是事后总结，是当场记录。
写完再去改 ``AGENTS.md`` —— 如果这条错误暴露了流程漏洞。

格式：日期 | 错误 | 我当时的（错误）假设 | 真相 | 依据
"@
        [System.IO.File]::WriteAllText($MISTAKES, $head, [System.Text.UTF8Encoding]::new($false))
    }
    Add-Content -Path $MISTAKES -Value "`n## $stamp`n`n$text`n" -Encoding UTF8
    Write-Host "  已记入 $MISTAKES" -ForegroundColor Green
}

function Show-Log {
    if (-not (Test-Path $MISTAKES)) {
        Write-Host '  还没有错误记录。' -ForegroundColor DarkGray
        return
    }
    Get-Content $MISTAKES -Encoding UTF8 | ForEach-Object { "  $_" }
}

function Show-Help {
    Write-Host ''
    Write-Host '  preflight.ps1 before    开工前：列出必读材料与铁律清单'
    Write-Host '  preflight.ps1 after     交付前：跑全部校验并给结论（不通过则 exit 1）'
    Write-Host '  preflight.ps1 log       查看错误日志'
    Write-Host '  preflight.ps1 mistake "..."   记一次错误'
    Write-Host ''
}

switch ($Action) {
    'before'  { Show-Before }
    'after'   { Show-After }
    'log'     { Show-Log }
    'mistake' { Show-Mistake $Rest }
    default   { Show-Help }
}
