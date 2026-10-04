/**
 * qa-mod-workflow — 在系统提示词里注入一份【强制工作流】。
 *
 * 为什么需要它
 * ------------
 * `AGENTS.md` 已经是自动注入的，但它走的是 workspace-instructions 通道，
 * 内容是"参考材料"的语气，而且我可以忽略它。这个插件把工作流作为
 * system-prompt 的一个 section 注入，位置固定、语气是命令式的，
 * 与 persona 同级 —— 不是"建议",是"规则"。
 *
 * 设计原则：**注入的是清单，不是判决。**
 * 插件无法知道我有没有真的读过教程，也不该假装能。它能让"跳过查证"
 * 变成一个显式的、可见的动作，这才是插件在流程问题上能做到的事。
 *
 * 与 __dirname 相关：相对预设行按用户主目录解析裸标识符（因为
 * @deepseek-ai/* 没有装在主目录里），所以这个文件零外部 import。
 */

/** Cordis 插件名，供 loader 诊断使用。 */
export const name = 'qa-mod-workflow'

/** 提示词装配必须存在。 */
export const inject = ['systemPrompt']

const WORKFLOW = `你在为一个 Caves of Qud 模组工作，工作区是 D:\\caves of qud 模组制作。
在本次会话的任何代码工作之前，以下规则具有强制力。

## 第〇条：不确定就去查。绝不自己推模型。

这是最高优先级规则，它的存在是因为违反它造成过连续六轮失败：为"世界地图坐标与
区域格子坐标是两套需要换算的体系"这个纯属臆造的前提反复寻找证据，把三个不同
来源的数字当成三次对同一量的测量，得出"原点不稳定"的虚构结论，并据此又改了两轮。
真相是坐标就是坐标。

**自查信号**：如果你开始为某个假设反复寻找证据，且每次"验证"都要引入新的解释
来圆场 —— 停下，去查。

## 第〇之一条：动代码之前，按顺序查这四处

1. \`Caves of Qud 模组制作入门指南.md\` —— Wiki 整理教程，131 KB，带大量错误更正标注
2. \`_tools\\qud.py\` —— 机制 / 属性 / 事件 / 技能的离线数据库
3. **原版是怎么做的** —— 在 \`qud_src\\\` 里搜同类操作，看有多少处、写法是否一致。
   一致就是惯例，照抄。这条往往最快也最可靠。
4. 反编译源码 —— 直接读 \`qud_src\\XRL\\...\\<类>.cs\` 的方法体

**报告时必须写出处**：\`文件:行号\` 或命令与输出。没有出处就不是查证，是记忆。

## 第〇之二条：开工前跑预检

\`\`\`powershell
pwsh -File "D:\\caves of qud 模组制作\\_tools\\preflight.ps1" before
\`\`\`

它列出必读材料、必查项与铁律。交付前跑 \`after\`，它会实际执行全部校验并给结论；
不通过就 exit 1 —— **那时不允许声称完成**。

## 第〇之三条：犯错后立刻更新 \`AGENTS.md\`

\`\`\`powershell
pwsh -File "D:\\caves of qud 模组制作\\_tools\\preflight.ps1" mistake "一句话描述这次错误"
\`\`\`

记的不是"我下次注意"，而是：错误 / 我当时那个错误的假设 / 真相 / 依据。
如果这条错误暴露了流程漏洞，**当场把它变成 \`AGENTS.md\` 里的一条规则**。
规则写进不会自动加载的文件等于没写 —— \`AGENTS.md\` 才是每次请求都注入的那一份。

## 第〇之四条：报告纪律

- 有出处就写出处，没验证就明说"未验证"，不要用肯定语气叙述推断。
- 不要虚报完成度：跑通了才说跑通。
- 用户的游戏内实测是唯一验收标准，\`Player.log\` 是证据来源。
- 结论与预期不符时，第一反应是"我的假设错了"，不是"再加一层解释"。

## 三条结构铁律（已经踩过坑）

1. **坐标就是坐标。** 区域 80x25，左上角 (0,0)，直接 \`Z.GetCell(x, y)\`。
   不要为坐标写换算函数。原版按坐标放对象 573 处，没有一处换算；
   \`x / 3\` 那类运算只属于"在世界地图那一层找格子"。
2. **部件必须放在 \`XRL.World.Parts\`**，XML 里写裸名。引擎的前缀是**无条件拼接**的，
   没有回退到裸类型名（\`ModManager.ResolveType\` 的文档注释在这一点上是错的）。
3. **\`<anatomy Category=>\` 必须是 \`BodyPartCategory\` 的合法值**，未知值会让整份
   Bodies.xml 加载失败。`

/**
 * @param {import('cordis').Context} ctx
 * @param {{ enabled?: boolean, order?: number }} [config]
 */
export function apply(ctx, config) {
  const cfg = config || {}
  if (cfg.enabled === false) return

  const order = typeof cfg.order === 'number' ? cfg.order : -100

  ctx.on('system-prompt/assemble', async (_assembly, _context, next) => {
    const assembled = await next()
    if (!assembled || !Array.isArray(assembled.sections)) return assembled

    // 同名既有的先移除，保证重复装配不会叠加出两份。
    const rest = assembled.sections.filter((section) => section && section.name !== 'qa-mod-workflow')

    return {
      ...assembled,
      sections: [...rest, { name: 'qa-mod-workflow', text: WORKFLOW, order }],
    }
  })
}
