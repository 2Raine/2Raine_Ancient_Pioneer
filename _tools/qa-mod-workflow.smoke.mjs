import { mkdirSync, writeFileSync, rmSync, existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { pathToFileURL } from 'node:url'

const PLUGIN = pathToFileURL('C:/Users/16064/.dsh/.agent-presets/modder/qa-mod-workflow.mjs').href

const ROOT = join(tmpdir(), 'qamw-test-' + Date.now())
const WS = join(ROOT, 'ws')
const HOME = join(ROOT, 'fakehome')
process.env.DSH_HOME = HOME

rmSync(ROOT, { recursive: true, force: true })
mkdirSync(join(WS, 'mod', 'Toncihana_Elemental'), { recursive: true })
mkdirSync(join(WS, '_tools'), { recursive: true })

writeFileSync(join(WS, 'AGENTS.md'), [
  '# Test workspace',
  '',
  '## 必读材料（机器读，改这一节就改了门禁）',
  '',
  '| 文件 | 作用 |',
  '| --- | --- |',
  '| `AGENTS.md` | 工作流与铁律 |',
  '| `教程.md` | Wiki 整理教程 |',
  '| `数据库说明.md` | qud.py 完整用法 |',
  '',
].join('\n'), 'utf8')
writeFileSync(join(WS, '教程.md'), 'tutorial v1\n', 'utf8')
writeFileSync(join(WS, '数据库说明.md'), 'db doc v1\n', 'utf8')
writeFileSync(join(WS, '错误日志.md'), '# 错误日志\n', 'utf8')
writeFileSync(join(WS, 'sync.ps1'), '# sync\n', 'utf8')

const mod = await import(PLUGIN)

const results = []
function check(label, actual, expected) {
  const pass = actual === expected
  results.push(pass)
  console.log(`${pass ? 'PASS' : 'FAIL'}  ${label}`)
  if (!pass) console.log(`        expected=${JSON.stringify(expected)}\n        actual  =${JSON.stringify(actual)}`)
}

/** 每个"会话"一套独立的 guard/工具视图，模拟真实的多会话共存。 */
function boot(sessionId, ws = WS) {
  const view = { guard: null, tools: new Map(), listeners: new Map(), sectionText: null }
  const ctx = {
    tools: {
      guard(fn) { view.guard = fn; return () => {} },
      register(def) { view.tools.set(def.name, def); return () => {} },
    },
    systemPrompt: { section(def) { view.sectionText = def.text({}); return () => {} } },
    effect(fn) { return fn() },
    on(name, fn) {
      if (!view.listeners.has(name)) view.listeners.set(name, [])
      view.listeners.get(name).push(fn)
    },
  }
  mod.apply(ctx, { workspace: ws, protectedPrefixes: ['mod\\', 'sync.ps1', '_tools\\'] })
  const agent = { id: sessionId, session: { header: { cwd: ws }, id: sessionId }, inbox: { append() {} } }
  view.agent = agent
  view.allow = (name, args) => view.guard({ name, arguments: args, agent }) === undefined
  view.deny = (name, args) => typeof view.guard({ name, arguments: args, agent }) === 'string'
  view.announce = async () => {
    for (const fn of view.listeners.get('system-prompt/assemble') ?? []) {
      await fn({ sections: [] }, { agent }, async () => ({ sections: [] }))
    }
  }
  return view
}

const A = boot('sess-A')
await A.announce()

console.log('--- 1. 不相关的写入必须放行 ---')
check('写工作区外的文件', A.allow('write', { file_path: join(ROOT, 'outside.txt') }), true)
check('写受保护区域外的文件', A.allow('write', { file_path: join(WS, 'README.md') }), true)
check('读工具', A.allow('read', { file_path: join(WS, 'mod', 'x.cs') }), true)

console.log('\n--- 2. 无回执时受保护写入必须拦截 ---')
const CODE = join(WS, 'mod', 'Toncihana_Elemental', 'Scripts', 'A.cs')
check('write mod/**.cs', A.deny('write', { file_path: CODE }), true)
check('edit mod/**.xml', A.deny('edit', { file_path: join(WS, 'mod', 'Toncihana_Elemental', 'Items.xml') }), true)
check('write _tools/**.py', A.deny('write', { file_path: join(WS, '_tools', 'x.py') }), true)
check('write sync.ps1', A.deny('write', { file_path: join(WS, 'sync.ps1') }), true)
check('相对路径也算', A.deny('write', { file_path: 'mod\\Toncihana_Elemental\\a.cs' }), true)

console.log('\n--- 3. shell 写入检测 ---')
check('pwsh 重定向到受保护文件', A.deny('pwsh', { command: `Set-Content -Path "${CODE}" -Value x` }), true)
check('pwsh 只读命令', A.allow('pwsh', { command: `Get-ChildItem "${join(WS, 'mod')}"` }), true)
check('pwsh 写工作区外', A.allow('pwsh', { command: 'echo hi > C:\\temp\\x.txt' }), true)
check('pwsh 只读校验工具（2>&1 不算写入）', A.allow('pwsh', { command: `& python "${join(WS, '_tools', 'validate_mod.py')}" 2>&1` }), true)
check('pwsh 写 _tools 下文件应拦截', A.deny('pwsh', { command: `Set-Content -Path "${join(WS, '_tools', 'x.py')}" -Value y` }), true)

console.log('\n--- 4. 计划回执 ---')
check('workflow_plan 已注册', A.tools.get('workflow_plan') !== undefined, true)
check('workflow_status 已注册', A.tools.get('workflow_status') !== undefined, true)
check('workflow_mistake 已注册', A.tools.get('workflow_mistake') !== undefined, true)

const planTool = A.tools.get('workflow_plan')
check('plan 太短被拒', (await planTool.execute({ task: 't', plan: 'short', citations: ['a:1'] }, {})).startsWith('拒绝'), true)
check('citations 为空被拒', (await planTool.execute({ task: 't', plan: 'x'.repeat(40), citations: [] }, {})).startsWith('拒绝'), true)

const ok = await planTool.execute({
  task: '加一个部件', plan: '把 A2Raine_X 部件加到 XRL.World.Parts 下，并按坐标直接 GetCell。',
  sources: ['原版惯例', '教程'], citations: ['qud_src/XRL/World/Zone.cs:120', '教程.md:2616'],
}, {})
check('正常提交成功', ok.startsWith('回执已记录'), true)

const stateDir = join(HOME, 'dsh-qa-mod-workflow')
check('状态写在 harness home 而不是工作区', existsSync(stateDir) && !existsSync(join(WS, '.dsh')), true)

console.log('\n--- 5. 有回执后放行 ---')
check('write mod/**.cs 放行', A.allow('write', { file_path: CODE }), true)
check('write sync.ps1 放行', A.allow('write', { file_path: join(WS, 'sync.ps1') }), true)

console.log('\n--- 6. 别的会话不继承回执 ---')
const B = boot('sess-B')
await B.announce()
check('新会话被拦截', B.deny('write', { file_path: CODE }), true)

console.log('\n--- 7. 材料一改，回执失效 ---')
writeFileSync(join(WS, '教程.md'), 'tutorial v2 CHANGED\n', 'utf8')
check('改教程后拦截', A.deny('write', { file_path: CODE }), true)
const statusText = await A.tools.get('workflow_status').execute({}, {})
check('status 报出"教程.md 指纹变了"', /教程\.md 在回执之后改过/.test(statusText), true)

console.log('\n--- 8. 重新交计划后再次放行 ---')
await planTool.execute({ task: 't2', plan: 'y'.repeat(40), citations: ['教程.md:1'] }, {})
check('重新交计划后放行', A.allow('write', { file_path: CODE }), true)

console.log('\n--- 9. 记错误会让回执失效 ---')
const mistakeOut = await A.tools.get('workflow_mistake').execute({
  title: '测试错误', error: 'E', assumption: 'A', truth: 'T', evidence: 'f:1',
}, {})
check('追加到错误日志', readFileSync(join(WS, '错误日志.md'), 'utf8').includes('测试错误'), true)
check('记录后拦截', A.deny('write', { file_path: CODE }), true)
check('提示回执已失效', /计划回执已失效/.test(mistakeOut), true)

console.log('\n--- 10. 别的工作区 / 无 agent 不受影响 ---')
const OTHER = join(ROOT, 'other')
mkdirSync(OTHER, { recursive: true })
const C = boot('sess-C', OTHER)
await C.announce()
check('别的工作区的会话放行', C.allow('write', { file_path: CODE }), true)
check('没有 agent 时放行', A.guard({ name: 'write', arguments: { file_path: CODE } }) === undefined, true)

console.log('\n--- 11. system prompt section ---')
mod.apply({
  tools: { guard() { return () => {} }, register() { return () => {} } },
  systemPrompt: { section(def) { A.sectionText = def.text({}); return () => {} } },
  effect(fn) { return fn() },
  on() {},
}, { workspace: WS, protectedPrefixes: ['mod\\'] })
check('section 渲染出必读材料', A.sectionText !== null && A.sectionText.includes('教程.md'), true)
check('section 渲染出门禁状态', A.sectionText !== null && A.sectionText.includes('门禁状态'), true)
check('section 含三条铁律', A.sectionText !== null && A.sectionText.includes('坐标就是坐标'), true)

console.log('\n--- 12. 禁用开关 ---')
let registeredWhileDisabled = 0
mod.apply({
  tools: { guard() { return () => {} }, register() { registeredWhileDisabled++; return () => {} } },
  systemPrompt: { section() { return () => {} } },
  effect(fn) { return fn() },
  on() {},
}, { enabled: false, workspace: WS })
check('enabled:false 不注册任何东西', registeredWhileDisabled, 0)

rmSync(ROOT, { recursive: true, force: true })

const failed = results.filter((r) => !r).length
console.log(`\n==== ${results.length - failed}/${results.length} passed ====`)
process.exit(failed === 0 ? 0 : 1)
