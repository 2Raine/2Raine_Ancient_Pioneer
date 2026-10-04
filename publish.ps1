# 提交并推送到 GitHub
#
# 为什么单独一个脚本
# ----------------
# 建 GitHub 库的目的就是"方便查看历史代码"。但如果推送要靠每次记得手敲
# git push，它迟早会被忘掉 —— 那样仓库就退化成一份本地快照，建库白费。
# 所以把"改了代码"和"同步到 GitHub"绑成一条命令。
#
# 用法：
#   powershell -File publish.ps1 "这次改了什么"        # 提交 + 推送
#   powershell -File publish.ps1 "..." -NoPush        # 只提交，不推送
#   powershell -File publish.ps1 -Status              # 只看状态
#
# 前置：远程库必须已存在。首次建库见 README。

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Message,

    [switch]$NoPush,
    [switch]$Status
)

$ErrorActionPreference = 'Continue'

$RepoRoot = $PSScriptRoot
$Remote   = 'origin'
$Branch   = 'main'

Push-Location $RepoRoot
try {
    Write-Host ''
    Write-Host '────────────────────────────────────────────────────────' -ForegroundColor Cyan
    Write-Host '  模组仓库 → GitHub' -ForegroundColor Cyan
    Write-Host '────────────────────────────────────────────────────────' -ForegroundColor Cyan
    Write-Host ''

    # ── 远程与状态 ──────────────────────────────────────────────────────────
    $remoteUrl = (& git remote get-url $Remote 2>&1 | Out-String).Trim()
    $hasRemote = ($LASTEXITCODE -eq 0) -and (-not [string]::IsNullOrWhiteSpace($remoteUrl))
    if ($hasRemote) {
        Write-Host "远程: $remoteUrl" -ForegroundColor DarkGray
    }

    $changes = (& git status --short 2>&1 | Out-String).Trim()

    if ($Status) {
        Write-Host ''
        Write-Host "分支: $Branch"
        if ($hasRemote) {
            $ahead = (& git rev-list --count "$Remote/$Branch..$Branch" 2>&1 | Out-String).Trim()
            Write-Host "未推送的提交: $ahead"
        } else {
            Write-Host '未推送的提交: 未知（还没配置远程）' -ForegroundColor Yellow
        }
        Write-Host ''
        if ([string]::IsNullOrWhiteSpace($changes)) {
            Write-Host '工作区干净' -ForegroundColor Green
        } else {
            Write-Host '未提交的改动:' -ForegroundColor Yellow
            $changes -split "`n" | Where-Object { $_.Trim() } | ForEach-Object { Write-Host "  $_" }
        }
        # 状态查询不因缺少远程而失败 —— 它恰恰是用来诊断这件事的。
        exit 0
    }

    # ── 提交（永远先做，不需要远程）─────────────────────────────────────────
    #
    # 顺序很重要：本地提交不能依赖远程是否存在。否则在建库之前，
    # 每一次改动都进不了历史 —— 那正是这个脚本要防的事情。
    if (-not [string]::IsNullOrWhiteSpace($changes)) {
        if ([string]::IsNullOrWhiteSpace($Message)) {
            Write-Host '✗ 有未提交的改动，但没给提交说明。' -ForegroundColor Red
            Write-Host '  用法: powershell -File publish.ps1 "这次改了什么"'
            Write-Host ''
            $changes -split "`n" | Where-Object { $_.Trim() } | ForEach-Object { Write-Host "  $_" }
            exit 1
        }
        & git add -A | Out-Null
        & git commit -q -m $Message
        if ($LASTEXITCODE -ne 0) {
            Write-Host '✗ 提交失败' -ForegroundColor Red
            exit 1
        }
        $short = (& git rev-parse --short HEAD 2>&1 | Out-String).Trim()
        Write-Host "已提交 $short  $Message" -ForegroundColor Green
    } else {
        Write-Host '工作区干净，没有新改动需要提交。' -ForegroundColor DarkGray
    }

    if ($NoPush) {
        Write-Host '（-NoPush：跳过推送）' -ForegroundColor DarkGray
        exit 0
    }

    # ── 推送（这里才需要远程）───────────────────────────────────────────────
    if (-not $hasRemote) {
        Write-Host ''
        Write-Host "✗ 没有配置远程 '$Remote'，本地改动已提交但无法推送。" -ForegroundColor Red
        Write-Host ''
        Write-Host '  首次使用需要先建库并配置：'
        Write-Host '    1) 在 https://github.com/new 建一个【空】库（不要勾 README / .gitignore / license）'
        Write-Host '    2) 回到这里执行：'
        Write-Host '       git remote add origin git@github.com:2Raine/2Raine_Ancient_Pioneer.git'
        Write-Host '       powershell -File publish.ps1 "首次推送"'
        Write-Host ''
        exit 1
    }

    Write-Host ''
    Write-Host '正在推送...' -ForegroundColor Cyan

    # git 把进度写到 stderr（"To github.com:... / * [new branch] ..."）。
    # 直接 `git push 2>&1` 会让 PowerShell 把这些行包成 ErrorRecord 并染红打印，
    # 看起来像失败 —— 而且 $ErrorActionPreference='Stop' 时还会在推送【成功】
    # 之后抛异常、exit 1。
    #
    # 所以把两个流分别重定向到临时文件：PowerShell 就不会把子进程的 stderr
    # 当错误，成败只看进程退出码。
    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()
    $pushCode = 1
    try {
        $proc = Start-Process -FilePath 'git' `
            -ArgumentList @('push', $Remote, $Branch) `
            -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $outFile -RedirectStandardError $errFile
        $pushCode = $proc.ExitCode

        foreach ($stream in @($outFile, $errFile)) {
            if (Test-Path $stream) {
                Get-Content $stream -Encoding UTF8 | Where-Object { $_.Trim() } |
                    ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
            }
        }
    }
    finally {
        Remove-Item $outFile, $errFile -Force -ErrorAction SilentlyContinue
    }

    if ($pushCode -ne 0) {
        Write-Host ''
        Write-Host '✗ 推送失败：' -ForegroundColor Red
        Write-Host ''
        Write-Host '  常见原因：' -ForegroundColor Yellow
        Write-Host '    - 远程库还没建（去 https://github.com/new）'
        Write-Host '    - 端口 22 被挡（~/.ssh/config 已配置走 ssh.github.com:443）'
        Write-Host '    - 远程有本地没有的提交（先 git pull --rebase）'
        exit 1
    }

    $head = (& git log --oneline -1 2>&1 | Out-String).Trim()
    Write-Host ''
    Write-Host "✓ 已推送到 $Remote/$Branch" -ForegroundColor Green
    Write-Host "  最新: $head"
    Write-Host ''
}
finally {
    Pop-Location
}
