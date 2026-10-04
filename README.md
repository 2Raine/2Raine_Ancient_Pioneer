# 2Raine_Ancient_Pioneer

Caves of Qud 模组制作仓库。以父种族「远古开拓者 / Ancient Pioneer」命名，其下挂子类型。

当前唯一的子类型是 **锺起唤 / Toncihana**（Ton- + ci- + -hana + -a，"唤雷者"）。

游戏版本：**2.0.211.56**

---

## 目录结构

**仓库根 = 工作区根**（`D:\caves of qud 模组制作\`）。一份历史覆盖模组本体、
设计笔记、工具与错误日志 —— 不会出现"笔记改了没提交"或"工具在另一个目录
所以没进库"这类漏洞。

```
mod/Toncihana_Elemental/     模组本体 —— 改代码改这里
  manifest.json              模组清单
  Genotypes.xml              种族：2Raine_AncientPioneer_Elemental
  Subtypes.xml               子类型：2Raine_Toncihana_Elemental
  Bodies.xml                 解剖：2Raine_Elemental
  Factions.xml               派系：Elementals + 18 个中立化派系
  PopulationTables.xml       种群表（含 Merge 进原版 SaltDesertPerSector）
  Conversations.xml          约帕祭司对话
  ObjectBlueprints/          蓝图：生物、物品、书、技能、突变
  Scripts/                   C# 源码
  Textures/                  贴图

_tools/                      查询、校验、预检工具（30 个）
  qud.py                     ★ 主查询工具
  preflight.ps1              ★ 开工前 / 交付前自检
  validate_mod.py                XML/引用校验
  audit_references.py            全引用审计（按引擎解析路径逐项核对）
  check_csharp.ps1               独立编译校验（不启动游戏）
  blueprint_chain.py             蓝图继承链解析
  index_*.py / extract_blocks.py 数据库重建（幂等）
  rpm_analyze.py                 .rpm 地图解析
  Reflect/                       C# 反射工具

preset/                       DSH 插件预设副本（真身 ~/.dsh/.agent-presets/modder/）
docs/                         调研资料 + images/（渲染预览图）

根                            AGENTS.md、README.md、错误日志.md、
                              Toncihana_制作笔记与调参参考.md 等设计文档
                              sync.ps1、publish.ps1

qud_db/  qud_src/             重建产物，已 .gitignore（重建法见下文）
```

### 脚本一览

| 脚本 | 作用 |
| --- | --- |
| `sync.ps1 diff\|push\|pull` | 工作区 <-> 游戏模组目录 双向同步 |
| `publish.ps1 "说明"` | 提交 + 推送到 GitHub |
| `_tools\preflight.ps1 before\|after\|mistake\|log` | 开工前清单 / 交付前校验 / 记错误 / 看错误 |


---

## 同步脚本

游戏只从它自己的 `Mods\` 目录加载模组，而源码要在仓库里，所以两边各有一份。

```powershell
# 先看差异，不动文件
pwsh -File sync.ps1 diff

# 以工作区为准，覆盖游戏目录（改完代码走这个，然后重启游戏）
pwsh -File sync.ps1 push

# 以游戏目录为准，覆盖工作区（在游戏目录里临时试改过之后用）
pwsh -File sync.ps1 pull
```

脚本**绝不复制 `*.dll` / `*.pdb`** —— 那是游戏编译 `Scripts\*.cs` 之后写到
`%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\ModAssemblies\` 的产物。

> **改动生效规则**
> - 改 `.cs`：必须**完全重启游戏**才会重新编译
> - 改 XML：可以在游戏里 `wish reload`
> - 改 `Genotypes.xml` / `Subtypes.xml`：只影响**新建角色**
> - 区域内容一旦生成就不再受蓝图控制，要 `wish rebuild` 或开新档


---

## 提交与推送

建 GitHub 库的目的是**方便查看历史代码**。但如果推送要靠每次记得手敲
`git push`，它迟早会被忘掉 —— 那样仓库就退化成一份本地快照，建库白费。
所以把"改了代码"和"同步到 GitHub"绑成一条命令：

```powershell
# 提交 + 推送（最常用）
pwsh -File publish.ps1 "这次改了什么"

pwsh -File publish.ps1 "..." -NoPush   # 只提交，不推送
pwsh -File publish.ps1 -Status         # 只看状态
```

**每次改完代码都应该跑一次。** 完整的交付循环是：

```powershell
# 1) 改代码（在仓库里改，不要在游戏目录里改）
# 2) 同步到游戏 + 校验
pwsh -File sync.ps1 push
pwsh -File "..\_tools\preflight.ps1" after      # 编译 + XML/引用校验 + 日志 + git 状态
# 3) 推送到 GitHub
pwsh -File publish.ps1 "说明这次改了什么"
```

### 首次使用：远程库必须先存在

`git push` **不会自动建库**（GitHub 的规则，不是脚本的限制）。首次需要：

1. 在 https://github.com/new 建一个**空**库
   - 名字：`2Raine_Ancient_Pioneer`
   - **三个初始化选项全部不要勾**（README / .gitignore / license）
     —— 勾了会产生一个远程提交，和本地历史冲突
2. 配置远程并推送：

```powershell
cd "D:\caves of qud 模组制作"
git remote add origin git@github.com:2Raine/2Raine_Ancient_Pioneer.git
git push -u origin main
```

> **已完成。** 远程是 `git@github.com:2Raine/2Raine_Ancient_Pioneer.git`，
> 默认分支 `main`，`publish.ps1` 可直接用。

### SSH 走 443 端口

本机到 `github.com:22` 的连接会被拒，但 `ssh.github.com:443` 通。
`~/.ssh/config` 已经配好把 github.com 重定向到该端口，所以 `git@github.com:...`
这种地址可以直接用，不需要改 URL：

```
Host github.com
    HostName ssh.github.com
    Port 443
    User git
    IdentitiesOnly yes
    IdentityFile ~/.ssh/id_ed25519
```

验证：`ssh -T git@github.com` 应回 `Hi 2Raine!`

---

## 验证工具（改完必跑）

```powershell
$py = "C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"

# 1) 独立编译（不启动游戏，约 10 秒）
powershell -File tools\check_csharp.ps1

# 2) XML 结构 + 引用完整性
& $py tools\validate_mod.py

# 3) 全引用审计：按引擎的真实解析路径核对每一类引用
& $py tools\audit_references.py
```

`audit_references.py` 会检查 `<part>` / `<mutation>` / `<skill>` / `<stat>` /
`Factions=` / `Blueprint=` / `Inherits=` / `<table>` / `<anatomy Category=>`，
**并按引擎实际使用的前缀解析**，而不是按名字猜。

---

## 机制数据库

`tools/` 里的脚本背后是一套离线数据库（蓝图 5,221 个、机制 1,432 个、突变类 130 个）。
数据库本身**没有纳入仓库**（约 106 MB），用下面的命令重建，脚本全部幂等：

```powershell
$ilspy = "C:\Users\16064\.dotnet\tools\ilspycmd.exe"
$base  = "D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base"
$dll   = "D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll"
$out   = "<repo>\qud_db"
$src   = "<repo>\qud_src"

& $py tools\extract_blocks.py     --base $base --outdir $out
& $py tools\index_mechanisms.py   --dll $dll --blocks "$out\qud_blocks.sqlite" --outdir $out
& $ilspy -p -o $src --nested-directories $dll
& $py tools\index_csharp.py       --src $src --db "$out\qud_mechanisms.sqlite" --outdir $out

& $py tools\qud.py stats          # 自检，吻合率应为 100%
```

---

## 制作原则

这个项目**不接受没有依据的结论**。以下几条是踩过坑之后定下来的：

0. **遇到不确定的机制，按顺序查这三处，不要自己推模型。**
   1. `docs/Caves of Qud 模组制作入门指南.md` —— Wiki 整理，带大量更正标注
   2. `tools/qud.py` —— 机制 / 属性 / 事件 / 技能的离线数据库
   3. **原版是怎么做的** —— 在 `qud_src`（反编译树）里搜同类操作，
      看有多少处、写法是否一致

   **绝不要**：自己推一套模型 → 拿它当事实 → 再回头找证据维护它。
   这一条是被一次连续六轮的坐标错误逼出来的（见 `docs/Toncihana_制作笔记与调参参考.md`
   的〇之二十六）。mod 制作是说一不二的：是就是，不是就不是。

1. **不要从 XML 推断"某机制不存在"。**
   Qud 的数据 = XML 声明 + 代码默认值 + 代码后处理。
   已经错过三次同类问题，都是因为只看了 XML。

2. **坐标就是坐标。** 区域是 80x25，左上角 (0,0)，直接 `Z.GetCell(x, y)`。
   不要为坐标写换算函数。原版按坐标放对象有 573 处，没有一处做过换算；
   `x / 3` 那类运算只属于"在世界地图那一层找格子"。

3. **`extract_api.py` 的合成参数标记不可信。**
   要拿真实签名，就故意写一个错的调用，读编译器给的 `CS1502` / `CS1503` / `CS1739`。

4. **数据 XML 里不写注释。**
   `--` 会破坏良构性（已经踩过 6 次）。理由写进 `docs/Toncihana_制作笔记与调参参考.md`。

5. **绝不用 Harmony。**
   补丁失败会导致整个程序集中止加载。

6. **部件的命名空间是有意义的。**
   引擎按 `<part Name="X">` 解析 `XRL.World.Parts.X`，前缀是**无条件拼接**的，
   没有回退到裸类型名（`ModManager.ResolveType` 的文档注释在这一点上是错的）。
   所以模组自己的部件**必须放在 `XRL.World.Parts` 命名空间**，XML 里写裸名。

7. **序列化布局一旦发布就不能改。**
   `IComponent<T>.Read/.Write` 按字段声明顺序走，只能追加。

---

## 已知待办

- [ ] 精灵石作为食材 / 烹饪效果
- [ ] 雷书的秘密 → 地图标注（`JournalAPI.AddMapNote` + `RevealSecretOnRead`）
- [ ] 其他元素变体（火 / 冰 / 毒 / 盐）
- [ ] 元素生物掉落与吸收机制
- [ ] 五个雷系技能目前 `AbilitiesUnlocked = false`（新角色不获得，技能树里显示未学习）
