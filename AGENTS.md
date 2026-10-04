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
pwsh -File "D:\caves of qud 模组制作\_tools\preflight.ps1" before

# 交付前（全跑一遍校验，不通过就 exit 1 —— 那时不许声称完成）
pwsh -File "D:\caves of qud 模组制作\_tools\preflight.ps1" after

# 犯错后立刻记一条
pwsh -File "D:\caves of qud 模组制作\_tools\preflight.ps1" mistake "一句话描述这次错误"
pwsh -File "D:\caves of qud 模组制作\_tools\preflight.ps1" log
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

### 动模组前的固定流程

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
pwsh -File sync.ps1 push
pwsh -File _tools\preflight.ps1 after     # 编译 + XML/引用校验 + 日志 + git 状态，给结论
#    单独跑也可以：
#    powershell -File "$ws\_tools\check_csharp.ps1"      # exit=0 才算过
#    & $py "$ws\_tools\validate_mod.py"                  # 结构/命名/部件命名空间/解剖类别
#    & $py "$ws\_tools\audit_references.py"              # 按引擎真实解析路径核对引用

# 4) 让用户在游戏里实测，然后读日志
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log" `
              -Pattern '\[Toncihana\]|MODERROR.*Storm-Caller'

# 5) 提交并推送到 GitHub（一条命令）
pwsh -File publish.ps1 "说明这次改了什么"
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
pwsh -File sync.ps1 diff       # 只看差异，不动文件
pwsh -File sync.ps1 push       # 工作区 -> 游戏（改完走这个，否则游戏跑旧代码）
pwsh -File sync.ps1 pull       # 游戏 -> 工作区（在游戏目录临时试改过之后用）
pwsh -File publish.ps1 "说明"   # 提交 + 推送到 GitHub
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
