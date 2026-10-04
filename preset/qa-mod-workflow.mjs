/**
 * qa-mod-workflow —— Caves of Qud 模组制作的强制工作流门禁。
 *
 * 前身只做一件事：往系统提示词里塞一段规则文本。它可以被我无视，
 * 因为它不参与任何决策。这一版把同一套规则接到引擎真正的决策点上：
 *
 *   1. system-prompt section  —— 每轮装配时重算的实时状态（不是一段死文本）
 *   2. 三个工具               —— workflow_status / workflow_plan / workflow_mistake
 *   3. ctx.tools.guard()      —— 单调守卫，改受保护文件前必须先交计划回执
 *
 * 为什么用 guard 而不是 tools/pre-execute：
 * guard 是单调的 —— 任何后续监听器都不能把一次拒绝翻回放行。pre-execute
 * 是可重排的瀑布，后面的监听器可以覆盖前面的决定。
 *
 * 回执为什么带指纹、又为什么按会话绑定：
 * 回执记下当时读过的必读材料的 SHA256。材料一改（尤其是错误日志变长），
 * 回执自动失效，必须重读。换个会话也是一次全新的开始，不继承旧回执。
 * 这就是"下次避开同样的错误"的实现方式 —— 不是靠我记得，而是靠旧回执
 * 对不上新文件、也对不上新会话。
 *
 * 门禁状态写在 harness home（~/.dsh/dsh-qa-mod-workflow/<工作区哈希>/），
 * 不写进工作区 —— 运行时产物不该污染仓库，也不该出现在每次 git status 里。
 *
 * 回归测试：`node _tools/qa-mod-workflow.smoke.mjs`（35 项，覆盖放行/拦截/
 * 失效/跨会话/跨工作区/禁用开关）。改这个文件后必须重跑。
 *
 * 零外部 import：相对预设行按用户主目录解析裸标识符，@deepseek-ai/* 不在那里。
 */

import { createHash } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync, appendFileSync } from 'node:fs'
import { join, resolve, sep } from 'node:path'
import { tmpdir } from 'node:os'

/** Cordis 插件名，供 loader 诊断使用。 */
export const name = 'qa-mod-workflow'

/** 提示词装配与工具注册必须存在。 */
export const inject = ['systemPrompt', 'tools']

const STATUS_TOOL = 'workflow_status'
const PLAN_TOOL = 'workflow_plan'
const MISTAKE_TOOL = 'workflow_mistake'

const EDIT_TOOLS = new Set([
  'write', 'edit', 'str_replace_editor', 'apply_patch', 'create_file', 'notebook_edit',
])

const SHELL_WRITE_PATTERN = /(^|[^0-9<])>(?!&)|out-file|set-content|add-content|new-item|copy-item|move-item|remove-item|set-itemproperty|tee-object|\btee\b|\[System\.IO\.File\]::WriteAll|\.WriteAllText|\.WriteAllBytes/i

const DEFAULT_CONFIG = {
  enabled: true,
  workspace: 'D:\\caves of qud 模组制作',
  protectedPrefixes: ['mod\\', 'sync.ps1', 'publish.ps1', '_tools\\'],
  planTtlMinutes: 240,
  requireGitClean: true,
  gitTimeoutMs: 20000,
}

const MATERIALS_HEADER = '## 必读材料（机器读，改这一节就改了门禁）'

/**
 * 错误日志也算一份"材料"。它是唯一一份会自己长大的：一旦追加了新错误，
 * 旧回执必须作废 —— 这正是"犯错后下次必须重读"的落地点。
 */
const MISTAKES_NAME = '错误日志.md'

/** 注：守卫是同步的，所以这里全部用同步文件 IO。缓存按 mtime+size 失效。 */
export function apply(ctx, config) {
  const cfg = { ...DEFAULT_CONFIG, ...(config || {}) }
  if (cfg.enabled === false) return

  const WS = resolve(cfg.workspace)
  const AGENTS_MD = join(WS, 'AGENTS.md')
  const MISTAKES_MD = join(WS, '错误日志.md')

  /**
   * 门禁状态放在 harness home 下，不放进工作区 —— 运行时产物不该污染用户的仓库，
   * 也不该出现在每次 git status 里。每个工作区一个目录。
   */
  const HOME = process.env.DSH_HOME
    || (process.env.USERPROFILE ? join(process.env.USERPROFILE, '.dsh') : null)
    || (process.env.HOME ? join(process.env.HOME, '.dsh') : null)
  const STATE_DIR = HOME === null
    ? join(tmpdir(), 'dsh-qa-mod-workflow')
    : join(HOME, 'dsh-qa-mod-workflow')
  const WS_KEY = createHash('sha256').update(WS.toLowerCase()).digest('hex').slice(0, 12)
  const WS_STATE = join(STATE_DIR, WS_KEY)
  const RECEIPT = join(WS_STATE, 'receipt.json')
  const LEDGER = join(WS_STATE, 'plans.jsonl')
  const TOUCHED = join(WS_STATE, 'touched.json')
  try {
    mkdirSync(WS_STATE, { recursive: true })
  } catch { /* 建不出来就退化成"永远无回执"，宁可拦错也不静默放行 */ }

  const agents = new Map()
  const fingerprints = new Map()
  const gitCache = new Map()
  let materialsCache = null

  function readJson(file, fallback) {
    try {
      return JSON.parse(readFileSync(file, 'utf8'))
    } catch {
      return fallback
    }
  }

  function materialFingerprint(file) {
    let st
    try {
      st = statSync(file)
    } catch {
      fingerprints.delete(file)
      return 'MISSING'
    }
    const key = `${st.mtimeMs}:${st.size}`
    const hit = fingerprints.get(file)
    if (hit !== undefined && hit.key === key) return hit.value
    let value
    try {
      value = createHash('sha256').update(readFileSync(file)).digest('hex').slice(0, 16)
    } catch {
      value = 'UNREADABLE'
    }
    fingerprints.set(file, { key, value })
    return value
  }

  /**
   * 必读清单来自 AGENTS.md 自己的一个 markdown 表格 —— 改文件就改门禁，
   * 不需要动插件代码。解析不出来时退回内置清单（fail open，绝不让门禁
   * 因为格式变化而把工作区锁死）。
   */
  function materials() {
    const fp = materialFingerprint(AGENTS_MD)
    if (materialsCache !== null && materialsCache.fp === fp) return materialsCache.rows
    const rows = []
    if (fp !== 'MISSING' && fp !== 'UNREADABLE') {
      try {
        const text = readFileSync(AGENTS_MD, 'utf8')
        const start = text.indexOf(MATERIALS_HEADER)
        if (start >= 0) {
          for (const line of text.slice(start).split(/\r?\n/)) {
            const m = /^\s*\|([^|]+)\|([^|]*)\|\s*$/.exec(line)
            if (m === null) continue
            const rel = m[1].trim().replace(/`/g, '')
            if (rel === '' || rel.includes('---') || rel.startsWith('文件')) continue
            rows.push({ rel, why: m[2].trim() })
            if (rows.length >= 8) break
          }
        }
      } catch { /* 解析失败就走兜底 */ }
    }
    if (rows.length === 0) {
      rows.push({ rel: 'AGENTS.md', why: '工作流与铁律' })
      rows.push({ rel: 'Caves of Qud 模组制作入门指南.md', why: 'Wiki 整理教程' })
      rows.push({ rel: 'Qud机制数据库_使用说明.md', why: 'qud.py 完整用法' })
    }
    materialsCache = { fp, rows }
    return rows
  }

  function materialRows() {
    const rows = materials().map((row) => {
      const file = join(WS, row.rel)
      return { ...row, file, exists: existsSync(file), fp: materialFingerprint(file) }
    })
    // 错误日志永远在册：它不存在也要记录"缺失"，这样第一次追加就能被发现。
    if (!rows.some((row) => row.rel === MISTAKES_NAME)) {
      rows.push({
        rel: MISTAKES_NAME,
        why: '犯过的错 —— 回执之后被追加过就作废',
        file: MISTAKES_MD,
        exists: existsSync(MISTAKES_MD),
        fp: materialFingerprint(MISTAKES_MD),
      })
    }
    return rows
  }

  function workspaceOf(agent) {
    const cwd = agent?.session?.header?.cwd
    return typeof cwd === 'string' && cwd !== '' ? resolve(cwd) : WS
  }

  /**
   * 工具收到的相对路径是相对 Agent 的工作目录，不是相对 Node 进程的 cwd。
   * 用错基准会静默放行 —— 这一条是冒烟测试抓出来的。
   */
  function resolveTarget(path, ws) {
    return resolve(ws, path)
  }

  function inWorkspace(path, ws) {
    const p = resolve(ws, path)
    return p === resolve(ws) || p.startsWith(resolve(ws) + sep)
  }

  function isProtected(path, ws) {
    const p = resolveTarget(path, ws)
    for (const prefix of cfg.protectedPrefixes) {
      const target = resolve(ws, prefix)
      if (p === target || p.startsWith(target.endsWith(sep) ? target : target + sep)) return true
    }
    return false
  }

  /** 命令里点名的受保护目标。空数组 = 没有点名。 */
  function protectedTargetsInCommand(command, ws) {
    const hits = []
    for (const prefix of cfg.protectedPrefixes) {
      const probe = resolve(ws, prefix)
      const bare = prefix.replace(/\\+$/, '').replace(/^\.?[\\/]/, '')
      if (command.includes(probe) || (bare !== '' && new RegExp(`(^|[^\\w])${bare.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`, 'i').test(command))) {
        hits.push(probe)
      }
    }
    return hits
  }

  function shellWrites(command) {
    return SHELL_WRITE_PATTERN.test(command)
  }

  /** 从工具参数里取出目标路径；取不到就返回 undefined（= 不拦截）。 */
  function targetPath(exec) {
    const args = exec.arguments
    if (args === null || typeof args !== 'object') return undefined
    for (const key of ['file_path', 'path', 'filePath']) {
      const value = args[key]
      if (typeof value === 'string' && value.trim() !== '') return value
    }
    return undefined
  }

  /**
   * 当前活跃在这个工作区的会话 id。
   * 回执是【按会话】绑定的：换一个会话就是一次全新的开始，必须自己重读材料。
   * 读不出来（没有活跃 agent）就返回 null —— 那时不做会话校验，宁可少拦也不要误拦。
   */
  function currentSessionId() {
    for (const agent of agents.values()) {
      if (workspaceOf(agent) === WS) return agent.id
    }
    return null
  }

  function loadReceipt() {
    const data = readJson(RECEIPT, null)
    if (data === null || typeof data !== 'object') return null
    if (typeof data.plan !== 'string' || !Array.isArray(data.materials)) return null
    if (data.workspace !== WS) return null
    const session = currentSessionId()
    if (session !== null && data.session !== session) return null
    const age = Date.now() - Number(data.at || 0)
    if (!Number.isFinite(age) || age < 0 || age > cfg.planTtlMinutes * 60000) return null
    return data
  }

  /** 回执与当前材料对不上的地方。空数组 = 有效。 */
  function receiptProblems(receipt) {
    if (receipt === null) return ['还没有本会话的计划回执（workflow_plan 未调用）']
    const problems = []
    const recorded = new Map(receipt.materials.map((m) => [m.rel, m.fp]))
    for (const row of materialRows()) {
      if (!row.exists) continue
      if (!recorded.has(row.rel)) problems.push(`回执里没有 ${row.rel}`)
      else if (recorded.get(row.rel) !== row.fp) {
        problems.push(row.rel === MISTAKES_NAME
          ? `${row.rel} 在回执之后被追加过（新错误必须重读，旧回执作废）`
          : `${row.rel} 在回执之后改过（指纹变了，必须重读并重新交计划）`)
      }
    }
    if (typeof receipt.plan !== 'string' || receipt.plan.trim().length < 20) {
      problems.push('计划正文太短（<20 字符），不算计划')
    }
    return problems
  }

  /**
   * 单调守卫：返回字符串 = 拒绝这次调用。
   * 只有在"目标是受保护区域 + 本会话确实在这个工作区"时才可能拒绝。
   */
  ctx.tools.guard((exec) => {
    const agent = exec.agent
    if (agent === undefined || agent === null) return undefined
    const ws = workspaceOf(agent)
    if (ws !== WS) return undefined

    const args = exec.arguments
    let hits = []
    const named = targetPath(exec)
    if (EDIT_TOOLS.has(exec.name)) {
      if (named !== undefined && isProtected(named, ws)) hits = [resolveTarget(named, ws)]
    } else if (exec.name === 'pwsh' || exec.name === 'bash') {
      const command = typeof args?.command === 'string' ? args.command : ''
      if (command !== '' && shellWrites(command)) hits = protectedTargetsInCommand(command, ws)
    }
    if (hits.length === 0) return undefined

    const problems = receiptProblems(loadReceipt())
    if (problems.length === 0) return undefined

    return [
      `工作流门禁拒绝：${exec.name} 正在修改受保护区域（${hits[0]}）。`,
      '',
      ...problems.map((p) => `- ${p}`),
      '',
      '补齐方式：先读必读材料，再调用 workflow_plan 交一份带出处的计划，然后重试这次调用。',
      `查看完整状态：调用 ${STATUS_TOOL}。`,
    ].join('\n')
  })

  ctx.systemPrompt.section({
    name: 'qa-mod-workflow',
    order: -99,
    text: () => {
      const rows = materialRows()
      const missing = rows.filter((row) => !row.exists)
      const stale = rows.filter((row) => !row.exists ? false : row.fp === 'MISSING')
      const receipt = loadReceipt()
      const problems = receiptProblems(receipt)
      const lines = [
        '## Qud 模组制作工作流（本 section 由 qa-mod-workflow 插件实时维护）',
        '',
        '本工作区受流程门禁约束：**改受保护区域的代码文件之前，必须先读必读材料、再交计划回执。**',
        '这不是建议 —— 引擎层已经拦截，没有回执的写入会被拒绝并附上缺什么。',
        '',
        '### 受保护区域（写入前需要回执）',
        '',
        `\`${cfg.protectedPrefixes.join('`, `')}\``,
        '',
        '### 必读材料',
        '',
      ]
      for (const row of rows) {
        const mark = row.exists ? row.fp : '缺失'
        lines.push(`- \`${row.rel}\` — ${row.why} — 指纹 ${mark}`)
      }
      if (missing.length > 0) {
        lines.push('', `⚠ 有 ${missing.length} 份必读材料不存在：${missing.map((m) => m.rel).join('、')}`)
      }
      if (stale.length > 0) {
        lines.push('', `⚠ 有 ${stale.length} 份材料读不出来，门禁无法核对指纹。`)
      }
      lines.push('')
      lines.push('### 本会话门禁状态')
      lines.push('')
      if (receipt === null) {
        lines.push('- **无计划回执** —— 受保护区域的写入会被拒绝。先读材料，再调 `workflow_plan`。')
      } else if (problems.length === 0) {
        const mins = Math.round((Date.now() - Number(receipt.at)) / 60000)
        lines.push(`- **回执有效**（${mins} 分钟前提交）`)
        lines.push(`- 计划：${String(receipt.plan).replace(/\s+/g, ' ').slice(0, 400)}`)
      } else {
        lines.push('- **回执已失效** —— 受保护区域的写入会被拒绝：')
        for (const p of problems) lines.push(`  - ${p}`)
      }
      lines.push('')
      lines.push('### 无条件生效的三条铁律')
      lines.push('')
      lines.push('1. **坐标就是坐标。** 区域 80x25，左上角 (0,0)，直接 `Z.GetCell(x, y)`，不写换算函数。')
      lines.push('2. **部件必须放在 `XRL.World.Parts`**，XML 里写裸名 —— 引擎前缀是无条件拼接的，不会回退到裸类型名。')
      lines.push('3. **`<anatomy Category=>` 必须是 `BodyPartCategory` 的合法值**，未知值会让整份 Bodies.xml 加载失败。')
      lines.push('')
      lines.push('### 犯错后')
      lines.push('')
      lines.push(`调用 \`${MISTAKE_TOOL}\` 记录：错误 / 当时的错误假设 / 真相 / 依据。`)
      lines.push('记录会自动让回执失效 —— 下次改代码前必须重读，这就是"下次避开同一个错误"的机制。')
      lines.push('如果这条错误暴露了**流程**漏洞，当场把它变成 AGENTS.md 里的一条规则。')
      lines.push('')
      lines.push('### 交付后')
      lines.push('')
      lines.push('代码作业完成必须提交：`pwsh -File publish.ps1 "说明这次改了什么"`。未提交的改动会在回合结束时被点名。')
      return lines.join('\n')
    },
  })

  const textOutput = { schema: { type: 'string' }, render: (_args, value) => [{ type: 'text', text: value }] }

  function agentWorkspace() {
    for (const agent of agents.values()) {
      const ws = workspaceOf(agent)
      if (ws === WS) return WS
    }
    return null
  }

  ctx.tools.register({
    name: STATUS_TOOL,
    description: '显示 Qud 模组制作工作流门禁的当前状态：必读材料的路径与指纹、计划回执是否有效、哪些受保护区域被拦、以及未提交的改动。被门禁拒绝时先跑这个。',
    parameters: { type: 'object', properties: {}, required: [], additionalProperties: false },
    output: textOutput,
    execute() {
      const rows = materialRows()
      const receipt = loadReceipt()
      const problems = receiptProblems(receipt)
      const lines = ['# 工作流门禁状态', '', `工作区: ${WS}`, `Agent 工作目录: ${agentWorkspace() ?? '（不在此工作区，门禁不生效）'}`, '']
      lines.push('## 必读材料')
      for (const row of rows) {
        lines.push(`- ${row.exists ? '[在]' : '[缺失]'} ${row.rel} — ${row.why}`)
        lines.push(`      指纹 ${row.fp}`)
      }
      lines.push('', '## 计划回执')
      if (receipt === null) {
        lines.push('- 无（或已过期）—— 受保护区域的写入会被拒绝')
      } else {
        lines.push(`- 提交于 ${new Date(Number(receipt.at)).toLocaleString()}`)
        lines.push(`- 任务: ${receipt.task || '(未填)'}`)
        lines.push(`- 计划: ${receipt.plan}`)
        lines.push(`- 材料: ${receipt.materials.map((m) => `${m.rel}@${m.fp}`).join(', ')}`)
        lines.push(`- 证据: ${(receipt.evidence || []).length} 条`)
      }
      lines.push('', '## 门禁判定')
      lines.push(problems.length === 0 ? '- 放行' : problems.map((p) => `- 拦截: ${p}`).join('\n'))
      lines.push('', '## 未提交的改动')
      lines.push(gitDirty() ?? '（git 读不出来）')
      return lines.join('\n')
    },
  })

  ctx.tools.register({
    name: PLAN_TOOL,
    description: '提交本会话的工作流计划回执。必须在修改受保护区域（mod\\、_tools\\、sync.ps1、publish.ps1）之前调用。插件会记录四份必读材料的当前指纹 —— 材料之后一改，回执自动失效，必须重读再交。evidence 里的每条都必须写出处（文件:行号 或 命令）。',
    parameters: {
      type: 'object',
      properties: {
        task: { type: 'string', description: '这次要做的代码作业，一句话' },
        plan: { type: 'string', description: '方案本体：改什么、为什么这么改、依据是什么。至少 20 字符。' },
        sources: {
          type: 'array',
          items: { type: 'string', enum: ['教程', 'qud.py', '原版惯例', '反编译源码'] },
          description: '本次查证用到的来源（可多选）',
        },
        citations: {
          type: 'array',
          items: { type: 'string' },
          description: '每条来源对应的出处，写成 文件:行号 或 命令与输出。与 sources 一一对应。',
        },
      },
      required: ['task', 'plan', 'citations'],
      additionalProperties: false,
    },
    output: textOutput,
    execute(args) {
      const plan = String(args.plan || '').trim()
      if (plan.length < 20) return '拒绝：plan 太短（<20 字符）。计划要写清改什么、为什么、依据。'
      const sources = Array.isArray(args.sources) ? args.sources.map(String) : []
      const citations = Array.isArray(args.citations) ? args.citations.map((c) => String(c).trim()).filter((c) => c !== '') : []
      if (citations.length === 0) return '拒绝：citations 是空的。没有出处就不是查证，是记忆。至少写一条 文件:行号 或命令。'

      const rows = materialRows()
      const missing = rows.filter((row) => !row.exists)
      const receipt = {
        version: 1,
        workspace: WS,
        at: Date.now(),
        session: currentSessionId(),
        task: String(args.task || '').trim(),
        plan,
        sources,
        citations,
        materials: rows.map((row) => ({ rel: row.rel, fp: row.fp, exists: row.exists })),
      }
      mkdirSync(WS_STATE, { recursive: true })
      writeFileSync(RECEIPT, JSON.stringify(receipt, null, 2), 'utf8')
      const evidence = sources.map((s, i) => ({ source: s, citation: citations[i] ?? citations[citations.length - 1] }))
      appendFileSync(LEDGER, JSON.stringify({ at: new Date().toISOString(), kind: 'plan', ...receipt, evidence }) + '\n', 'utf8')

      const lines = [
        '回执已记录，受保护区域的写入现在放行。',
        '',
        `任务: ${receipt.task || '(未填)'}`,
        `材料: ${rows.length} 份（已记指纹）`,
        `证据: ${citations.length} 条`,
        `有效期: ${cfg.planTtlMinutes} 分钟，或直到任意一份必读材料被改动`,
      ]
      if (missing.length > 0) lines.push('', `注意：这些必读材料不存在，指纹记为缺失 —— ${missing.map((m) => m.rel).join('、')}`)
      if (sources.length > 0) {
        lines.push('', '本次查证:')
        for (const e of evidence) lines.push(`- [${e.source}] ${e.citation}`)
      }
      return lines.join('\n')
    },
  })

  ctx.tools.register({
    name: MISTAKE_TOOL,
    description: '把一次错误追加进工作区的 错误日志.md。格式：错误 / 当时的（错误）假设 / 真相 / 依据。记录会让计划回执失效 —— 下次改代码前必须重读材料。如果这条错误暴露了流程漏洞，紧接着把它变成 AGENTS.md 里的一条规则。',
    parameters: {
      type: 'object',
      properties: {
        title: { type: 'string', description: '一句话标题' },
        error: { type: 'string', description: '错误是什么' },
        assumption: { type: 'string', description: '我当时的（错误）假设' },
        truth: { type: 'string', description: '真相' },
        evidence: { type: 'string', description: '依据：文件:行号 或命令与输出' },
      },
      required: ['title', 'error', 'truth', 'evidence'],
      additionalProperties: false,
    },
    output: textOutput,
    execute(args) {
      const stamp = new Date()
      const date = `${stamp.getFullYear()}-${String(stamp.getMonth() + 1).padStart(2, '0')}-${String(stamp.getDate()).padStart(2, '0')}`
      const body = [
        '',
        `## ${date} | ${String(args.title).trim()}`,
        '',
        `- **错误**：${String(args.error).trim()}`,
        `- **我当时的（错误）假设**：${String(args.assumption || '（未填）').trim()}`,
        `- **真相**：${String(args.truth).trim()}`,
        `- **依据**：${String(args.evidence).trim()}`,
        '',
      ].join('\n')
      if (!existsSync(MISTAKES_MD)) {
        writeFileSync(MISTAKES_MD, '# 错误日志\n\n**规则：每次犯错，立刻在这里追加一条。**\n\n格式：日期 | 错误 | 我当时的（错误）假设 | 真相 | 依据\n', 'utf8')
      }
      appendFileSync(MISTAKES_MD, body, 'utf8')
      fingerprints.delete(MISTAKES_MD)

      const receipt = loadReceipt()
      let invalidated = false
      if (receipt !== null && receiptProblems(receipt).length > 0) invalidated = true
      const lines = [`已追加到 ${MISTAKES_MD}`, '', body.trim()]
      lines.push('')
      lines.push(invalidated
        ? '计划回执已失效 —— 下次改受保护文件前必须重读材料并重新提交 workflow_plan。'
        : '计划回执仍然有效（本次记录没有改动必读材料）。')
      lines.push('如果这条错误暴露了流程漏洞，现在就去改 AGENTS.md，把它变成一条规则。')
      return lines.join('\n')
    },
  })

  /**
   * 同步读 git 状态。守卫的邻居：只在回合收尾时调用，60 秒缓存。
   * 判据只看退出码 —— 这一点踩过坑（stderr 有输出不等于失败）。
   */
  function gitDirty() {
    const cwd = WS
    const cached = gitCache.get(cwd)
    if (cached !== undefined && Date.now() - cached.at < 60000) return cached.value
    let value
    try {
      const out = execFileSync('git', ['status', '--porcelain'], {
        cwd, encoding: 'utf8', timeout: cfg.gitTimeoutMs, windowsHide: true,
      })
      value = out.trim() === '' ? '（干净，无未提交改动）' : out.trim()
    } catch (error) {
      value = `（git status 失败：${error?.message ?? String(error)} —— 失败与"干净"不是一回事）`
    }
    gitCache.set(cwd, { at: Date.now(), value })
    return value
  }

  function recordTouched(agent) {
    if (workspaceOf(agent) !== WS) return
    const state = readJson(TOUCHED, {})
    state[agent.id] = { at: Date.now(), cwd: workspaceOf(agent) }
    try {
      mkdirSync(WS_STATE, { recursive: true })
      writeFileSync(TOUCHED, JSON.stringify(state), 'utf8')
    } catch { /* 记不上就算了，不阻断工作 */ }
  }

  ctx.on('tools/result', (exec) => {
    const agent = exec.agent
    if (agent === undefined || agent === null) return
    if (agentWorkspace() === null) return
    if (EDIT_TOOLS.has(exec.name)) recordTouched(agent)
    else if ((exec.name === 'pwsh' || exec.name === 'bash') && touchedShellWrite(exec.arguments)) recordTouched(agent)
    else if (exec.name === PLAN_TOOL || exec.name === MISTAKE_TOOL) gitCache.delete(WS)
  })

  function touchedShellWrite(args) {
    const command = typeof args?.command === 'string' ? args.command : ''
    return command.includes(WS) && SHELL_WRITE_PATTERN.test(command)
  }

  /**
   * 回合收尾提醒：动过这个工作区、但仓库还是脏的，就把提醒挂进收件箱。
   * 只在确实动过东西、且 git 确认有改动时才说话 —— 不制造噪音。
   */
  ctx.on('agent/turn-stopping', async (payload) => {
    const agent = payload?.agent
    if (agent === undefined || agent === null) return
    if (workspaceOf(agent) !== WS) return
    if (typeof cfg.requireGitClean !== 'boolean' || cfg.requireGitClean === false) return
    const state = readJson(TOUCHED, {})
    const seen = state[agent.id]
    if (seen === undefined || Date.now() - Number(seen.at) > 12 * 3600 * 1000) return
    const dirty = gitDirty()
    if (dirty.startsWith('（干净') || dirty.startsWith('（git status 失败')) return
    try {
      agent.inbox.append('next-step', {
        id: `qa-mod-workflow-commit-${Date.now()}`,
        role: 'user',
        source: { kind: 'plugin', plugin: name },
        content: [{
          type: 'text',
          text: [
            '【工作流门禁】本轮动过工作区文件，但仓库还有未提交的改动：',
            '',
            dirty,
            '',
            '代码作业完成就要提交：`pwsh -File publish.ps1 "说明这次改了什么"`。',
            '如果这轮确实还没做完、不打算提交，就在回复里说明原因。',
          ].join('\n'),
        }],
      })
    } catch { /* 收件箱竞争：跳过这次提醒 */ }
  })

  ctx.on('system-prompt/assemble', async (_assembly, context, next) => {
    const assembled = await next()
    const agent = context?.agent
    if (agent !== undefined && agent !== null && agent.id !== undefined) agents.set(agent.id, agent)
    return assembled
  })
}
