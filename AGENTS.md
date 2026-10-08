# Caves of Qud 模组制作工作区

本工作区已建成一个**离线可查的 Caves of Qud 机制数据库**，覆盖游戏 2.0.211.56 的物品、
技能、变异、事件与方法实现。**先读这份文件，再动手**，可以省掉大量反编译与试错。

---

## 〇〇、两层强制机制（先看这个）

工作流不靠"我下次注意"，而是做成两层，一层比一层难绕过：

| 层 | 位置 | 何时生效 | 强度 |
| --- | --- | --- | --- |
| **1. 本文件** | `AGENTS.md` | **每次对话的第一次请求**自动注入，之后常驻历史直到上下文压缩 | 参考材料语气，可被忽略 |
| **2. 预检脚本** | `_tools\preflight.ps1` | 我主动跑；`after` 会实际执行全部校验并 `exit 1` | 把跳过变成显式动作 |

### 必读材料

开工前必读这几份：

| 文件 | 作用 |
| --- | --- |
| `AGENTS.md` | 工作流与铁律 |
| `Caves of Qud 模组制作入门指南.md` | Wiki 整理教程，含大量更正标注 |
| `Qud机制数据库_使用说明.md` | `qud.py` 的完整用法 |
| `Toncihana_制作笔记与调参参考.md` | 本模组设计记录与踩坑，改数值前必读 |

```powershell
# 开工前
powershell -File "D:\caves of qud 模组制作\_tools\preflight.ps1" before

# 交付前（全跑一遍校验，不通过就 exit 1 —— 那时不许声称完成）
powershell -File "D:\caves of qud 模组制作\_tools\preflight.ps1" after

# 犯错后立刻记一条
powershell -File "D:\caves of qud 模组制作\_tools\preflight.ps1" mistake "一句话描述这次错误"
powershell -File "D:\caves of qud 模组制作\_tools\preflight.ps1" log
```

**犯错后的固定动作**：先 `mistake` 记进 `错误日志.md`，
再判断这条错误是否暴露了流程漏洞；**如果是，当场把它变成下面第〇节里的一条规则** ——
规则写进不会自动加载的文件等于没写。

---

## 〇、工作流：不确定就去查，永远不要自己推模型

**这一节排在所有技术内容之前，因为它比任何一条技术细节都重要。**

### 铁律

> **不确定的机制，去查。不要推一套模型，然后拿它当事实，再回头找证据维护它。**

违反这条规则已经造成过一次连续六轮的失败：我为"世界地图坐标与区域格子坐标是两套
需要换算的体系"这个**纯属臆造的前提**反复寻找证据，把三个不同来源的数字当成"三次
对同一量的测量"，得出"原点不稳定"的虚构结论，并据此又改了两轮。真相是坐标就是坐标，
玩家从一开始就说对了。

**判断自己是否正在犯错**：如果你开始为某个假设反复寻找证据，且每次"验证"都要引入
新的解释来圆场 —— 停下，去查。

### 遇到不确定时，按顺序查这四处

| 顺序 | 查什么 | 怎么查 |
| --- | --- | --- |
| 1 | **官方 Wiki 整理的教程** | `Caves of Qud 模组制作入门指南.md`（131 KB，含大量 Wiki 错误更正标注） |
| 2 | **机制数据库** | `& $py $q mech <部件>` / `code <部件>` / `events <事件>` / `attr <属性>` |
| 3 | **原版是怎么做的** | 在 `qud_src\` 里搜同类操作，**看有多少处、写法是否一致**。一致就是惯例，照抄 |
| 4 | **反编译源码细节** | 直接读 `qud_src\XRL\...\<类>.cs` 的方法体 |

**第 3 条往往最快也最可靠**。例：想按坐标放对象？搜 `GetCell.*AddObject` —— 原版 573 处，
全是直接 `Z.GetCell(x, y).AddObject(...)`，没有一处换算。**这就是答案，不需要推理。**

### 开工硬流程：先查证，再动手

**本节只适用于本工作区（卡德洞窟 / Caves of Qud）的模组作业** —— 改代码、改 XML、
调数值、查机制、做资源。别的话题不受它约束。

动手的第一件事**不是写文件**，而是先在回复里给出下面三样，缺一样就算没开工：

1. **`preflight before` 的结论** —— 它列出的必读材料与指纹。
2. **这次要用的机制 / 属性 / 部件 / 惯例出在哪** —— 指明是上面"按顺序查这四处"的哪一处查到的。
3. **出处** —— `文件:行号`，或可直接重跑的命令。

查不到就写 **"未查到，以下为推断（未验证）"** —— 不许把推断说成事实。
**这三样没给全之前，不许写 `mod\`、`_tools\` 下的任何文件。**
收尾同样要贴结论：`sync.ps1 push`，以及 `preflight.ps1 after` 的退出码与结论。

**还有一条更早的前置动作：先把"要动的那个东西"的确切名字查出来。**
机制查清 ≠ 目标认对。上面三样回答"这件事怎么做"，这一条回答"我动的是哪个东西"。

踩过：做 warm static 的突变豁免，把 `LiquidWarmStatic.GlitchMutations` 与 `Mutations`
的增删路径查得很细，却**没确认要保护的是哪个类名** —— 于是保护了父类
`ElectricalGeneration`，而角色身上实际的部件是子类
`A2Raine_Toncihana_Stormcharge`（`ARaine_Charge.cs:49` 优先按子类名查、`:56` 才退回父类）。
豁免写完了，保护的却是一个角色并不拥有的东西，等于没写。

**所以在查机制之前，先用一行命令把目标的 `Name=` / `Class=` / `Inherits=` 取出来**，
例如 `grep -n "class <类名>\|Inherits=" <文件>`。一个 `grep` 的成本，
换掉一整轮返工。

```powershell
# 0) 赋值（每个新会话都要重来一次）
#    仓库根 = 工作区根，所以只有一个 $ws
$py = "C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
$q  = "D:\caves of qud 模组制作\_tools\qud.py"
$ws = "D:\caves of qud 模组制作"

# 1) 动手前：查机制 / 看原版
& $py $q mech <你要用的部件>
# 或直接在源码里找惯例
Select-String -Path "$ws\qud_src\XRL\World\ZoneBuilders\*.cs" -Pattern "GetCell"

# 2) 改代码：改 $ws\mod\Toncihana_Elemental\ 下的文件
#    那是真源。游戏目录只是部署目标：
#    %USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\Toncihana_Elemental\

# 3) 同步 + 全套校验（不启动游戏）
cd $ws
powershell -File sync.ps1 push
powershell -File _tools\preflight.ps1 after     # 编译 + XML/引用校验 + 日志 + git 状态，给结论
#    单独跑也可以：
#    powershell -File "$ws\_tools\check_csharp.ps1"      # exit=0 才算过
#    & $py "$ws\_tools\validate_mod.py"                  # 结构/命名/部件命名空间/解剖类别
#    & $py "$ws\_tools\audit_references.py"              # 按引擎真实解析路径核对引用

# 4) 让用户在游戏里实测，然后读日志
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log" `
              -Pattern '\[Toncihana\]|MODERROR.*Storm-Caller'

# 5) 提交并推送到 GitHub（一条命令）
powershell -File publish.ps1 "说明这次改了什么"
```

### 交付前自检

- [ ] `check_csharp.ps1` 退出码 0，且 `csc_out.txt` 里没有 `error`
- [ ] `validate_mod.py` 报 `all checks passed`
- [ ] `audit_references.py` 报 `no unresolved references found`
- [ ] 改动已 `sync.ps1 push` 到游戏目录（**否则游戏跑的还是旧代码**）
- [ ] 若改了 `.cs`，已提醒用户**完全重启游戏**
- [ ] 若结论来自推断而非查证，**明确标注"未验证"**，不要说得像事实

### 报告纪律

- **有出处就是有出处**：写清 `文件:行号` 或命令。
- **没验证就说没验证**：不要用肯定语气叙述推断。
- **不要虚报完成度**：跑通了才说跑通。
- **用户实测是唯一验收标准**，日志是证据来源。
- **报结论之前先确认取证命令本身成功了**（`exit code` 为 0），
  **不要把命令自己的报错当成数据**。踩过的坑：`git ls-remote -c` 这个参数不存在，
  命令报 `unknown switch` 直接失败，而判定逻辑是"输出里没有 not found 就算存在"——
  于是错误变成了肯定答案，我据此报了两次"远程库已建好"。
  **让"失败"与"否"落在不同分支上。**
- **告警要先判断"适不适用"。** 诊断/日志对不相关的对象也报警，就会训练人忽略它 ——
  一条长期存在的假告警，成本是每次看日志都要重新判一次。
  实例：`LOAD PROBLEM` 曾对非 Toncihana 存档报"数据全丢"，因为那段诊断没先问 `IsToncihana`。
  **诊断要么有信息量，要么不该出现。**
- **指认"某个实体是什么"时要回定义处。** 说"X 的躯体 / 攻击 / 派系 / 属性是 Y"之前，
  把定义那行 grep 出来（`Inherits=` / `Anatomy=` / `BodyObject=`）—— **引用比回忆可靠**。
  实例：曾把 Elemental 生物的 `2Raine_Elemental_HeadBlow` 说成 Toncihana 的攻击方式，
  而三条躯体就挤在同一个文件里（`2Raine_Toncihana_Bodies.xml`）。**读到 ≠ 记住。**
- **数量/次数不对时，先确认"一个对象代表几个"，再猜它在哪里。**
  Qud 的物品可以堆叠：**一个 `GameObject` 可能是一整堆**，数量在 `GameObject.Count`
  （`XRL/World/GameObject.cs:617-634`，即 `Stacker.Number`，无 Stacker 时为 1）。
  于是"只处理了 1 个"这种症状，第一嫌疑是**按对象计数而不是按堆叠数量结算**，
  而不是"它没被找到"。
  踩过：`Devour` 吃一堆精灵石只加 1 点充能，我连续两次推断成"石头被装备了所以遍历不到"
  和"石头在容器里所以遍历不到"，都为它写了一套"递归收集"的设法；真因只有一句 ——
  `total += meal.Amount` 漏掉了 `× item.Count`。**先读物品自己的属性，再谈它在哪。**
- **报机制结论时，先找"谁会破例"。** 一句"X 永远如此"必须顶得住反例，而那些反例往往**不在
  你查的那一层**：查 `Zone` / `ZoneManager` 只会看到"区域冻结、内容原样读回"，
  而破例的是**部件层**的惰性补算 —— `GenericInventoryRestocker.TurnTick`
  （`XRL/World/Parts/GenericInventoryRestocker.cs:125-142`）在玩家回来时拿
  `The.Game.TimeTicks` 和自己的 `LastRestockTick` 比差值，超过 6000 回合就补货。
  **结构层找不到的例外，去部件层找；找过再下断言。**
  实例：曾断言"区域内容永不随时间流逝改变"，被商人补货当场推翻，且因此漏掉了
  "世界时间在冻结期间照常推进"这个关键事实。

---

## 一、最重要的一件事：用 `qud.py` 查询，不要重新反编译

```powershell
$py = "C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
$q  = "D:\caves of qud 模组制作\_tools\qud.py"

# 总览 + 健康度
& $py $q stats

# ① 一个物品由哪些机制拼成（附出处 文件:行号）
& $py $q item "Freeze Ray"
& $py $q item "Chain Mail" --xml          # --xml 输出可粘贴的 XML

# ② 一个机制能配哪些属性（附 C# 类型与本体真实取值）
& $py $q mech EnergyAmmoLoader
& $py $q mech Projectile --all            # --all 连未用过的代码字段也列出

# ③ 机制"怎么运作"：方法实现 + 事件绑定
& $py $q code EnergyAmmoLoader
& $py $q code EnergyAmmoLoader --method GetChargePerAction
& $py $q events EndTurnEvent              # 哪些机制监听这个事件
& $py $q impl ChargeUse                   # 在方法体里搜代码

# ④ 技能树 → power → C# 实现（含花费、属性要求、前置）
& $py $q skills
& $py $q skills Axe

# ⑤ 反查：哪些机制接受某属性 / 谁用了某部件
& $py $q attr ChargeUse
& $py $q use MeleeWeapon
& $py $q find ammo
& $py $q list part --filter ammo
```

**别名 `$q` 和 `$py` 在每个新会话里都要重新赋值**（上面这段可直接复制）。

---

## 二、数据在哪

| 路径 | 内容 |
| --- | --- |
| `qud_db\qud_mechanisms.sqlite` | **机制库**：机制 / 属性指纹 / 成员（方法+属性）/ 事件绑定 / 技能桥接 |
| `qud_db\qud_blocks.sqlite` | **蓝图库**：蓝图条目 / 部件使用 / 部件属性 |
| `qud_db\*.jsonl` | 同上，JSON 行格式，便于 diff 与 grep |
| `qud_src\` | **反编译出的 5,438 个 `.cs`**（24.2 MB），可直接全文检索 |
| `_tools\qud.py` | 查询工具（10 个子命令） |
| `_tools\*.py` | 四个**幂等**索引器 + .NET 元数据读取器 + ASAR 读取器 |
| `Qud机制数据库_使用说明.md` | **完整使用说明** —— 要深入时读这份 |
| `Caves of Qud 模组制作入门指南.md` | 从 Wiki 整理的模组制作入门（含大量 Wiki 错误标注） |

> **要具体数字时跑 `qud.py stats`，不要相信任何文档里写死的数目。**
> 所有计数都是游戏 **2.0.211.56** 的快照；游戏一更新、索引一重跑就会变，
> 而写进文档的数字不会自己更新。本文件因此**刻意不列举统计数字** ——
> 曾经同时写在两份文档里的 10 个计数，我第一次核对就发现已经有一个对不上了。
> 权威来源只有一个：数据库本身。

### 这两份文档的分工

| 文件 | 作用 | 何时读 |
| --- | --- | --- |
| `AGENTS.md`（本文件） | **自动注入**每个对话的上下文。负责"知道有这么个东西、怎么用" | 每次自动，无需手动读 |
| `Qud机制数据库_使用说明.md` | 21 KB 完整手册：全部命令、实战示例、踩坑记录、重建流程 | 要动手时读 |

本文件是**摘要与指针**，不是替代品。两者用途不同，**不要把同一份内容抄进两边**。

### 直接查 SQLite

```powershell
& $py -c "import sqlite3;c=sqlite3.connect(r'D:\caves of qud 模组制作\qud_db\qud_mechanisms.sqlite');print(c.execute(\"SELECT part,attr,type FROM mech_attr WHERE attr='ChargeUse' LIMIT 5\").fetchall())"
```

关键表：

| 库 | 表 | 用途 |
| --- | --- | --- |
| mechanisms | `mechanisms` | 每个机制一行，`full` 是唯一键（全限定类名） |
| | `mech_attr` | 每个机制的每个属性：`type` / `declared_in` / `safety` / `observed` |
| | `mech_method` | 成员（`kind` = method/property），`body` 是完整实现 |
| | `mech_event` | 事件绑定，`role` ∈ register/want/handle/fire |
| | `power_class` | 技能 power → C# 类桥接 |
| blocks | `blocks` | 每个蓝图拆解（`json` 列是完整记录） |
| | `part_usage` / `part_attr` | 反查"谁用了这个部件" / 属性自动补全 |

---

## 三、理解数据的关键：属性可靠性分级

XML 属性名 = 部件类上的 C# 公共字段/属性名（吻合率 **100%**，已验证）。查询时会标注三级：

| 标记 | 含义 | 怎么用 |
| --- | --- | --- |
| `[OK]` | **本体 XML 真用过这个属性** | 放心照抄，附带的 `取值:` 是本体真实用法分布 |
| `[TRY]` | 代码里有此字段、但本体 XML 从未使用 | 可以试，但你是第一个；注意类型 |
| `[SKIP]` | 运行时内部状态（`LastStatus`、`Cached*`） | 写进 XML 无意义 |

**真正可放心用的是标 `[OK]` 的那一批**；`[TRY]` 数量大得多，不要被它淹没。
具体各有多少条，跑 `qud.py stats` 看（它会打印属性分级计数）。

---

## 四、本机环境（已配置好，不必重装）

| 项 | 路径 / 版本 |
| --- | --- |
| Python | `C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe` |
| .NET SDK | `C:\Users\16064\.dotnet\dotnet.exe`（8.0.425） |
| ilspycmd | `C:\Users\16064\.dotnet\tools\ilspycmd.exe`（9.1.0.7988） |
| 游戏安装 | `D:\SteamLibrary\steamapps\common\Caves of Qud\`（**2.0.211.56**） |
| 游戏数据（只读） | `...\CoQ_Data\StreamingAssets\Base\` |
| 游戏程序集 | `...\CoQ_Data\Managed\Assembly-CSharp.dll` |
| **模组开发目录** | `C:\Users\16064\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\` |
| 日志 | 同上层目录：`Player.log`、`build_log.txt`、`harmony.log.txt` |

**注意 PATH**：`dotnet` / `ilspycmd` **不在** PATH 里，必须用绝对路径调用。

**注意 shell**：本机**没有 `pwsh`**（PowerShell 7 未安装，`where pwsh` 为空，
`Program Files\PowerShell` 不存在）。仓库内所有命令示例一律用
`powershell -File ...`（Windows PowerShell 5.1）调用。
DSH 那边的工具**恰好也叫 `pwsh`**，但那是宿主自带的工具名，不是 PATH 上的 pwsh.exe ——
换宿主后不必也不该假设它还在。实测判据：`where.exe pwsh` 返回空。

**注意编译产物目录**：固定为 `%LOCALAPPDATA%\Temp\qud_api`，**不要**改回 `$env:TEMP\qud_api`。
有的宿主（如 Reasonix）会把 `TEMP` 每个会话改写成自己的临时目录，那里没有 `qud_api`，
`Out-File` 会因目录缺失而失败，把"编译通过"误报成"编译未通过"。
两个脚本都已按 `LOCALAPPDATA` 计算并自建目录，保留这个写法即可。

---

## 五、动模组前必须知道的坑

1. **`<objects>` 根下 `Load` 默认是"替换"而非合并** —— 改本体对象必须写 `Load="Merge"`，
   否则会静默抹掉该蓝图的全部数据。
2. **XML 属性名 ≠ 一定可写** —— 先查 `qud.py mech`，看是不是 `[OK]`。
3. **Qud 的 XML 是非良构的** —— 满文件都是没有分号的裸字符引用（`&#15;`、`&#11;`，CP437 字形码）。
   自己写解析器时必须容忍，否则会整文件解析失败。
4. **改 `.cs` 必须重启游戏**；改 XML 可以游戏内 `wish reload`。
5. **对象/区域一旦生成就不再受蓝图控制** —— 改完要重新 wish 或 `wish rebuild`。
6. **加了 `Load="Merge"` 就不要同时写 `Inherits`**。
7. **脚本模组有安全批准机制** —— 用户不批准，该模组**连 XML 都不会加载**。

### 本机工具链的坑

8. **含中文的 `.ps1` 被 `edit`/`write` 改过之后，BOM 会丢，脚本必崩。**
   这条管的是**任何**含中文的 `.ps1` —— 不分目录、不分名字，`%TEMP%` 下的临时脚本一样算。
   `write` **每次**都产出无 BOM 的 UTF-8，所以触发是必然的，不是偶然的。
   `powershell`（Windows PowerShell 5.1）在没有 BOM 时**按 ANSI 读 `.ps1`**，
   满文件中文被逐字节拆散，报错会指向一段**完全合法**的代码
   （例如 `Unexpected token '}'`），排查方向被彻底带偏。

   **判据**：
   ```powershell
   ([System.IO.File]::ReadAllBytes('...\x.ps1')[0..2]) -join ','   # 应为 239,187,191
   ```
   **修复**（写完 `.ps1` 立刻做，然后跑一次验证）：
   ```powershell
   $f = '...\x.ps1'
   $raw = [System.IO.File]::ReadAllText($f, [System.Text.UTF8Encoding]::new($false))
   [System.IO.File]::WriteAllText($f, $raw, [System.Text.UTF8Encoding]::new($true))
   ```
   `sync.ps1`、`publish.ps1`、`_tools\preflight.ps1` 是仓库里现成的三个例子，
   但**规则不止于它们**。已犯过三次（`错误日志.md` 有记）——
   前两次是 `sync.ps1` 和 `preflight.ps1`，第三次是临时脚本，
   三次都是因为把这条规则的范围**读窄了**。

9. **PowerShell 变量名大小写不敏感** —— 局部变量 `$status` 与参数 `[switch]$Status`
   是**同一个变量**，给它赋字符串会抛 `Cannot convert ... SwitchParameter`。命名时避开。

10. **往 `publish.ps1` 传提交信息时，信息里不要带引号。**
    它是 `[string]$Message`（单个位置参数），`powershell -File` 传参会按空格切分，
    信息里出现 `"` 就会被拆成两个参数，报
    `A positional parameter cannot be found that accepts argument '...'`。
    多行信息用数组 + `` -join "`n" `` 拼好再传；中文没问题，引号才是杀手。

11. **同名 `<part>` 是"合并"，不是"叠加"。** 一个蓝图里同名 `<part>` 只能有一个 ——
    `Parts` 是 `Dictionary<string, GamePartBlueprint>`（按部件名索引）。子蓝图重写同名 part 时，
    **只覆盖它写到的属性，没写到的保留祖先的值**（所以 `Tile` 会活下来）。
    两个后果，都要记住：
    - **想改继承来的部件属性** → 直接重写那个 part，只写要改的，其余自动保留。
    - **同一个生物身上有两件出生装备时，必须用两个不同的部件名**，否则后写的会把先写的
      整条吃掉，而**从游戏现象上看不出来**（本模组踩过：`A2Raine_BornEquipped` 装精灵石，
      把父蓝图同一部件名装的头武器整条覆盖，雷电元素的 `Head` 槽空了很久没人发现）。
    判据：`GameObjectBlueprint.cs:43` + `ObjectBlueprintLoader.cs:127`；
    原版 5221 个带 `<part>` 的蓝图里 **4573 个**都依赖这个语义。详见笔记 〇之二十七。

12. **Harmony 不是"一碰就炸整个模组"。** 第五轮曾把"四个技能 + 发电突变全没了"归因给它，
    **第六轮已经推翻这个归因** —— 真凶是 `Name` 必须等于 `Class`（`AddMutation` 静默返回 -1）。
    源码依据：`ModInfo.cs:819-820`（`ApplyHarmonyPatches(); return compilationResult.Success;`
    —— 返回值只看编译）、`:845-862`（`PatchAll` 的异常被 try/catch 吞掉，只记一行 `Failure :(`）。
    **真实风险只有两个**：①`PatchAll` **全有或全无** —— 一个补丁失败，**同一 assembly 里
    后面的补丁全不打**（但**不影响类注册**）；②游戏更新或别的 mod 改同一方法会让它失效。
    **用的话**：不要写 `[HarmonyPatch]` 特性（会被加载器扫进 `PatchAll`），改成
    `new Harmony(id).Patch(...)` **逐个打 + 各自 try/catch**，优先 **Postfix**，签名先去 `qud_src` 核准。
    **但优先级不变：能用事件/部件做的，优先事件/部件。** 详见笔记 〇之三 的补记。

13. **数据 XML 里不写注释。** 蓝图、部件、属性、刷新表这些文件**只放数据** —— 动机、出处、
    权衡都写到 C# 的 `///` 注释里，或者写进制作笔记。踩过：给投射物加 `NonPenetrating`
    时顺手在 `2Raine_Toncihana_StormLash.xml` 里解释了一遍理由，那是把 C# 的注释习惯
    带进了 XML。

14. **模组代码只能写 C# 5 —— 不要用 `?.`、`nameof`、字符串插值 `$"..."`、`=>` 成员体。**
    `check_csharp.ps1` 调的是 .NET 4.0 的 `csc.exe`（`Microsoft.NET\Framework64\v4.0.30319`），
    它按 C# 5 解析；游戏自身的 Unity 编译器**支持**新语法，所以这类错误**只会在自检里出现**，
    很容易被当成"游戏能跑就行"。踩过：`Object.GetPart<Mutations>()?.GetMutation(...)`
    报 `CS1525 无效的表达式项`，`nameof(Prefix)` 报 `CS0103 不存在名称 'nameof'`
    —— 报错行号与实际原因分离，第一眼看不出是语法版本问题。
    替代写法：显式 null 判断、字符串字面量、`string.Format` / `+` 拼接。

15. **装初始化的入口有三个，而且顺序不同 —— 挑错时机 = 功能静默失效。**
    由早到晚：

    | 入口 | 触发时机 | 覆盖 |
    | --- | --- | --- |
    | **`[ModSensitiveCacheInit]`**（静态方法） | **mod 加载完成** | **一切，包括角色创建** |
    | `[CallAfterGameLoaded]`（静态方法） | 加载存档 / 开始游戏 | **读档**，不含 chargen |
    | `PlayerMutator.mutate()` | 角色创建完成 | 开新档，但**在 chargen 之后** |

    **要影响的界面越靠前，入口就得越早。** 尤其 `chargen`（创建角色、选突变）比后两个都早，
    只有 `[ModSensitiveCacheInit]` 来得及 —— 它的写法是标在**静态 void 方法**上
    （实例：`GenotypeFactory.cs:80-81`），**类上不需要额外 Attribute**。
    判据：装初始化后，日志里**没有** `XXX installed` 那行，就说明那时机还没轮到。
    踩过：chargen 突变过滤器挂在后两个入口上，停留在角色创建界面时整份日志**一行本模组
    输出都没有**，过滤器从未安装，两次改动都白费。
    （`warm static` 突变豁免只发生在游戏中，所以它在后两个入口也够，但已一并提前。）

16. **`Name` 必须等于 `Class` 是"本模组自己的突变"上的约定 —— 原版不是这样。**
    我们自己的突变两者写成一样（理由见第〇节：`AddMutation` 通过 `Name` 解析类型，
    对不上就静默返回 -1）。但原版很多突变两者**不同**，例如
    `Base/Mutations.xml:16`：

    ```xml
    <mutation Name="Electrical Generation" Cost="4" MaxSelected="1"
              Class="ElectricalGeneration" Exclusions="" ... />
    ```

    **凡是要比对原版突变的地方，用的都是 `Name`（XML 里那个，可以带空格）**：
    `MutationEntry.Name` 就是它；`OkWith` 的排除比对是它（`MutationEntry.cs:224`）；
    chargen 列表项的 `Id` 也是它（`QudMutationsModuleWindow.cs:269`）。
    **`Class` 只用于解析 C# 类型**（例如 `Creatures.xml` 里给生物直接发突变）。
    踩过：把 `Exclusions` 由 `Electrical Generation` 改成 `ElectricalGeneration`，
    以为在"对齐 Class"，结果让一条**本来生效**的排除失效了。

更完整的坑与做法见 `Caves of Qud 模组制作入门指南.md`。

---

## 六、这套数据库的已知限制（不要过度信任）

- **方法体是反编译产物**：官方无公开源码，变量名可能与官方不同，**别假定名字一致**。
- **有一小批类型确实没有任何成员**：已核实是真空类（纯继承标记，逻辑在泛型基类里），
  不是解析漏失。具体数量跑 `qud.py stats` 对照。
- **8 个短名对应两个不同的类**（`Shield` 既是部件又是技能；`StairsDown` 既是部件又是区域生成器）——
  查询时会要求消歧，这是有意的，不要绕过。
- **属性按短名归因**：用 `mech <名> --all` 时若看到"同名异类属性被过滤"，那是另一类的属性。
- 数据对应**游戏 2.0.211.56**；游戏更新后需重跑索引（见下）。

---

## 七、游戏更新后如何重建

四个脚本全部**幂等**，可反复运行；顺序有依赖，别打乱：

```powershell
$py   = "C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
$ilspy= "C:\Users\16064\.dotnet\tools\ilspycmd.exe"
$base = "D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base"
$dll  = "D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll"
$out  = "D:\caves of qud 模组制作\qud_db"
$src  = "D:\caves of qud 模组制作\qud_src"
$T    = "D:\caves of qud 模组制作\_tools"

# 1) 拆蓝图
& $py "$T\extract_blocks.py" --base $base --outdir $out

# 2) 机制索引（DLL 字段/属性 × XML 用法）
& $py "$T\index_mechanisms.py" --dll $dll --blocks "$out\qud_blocks.sqlite" --outdir $out

# 3) 反编译整程序集（约 36 秒）
Remove-Item $src -Recurse -Force -ErrorAction SilentlyContinue
& $ilspy -p -o $src --nested-directories $dll

# 4) 索引成员与事件（约 5 秒）
& $py "$T\index_csharp.py" --src $src --db "$out\qud_mechanisms.sqlite" --outdir $out

# 5) 自检：吻合率应为 100%
& $py "$T\qud.py" stats
```

`index_mechanisms.py` 与 `index_csharp.py` **共享** `qud_mechanisms.sqlite`，
但各自只 DROP 自己拥有的表，**不会删除文件**。若自检发现 C# 索引为 0 行，重跑第 4 步即可。

---

## 八、做实际模组时的起点

### 仓库布局：仓库根 = 工作区根

`D:\caves of qud 模组制作\` **本身就是一个 git 仓库**（远程 `2Raine/2Raine_Ancient_Pioneer`）。
一份历史覆盖模组本体、设计笔记、工具与错误日志 —— 不会出现"笔记改了没提交"
或"工具在另一个目录所以没进库"这类漏洞。

```
mod/Toncihana_Elemental/     模组本体 —— 改代码改这里
_tools/                      查询、校验、预检工具（含 preflight.ps1、Reflect/）
preset/                      门禁插件与预设的仓库副本（已卸载，未挂载到 DSH）
docs/                        调研资料 + images/
sync.ps1                     工作区 <-> 游戏模组目录 双向同步
publish.ps1                  提交 + 推送到 GitHub
根                           设计笔记、错误日志.md、AGENTS.md、README.md
qud_db/  qud_src/            重建产物，已 .gitignore（重建法见 README）
```

**游戏从它自己的目录加载模组**，所以改完必须同步：

```powershell
cd "D:\caves of qud 模组制作"
powershell -File sync.ps1 diff       # 只看差异，不动文件
powershell -File sync.ps1 push       # 工作区 -> 游戏（改完走这个，否则游戏跑旧代码）
powershell -File sync.ps1 pull       # 游戏 -> 工作区（在游戏目录临时试改过之后用）
powershell -File publish.ps1 "说明"   # 提交 + 推送到 GitHub
```

脚本**绝不复制 `.dll` / `.pdb`** —— 那是游戏编译产物，在 `ModAssemblies\` 下。

**远端访问**：本机 `github.com:22` 被拒，`~/.ssh/config` 已把 github.com 指向
`ssh.github.com:443`，所以 `git@github.com:...` 可直接用。验证 `ssh -T git@github.com`
应回 `Hi 2Raine!`。GitHub 不会因 push 自动建库，首次必须先在网页建空库。

**关键设计文档**：`Toncihana_制作笔记与调参参考.md`（仓库根，2200+ 行）
—— 数值调参、踩坑记录、每条结论的出处。**改数值前先查这份。**

**常用定位**：

| 想找 | 看 |
| --- | --- |
| 种族 / 子类型 | `mod/Toncihana_Elemental/Genotypes.xml`、`Subtypes.xml` |
| 解剖与身体部位 | `Bodies.xml`（注意 `Category` 必须是 `BodyPartCategory` 的合法值） |
| 生物蓝图 | `ObjectBlueprints\Creatures.xml`、`2Raine_Toncihana_Bodies.xml` |
| 物品 / 书 / 技能 | `ObjectBlueprints\Items.xml`、`2Raine_Toncihana_Books.xml`、`2Raine_Toncihana_Skills.xml` |
| 刷新与派系 | `PopulationTables.xml`、`Factions.xml` |
| C# 逻辑 | `Scripts\`（部件在 `XRL.World.Parts`，技能在 `XRL.World.Parts.Skill`） |

### 美术脚本

`_tools\` 下：`recolour_tile.py`、`mirror_tile.py`、`render_two_variants.py`、
`make_cyf_contact_sheet.py`、`preview_colours.py`、`validate_mod.py`
