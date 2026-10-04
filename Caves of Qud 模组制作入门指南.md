# Caves of Qud 模组制作入门指南

> 依据官方 Wiki `Modding:Overview` 及其全部一级超链接整理，并结合本机实际安装的游戏文件（`D:\SteamLibrary`，版本 **2.0.211.56**）逐条核对。
> 所有引用的 XML / C# / JSON 片段均来自 Wiki 原文或游戏本体数据文件，未做臆造。

---

## 目录

1. [核心结论速览](#1-核心结论速览)
2. [本机实测路径（你电脑上的真实位置）](#2-本机实测路径你电脑上的真实位置)
3. [模组的文件结构与加载规则](#3-模组的文件结构与加载规则)
4. [配置文件：manifest.json / workshop.json / modconfig.json](#4-配置文件manifestjson--workshopjson--modconfigjson)
5. [三层能力阶梯：数据模组 / 脚本模组 / Harmony](#5-三层能力阶梯数据模组--脚本模组--harmony)
6. [XML 数据模组详解](#6-xml-数据模组详解)
   - 对象定义 · 继承 · 常用部件 · 种群表 · **对话** · **躯体** · 语法 · 其它根类型
7. [对象（生物 / 物品）与部件系统](#7-对象生物--物品与部件系统)
8. [贴图与渲染](#8-贴图与渲染)
9. [C# 脚本模组](#9-c-脚本模组)
   - 部件 · **事件系统** · Wish · **主动技能与 IActivePart** · StatShifter · Effect · **启动钩子** · **序列化** · 随机数 · 命名参数 · Harmony · 反编译
10. [世界、区域与地图](#10-世界区域与地图)
    - ZoneID · Worlds.xml · 区域 builder · 地图（.rpm）· 地图编辑器 · 内部区域 · 任务 · 变异 · 宠物
11. [调试方法论](#11-调试方法论)
12. [兼容性与"守规矩"](#12-兼容性与守规矩)
13. [哪些东西没有 XML 接口](#13-哪些东西没有-xml-接口)
14. [发布到 Steam 创意工坊](#14-发布到-steam-创意工坊)
15. [完整最小可运行示例](#15-完整最小可运行示例)
16. [学习路线与资源](#16-学习路线与资源)
- 附录 A：[完整调色板](#附录-a完整调色板) · B：[核心概念一句话总结](#附录-b一句话总结每个核心概念) · C：[脚本模组 IDE 工程](#附录-c脚本模组的-ide-工程可选但强烈推荐) · D：[代码页 437](#附录-d代码页-437cp437符号) · E：[自定义玩家贴图](#附录-e自定义玩家贴图--预设角色)

> **⚠️ 阅读提示**：本文档会对 Wiki 的**已知错误和文档缺口**做显式标注。看到 ⚠️ 的地方请务必读完 —— 那些都是会让新手卡住几小时的坑。凡是标注"Wiki 上没有文档"的内容，都需要你自己反编译 `Assembly-CSharp.dll` 确认，不要照猜。

---

## 1. 核心结论速览

| 问题 | 答案 |
| --- | --- |
| 模组本质是什么？ | 游戏数据目录 `Mods/` 下的**一个文件夹**，里面放 `.xml`（数据）、`.cs`（脚本）、`Textures/`（图片） |
| 需要写代码吗？ | 不需要。**数据模组**用 XML 就能加生物、物品、对话、任务、地点、阵营、基因型 |
| 游戏本体怎么做内容？ | 用的是**同一套 XML 机制** —— 所以本体数据文件就是最好的教材 |
| 两类文件放哪？ | `manifest.json`、`workshop.json` 必须在模组根目录；`.xml`、`.cs`、`Textures/` 可以嵌套在任意子目录 |
| 文件名重要吗？ | 不重要，只要后缀是 `.xml` / `.cs`。**决定含义的是最外层标签**，例如 `<objects>` |
| 改东西要抄整段吗？ | 不要。用 `Load="Merge"` 只写**有差异的部分** |
| 怎么调试？ | 看 `Player.log`（运行时）、`build_log.txt`（加载期）、`harmony.log.txt`（Harmony） |
| 怎么在游戏里看到自己的东西？ | 用 **Wish（许愿）** 命令：`wish` 然后输入蓝图 ID |
| 改动后怎么生效？ | XML 可以游戏内 `wish reload`；C# 必须**重启游戏** |

模组按"能力"分三层，难度和维护成本递增：

- **数据模组（Data mod）** —— XML 定义对象、躯体、对话、种群表。本体玩法就是这么写的。
- **脚本模组（Script mod）** —— C# 代码加新逻辑。关闭 "Allow scripting mods" 即可全部禁用。
- **Harmony 模组** —— 最强大也最脆弱，可以往任意位置插代码。**Wiki 明确建议：只当最后手段**。

### 已知的数据根元素（决定 XML 文件的含义）

| 根标签 | 定义的内容 |
| --- | --- |
| `<objects>` | 对象蓝图（生物、物品、家具、尸体、模板） |
| `<bodies>` | `bodyparttypes` / `bodyparttypevariants` / `anatomies` |
| `<conversations>` | 对话模板（按 `ID` 索引） |
| `<populations>` | 现代刷新系统（`population` / `group` / `object` / `table`） |
| `<encountertables>` | **遗留**刷新系统（`encountertable` / `objects` / `object`） |
| `<embarkmodules>` | 角色创建预设（`module` / `pregens` / `pregen`） |
| `<genotypes>` / `<subtypes>` | 基因型 / 亚型 |
| `<wishcommands>` | 许愿菜单项 |
| `<mods>` | 物品改造 |
| `<help>` | 游戏内帮助文本 |

> ⚠️ 关于刷新系统有个重要事实：Wiki 上 `Modding:Populations` 页面挂着横幅 **"Everything about encounter tables is out of date."**。游戏里**两套刷新系统并存** —— 遗留的 `EncounterTables.xml` 系统，和较新的 `ZoneTemplates.xml` + `PopulationTables.xml` 系统，而**游戏至今仍有大量内容通过 `EncounterTables.xml` 刷新**。写刷新逻辑时两处都要留意。

---

## 2. 本机实测路径（你电脑上的真实位置）

> 已在你机器上逐条验证存在。

### 2.1 你要写模组的地方（"离线模组"目录）

```
C:\Users\16064\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\
```

当前里面已有 6 个示例模组，**强烈建议先打开看**：

```
Mods\
├── Freehold_Pet_Gloaming\     ← 宠物 + 对话 + 书本（官方示例宠物模组）
├── Freehold_Pet_EitherOr\
├── Freehold_Pet_ImportantFish\
├── Freehold_Pet_Uthabatafah\
├── Freehold_Pet_YouButMechanical\
└── supersoupsludge\           ← 基因型/亚型 + 起始装备 + 自定义贴图（结构最完整的小模组）
```

### 2.2 游戏本体数据文件（最重要的参考资料，**只读，不要改**）

```
D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base\
```

关键文件（你机器上实测大小）：

| 文件 | 大小 | 作用 |
| --- | --- | --- |
| `ObjectBlueprints\Creatures.xml` | 965 KB | 全部生物蓝图 |
| `ObjectBlueprints\Items.xml` | 806 KB | 全部物品蓝图 |
| `Bodies.xml` | 69 KB | 躯体部位与解剖结构 |
| `Conversations.xml` | 647 KB | 对话脚本 |
| `PopulationTables.xml` | 850 KB | 种群表（决定什么怪在哪刷） |
| `Mutations.xml` | 17 KB | 变异 |
| `Skills.xml` | 53 KB | 技能树 |
| `Quests.xml` | 28 KB | 任务 |
| `Worlds.xml` | 155 KB | 世界与区域 |
| `Colors.xml` / `Genders.xml` / `Genotypes.xml` / `Subtypes.xml` | — | 颜色 / 性别 / 基因型 / 亚型 |
| `Mods.xml` | 13 KB | 物品改造（item mod）定义 |
| `ActivatedAbilities.xml` | 80 KB | 主动技能 |
| `*.rpm` | — | 静态地图文件（区域地图） |

### 2.3 日志（调试必看）

```
C:\Users\16064\AppData\LocalLow\Freehold Games\CavesOfQud\
├── Player.log              ← 运行期错误（搜 MODERROR / MODWARN）
├── build_log.txt           ← 加载期/编译期错误（搜 Success / MISSING / error）
├── harmony.log.txt         ← Harmony 补丁记录（含 IL 转储）
└── ModAssemblies\          ← 游戏编译 .cs 后生成的 DLL 存放处
```

你机器上 `build_log.txt` 的实际内容示范了编译流程：

```
==== BUILDING MODS ====
Defined symbol: VERSION_1_0
Defined symbol: BUILD_2_0_211
=== EULE`S EXILE'S REFUGE: WIGHT UPDATE ===
Compiling 1 file...
Success :)
Location: ...\ModAssemblies\3104658246.dll
```

### 2.4 其它

| 用途 | 路径 |
| --- | --- |
| C# 引用的托管库（`QudLibPath`） | `D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\`（含 `Assembly-CSharp.dll` 11.8 MB、`0Harmony.dll`） |
| 已装创意工坊模组 | `D:\SteamLibrary\steamapps\workshop\content\333640\<工坊ID>\`（当前有 44 个） |
| 存档 | `...\CavesOfQud\Synced\Saves\` |
| 游戏版本 | **2.0.211.56**（标题画面右下角：亮色是 `MarketingVersion`，`build` 开头暗色是 `CoreVersion`） |

---

## 3. 模组的文件结构与加载规则

### 3.1 目录结构

```
你的模组名\
├── manifest.json          ← 必须在根目录（元数据 + 加载控制）
├── workshop.json          ← 必须在根目录（创意工坊上传配置）
├── modconfig.json         ← 可选，贴图着色器/尺寸配置
├── preview.png            ← 模组预览图（建议 512×512）
├── ObjectBlueprints\
│   └── Creatures.xml      ← 文件名随意，但建议模仿本体命名便于对照
├── Bodies.xml
├── PopulationTables.xml
├── Conversations.xml
├── Textures\
│   └── 你的前缀\
│       └── my_creature.png
└── Scripts\
    └── MyPart.cs
```

**规则要点：**

- `manifest.json` 和 `workshop.json` **必须**在根目录。
- 所有 `.xml` 文件**可以嵌套在任意深度**，文件名任意，只要后缀是 `.xml`。
- 所有 `.cs` 文件同理，文件名不影响编译。
- 贴图放在 `Textures/` 下；**引用时不要写 `Textures/`**。
  例：文件 `Textures\Pyovya_SnapjawMage\snapjaw_mage.png` → 引用为 `Pyovya_SnapjawMage/snapjaw_mage.png`。

### 3.2 加载机制的三个关键点

1. **最外层标签决定数据类型。** `<objects>` → 对象蓝图；`<bodies>` → 躯体；`<populations>` → 种群表；`<conversations>` → 对话；`<genotypes>` → 基因型。
2. **一个 XML 文件只能有一个根元素。** 根元素之后的任何内容会被**静默忽略**（不报错）。
3. **文件名不影响任何事**，只影响你自己的可读性。

### 3.3 `Load` 三种策略（最核心的概念）

| 写法 | 含义 |
| --- | --- |
| （不写） | **`objects` 根下默认是"替换"** —— 同名对象被后加载的完全覆盖 |
| `Load="Merge"` | 与已有定义**合并**，只写差异部分 |
| `Load="Remove"` | 删除一个已有条目 |
| `Load="Replace"` | 用新条目覆盖已有条目 |
| `Load="Add"` | 仅见于对话元素（`Modding:Conversations` 列出的有效值为 Merge / Replace / Add / Remove） |

⚠️ **最容易踩的坑**：`objects` 根下的默认行为是**替换**而非合并（和大多数其它根类型相反，别的根类型默认就合并）。所以想改 Ctesiphus 的颜色，**必须**写：

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
  <object Name="Ctesiphus" Load="Merge">
    <!-- 把 Ctesiphus 变蓝 -->
    <part Name="Render" ColorString="&amp;B" />
  </object>
</objects>
```

如果漏掉 `Load="Merge"`，就会**新建一个同名对象并抹掉本体那只猫的全部数据**。

`populations` 根下的规则要更细一层：种群表本身默认合并，但 `population` **内部**的元素（如 `<object>`）默认代表新增条目；想让它们改为"修改已有条目的属性"，也要显式写 `Load="Merge"`。

### 3.4 XML 语法硬性要求

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
  <!-- 这是注释 -->
  <object Name="Foo" />
  <object Name="Bar"></object>   <!-- 与上一行等价 -->
</objects>
```

- **标签必须闭合。** 这是新手第一大错误来源。
- `&` 必须写成实体 `&amp;`。所以颜色 `&B` 在 XML 里写成 `ColorString="&amp;B"`。
- 其它实体：`&lt;` `<`、`&gt;` `>`、`&quot;` `"`。
- 建议第一行固定写 `<?xml version="1.0" encoding="utf-8"?>`。
- 编码提示：Qud 历史上一律按 **code page 437** 解释文本，即使声明 UTF-8。想让 `áéíóú` 正确显示需要在根元素上加 `Encoding="utf-8"` 属性（`<objects Encoding="utf-8">`），但**该功能目前只在 `lang-experimental` 分支可用**。

---

## 4. 配置文件：manifest.json / workshop.json / modconfig.json

三个文件**全部可选**，都放在模组根目录。JSON 键**大小写不敏感**。

### 4.1 `manifest.json` —— 显示 + 加载控制

| 字段 | 说明 |
| --- | --- |
| `ID` | 主标识符，模组管理器里的键，也是别的模组依赖你时用的名字。**建议只用字母数字**（它还会被当作编译预处理符号） |
| `Title` | 模组管理器里显示的名字，支持颜色标记 |
| `Description` | 简短描述，支持颜色标记 |
| `Version` | 版本号 |
| `Author` | 作者，支持颜色标记 |
| `Tags` | 逗号分隔，**仅用于管理器显示**，与工坊标签无关 |
| `PreviewImage` | 图标相对路径，建议 512×512 |
| `LoadOrder` | 整数，**越小越先加载**。**自 build 210 起已被 `Dependencies` 取代** |
| `Dependencies` | `{ "模组ID": "版本范围" }`，要求先加载的模组（**build 210 新增**） |
| `Dependency` | 单个依赖的简写，与 `Dependencies` 互斥 |
| `LoadBefore` / `LoadAfter` | 单个 ID 或 ID 数组，声明相对加载顺序 |
| `Directories` | 目录对象数组，可按游戏版本/依赖条件**条件加载不同目录**（**build 210 新增**） |

`Directories` 里每个条目支持：`Path`/`Paths`、`Version`（匹配 `MarketingVersion`）、`Build`（匹配 `CoreVersion`）、`Dependency`/`Dependencies`、`Exclusion`/`Exclusions`、`Option`/`Options`。路径**区分大小写**（在某些操作系统上），且**禁止逃出模组目录**。

**版本范围语法：**

| 写法 | 含义 |
| --- | --- |
| `*` | 任意版本 |
| `1.0.*` | ≥ 1.0.0 且 < 1.1.0 |
| `2.0.208 - 3.0.0` | 闭区间 ≥ 2.0.208 且 ≤ 3.0.0 |
| `2.0.209.52 - *` | ≥ 2.0.209.52 |
| `>3.5` | ≥ 3.6.0（注意进位语义） |
| `>=2 <5` | ≥ 2.0.0 且 < 5.0.0 |
| `^0.5.2 \|\| 7.2.1` | ≥ 0.5.2 且 < 0.6.0，**或** 等于 7.2.1 |

**最小可用 `manifest.json`：**

```json
{
    "ID": "Alice_MyFirstMod",
    "Title": "我的第一个模组",
    "Description": "给 Caves of Qud 添加一只新生物。",
    "Version": "0.1.0",
    "Author": "Alice",
    "Tags": "Creature",
    "PreviewImage": "preview.png"
}
```

**进阶版（演示条件目录）：**

```json
{
    "ID": "Pyovya_SnapjawMage",
    "LoadOrder": 1,
    "Title": "{{R|Snapjaw}} {{C|Mages}}!",
    "Description": "Adds the new {{Y|snapjaw}} {{R|fire}} {{Y|mage}} and {{Y|snapjaw}} {{C|ice}} {{Y|mage}} creatures to Caves of Qud.",
    "Version": "0.1.0",
    "Author": "{{M|Pyovya}}",
    "Tags": "Creature",
    "PreviewImage": "preview.png",
    "LoadBefore": "SightlessFray",
    "LoadAfter": [ "Tamago_PlatypusCommune", "ChromeGarlands" ],
    "Dependencies": {
        "Pyovya_SaltOrphan": "1.0.0 - *"
    },
    "Directories": [
        { "Paths": [ "/Common/", "/Assets/Textures/" ] },
        { "Path": "/Old/", "Build": "<2.0.209.43" },
        { "Paths": [ "/NewCS/", "/NewXML/" ], "Build": ">=2.0.209.43" },
        {
            "Path": "/GooeyAddon/",
            "Version": ">=1.0.0",
            "Options": [ "OptionSnapjawMage_AddGooeyIck == Yes", "OptionSound != No" ],
            "Dependencies": { "Momo_CyberneticGenders": "^2.*", "IckySounds": ">=999.7.1.X" }
        },
        {
            "Path": "/SaltAddon/",
            "Option": "OptionSnapjawMage_AddRiotSalt == Yes",
            "Dependency": "Yarif_RiotCooking",
            "Exclusion": "Momo_Saltlicks"
        }
    ]
}
```

> `Options` 有个怪癖：包含被引用选项的那个 XML，**文件名里必须含有 `Option` 字样**。

注意：**实际游戏里的小写键同样有效**。你机器上 `supersoupsludge\manifest.json` 用的就是全小写：

```json
{
  "id": "Supersoupsludge",
  "title": "{{biomech|Soupysludge Genotype}}",
  "description": "Lets you play as soupysludge",
  "tags": "Genotype, Subtype, Item",
  "version": "1.0",
  "author": "horndycibar",
  "previewImage": "preview.png"
}
```

### 4.2 `workshop.json` —— 创意工坊上传

通常**不用手写**，用游戏内的 Workshop Uploader 生成和修改。

| 字段 | 说明 |
| --- | --- |
| `WorkshopId` | 工坊上的唯一 ID |
| `Title` | 工坊显示的标题 |
| `Description` | 工坊描述，支持 Steam 格式化标签 |
| `Tags` | 逗号分隔，建议复用现有工坊标签便于检索 |
| `Visibility` | `"0"` 私有 / `"1"` 仅好友 / `"2"` 公开 |
| `ImagePath` | 预览图相对路径，建议 512×512 |

```json
{
  "WorkshopId": 2995934012,
  "Title": "Snapjaw Mages",
  "Description": "[h1]Snapjaw Mages[/h1]\n\nThis mod adds the new [b]Snapjaw Mage[/b] creature to Caves of Qud.",
  "Tags": "Creatures",
  "Visibility": "2",
  "ImagePath": "Preview.png"
}
```

> **小技巧**：把 `Visibility` 手动固定成 `"2"`，可以防止游戏在每次更新推送时把模组自动改成私有。

### 4.3 `modconfig.json` —— 全模组贴图设置

| 字段 | 说明 |
| --- | --- |
| `ShaderMode` | `0` = 默认三色着色器；`1` = 真彩色着色器（**目前只对世界内渲染生效，多数 UI 不支持**） |
| `TextureWidth` / `TextureHeight` | 贴图像素宽高 |

```json
{
  "shaderMode": 0,
  "textureWidth": 16,
  "textureHeight": 24
}
```

### 4.4 `config.json`

已在 **2.0.201.44** 版本废弃，功能被 `manifest.json` 取代。**新模组不要用。**

---

## 5. 三层能力阶梯：数据模组 / 脚本模组 / Harmony

| 我想加… | 能纯 XML 做吗 | 推荐页面 | 备注 |
| --- | --- | --- | --- |
| 对话 | ✅ | Modding:Conversations | XML 支持很完善，必要时可用 C# 写新行为 |
| 生物 / NPC | ✅ | Modding:Objects | 大量属性可 XML 定义，**独特行为一般要 C#** |
| 阵营 | ✅ | — | 关心的属性大多可 XML 定义 |
| 基因型 / 亚型 | ✅ | Modding:Genotypes_and_Subtypes | 同上 |
| 物品 | ✅ | Modding:Objects | 同上 |
| **物品改造（item mod）** | ❌ | — | 有 XML 接口，但只覆盖部分属性 |
| **液体** | ❌ | Modding:Liquids | **完全没有 XML 接口**（`lang-experimental` 分支上有一个在开发） |
| 地点 / 世界 | ✅ | Modding:Intro_-_Zones_and_Worlds | XML 支持很完善 |
| 音乐 / 音效 | ✅ | — | 分别挂到地点数据、物品数据上 |
| **变异** | ❌ | Modding:Mutations | 有 XML 接口，但只覆盖部分属性 |
| 预设角色 | ✅ | Modding:Tutorial_-_Custom_Player_Tiles | 大多属性可 XML 定义 |
| **配方（recipe）** | ❌ | — | **完全没有 XML 接口**，未来可能加入 |
| 任务 | ✅ | Modding:Quests | 可完全靠对话驱动；复杂逻辑用 C# |
| **技能** | ❌ | — | 有 XML 接口，但只覆盖部分属性 |
| **状态效果** | ❌ | — | **完全没有 XML 接口** |
| **Wish 命令** | ❌ | Modding:Wishes | **完全没有 XML 接口** |

**实践建议**：永远从"能不能用数据模组做"这个问题开始。绝大多数"加内容"的需求 XML 就够，只有当你要**全新的运行时行为**时才动 C#。

---

## 6. XML 数据模组详解

### 6.1 本体数据文件导读（写模组前先读这里）

写 XML 模组时 90% 的时间你只需要看两个文件：

- `ObjectBlueprints\Creatures.xml` —— 所有生物
- `ObjectBlueprints\Items.xml` —— 所有物品

其它按需查阅：`Bodies.xml`（解剖结构）、`Conversations.xml`（对话）、`Mutations.xml`（变异）、`PopulationTables.xml`（刷新表）、`Skills.xml`（技能树）、`Quests.xml`（任务）。

> ❗ **绝对不要修改本体数据文件。** 改坏了可能要重装游戏。所有改动都写在你自己模组的文件夹里。

### 6.2 对象定义的解剖

`<object>` 支持以下子标签（来自 `Modding:Objects`）：

| 子标签 | 说明 |
| --- | --- |
| `<part>` | 加载指定名称的部件（part）。"部件"就是任何继承 `IPart` 的 C# 类 |
| `<mutation>` | 给生物加变异；重定义已有变异时按属性合并覆盖 |
| `<builder>` | — |
| `<skill>` | 加技能；重定义已有技能时按属性合并覆盖 |
| `<inventoryobject>` | 加入生物背包。`Number` 指定数量；蓝图名可用 `@` 前缀从种群表采样，如 `Blueprint="@DynamicObjectsTable:EnergyCells:Tier{ownertier}"` |
| `<stat>` | 设置属性值 |
| `<property>` | 设置性质 |
| `<intproperty>` | 设置整数性质 |
| `<xtag>` | 扩展标签 |
| `<tag>` | 标签。可用 `Value="*delete"` 删除已有标签，但**目前有 bug**：与 `Load="Merge"` 组合或在继承来的标签上无效 |
| `<stag>` | 把对象加入"动态语义表" |
| `<mixin>` | 混入，替代或补充继承，自动从另一个对象拷贝部件和标签 |

`<mixin>` 的属性：`Name`（用于混入的蓝图）、`Include`（逗号分隔，要包含的元素）、`Exclude`（逗号分隔，要排除的元素）、`Priority`（整数，越小越早）、`Load`（值为 `Fill` 时把混入插在正常继承**之前**，否则之后）。

**反向操作**：大多数对象标签都有对应的 `remove*` 版本。`<removepart Name="..." />` 移除部件；`<removemutation Name="..." />` 移除变异。

### 6.3 继承（`Inherits`）与基础对象

```xml
<object Name="Pyovya_SnapjawMage_Snapjaw Mage" Inherits="Snapjaw">
</object>
```

- `Name` 是**唯一内部标识符**（不是游戏里显示的名字），也是你用 Wish 时要输入的字符串。
- `Inherits` 表示继承 `Snapjaw` 的全部属性。

**把某个对象做成"仅供继承的基类"**（本身不在游戏里出现）：

```xml
<tag Name="BaseObject" Value="*noinherit" />
```

本体的 `Snapjaw` 就定义在 `Creatures.xml` 第 2407 行：`<object Name="Snapjaw" Inherits="BaseHumanoid">`。

### 6.4 常用部件速查

以下是 Wiki `Modding:Objects` 列出的最常用部件（**远不完整**，共有一千多个）。

**通用**

| 部件名 | 关键属性 | 作用 |
| --- | --- | --- |
| `Description` | `Short` | 查看对象时的描述文本 |
| `Render` | `DisplayName`, `RenderString`, `RenderLayer`, `RenderIfDark`, `DetailColor`, `ColorString`, `Tile` | 渲染外观 + 游戏内显示名 |
| `DeployWith` | `Blueprint`, `PreferredDirection`, `SameCell`, `Chance`, `CarryOverOwner` | 与其它对象一起生成 |
| `Food` | `Message`, `Satiation`, `Thirst`, `Healing` | 使对象可食用 |
| `Interesting` | `Key`, `Radius`, `IconTile`, ... | 是否算"值得注意"（可被"移动到兴趣点"选中） |
| `RandomColors` | `DetailColor`, `TileColor` | 随机主色/细节色（逗号分隔列表） |
| `RandomTile` | `Tiles` | 从逗号分隔列表中随机选贴图 |

**生物 / 尸体专用**

| 部件名 | 关键属性 | 作用 |
| --- | --- | --- |
| `Body` | `Anatomy` | 使用哪种躯体类型 |
| `Brain` | `Hostile`, `Calm`, `Factions` | 默认敌意状态与所属阵营 |
| `Corpse` | `CorpseBlueprint`, `CorpseRequiresBodyPart`, `Burnt...`, `Vaporized...` | 死亡后留下的尸体 |
| `Butcherable` | `OnSuccessAmount`, `OnSuccess` | 可用于屠宰 |
| `Consumer` | `Chance`, `WeightThresholdPercentage`, `Message` | 吞食路径上的东西（盐章鱼用） |
| `ConversationScript` | `ConversationID`, `Quest`, `PreQuestConversationID`, ... | 指定对话脚本 ID |
| `Followers` | `Table` | 生成时按种群表生成随从 |
| `GenerateName` | `SpecialType`, `NamingContext` | 生成时赋予专有名字 |
| `GivesRep` | `repValue` | 允许水之仪式 |
| `MentalShield` | — | 心灵护盾 |
| `SocialRoles` | `Roles` | 名字后的"身份"（如"Yd Freehold 居民"） |
| `Titles` | `Primary`, `Ordinary` | 名字后的头衔 |

**物品 / 家具专用**

| 部件名 | 关键属性 | 作用 |
| --- | --- | --- |
| `Physics` | `Weight`, `Conductivity`, `FlameTemperature`, `Solid`, `Takeable`, `Category`, `UsesTwoSlots`, `Owner` | 物理属性：重量、两格装备、燃烧/冰冻温度、分类 |
| `Commerce` | `Value` | 交易价值 |
| `Armor` | `AV`, `DV`, `MA`, `Acid`, `Elec`, `Cold`, `Heat`, 各属性加成, `WornOn` | 护甲数值与可装备部位 |
| `MeleeWeapon` | `MaxStrengthBonus`, `BaseDamage`, `Skill`, `Stat`, `Slot` | 近战武器伤害/技能树/槽位 |
| `MissileWeapon` | `ShotsPerAction`, `AmmoPerAction`, `MaxRange`, `WeaponAccuracy`, `AmmoChar`, `Skill`, `SlotType` | 远程武器行为 |
| `Projectile` | `BasePenetration`, `BaseDamage`, `ColorString`, `RenderChar`, `PassByVerb` | 弹体伤害与外观 |
| `MagazineAmmoLoader` | `ProjectileObject`, `AmmoPart`, `MaxAmmo` | 需要弹药的远程武器 |
| `EnergyAmmoLoader` | `ProjectileObject`, `ChargeUse`, `IsPowerLoadSensitive` | 消耗能量电池 |
| `LiquidAmmoLoader` | `ProjectileObject`, `Liquid`, `ShotsPerDram` | 消耗液体（喷火器） |
| `CooldownAmmoLoader` | `ProjectileObject`, `Cooldown`, `Readout` | 只有冷却、不耗弹药 |
| `BioAmmoLoader` | `MaxCapacity`, `TurnsToGenerate`, `ProjectileObject` | 自体再生弹药（鼻涕虫） |
| `LiquidVolume` | `InitialLiquid`, `MaxVolume`, `StartVolume` | 可装液体 |
| `EnergyCellSocket` | `SlotType`, `ChanceSlotted`, `ChanceFullCell` | 可插能量电池 |
| `Examiner` | `Complexity`, `Difficulty`, `Alternate` | 神器鉴定难度 |
| `TinkerItem` | `Bits`, `CanDisassemble`, `CanBuild`, `Ingredient` | 是否可拆解/建造 |
| `CyberneticsBaseItem` | `Slots`, `Cost`, `BehaviorDescription` | 可植入义体 |
| `MutationOnEquip` | `Level`, `ClassName`, `Constructor` | 装备时赋予变异 |
| `Chat` | `Says`, `ShowInShortDescription` | 物品上刻的字（招牌用） |
| `Animated` | `ChanceOneIn` | 概率成为"活化"物品 |
| `BootSequence` | `BootTime`, `SoundOnBootDone`, ... | 启动序列 |
| `Harvestable` | `RipeTileColor`, `UnripeTileColor`, `StartRipeChance` | 可采收 |
| `Interior` | `WX`, `WY`, `X`, `Y`, `Z`, `Unique` | 给予内部空间 |
| `Metal` | — | 是否金属材质 |
| `Springy` | `Factor` | 弹性（减少爆炸击退） |

### 6.5 实例：一个完整的生物定义

这是 Snapjaw Mages 教程中反复迭代后的最终形态：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<objects>
  <object Name="Pyovya_SnapjawMage_Snapjaw Mage" Inherits="Snapjaw">
    <part Name="Description" Short="A tattered robe and decaying hat are all that protect =pronouns.possessive= thin layer of grizzled fur from the forces of nature. But behind that furtive glance, =pronouns.subjective= =verb:prepare:afterpronoun= an elemental spell, deployed at a moment's notice against threats to =pronouns.objective= and =pronouns.possessive= kin. =pronouns.Subjective= =verb:understand:afterpronoun= not the power that =pronouns.subjective= =verb:wield:afterpronoun=; and that only makes =pronouns.objective= all the more dangerous." />
    <part Name="Render" DisplayName="snapjaw mage" Tile="Pyovya_SnapjawMage/snapjaw_mage.png" ColorString="&amp;O" DetailColor="Y" />

    <inventoryobject Blueprint="Walking Stick" Number="1" />
    <inventoryobject Blueprint="Cloth Robe" Number="1" />
    <inventoryobject Blueprint="Sandals" Number="1" />
    <inventoryobject Blueprint="StandaloneMarkovBook" Number="1-2" Chance="10" />

    <skill Name="Cudgel" />

    <stat Name="Hitpoints" Value="15" />
    <stat Name="DV" Value="4" />
    <stat Name="Ego" sValue="19" />
    <stat Name="Willpower" sValue="19" />
  </object>
</objects>
```

要点解读：

- `<inventoryobject ... Number="1-2" Chance="10" />` = **10% 概率携带 1~2 本书**。
- `<stat Name="Ego" sValue="19" />` —— `sValue` 表示"设为该值"。⚠️ 但**除非你在做独特生物，优先用 `value` 而不是 `sValue`**：蓝图加载时会同时读入 `sValue` 和 `value`，若 `sValue` 有值则优先使用它。
- 描述文本里的 `=pronouns.subjective=`、`=verb:understand:afterpronoun=` 是**语法替换标记**（见 6.8）。

**派生两个子类型**（火法师 / 冰法师）：

```xml
<object Name="Pyovya_SnapjawMage_Fire Mage" Inherits="Pyovya_SnapjawMage_Snapjaw Mage">
  <part Name="Render" DisplayName="snapjaw {{R|fire}} mage" Tile="Pyovya_SnapjawMage/snapjaw_mage.png" ColorString="&amp;O" DetailColor="R" />
  <mutation Name="Pyrokinesis" Level="1" />
</object>

<object Name="Pyovya_SnapjawMage_Ice Mage" Inherits="Pyovya_SnapjawMage_Snapjaw Mage">
  <part Name="Render" DisplayName="snapjaw {{C|ice}} mage" Tile="Pyovya_SnapjawMage/snapjaw_mage.png" ColorString="&amp;O" DetailColor="C" />
  <mutation Name="Cryokinesis" Level="1" />
</object>
```

`{{R|fire}}` 是"把 fire 这个词渲染成红色"。

### 6.6 从已有物品"抄"出魔法书（真实本体片段）

教程教你直接打开本体 `ObjectBlueprints/Items.xml` 搜索 `Flamethrower` 和 `Freeze Ray`，照抄它们的部件组合：

```xml
<object Name="Freeze Ray" Inherits="BaseRifle">
  <part Name="Render" DisplayName="{{freezing|freeze}} ray" Tile="items/sw_raygun.bmp" ColorString="&amp;C" DetailColor="y" />
  <part Name="Physics" UsesTwoSlots="true" Weight="28" />
  <part Name="MissileWeapon" Skill="Rifle" AmmoChar="FR" ShotsPerAction="1" AmmoPerAction="1" ShotsPerAnimation="1" WeaponAccuracy="0" />
  <part Name="Commerce" Value="750" />
  <part Name="EnergyAmmoLoader" ChargeUse="500" ProjectileObject="ProjectileFreezeRay" />
  <part Name="EnergyCellSocket" SlotType="EnergyCell" />
  <part Name="Description" Short="Gaseous coolant billows through a chiffon tube..." />
  <part Name="Examiner" Complexity="5" />
  <part Name="TinkerItem" Bits="12345" CanDisassemble="true" CanBuild="true" />
  <part Name="Metal" />
  <part Name="ItemElements" Elements="ice:10" />
  <tag Name="MissileFireSound" Value="lazerMedium4" />
  <tag Name="Mods" Value="MissileWeaponMods,FirearmMods,CommonMods,RifleMods,ElectronicsMods,BeamWeaponMods" />
  <tag Name="Tier" Value="5" />
  <tag Name="DynamicObjectsTable:Guns" />
  <stag Name="Cold" />
</object>
```

**这个"读本体 → 猜部件作用 → 组合出新东西"的循环，就是 Qud 数据模组的主要工作方式。**

### 6.7 种群表：控制刷新

**静态表**：定义一张新表，再用 `<table>` 把它挂到已有的刷新组里。

```xml
<?xml version="1.0" encoding="utf-8" ?>
<populations>
  <population Name="Pyovya_SnapjawMage_Mages">
    <group Name="Mages" Style="pickone">
      <object Number="1" Blueprint="Pyovya_SnapjawMage_Fire Mage" />
      <object Number="1" Blueprint="Pyovya_SnapjawMage_Ice Mage" />
    </group>
  </population>

  <population Name="SnapjawParty0" Load="Merge">
    <group Name="Creatures" Load="Merge">
      <table Name="Pyovya_SnapjawMage_Mages" />
    </group>
  </population>
</populations>
```

注意三层 `Load="Merge"`：种群表本身、`group`、以及新增的 `<table>` 引用。

**动态表**（不需要写种群表，靠标签自动归类）：

```xml
<object Name="Shrewd Baboon" Inherits="Baboon">
  <part Name="Render" DisplayName="shrewd baboon" ColorString="&amp;B" />
  <stat Name="AV" Value="3" />
  <stat Name="Intelligence" Boost="1" />
  <stat Name="Hitpoints" Value="20" />
  <property Name="Role" Value="Leader" />
  <tag Name="DynamicObjectsTable:Baboons" />
</object>
```

三种动态表机制：

| 机制 | 含义 |
| --- | --- |
| `DynamicObjectsTable:XXX` | 所有显式打了该标签的对象 |
| `DynamicInheritsTable:XXX` | 所有**继承**自 XXX 的对象（**继承链是传递的**） |
| `DynamicSemanticTable` | 按交叉分类（如"medical" + "furniture"） |

```xml
<!-- 继承表：可按等级加权 -->
<table Name="DynamicInheritsTable:Tool" Chance="15" />
<table Name="DynamicInheritsTable:BaseLongBlade:Tier5" />

<!-- 语义表：要求同时有 <stag Name="Medical" /> 和 <stag Name="Furniture" /> -->
<table Chance="80" Number="1-8" Name="DynamicSemanticTable:Medical,Furniture:4:6" Hint="AlongWall" />
```

`DynamicInheritsTable:BaseLongBlade:Tier5` 的意思是：**正好 5 级**的物品权重 1000，**上下差 1 级**的权重 100，**差 2 级**的权重 10，其余权重 1。
`DynamicSemanticTable:Medical,Furniture:4:6` 里 `4:6` 表示等级越接近 4、或科技等级越接近 6，权重越高。

用 `<tag Name="ExcludeFromDynamicEncounters" />` 可把对象排除出动态表 —— 官方特别推荐对**独特/稀有 NPC** 和**仅供继承的基类对象**使用。

> ⚠️ **双刃剑**：因为你的 Snapjaw 法师继承自 `Snapjaw` → … → `Creature`，它**在你还什么都没写的时候就已经会被 `DynamicInheritsTable:Creatures` 抽到**（概率很低）。反过来，你只要给新生物加一行 `<tag Name="DynamicObjectsTable:Snapjaws" />`，就立即会出现"传奇 Snapjaw 法师的巢穴"。

**种群表元素的实际可用属性**（Wiki 上**没有**属性参考表，以下是从示例中归纳的）：

| 元素 | 可用属性 |
| --- | --- |
| `<population>` | `Name`, `Load` |
| `<group>` | `Name`, `Style`, `Load`。`Style` 见过 `pickeach`（每项独立判定）、`pickone`（只选一项） |
| `<object>` | `Blueprint`, `Number`, `Chance`, `Weight`, `Hint` |
| `<table>` | `Name`, `Weight`, `Chance`, `Number`, `Hint` |

- `Number` 接受范围与骰子：`"1-2"`、`"3d6"`。
- `Hint` 用于告诉生成器"放哪里"，见过的值：`Interior`、`InsideCorner`、`OutsideDoor:1`、`OutsideDoor:2`、`AlongInsideWall`、`AlongWall`。

**`@` 前缀采样**：属性值里可以用 `@表名` 从种群表动态取，占位符有 `{zonetier}` 和 `{ownertier}`：

```xml
<inventoryobject Blueprint="@DynamicObjectsTable:EnergyCells:Tier{ownertier}" Number="1-2" Chance="4" />
```

**两个专门用于调试刷新的 Wish**（都支持动态表）：

| Wish | 作用 |
| --- | --- |
| `population:findblueprint:<蓝图ID>` | 查这个蓝图会出现在哪些刷新表里 |
| `population:generate:<表名>#<数量>` | 直接按表生成若干个 |

### 6.8 对话（Conversations）

对话是 XML 树，从 `Conversations.xml` 加载，通常由对象上的 `ConversationScript` 部件触发。

**挂到生物上：**

```xml
<objects>
  <object Name="Snapjaw Pal" Inherits="Snapjaw">
    <part Name="ConversationScript" ConversationID="FriendlySnapjaw" />
  </object>
</objects>
```

**定义对话本体**（注意外层 `<conversations>` 标签是**必需的**）：

```xml
<conversations>
  <conversation ID="FriendlySnapjaw">
    <start ID="Welcome">
      <text>ehekehe. gn. welcom.</text>
      <choice Target="LibDink">Thank you.</choice>
    </start>
    <node ID="LibDink">
      <text>hrffff... lib? dink?</text>
      <text>nyeh. heh! friemd?</text>
      <choice Target="End">Live and drink.</choice>
    </node>
  </conversation>
</conversations>
```

| 标签 | 说明 |
| --- | --- |
| `<conversation>` | 对话模板，含 `<node>` 和 `<start>`，通过 `ID` 与 `ConversationScript` 关联 |
| `<node>` | 一段 `<text>`（说话人视角）+ 若干 `<choice>`（玩家选项）。`AllowEscape="false"` 阻止玩家提前关掉对话窗 |
| `<start>` | `<node>` 的特例，可在开始对话时被选中。为兼容旧版，ID 为 `Start` 的 `<node>` 行为类似 |
| `<choice>` | 常用 `Target` 指向要跳转的 `<node>`。`Target` 有两个特殊值：`Start` 和 `End`。旧版写法 `GotoID` 行为类似 |
| `<text>` | 可定义多个，会**随机选一个有效的**；`<text>` 内可递归嵌套 `<text>`。旧版用 `~` 分隔文本，行为类似 |
| `<part>` | 引用继承 `IConversationPart` 的 C# 类；属性会注入字段，子元素可用自定义 C# 处理 |

**几个关键机制：**

- **省略 ID 会自动生成**：`<text>` → `Text`、`Text2`、`Text3`；`<choice Target="LibDink">` → `LibDinkChoice`。
- **对话有自己的继承**：所有对话继承自 `BaseConversation`（含交易、水之仪式等公共元素）。`Inherits` 可接受逗号分隔列表，且**当前元素的属性优先于被继承的**。支持点号跨引用：`<choice Inherits="ExcitedSnapjaw.SnappyBye.EndChoice" />`。
- **`Distribute` 分发**：`Distribute` 通常接元素类型列表；若指定 `Qualifier="ID"` 则接 ID 列表。定义在 `<conversation>` 下的 `<choice>` 默认会传播到所有 start 节点。

  ```xml
  <choice Target="End">Live and drink.</choice> <!-- 会加到两个 start 节点 -->
  <choice GiveItem="Dagger" Distribute="SnappyNoise" Qualifier="ID">It is time to grill cheese.</choice>
  ```

- **谓词（Predicates）与动作（Actions）**：谓词控制元素是否可用，动作在选择时执行。深丛林更新后它们对父元素类型**基本不可知**了：

  ```xml
  <start ID="FurFriend" IfHavePart="ThickFur"> <!-- 玩家没有厚毛皮时隐藏 -->
    <text>ooohh. pretty...</text>
    <text IfReputationAtLeast="Loved">deheh. like you. hohohoho.</text>
    <choice Target="End" IfReputationAtLeast="Loved" GiveItem="Dagger">I like you too.</choice>
    <choice Target="End">Thank you.</choice>
  </start>
  ```

  - 取反用 `IfNot...`；说话人视角用 `IfSpeaker...` / `SetSpeaker`。
  - 谓词和动作**支持逻辑表达式**，可用括号 + `AND` / `OR` / `NOT`：
    `IfHaveActiveQuest="(Quest1 AND Quest2)"`

**常用谓词**：`IfHaveQuest`、`IfHaveActiveQuest`、`IfFinishedQuest`、`IfFinishedQuestStep`（格式 `"任务ID~步骤ID"`）、`IfHaveState`、`IfTestState`（格式 `"ID 运算符 值"`，如 `"SlynthSettlementFaction = Joppa"`）、`IfHaveConversationState`、`IfLastChoice`、`IfCommand`、`IfReputationAtLeast`（Loved/Liked/Indifferent/Disliked/Hated）、`IfTime`、`IfZoneID`、`IfZoneLevel`、`IfZoneTier`、`IfGenotype`、`IfSubtype`、`IfTrueKin`、`IfMutant`、`IfHaveItem`、`IfWearingBlueprint`、`IfHavePart`、`IfHaveTag`、`IfHaveProperty`、`IfHaveLiquid`、`IfLevelLessOrEqual`。

**常用动作**：`AwardXP`、`FinishQuest`、`FireEvent`、`SetStringState` / `SetIntState` / `SetBooleanState`、`AddIntState`、`ToggleBooleanState`、`SetStringConversationState` / `SetIntConversationState` / `SetBooleanConversationState`、`SetStringProperty` / `SetIntProperty`、`RevealObservation`、`RevealMapNote`、`GiveLiquid`、`UseLiquid`、`SetLeader`、`Notify`；部件生成器 `StartQuest`、`CompleteQuestStep`、`GiveItem`、`TakeItem`。

**⚠️ 文档缺口**：Wiki 上**没有** `<goto>` / `<setvar>` / `<if>` / `<wish>` 这些标签 —— 它们不存在。对应功能由 `Target`/`GotoID`、`Set*State` 系列、`If*` 谓词承担。

**对话专用部件**（`<part Name="..." />`，本质是 C# 类）：

| 部件 | 示例 |
| --- | --- |
| `QuestHandler` | `<part Name="QuestHandler" Action="Step" QuestID="Fetch Argyve a Knickknack" StepID="Return to Argyve" XP="75" />` |
| `ReceiveItem` | `<part Name="ReceiveItem" Pick="true" Mods="1" Blueprints="Long Sword4,Cudgel4,Dagger4,Battle Axe4" Identify="All" />` |
| `RequireReputation` | `<part Name="RequireReputation" Faction="Snapjaws" Level="Loved" />` |
| `TextFilter` | `<part Name="TextFilter" FilterID="Lallated" Extras="*growl*,*whine*" />` |
| `TextInsert` | `<part Name="TextInsert" Spoken="false" NewLines="2">[Press Tab or T to open trade]</part>` |
| `Tag` | `<part Name="Tag">{{g|[begin trade]}}</part>` |

其它：`AddSlynthCandidate`、`ChangeTarget`、`GiveArtifact`、`GiveReshephSecret`、`IPredicatePart`、`LibrarianGiveBook`、`PaxInfectLimb`、`SpiceContext`、`TakeItem`、`Trade`、`VillageContext`、`WaterRitualRandomMutation`。

**命名空间**：部件名里含点号时视为你指定了自己的命名空间。也可以在根 `<conversations>` 上声明 `Namespace`，并在每个 `<conversation>` 上拼接子命名空间。

**事件传播方向是"向上"**：Choice → Node → Conversation，并按视角（Speaker / Listener）拆分。默认部件注册在它所处的视角，可用 `Register` 属性覆盖：`<part Name="SpiceContext" Register="All" />`。

**自定义谓词/动作需要 C#**：类上加 `[HasConversationDelegate]`，静态方法上加 `[ConversationDelegate(Speaker = true)]`，返回 `bool` 即为谓词、返回 `void` 即为动作。系统会**自动生成变体**（如取反的 `IfNotHaveItem`、说话人视角的 `IfSpeakerHaveItem`）。

**文本变量**：`=subject.name=`（见下）、`=spice.commonPhrases.sacred.!random=`（`SpiceContext`）、`=village.sacred=`（`VillageContext`）、`=mutation.name=`（`<part Name="WaterRitualRandomMutation" Category="Physical">You gain =mutation.name=.</part>`）。

> `=subject.name=` 比较特殊：**它会设置新的 Subject 和 Object**，然后才做标准的 `=` 替换。

### 6.9 躯体（Bodies）

`Bodies.xml` 有三段：`bodyparttypes`（部位的种类）、`bodyparttypevariants`（可互换的子类型）、`anatomies`（实际的身体结构，由对象通过 `Body` 部件引用）。

```xml
<bodies>
  <bodyparttypes>
    <bodyparttype Type="Head" LimbBlueprintProperty="SeveredHeadBlueprint" LimbBlueprintDefault="GenericHead"
                  Mortal="true" Appendage="true" UsuallyOn="Body"
                  Branching="Lateral,Longitudinal,Vertical,Stratal" ChimeraWeight="3" />
  </bodyparttypes>
</bodies>
```

`<bodyparttype>` 已文档化的属性：

| 属性 | 说明 |
| --- | --- |
| `Name` | 应唯一 |
| `Abstract` | "不映射到实际肢体的装备槽"，如"漂浮在旁"、投掷武器、远程武器槽。抽象部位不能作为主手 |
| `Appendage` | 允许被切断（抽象部位默认不可切断） |
| `ChimeraWeight` | 奇美拉变异的权重 |
| `Description` | 覆盖该槽位在背包菜单里的名称 |
| `DescriptionPrefix` | 前缀，如 `Back` 槽的前缀是 `Worn on` |
| `Extrinsic` | 阻止该部位被设为主手，且被切断时不掉落物品 |
| `LimbBlueprintProperty` | 断肢时用哪个蓝图 —— 在对象里写 `<tag Name="SeveredHandBlueprint" Value="RobotHand" />` |
| `LimbBlueprintDefault` | 默认断肢蓝图 |
| `Mobility` | 移动力权重。`Mobility="10"` 的腿贡献是 `Mobility="1"` 的十倍 |
| `Mortal` | 影响"断肢""斩首"等技能 |
| `NoArmorAveraging` | 不参与护甲平均 |

> Wiki 上 **`Branching`、`Category`、`Contact`、`DefaultBehavior`、`ImpliedBy`、`ImpliedPer`、`Integral`、`Plural`、`UsuallyOn` 这些属性没有解释**。这个页面是 stub。

**类型变体**：

```xml
<bodyparttypevariant VariantOf="Hand" Type="Tentacle" DefaultBehavior="SoftManipulator" Mobility="1" UsuallyOn="Body" />
```

`VariantOf` 指定原始类型，`Type` 是变体的唯一名。变体可以定义 `bodyparttype` 能定义的任何属性。

**解剖结构**是一棵嵌套的 `<part>` 树：

```xml
<anatomy Name="Bird">
  <part Type="Head">
    <part Type="Face" DefaultBehavior="Beak" />
  </part>
  <part Type="Back" />
  <part Type="Foot" Laterality="Right" SupportsDependent="Feet" />
  <part Type="Foot" Laterality="Left" SupportsDependent="Feet" />
  <part Type="Missile Weapon" Laterality="Right" />
  <part Type="Missile Weapon" Laterality="Left" />
  <part Type="Feet" DependsOn="Feet" />
  <part Type="Tail" />
</anatomy>
```

`<anatomy>` 属性：`Name`、`BodyMobility`（把身体槽计入总移动力；蛇、蛞蝓这类**没有脚槽**的生物用它）、`BodyType`（替换默认 `Body` 槽为自定义变体）、`FloatingNearby`、`ThrownWeapon`。（`BodyCategory`、`Category` 无解释。）

例：`<anatomy Name="BipedalRobot" Category="Mechanical" ThrownWeapon="Middle Hardpoint">`，内部含 `Control Unit` / `Sensor Array` / `Chassis` / `Hardpoint` ×2 / `Feet`。

解剖结构里 `<part>` 的属性：`Type`（要加的部位类型或变体名）、`Abstract`、`Category`、`Contact`、`DefaultBehavior`、`DependsOn`、`Extrinsic`、`IgnorePosition`、`Integral`、`Laterality`、`Mass`、`Mobility`、`Mortal`、`Plural`、`RequiresLaterality`、`RequiresType`、`SupportsDependent`。

### 6.10 语法系统（Grammar）：代词与动词

描述文本里可以写两种替换标记。

**代词** `=pronouns.(代词项)=`

| 名称 | 示例 |
| --- | --- |
| `subjective` | **SHE** went to the store. |
| `objective` | You are water-bonded with **HIM**. |
| `possessive` / `possessiveAdjective` | **HER** desert rifle rusted. |
| `substantivePossessive` | Kindrish is **HERS**. |
| `reflexive` | He can only blame **HIMSELF**. |
| `indicativeProximal` | **THIS** artifact is too complex… |
| `indicativeDistal` | **THAT** evidence was found… |
| `personTerm` | 该性别的人被称为什么 |
| `immaturePersonTerm` | 幼体称呼 |
| `formalAddressTerm` | 正式称呼 |
| `offspringTerm` | 后代称呼 |
| `siblingTerm` | 兄弟姐妹称呼 |
| `parentTerm` | 父母称呼 |

还有**大小写变体**：`=pronouns.personTerm=` → `human`（全小写）；`=pronouns.PersonTerm=` → `Human`（首字母大写）。

**动词** `=verb:(词)[:afterpronoun]=`

其中 `:afterpronoun` 可选；若设置，则按对象自己的代词集来变位。所以 `=verb:understand:afterpronoun=` 会根据主语是 he/she/they 自动变成 understands / understand。

这样写出来的一句话对**任何性别、任何单复数**的主语都语法正确 —— 这是 Qud 文本质量的基石。

### 6.11 其它数据根类型

| 根标签 | 文件示例 | 作用 |
| --- | --- | --- |
| `<bodies>` | `Bodies.xml` | 躯体部位类型与解剖结构 |
| `<conversations>` | `Conversations.xml` | 对话节点脚本 |
| `<genotypes>` | `Genotypes.xml` | 基因型（可选种族） |
| `<subtypes>` | `Subtypes.xml` | 亚型 / 职业 |
| `<mutations>` | `Mutations.xml` | 变异 |
| `<skills>` | `Skills.xml` | 技能树与能力 |
| `<quests>` | `Quests.xml` | 任务步骤与奖励 |
| `<worlds>` | `Worlds.xml` | 世界 / 区域 |
| `<wishcommands>` | `WishCommands.xml` | 许愿菜单项（本体只用了 17 行） |
| `<mods>` | `Mods.xml` | 物品改造 |
| `<help>` | `Manual.xml` | 游戏内帮助文本（支持 `{{W|...}}` 富文本、`~CmdXxx` 键位引用） |

**基因型实例**（来自你本机的 `supersoupsludge` 模组，节选）：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<genotypes>
  <genotype Name="Soupysludge" MutationPoints="12" StatPoints="44" AllowedMutationCategories="*"
            RandomWeight="90" DisplayName="Soupysludge" Subtypes="Characters" Class=""
            Tile="UI/Soupy_sludge.png" DetailColor="r" BodyObject="SoupSludge"
            BaseHPGain="2-4" BaseSPGain="50" BaseMPGain="2" Species="SoupSludge" IsMutant="true"
            CharacterBuilderModules="XRL.CharacterBuilds.Qud.QudCasteModule,XRL.CharacterBuilds.Qud.QudCyberneticsModule">
    <stat Name="Strength" Minimum="9" Maximum="23" ChargenDescription="Your {{W|Strength}} score determines ..." />
    <skills>
      <skill Name="Tactics_Run" />
      <skill Name="Survival_Camp" />
    </skills>
    <reputations>
      <reputation With="Entropic" Value="300" />
      <reputation With="Oozes" Value="400" />
    </reputations>
    <extrainfo>Mutations</extrainfo>
    <extrainfo>+300 reputation with {{C|Highly entropic beings}}</extrainfo>
  </genotype>
</genotypes>
```

可见 `CharacterBuilderModules` 直接引用 C# 类名（`XRL.CharacterBuilds.Qud.QudCasteModule`），说明即使是"数据"定义也常常需要对接游戏代码。

---

## 7. 对象（生物 / 物品）与部件系统

### 7.1 组件系统（ECS）思维模型

Caves of Qud 的代码库采用**实体-组件系统（Entity Component System）**风格。在 Qud 的 XML 里，**组件被称为"部件（part）"**。

这带来一个对模组作者极其友好的结果：**你只要把已有对象的部件重新组合，就能造出全新的东西，几乎不用写代码。**

例如：把一个 `LiquidVolume` 部件和一个 `MeleeWeapon` 部件拼在一起 = **一把能装液体的武器**。

> 官方开发者 Brian Bucklew 在 IRDC 2015 有一场专门讲这个系统的演讲（`Modding:Objects` 页面内嵌了视频）。

### 7.2 部件能做什么、怎么发现新部件

- 部件是**任何继承 `IPart` 的 C# 类**，实现位于 `XRL.World.Parts` 命名空间。
- 本体有**一千多个**部件。绝大多数是针对一两个生物写的专用逻辑（例如只有"墓穴栖居者"用的 `CryptSitterBehavior`）。
- **发现部件的最好方法**：找一个行为和你想要的相近的本体生物/物品，看它的 XML 定义，反推每个部件的作用。

```xml
<object Name="Snapjaw" Load="Merge">
  <part Name="Corpse" CorpseChance="90" CorpseBlueprint="Snapjaw Corpse" />
</object>
```

移除部件：

```xml
<removepart Name="Corpse" />
```

### 7.3 对象属性总览（`<object>` 的属性）

> ⚠️ **重要的文档缺口**：Wiki 上**没有 `<object>` 的完整属性表**。所有示例中出现在 `<object>` 上的属性只有三个：`Name`、`Load`、`Inherits`。下表依据示例用法、教程与本机本体数据汇总而来，其中标注"（示例用法）"的几项来自实际数据文件观察，**未在 Wiki 上正式文档化**，请以反编译 `Assembly-CSharp.dll` 为准（见 9.10）。

| 属性 | 说明 |
| --- | --- |
| `Name` | **唯一内部标识符**（用 Wish 时输入它）。本体、文档、教程一致使用 |
| `Inherits` | 继承的父蓝图。⚠️ **不要和 `Load="Merge"` 同时用** |
| `Load` | `Merge` / `Remove` / `Replace`（对话元素另有 `Add`） |
| `Tag` / `Level` / `Difficulty` | 标签与等级（示例用法） |
| `Gender` / `Genotype` / `Faction` | 性别 / 基因型 / 阵营（示例用法） |

**注意区分"属性"和"部件"** —— 下面这些**不是** `<object>` 的属性，而是**部件**，要用 `<part Name="..." ... />` 挂载：

| 你可能以为的属性 | 实际是 |
| --- | --- |
| `DisplayName` / `Render` / `Color` / `Tile` / `TileColor` / `DetailColor` | **`Render` 部件的属性** |
| `Inventory` | **部件**（且已遗留） |
| `Body` | **部件**（`<part Name="Body" Anatomy="Spider" />`） |
| `Brain` | **部件**（`<part Name="Brain" Hostile="..." Factions="..." />`） |

**并且不存在 `<stats>` / `<skills>` / `<inventory>` / `<equipment>` 这类"包裹块"** —— 它们都是**单个**子元素重复出现：

```xml
<stat Name="Hitpoints" Value="20" />
<stat Name="Intelligence" Boost="1" />
<skill Name="Cudgel" />
```

同理，**不存在带 `Chance` / `Number` / `Object` 属性的 `<inventory>` 块** —— 那些属性名属于**种群表条目**，不是对象蓝图。给生物配装有两种方式：

```xml
<!-- 方式一：直接给，可选 Number / Chance -->
<inventoryobject Blueprint="Walking Stick" Number="1" />
<inventoryobject Blueprint="StandaloneMarkovBook" Number="1-2" Chance="10" />

<!-- 方式二（现代、推荐）：从种群表随机采样 -->
<tag Name="InventoryPopulationTable" Value="MyNewSnapjawPopulationTableWhatever" />
```

本体数据里还能见到明确标注为**遗留**的写法，**不要模仿**：

```xml
<part Name="Inventory" Builder="InventoryChestJunk3or4" />   <!-- Legacy -->
```

### 7.4 物品改造（`Mods.xml` 与 `<mod>`）

本体 `Mods.xml` 用 `<mod Part="..." Tables="..." Rarity="..." Value="..." TinkerDisplayName="..." TinkerCategory="..." Description="..." />` 定义物品改造。例如：

```xml
<mod Part="ModLacquered" Tables="CommonMods" Rarity="C" TinkerDisplayName="lacquered" Value="1.1" TinkerCategory="utility" Description="Lacquered: This item cannot rust." />
<mod Part="ModSharp" Tables="BladeMods,ThrownWeaponMods" Rarity="C" TinkerDisplayName="sharp" Value="1.1" NoSparkingQuest="true" Description="Sharp: +1 to penetration rolls" TinkerCategory="melee weapons" />
```

对象通过 `<tag Name="Mods" Value="MissileWeaponMods,FirearmMods,CommonMods,RifleMods,ElectronicsMods,BeamWeaponMods" />` 声明自己能接受哪些改造类别。**物品改造本身没有完整的 XML 接口**，需要 C# 才能真正自制。

---

## 8. 贴图与渲染

### 8.1 文件位置与命名

- 所有贴图放在模组的 `Textures/` 目录下，可再建子目录。
- **引用时省略 `Textures/`**：
  - 文件：`Mods\你的模组\Textures\creatures\new_tile.png`
  - 引用：`Tile="creatures/new_tile.png"`
- **只支持 `.png`**（本体数据里出现的 `.bmp` 是内置资源，模组请用 png）。

### 8.2 尺寸

- 三色贴图实测标准尺寸：**16 × 24 像素**（你本机的 `Soupsludge.png` 与 `Soupy_sludge.png` 均为 16×24）。
- 若要覆盖默认的贴图宽高，在模组根目录放 `display.txt`：

```json
{
    "tiles":{
        "width":"24",
        "height":"24"
    }
}
```

- 预览图建议 **512 × 512**（管理器实际最大显示 128×128；若同时作为工坊封面，首页最多显示 435×435）。

### 8.3 三色着色原理（非常重要的美术约束）

Qud 默认使用**三色贴图**：

| 贴图像素 | 游戏内被涂成 |
| --- | --- |
| 黑色不透明 | **主色**（`ColorString`，或 `TileColor` 若指定） |
| 白色不透明 | **细节色**（`DetailColor`） |
| 透明白 | **背景色**（游戏内称为 viridian） |

**三个属性的精确回退规则**（容易搞错）：

- `ColorString` —— "包含用于 ASCII 和贴图的前景色，可选背景色"，如 `&B` 或 `&B^r`。
- `TileColor` —— "与 `ColorString` 相同，但**只作用于贴图**。**若不指定，贴图回退使用 `ColorString`。**"
- `DetailColor` —— "改变只用于贴图的'第三种颜色'，不影响 ASCII……**永远只是单个字符**。**若不指定，默认为背景色 viridian。**"

所以**只写 `ColorString` 就够了**，它同时管 ASCII 和贴图主色。

所以**你只需要画黑白 + 透明三种颜色的图**，颜色由 XML 动态赋上。这意味着**同一张贴图可以复用出多个不同颜色的生物** —— 教程里的火法师和冰法师就共用 `snapjaw_mage.png`，只改 `DetailColor`。

```xml
<part Name="Render" Tile="items/sw_spray.bmp" DisplayName="&amp;ySpray&amp;r-&amp;ya&amp;r-&amp;yBrain"
      ColorString="&amp;G" TileColor="&amp;G" DetailColor="r" RenderString="012" RenderLayer="5"></part>
```

显示名可以逐字上色：

```xml
<part Name="Render" DisplayName="&amp;Cb&amp;Be&amp;ba&amp;cd&amp;Ce&amp;Bd&amp;y bracelet" ColorString="&amp;C"></part>
```

> ⚠️ **极易踩的坑**：`ColorString="O"` 和 `ColorString="&O"` 含义**完全不同**。教程里有个"没有任何报错但颜色不对"的例子，就是因为漏了 `&`。正确写法在 XML 里是 `ColorString="&amp;O"`。

**第四色**：少数贴图用了第 4 种颜色（通常是 RGBA `(124, 101, 44, 255)`）。渲染时会按该颜色 R 通道做加权混合 —— R=255 时效果等同于细节色。

### 8.4 真彩色贴图

如果想要按原色渲染（而非三色着色），在模组根目录加 `modconfig.json`：

```json
{
  "shadermode":"1"
}
```

⚠️ 官方提示：真彩色模式**目前只对世界内渲染生效**，多数 UI 元素不支持。

### 8.5 代码页 437

Qud 历史上以 **code page 437** 解释文本。这影响 `RenderString` 里的字符选择（`Render` 值为 `{0}` 之类），以及非 ASCII 字符的显示。要让文本真正按 UTF-8 解析，需要在根元素上写 `Encoding="utf-8"`（**仅 `lang-experimental` 分支可用**）。

### 8.6 墙面 / 栅栏 / 液体的"绘制"贴图（工作量大，先了解）

墙、栅栏、液体是**"绘制型"（painted）**贴图：游戏会分析它与相邻格子的连接关系，然后挑选对应贴图。

- **墙和液体**：文件名后缀格式 `-00000000`，8 位数字每位 0/1，表示 8 个相邻格是否有同类，**从正北开始顺时针**。例如东西两侧都有墙的水平段用 `-00100010`。
  - ➡️ 要支持全部连接情况，**必须提供 256 张图**。
- **栅栏**：后缀格式 `_nsew`，只考虑四个正方向，字母顺序固定。水平段用 `_ew`，孤立一段用 `_`。
  - ➡️ **16 张图**。

**路径构造规则：**

- 用 `<tag Name="PaintedWall" Value="wall_plant" />` 或 `<tag Name="PaintedFence" Value="..." />` 指定根文件名，游戏自动追加后缀。
- 默认路径为 `Textures/Tiles/`，可用 `<tag Name="PaintedWallAtlas" Value="你的路径" />` / `PaintedFenceAtlas` 覆盖。
- 默认扩展名是 `.bmp`，可用 `<tag Name="PaintedWallExtension" Value=".png" />` / `PaintedFenceExtension` 改成 png。
- `<tag Name="PaintWith" Value="MainframeWalls" />`：让不同种类的墙被视为同一种来绘制（值只要相同即可）。

**省力工具**：开发者 unormal 写的 [ImageSlicer](https://bitbucket.org/bbucklew/imageslicer) 可以把一张 5×5 的墙/栅栏示意图自动切成全套。注意要在可执行文件同目录**手动创建 `Output` 目录**。

### 8.7 动画

**纯 XML 动画**（不需要写代码）：

```xml
<part Name="AnimatedMaterialGeneric"
  AnimationLength="20"
  LowFrameOffset="1"
  HighFrameOffset="1"
  TileAnimationFrames="0=Tiles2/sw_fan_1.bmp,5=Tiles2/sw_fan_2.bmp,10=Tiles2/sw_fan_3.bmp,15=Tiles2/sw_fan_4.bmp"
  RequiresOperationalActivePart="Fan" />
```

- `AnimationLength`：整个动画持续多少 tick 后循环。
- `TileAnimationFrames`：`tick=贴图` 的映射列表。

**代码动画**：部件可以监听 `RenderEvent`，动态替换贴图。这是"伪装"效果（`XRL.World.Effects.Disguised`）的实现方式：

```csharp
using System;
using XRL.World.Parts;

[Serializable]
public class MyPart : IPart {
    public override bool Render(RenderEvent E) {
        E.Tile = "Assets_Content_Textures_Creatures_sw_snapjaw.bmp";
        return true;
    }
}
```

⚠️ **性能警告**：`Render` 被游戏调用得**极其频繁**。写得低效会明显拖慢玩家游戏。所以能用 `AnimatedMaterialGeneric` 就别写代码。

### 8.8 获取本体贴图资源

- Wiki 提供一个（较旧的）贴图包：`caves-of-qud-tiles-200.71.zip`。
- **更新的方式**：用 Unity 资源提取工具，或者用 Armithaig 的 **Brinedump 模组**（通过游戏内 Wish 导出资源）。
- 授权限制：这些文件**只能用于为 Caves of Qud 制作模组**。

---

## 9. C# 脚本模组

### 9.1 你不用自己编译

这是 Qud 脚本模组最特别的一点，也是你机器上 `build_log.txt` 直接证实的事实：

```
=== PICKPOCKET SKILL ===
Loading path: \
Compiling 1 file...
Success :)
Location: C:\Users\16064\AppData\LocalLow\Freehold Games\CavesOfQud\ModAssemblies\3637646237.dll
```

**游戏自己在启动时用 Roslyn 编译你模组里的 `.cs` 文件**，产物写到 `ModAssemblies\<模组ID>.dll`（这个目录里现在已经有 50 多个 dll/pdb 对）。所以你**不需要** Visual Studio 编译，也不需要分发 DLL。

游戏在编译时会定义预处理符号，可用于条件编译：

```
Defined symbol: VERSION_1_0
Defined symbol: BUILD_2_0_211
Defined symbol: MOD_3104658246        ← 每个已启用模组的 ID
```

### 9.2 前置开关

必须开启游戏内的 **Modding > Enable Mods**；关闭 **Allow scripting mods** 会禁用所有脚本模组。

### 9.3 本地开发用的 csproj 模板

本体自带 `Base\Mods.csproj.template.txt`（真正的模板，原文如下）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<AssemblyName>Mods</AssemblyName>
		<PackageId>Mods</PackageId>
		<Authors>$AUTHORS$</Authors>
		<TargetFramework>netstandard2.0</TargetFramework>
		<LangVersion>9</LangVersion>
		<GenerateAssemblyInfo>false</GenerateAssemblyInfo>
		<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
		<QudLibPath>$MANAGED_PATH$</QudLibPath>
		<DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
	</PropertyGroup>
	<ItemGroup>$REFERENCES</ItemGroup>
</Project>
```

关键点：**`netstandard2.0` + `LangVersion 9`**，`QudLibPath` 指向 `CoQ_Data\Managed\`，引用 `Assembly-CSharp.dll`、`0Harmony.dll`、以及一批 `UnityEngine.*.dll`。

**你的机器的 `QudLibPath` 应为：**

```
D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\
```

一个真实第三方模组的 csproj（`3397662971\misc.csproj`）就用了这个结构，只是把路径硬编码成了自己的 Steam 位置。**在 IDE 里用 csproj 只是为了代码补全和查错**；实际运行仍由游戏编译。

### 9.4 部件（Part）的真实写法

**⚠️ 先记住一个安全机制**：C# 模组拥有与游戏本体**同等的权限**，因此"**每当一个 C# 模组发生变化，用户都必须先批准它运行，才能被加载**"。创建或载入存档前会弹出批准窗口。**如果用户不批准，该模组的全部文件都不会被加载 —— 包括 XML 文件。**

需要开启的三项设置（`Modding:Scripting` 原文）：
- **Enable Mods (restart required.)**
- **Select enabled mods on new game.**
- **Allow scripting mods. Scripting mods may contain malicious code!**

**最小的部件类：**

```csharp
using System;

namespace XRL.World.Parts
{
    // 几乎所有部件都应该加 [Serializable]，这允许游戏在存档时把它转换成数据。
    // 极少数情况下可以把部件标记为 [NonSerialized]，明确要求游戏「不要」保存它。
    [Serializable]
    public class MyPart : IPart
    {
        public string Foo = "hello, world!";
    }
}
```

挂到对象上：

```xml
<objects>
  <object Name="Snapjaw Scavenger" Load="Merge">
    <part Name="MyPart" />
  </object>
</objects>
```

**注册机制就是"命名空间 + 类名"**：`<part Name="…"/>` 匹配的是 **C# 类名**；XML 属性按名字注入到部件的**字段**上（`<part Name="MyPart" Foo="goodbye!" />`）。

> ⚠️ Wiki 上**完全没有 `IMod`、`IModPart`、`[OnGameInit]`、`IEventRegistration`、`CommandEvent`、`MinEvent<C>`** 这些名字（全文搜索零结果）。**不存在 `IMod` 式的"模组入口类"。** 游戏靠**特性（attribute）扫描 + 反射**来发现你的类。不要假设这些名字存在。

不同数据类型的"唯一标识符"属性不一样，很容易搞混：

| 数据类型 | 标识符属性 | 玩家可见名属性 |
| --- | --- | --- |
| 对象蓝图 | `Name` | `Render` 部件 |
| **部件类** | **类名** | 仅主动部件：`NameForStatus` 字段 |
| 变异 | **`Class`** | `Name` |
| 技能 | `Class` | `Name` |
| 选项 | `ID` | `DisplayText` |
| 种群表 | `Name` | 无 |
| 事件（非 MinEvent） | 传给 `FireEvent` 的参数 | 无 |
| Wish | 传给 `WishCommand` 的参数 | 无（玩家看到的就是内部名） |
| 带种子的随机生成器 | 传给 `GetSeededRandomGenerator` 的参数 | 无 |

**真实可运行的代码**（来自你本机已安装的模组 `3637646237\Pickpocket.cs`，注册方法已按当前 API 改写）：

```csharp
using System;
using XRL.World;
using XRL.World.Parts;
using XRL.UI;

namespace XRL.World.Parts.Skill
{
    [Serializable]
    public class PickpocketMutation : BaseSkill
    {
        public Guid ActivatedAbilityID = Guid.Empty;

        // ✅ 当前版本的两参数签名
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("CommandPickpocket");
            base.Register(Object, Registrar);
        }

        public override bool AddSkill(GameObject GO)
        {
            ActivatedAbilityID = AddMyActivatedAbility(
                "Pickpocket",
                "CommandPickpocket",
                "Skill",
                "You attempt to steal from an adjacent creature.",
                "\u0003"
            );
            return base.AddSkill(GO);
        }

        public override bool RemoveSkill(GameObject GO)
        {
            RemoveMyActivatedAbility(ref ActivatedAbilityID);
            return base.RemoveSkill(GO);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "CommandPickpocket")
            {
                GameObject who = ParentObject;
                if (who == null) return false;
                Cell cell = who.CurrentCell;
                // ...
            }
            return base.FireEvent(E);
        }
    }
}
```

**核心 API 结构：**

- 类加 `[Serializable]`（存档需要）。
- `Register(GameObject Object, IEventRegistrar Registrar)` 里用 `Registrar.Register("事件ID")` 订阅事件。
- `FireEvent(Event E)` 是字符串事件处理器，通过 `E.ID` 分发。
- 主动能力用 `AddMyActivatedAbility(...)` / `RemoveMyActivatedAbility(ref id)` 注册/注销，用 `Guid` 记住句柄。
- 父对象用 `ParentObject` 访问。

> ⚠️ **API 版本漂移警告（很重要）**：Wiki 上**较旧的页面**（Key Mapping、Activated Abilities、Mutations）用的是
> `public override void Register(GameObject Object)` 配合 `Object.RegisterPartEvent(this, "X")`；
> **较新的页面**（Events、Spring Molting 更新说明）用的是
> `public override void Register(GameObject Object, IEventRegistrar Registrar)` 配合 `Registrar.Register("X")`。
>
> **`IEventRegistrar` 是 2024 年 Spring Molting 更新引入的**，所以单参数形式是旧 API。你机器上的 `build_log.txt` 也证实了这一点 —— 老模组会产生这类警告：
>
> ```
> warning CS0618: 'IPart.Register(GameObject)' is obsolete: 'Use Register(GameObject, IEventRegistrar)'
> ```
>
> **写新代码请用两参数版本。** 对应注销事件用 `GO.UnregisterPartEvent(this, "EnteredCell")`。

**部件操作 API**（Wiki 上出现过的）：`player.AddPart<T>()`、`player.RequirePart<T>()`（已存在则不重复添加）、`gameObject.TryGetPart<T>(out var part)`、`GO.GetPart("ActivatedAbilities") as ActivatedAbilities`、`GO.GetPart<Body>()`、`ParentObject.RemovePart(this)`、`GameObject.Create("Snapjaw Scavenger")`、`ParentObject.HasEffect("Cudgel_SmashingUp")`。

### 9.5 事件系统（Events）

⚠️ Wiki 自己在 `Modding:Events` 上挂着 **"Missing info"** 横幅，说它"**没有描述事件优先级、`IEventRegistrar`、也没有描述如何监听另一个 GameObject 上的事件**"，并且页面末尾留着 `<!-- TODO: Insert event table -->` —— **官方没有事件总表。**

官方对这套系统的自述："由于游戏十多年层层叠叠的开发，存在**多种不同类型的事件**，它们大体上（但不完全）覆盖相同的用途。" 心智模型是三步：① 部件**注册**它想听什么；② 某处**触发**事件，把细节派发给所有感兴趣的监听者；③ 监听部件**处理**它。

#### 9.5.1 字符串事件（旧式）

**监听**（在 `Register` 里）：

```csharp
public override void Register(GameObject Object, IEventRegistrar Registrar)
{
	// 监听该 GameObject 被授予经验值
	Registrar.Register("AwardXP");

	// 当 [this] 是 Effect 时，方法调用完全相同
	Registrar.Register("AwardXP");

	// 调用我们重写的基类 Register
	base.Register(Object, Registrar);
}
```

> `Register` "在部件/效果**被添加到对象时执行一次，随后被序列化**。把 `AllowStaticRegistration` 重写为返回 `true` 会改变这个行为：注册不再被序列化，**`Register` 会在每次读档时重新执行**。不过，只要你同时持有 IPart/Effect 和 GameObject 的实例，其实可以在**任何地方**注册事件。"

**触发**：

```csharp
public void AwardPlayerXP() {
	// 取得玩家 GameObject
	GameObject player = XRLCore.Core.Game.Player.Body;

	// 新建一个 "AwardXP" 事件，指定整数参数 "Amount" 为 50。
	// Event.New(ID, [参数名1, 参数值1, 参数名2, 参数值2, ...])
	Event awardXP = Event.New("AwardXP", "Amount", 50);

	// 也可以逐个设置参数
	awardXP.SetParameter("Amount", 50);

	// 在玩家身上触发事件
	player.FireEvent(awardXP);
}
```

**处理**：

```csharp
public override bool FireEvent(Event E)
{
	// 如果我们的事件 ID 是 "AwardXP"……
	if (E.ID == "AwardXP") {
		// 读取构造函数或 SetParameter 设置的参数
		int amount = E.GetIntParameter("Amount");
		this.ParentObject.Statistics["XP"].BaseValue += amount;
	}

	// 返回我们重写的基类 FireEvent 的结果（字面量 true）。
	// 若改为返回 false，后续事件处理就会停止，后面的部件和效果就没机会处理这个事件了。
	return base.FireEvent(E);
}
```

**❗ 贯穿所有事件系统的取消语义：处理函数返回 `false` → 后续处理停止。**

`Event` 的成员：`E.ID`、`E.SetParameter(name, value)`、`E.GetParameter<T>(name)`、`E.GetIntParameter(name)` / `(name, default)`、`E.GetGameObjectParameter(name)`、`E.SetSilent(true)`、`E.AddAICommand(...)`、`E.AddMark(...)`。

#### 9.5.2 MinEvent（新式）

"**MinEvent** 为每种事件类型实现了**独立的 C# 类**。它们随 Tomb of the Eaters 更新（V200）引入。"

| | 字符串 `Event` | MinEvent |
| --- | --- | --- |
| 身份 | 字符串 ID + 字符串参数名 | 每个事件一个专用 C# 类，**有类型的属性** |
| 订阅 | `Register(GameObject, IEventRegistrar)` → `Registrar.Register("名字")` | `WantEvent(int ID, int cascade)` |
| 触发 | `Event.New(...)` → `GameObject.FireEvent(E)` | `XEvent.FromPool([具名参数])` 或 `new XEvent(){...}` → `GameObject.HandleEvent(E)` |
| 处理 | `bool FireEvent(Event E)` + 比较 `E.ID` | 每种类型一个 `public override bool HandleEvent(SpecificEvent E)` |
| 取消 | `return false` 停止后续处理 | 相同 |

**监听用 `WantEvent`** —— ⚠️ "它**每次事件触发时都会执行，所以要写得精简**……如果你需要动态监听别的事件，建议把动态逻辑放在别处，然后翻转一个布尔变量给 `WantEvent` 用。"

```csharp
// 始终监听一个事件
public override bool WantEvent(int ID, int cascade) {
	// 检查 ID 是否匹配我们想要的事件之一，此处是 ZoneActivatedEvent。
	// IPart/Effect 的基础 WantEvent 总是返回 false。
	return base.WantEvent(ID, cascade) || ID == ZoneActivatedEvent.ID;
}

// 在别处根据逻辑条件翻转这个布尔值
bool wantEndTurn = false;

// 动态监听
public override bool WantEvent(int ID, int cascade) {
	if (ID == ZoneActivatedEvent.ID) return true;
	if (wantEndTurn && ID == EndTurnEvent.ID) return true;
	return base.WantEvent(ID, cascade);
}
```

**触发**（用对象池，而不是直接 `new`）：

```csharp
public void GivePlayerDrams() {
	GameObject player = XRLCore.Core.Game.Player.Body;

	// 从池中取出事件实例，这是首选方式
	GiveDramsEvent E = GiveDramsEvent.FromPool();
	// 有些事件有重载的 FromPool，可以在取出时设置属性
	E = GiveDramsEvent.FromPool(Actor: player, Drams: 50);
	// 仍然可以直接设置属性
	E.Liquid = "water";
	// 也有些事件没有可访问的 FromPool，那就只能自己实例化
	E = new GiveDramsEvent() {
		Actor = player,
		Drams = 50
	};

	// 在玩家身上触发事件
	player.HandleEvent(E);
}
```

**处理** —— 每种事件一个重载，或者用一个 `HandleEvent(MinEvent E)` 按 `E.ID`/类型分派：

```csharp
public override bool HandleEvent(GiveDramsEvent E) {
	liquid.GiveDrams(E.Liquid, ref E.Drams, E.Auto);

	// 和旧式事件一样，返回 false 会阻止后续处理。
	// 这里已经没有液体可给了，所以没必要继续。
	if (E.Drams <= 0) return false;

	return true;
}

public override bool HandleEvent(FrozeEvent E) {
	E.Object.DisplayName = "&CFrozen Object";
	return true;
}

// 仍然可以重写基础 HandleEvent，像旧式事件那样比较 ID 或类型。
// 这在你想把事件不加区分地级联给子对象时更有用。
public override bool HandleEvent(MinEvent E) {
	if (!base.HandleEvent(E)) return false;

	if (E.ID == GiveDramsEvent.ID) {
		return HandleEvent(E as GiveDramsEvent);
	} else if (E is FrozeEvent) {
		return HandleEvent(E as FrozeEvent);
	}
	return true;
}
```

事件 ID 是事件类上的静态 int：`ZoneActivatedEvent.ID`、`EndTurnEvent.ID`、`GiveDramsEvent.ID`、`AfterGameLoadedEvent.ID`、`GetShortDescriptionEvent.ID` 等。

#### 9.5.3 跨对象注册、优先级、`IEventRegistrar`

> Spring Molting 之前，MinEvent 只能在自己的硬编码级联层内使用……现在**事件处理器可以从外部事件源注册** MinEvent，并且带有**可选的顺序**来决定它相对于其它处理器的优先级。例如，一个全局游戏系统可以直接监听玩家身上的事件，或者队伍成员可以监听队长的的事件。

```csharp
using XRL.UI;

namespace XRL.World.Parts
{
    public class ExamplePart : IPart
    {
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            // 监听父对象升级，排在多数处理器之后
            Registrar.Register(AfterLevelGainedEvent.ID, EventOrder.LATE);
            // 监听玩家死亡，排在多数处理器之前
            // 注意：这不会跟随玩家更换身体，应配合 AfterPlayerBodyChangeEvent 更新注册
            Registrar.Register(The.Player, BeforeDieEvent.ID, EventOrder.VERY_EARLY);
            // 监听游戏切换活动区域
            Registrar.Register(The.Game, ZoneActivatedEvent.ID);
        }

        public override bool HandleEvent(AfterLevelGainedEvent E)
        {
            Popup.Show($"{ParentObject.an()} gained a level!");
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeDieEvent E)
        {
            return false;   // 让玩家不死
        }

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            Mutation.EvilTwin.CreateEvilTwin(
                Original: ParentObject,
                Prefix: "quasi-",
                TargetCell: E.Zone.GetRandomCell()
            );
            return base.HandleEvent(E);
        }
    }
}
```

`IEventRegistrar` 的三种调用形态：

| 调用 | 含义 |
| --- | --- |
| `Registrar.Register(string eventID)` / `Registrar.Register(int eventID)` | 监听**自己** |
| `Registrar.Register(int eventID, EventOrder order)` | 监听自己 + 优先级（`EventOrder.LATE`、`EventOrder.VERY_EARLY`） |
| `Registrar.Register(GameObject source, int eventID[, EventOrder order])` | 监听**另一个对象**（`The.Player`、`The.Game`） |

**部件优先级**是另一种控制顺序的方式："**优先级越高，在部件列表中排得越靠前，比低优先级更早收到事件**……这影响该部件的事件级联顺序，也影响它的序列化顺序。"

```csharp
public class ExamplePart : IPart
{
    public override int Priority => PRIORITY_HIGH;
```

**让任意类（不只是部件/效果）注册并处理 MinEvent** —— 实现 `IEventHandler`：

```csharp
using XRL;
using XRL.World;

namespace ExampleMod
{
    public class MyClass : IEventHandler
    {
        public static void Register()
        {
            var myClass = new MyClass();
            The.Player.RegisterEvent(myClass, AfterDieEvent.ID);
        }

        public bool WantEvent(int ID, int Cascade) => ID == AfterDieEvent.ID;

        public bool HandleEvent(AfterDieEvent E)
        {
            XRL.UI.Popup.Show("You died!");
            return true;
        }
    }
}
```

#### 9.5.4 自定义 MinEvent（给别的模组用的事件）

> 建议继承 **`ModPooledEvent<T>`**，它替你实现了事件的对象池与派发；如果事件没有状态、也不会自我嵌套，可以用更简单的 **`ModSingletonEvent<T>`**。事件的处理者需要实现 **`IModEventHandler<T>`**，其中 `T` 是你的自定义事件。

```csharp
using XRL.World;

namespace ExampleMod
{
    public class ExampleEvent : ModPooledEvent<ExampleEvent>
    {
        public static readonly int CascadeLevel = CASCADE_EQUIPMENT | CASCADE_EXCEPT_THROWN_WEAPON;

        public string Value;

        // 一个触发事件的静态方法。不是必须的，但这是游戏偏好的组织方式
        public static string GetFor(GameObject Object)
        {
            var E = FromPool();
            Object.HandleEvent(E);
            return E.Value;
        }

        // 事件被归还到对象池之前重置它
        public override void Reset()
        {
            base.Reset();
            Value = null;
        }

        // 我们的事件会级联多远。这个例子会级联到已装备的物品
        public override int GetCascadeLevel() => CascadeLevel;
    }

    public class ExampleHandler : IPart, IModEventHandler<ExampleEvent>
    {
        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade) || ID == ExampleEvent.ID;

        public bool HandleEvent(ExampleEvent E)
        {
            E.Value = "Handled!";
            return true;
        }
    }
}
```

#### 9.5.5 级联（Cascading）

"级联层级是一个位域，根据哪些位被翻开，决定事件应该在何时、向何处级联。"

```csharp
// MinEvent 的级联类别位
public const int CASCADE_NONE = 0x0;                  // 0b00000
public const int CASCADE_EQUIPMENT = 0x1;             // 0b00001
public const int CASCADE_INVENTORY = 0x2;             // 0b00010
public const int CASCADE_SLOTS = 0x4;                 // 0b00100
public const int CASCADE_COMPONENTS = 0x8;            // 0b01000
public const int CASCADE_EXCEPT_THROWN_WEAPON = 0x10; // 0b10000
public const int CASCADE_ALL = CASCADE_EQUIPMENT | CASCADE_INVENTORY | CASCADE_SLOTS | CASCADE_COMPONENTS;

List<GameObject> Inventory = new List<GameObject>();

public override bool WantEvent(int ID, int cascade)
{
	// 如果 cascade 变量带上了 inventory 位……
	if (MinEvent.CascadeTo(cascade, MinEvent.CASCADE_INVENTORY)) {
		// 检查我们背包里是否有人想要这个事件
		foreach (GameObject item in Inventory) {
			if (item.WantEvent(ID, cascade)) return true;
		}
	}
	return base.WantEvent(ID, cascade);
}

public override bool HandleEvent(MinEvent E)
{
	if (E.CascadeTo(MinEvent.CASCADE_INVENTORY)) {
		// 让背包里每个物品都处理这个事件。
		// 任何一个返回 false 就停止处理。
		foreach (GameObject item in Inventory) {
			if (!item.HandleEvent(E)) return false;
		}
	}
	return base.HandleEvent(E);
}
```

#### 9.5.6 Wiki 上有记录的事件名（**不是完整清单**）

**字符串事件**：`AwardXP`、`EnteredCell`、`Equipped`、`Unequipped`、`TakeDamage`、`CommandForceUnequipObject`、`CommandForceEquipObject`、`AIGetOffensiveMutationList`、`CommandCudgelSlam`、`CommandFlamingHands`，以及模组自定义的按键命令（如 `ModName_Cmd_One`）。

**MinEvent**：`ZoneActivatedEvent`、`EndTurnEvent`、`GiveDramsEvent`、`FrozeEvent`、`AfterGameLoadedEvent`、`GetShortDescriptionEvent`、`BeforeRenderEvent`、`AfterPetEvent`、`AfterConversationEvent`、`AfterLevelGainedEvent`、`BeforeDieEvent`、`AfterDieEvent`、`GetMeleeAttackChanceEvent`、`AnimateEvent`、`AfterPlayerBodyChangeEvent`。

> 这只是样本，不是清单 —— 事件表在 Wiki 上仍然是 TODO。**找事件名的实际办法：反编译 `Assembly-CSharp.dll`，搜 `Event.ID` 和 `WantEvent`。**

### 9.6 自定义 Wish 命令

```csharp
using XRL.Wish;

[HasWishCommand]
public class MyWishHandler
{
  // 处理 "testwish:foo" 或 "testwish foo"
  [WishCommand(Command = "testwish")]
  public static bool TestWishHandler(string rest)
  {
     Popup.Show("Matched: " + rest);
     // 不返回 true 的话，其它 wish 也会继续解析这条消息
     return true;
  }

  // 只处理光秃秃的 "testwish"
  [WishCommand(Command = "testwish")]
  public static void TestWishHandler()
  {
     Popup.Show("Matched it the short way");
     // 返回 void 视为已处理
  }

  public int count = 0;

  // 不写 Command 就用方法名
  [WishCommand]
  public void inc()
  {
    Popup.Show(count++);
  }

  [WishCommand(Regex = @"other fancy match \d things")]
  public void Handle(System.Text.RegularExpression.Match match)
  {
    Popup.Show(match.Groups[0].ToString());
  }
}
```

规则：类必须有 `[HasWishCommand]` 特性；方法必须是 **public**，返回 `void`（视为已处理）或 `bool`（`true` 表示已处理，阻止后续解析）；正则按**大小写不敏感**匹配。

> ⚠️ **Wiki 上的这段示例原文有错，不要照抄**：原始版本写的是
> `[WishCommand(Regex = @"other fancy match \d things"]`（**缺少右括号和字符串的收尾引号**），
> 以及 `System.Text.RegularExpression.Match`（**拼写错误**，.NET 里正确的类型是 `System.Text.RegularExpressions.Match`）。
> 上面的代码已经修正。

**许愿控制台**：默认绑定 **Ctrl+W**；可在 Escape → Key Mapping → `Debug` 分类里改键。注意"**多数许愿命令严格区分大小写**"（例如 `dismember:Tail` 与 `Dismember:Tail` 不同）。输入未被匹配的内容时，游戏会对所有对象蓝图和任务做**莱文斯坦距离**的词典序搜索（`XRL.Wish.WishSearcher` 的 `SearchForWish`），匹配上的会生成对象或开始任务。每个许愿的具体逻辑在 `XRL.World.Capabilities.Wishing` 类里。

**关于 `reload` 许愿（重要）**："重载游戏资源，例如来自所有已启用模组的 `ObjectBlueprints.xml`。几乎所有东西都会重载，但有些不会：**对象贴图要重启游戏客户端才会更新。对 `Worlds.xml` 的改动不会影响已有存档 —— 必须开新档。**"

### 9.7 主动技能（Activated Abilities）与 `IActivePart`

#### 9.7.1 主动技能

"任何部件都能添加主动技能，包括变异、技能、甚至装备……所有部件都通过同一个接口添加能力。" 引擎里统一由 `ActivatedAbilities` 部件管理，通过 `GO.GetPart("ActivatedAbilities") as ActivatedAbilities` 取得。

```csharp
public override bool AddSkill(GameObject GO)
{
  ActivatedAbilities part = GO.GetPart("ActivatedAbilities") as ActivatedAbilities;
  if (part != null)
  {
    this.ActivatedAbilityID = part.AddAbility(
        "Slam [&Wattack&y]",       // 显示名
        "CommandCudgelSlam",        // 使用时触发的事件
        "Skill",                    // 能力菜单里的分类
        -1, false, false,
        "You make an attack with a cudgel at an adjacent opponent at +1 penetration. ...",
        "-", false, false);
    this.Ability = part.AbilityByGuid[this.ActivatedAbilityID];
  }
  return true;
}

public override bool RemoveSkill(GameObject GO)
{
  if (this.ActivatedAbilityID != Guid.Empty)
    (GO.GetPart("ActivatedAbilities") as ActivatedAbilities).RemoveAbility(this.ActivatedAbilityID);
  return true;
}
```

> "`AddAbility` 调用里**需要注意的是前 3 个字段**。这告诉游戏对象添加一个叫 `Slam[attack]` 的新能力，并在使用时触发 `CommandCudgelSlam` 事件。这个能力会出现在能力菜单的 `Skill` 分类下。该函数返回一个 `Guid`，用于在你需要引用或移除它时标识这个能力。"

> ⚠️ **`AddAbility` 的完整参数表 Wiki 上没有文档**（只说前三个重要，其余按位置传）。所以更推荐用下面这种**具名参数**的封装。

**注册**（同时让 AI 也会使用这个技能）：

```csharp
public override void Register(GameObject Object, IEventRegistrar Registrar)
{
  Registrar.Register("CommandCudgelSlam");
  Registrar.Register("AIGetOffensiveMutationList");
  base.Register(Object, Registrar);
}
```

```csharp
if (E.ID == "AIGetOffensiveMutationList")
{
  int intParameter = E.GetIntParameter("Distance");
  if (E.GetGameObjectParameter("Target") == null
      || !this.IsPrimaryCudgelEquipped()
      || (this.ParentObject.pPhysics != null && this.ParentObject.pPhysics.IsFrozen()))
    return true;
  List<AICommandList> parameter = (List<AICommandList>) E.GetParameter("List");
  if (this.Ability != null && this.Ability.Cooldown <= 0 && intParameter <= 1)
    parameter.Add(new AICommandList("CommandCudgelSlam", 1));
  return true;
}
```

**时间与冷却（记住这两组数值）**：

| 概念 | 规则 |
| --- | --- |
| `UseEnergy(1000, "Skill Cudgel Slam")` | "**1000 能量等于 1 回合**，多数常规技能都应该用它。2000 能量是 2 回合，500 是半回合。**自由动作不需要调用这个函数。**" |
| 冷却 | "设置冷却只需给能力的 cooldown 属性赋值。**10 冷却等于 1 回合（在 16 意志力下）。**Cudgel 给自己的冷却计数加了 10，因为在你使用这个技能所需的时间里，冷却会自行减少 1。" |

**生命周期钩子**："`AddSkill` 和 `RemoveSkill` 是**技能特有**的函数。对于**变异**，你要在 `Mutate` 和 `UnMutate` 里添加能力；对于**装备**，你要监听 `OnEquipped` 和 `OnUnequipped` 消息。"

**更清晰的封装 + 具名参数**：

```csharp
namespace XRL.World.Parts.Skill
{
    public class ExampleSkill : BaseSkill
    {
        public Guid AbilityID;

        public override bool AddSkill(GameObject GO)
        {
            AbilityID = AddMyActivatedAbility(
                Name: "Example",
                Command: "CommandToggleExample",
                Class: "Skill",
                Toggleable: true,
                DefaultToggleState: true,
                IsWorldMapUsable: true
            );
            return base.AddSkill(GO);
        }
    }
}
```

`BaseMutation` 提供的同类方法：`AddMyActivatedAbility(...)`、`RemoveMyActivatedAbility(ref id, null)`、`CooldownMyActivatedAbility(id, 10, null)`、`IsMyActivatedAbilityAIUsable(id, null)`。

> ⚠️ **新添加的能力默认在世界地图上不可用**，需要 `IsWorldMapUsable: true` 显式开启。

#### 9.7.2 `IActivePart` —— 带"工作状态"的部件

"很多对象部件都基于 `IActivePart` 架构。" 两个核心概念：**subjects**（部件作用的对象）与 **status**（运转状态）。"部件与 `IActivePart` 集成的基本形式，就是检查自己的状态，如果处于可运转状态，就把行为施加到它的 subjects 上。"

**状态按此顺序求值，第一个匹配者胜出：**

```
NeedsSubject → SwitchedOff → EMP → Broken → Rusted → Booting → NotHanging →
LimbIncompatible → RealityStabilized → LocallyDefinedFailure →
PrimarySystemOffline → Unpowered → Operational
```

**"只有 `Operational` 状态代表功能正常。"**

**状态配置字段**（含 `IActivePart` 的默认值）：

| 字段 | 触发的失败状态 | 默认值 |
| --- | --- | --- |
| `ChargeUse` (int) | Unpowered | 0 |
| `ChargeMinimum` (int) | Unpowered | 0 |
| `IsBootSensitive` | Booting | false |
| `IsBreakageSensitive` | Broken | **true** |
| `IsEMPSensitive` | EMP | false |
| `IsHangingSensitive` | NotHanging | false |
| `IsPowerSwitchSensitive` | SwitchedOff | false |
| `IsRealityDistortionBased` | RealityStabilized | false |
| `IsRustSensitive` | Rusted | **true** |
| `NeedsOtherActivePartOperational` (string) | PrimarySystemOffline | null |
| `NeedsOtherActivePartEngaged` (string) | PrimarySystemOffline | null |
| `RequiresBodyPartCategory` (string) | LimbIncompatible | null |

`RequiresBodyPartCategory` 可用类别：`Animal`、`Arthropod`、`Plant`、`Fungal`、`Protoplasmic`、`Cybernetic`、`Mechanical`、`Metal`、`Wooden`、`Stone`、`Glass`、`Leather`、`Bone`、`Chitin`、`Plastic`、`Cloth`、`Psionic`、`Extradimensional`。

**subject 布尔开关**（全部默认 `false`）：`MustBeUnderstood`、`WorksOnAdjacentCellContents`、`WorksOnCarrier`、`WorksOnCellContents`、`WorksOnEnclosed`、`WorksOnEquipper`、`WorksOnHolder`、`WorksOnImplantee`、`WorksOnInventory`、`WorksOnSelf`、`WorksOnWearer`。

**可重写方法**：`public virtual bool WorksFor(GameObject obj)`、`GetActivePartLocallyDefinedFailure()`、`GetActivePartLocallyDefinedFailureDescription()`、`IsActivePartEngaged()`。

**核心方法 `IsReady`**（Wiki 列出了完整签名，参数一律用**具名**写法）：

```csharp
public bool IsReady(
    bool UseCharge = false,
    bool IgnoreCharge = false,
    bool IgnoreBootSequence = false,
    bool IgnoreBreakage = false,
    bool IgnoreRust = false,
    bool IgnoreEMP = false,
    bool IgnoreRealityStabilization = false,
    bool IgnoreSubject = false,
    bool IgnoreLocallyDefinedFailure = false,
    int MultipleCharge = 1,
    int? ChargeUse = null,
    bool UseChargeIfUnpowered = false
)
```

`IsDisabled(...)` 参数完全相同，返回相反值；`GetActivePartStatus(...)` 参数相同，返回 `ActivePartStatus`。"若部件的状态是 `Operational` 则返回 true。会触发相应的渲染颜色变化，并更新最后一次已知状态。"

**其它常用方法**：`WasReady()`、`WasDisabled()`、`GetLastActivePartStatus()`、`ConsumeCharge(int? ChargeUse = null)`、`ConsumeChargeIfOperational(...)`、`GetActivePartSubjects()` → `List<GameObject>`、`GetActivePartFirstSubject()` / `(Predicate<GameObject> Filter)`、`IsObjectActivePartSubject(GameObject obj)`、`ActivePartHasMultipleSubjects()`、`GetActivePartSubjectCount()`、`AnyActivePartSubjectWantsEvent(int ID, int cascade)`、`ActivePartSubjectsHandleEvent(MinEvent E)`、`GetStatusSummary(...)`、`AddStatusSummary(StringBuilder SB)`、`GetOperationalScopeDescription()`、`MyPowerLoadBonus(...)`、`MyPowerLoadLevel()`。

> ⚠️ `ForeachActivePartSubjectWhile(Predicate<GameObject> pProc, bool MayMoveAddOrDestroy = false)` —— "**如果 `pProc` 中的代码可能导致任何对象被移动、创建或销毁，就应该把 `MayMoveAddOrDestroy` 设为 true**；否则可能因循环控制被打断而抛出异常。"

**状态显示字段**：`NameForStatus`（string，"**默认是部件类的名字（所以对于像 `IntPropertyChanger` 这样非常通用的部件来说，这个字段尤其关键）**"）、`StatusStyle`（`angry` / `bio` / `leet` / `ooc` / `plain` / `structure` / `tech`，默认 `plain`）、`DescribeStatusForProperty`（string，null）、`IsBioScannable` / `IsStructureScannable` / `IsTechScannable`（bool，false）、`ReadyColorString` / `ReadyDetailColor` / `DisabledColorString` / `DisabledDetailColor`（状态变为 Operational / 非 Operational 时设置父对象 `Render` 的 `ColorString` / `DetailColor`）。

**状态摘要文本**：EMP → `"{{W|EMP}}"`；Unpowered → `"{{K|unpowered}}"`；SwitchedOff → `"{{K|switched off}}"`；Booting（仅当 `BootSequence.IsObvious()`）→ `"{{b|warming up}}"`；NeedsSubject → null；Operational → null；其它状态 → `"{{r|nonfunctional}}"`。

**`IPoweredPart`** 是"面向更技术性用途的 `IActivePart` 变体"，默认值不同：`ChargeUse = 1`、`IsBootSensitive = true`、`IsEMPSensitive = true`、`IsPowerSwitchSensitive = true`、`IsTechScannable = true`。

**作用是自己的例子**：

```csharp
using System;

namespace XRL.World.Parts
{
    [Serializable]
    public class HealSelfEveryTurn : IActivePart
    {
        public HealSelfEveryTurn()
        {
            WorksOnSelf = true;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == EndTurnEvent.ID;
        }

        public override bool HandleEvent(EndTurnEvent E)
        {
            if (IsReady(UseCharge: true))
            {
                ParentObject.Heal(1);
            }
            return true;
        }
    }
}
```

**作用在 subjects 上的例子**：

```csharp
[Serializable]
public class HealEveryTurn : IActivePart
{
    public override bool WantEvent(int ID, int cascade)
    {
        return base.WantEvent(ID, cascade) || ID == EndTurnEvent.ID;
    }

    public override bool HandleEvent(EndTurnEvent E)
    {
        if (IsReady(UseCharge: true))
        {
            foreach (GameObject obj in GetActivePartSubjects())
            {
                obj.Heal(1);
            }
        }
        return true;
    }
}
```

> 注意 `IsReady(UseCharge: true)` 的用法：**如果你要消耗充能就必须这样调用**；而一个找不到 subject 的部件会处于 `NeedsSubject` 状态、不可运转 —— 这也是上面第一个例子必须设 `WorksOnSelf = true` 的原因。

### 9.8 属性偏移：`StatShifter`

"任何继承 `IPart` 或 `Effect` 的东西都有用于跟踪施加到目标上的属性偏移的工具方法……`StatShifter` API 替你处理'当前偏移'的记账，方便地提供 `RemoveStatShifts`，或者用 `SetStatShift` 覆盖之前的偏移。" 所有方法"都设计为可以安全地传入空/null 对象、数量为 0、或其它不会产生任何偏移的情况"。

| 方法 | 说明 |
| --- | --- |
| `StatShifter.SetStatShift(GameObject target, string stat, int amount, bool baseValue = false)` | "你可以用不同的数值再次调用 `SetStatShift`，它会移除之前的偏移并施加新的。`baseValue` 选项是为了让你能偏移 `HitPoints` 属性而提供的，因为它的最大值存在 `BaseValue` 而不是其它所有属性用的 `Value`。这个带 target 的版本让护甲/穿戴物上的部件能够以**穿戴者**为目标，而不是默认地增强护甲自身的属性。" |
| `StatShifter.SetStatShift(string stat, int amount, bool baseValue = false)` | 施加到 `StatShifter` 的 Owner 上："对部件或变异来说是 `ParentObject`，对效果来说是 `Object`，也就是拥有该部件/效果的对象。" |
| `StatShifter.RemoveStatShift(GameObject target, string stat)` | 移除一项偏移 |
| `StatShifter.RemoveStatShifts(GameObject target)` | 移除该目标上的全部偏移 |
| `StatShifter.RemoveStatShifts()` | "移除这个 shifter 施加到所有对象上的全部属性偏移。" |

**描述文本会自动生成**："`StatShifter` 也会负责为偏移生成一段'描述'。它默认为效果（Effect）的 `GetDescription()`，对部件则为 `""`。shifter 的 'owner' 会与偏移的 'target' 比较：两者不同时显示为 `'{Owner}'s {DefaultDisplayName}'`；相同时只显示 `DefaultDisplayName`……用于'显示属性偏移'的调试选项，以及 `showstatshifts` 许愿。" `StatShifter.DefaultDisplayName` 可以按部件设置。

```csharp
private void CheckCamouflage()
{
    GameObject User = ParentObject.pPhysics.Equipped;
    if (User == null) return;
    if (User.pPhysics.CurrentCell != null)
    {
        if (User.pPhysics.CurrentCell.HasObjectWithPart("PlantProperties"))
        {
            StatShifter.DefaultDisplayName = "camouflage";
            StatShifter.SetStatShift(User, "DV", Bonus);
        }
        else
        {
            StatShifter.RemoveStatShifts(User);
        }
    }
}

public override bool FireEvent(Event E)
{
    if (E.ID == "EnteredCell")
    {
        CheckCamouflage();
        return true;
    }

    if (E.ID == "Equipped")
    {
        GameObject GO = E.GetParameter<GameObject>("EquippingObject");
        GO.RegisterPartEvent(this, "EnteredCell");
        CheckCamouflage();
        return true;
    }

    if (E.ID == "Unequipped")
    {
        GameObject GO = E.GetParameter<GameObject>("UnequippingObject");
        StatShifter.RemoveStatShifts(GO);
        GO.UnregisterPartEvent(this, "EnteredCell");
        return true;
    }
    return base.FireEvent(E);
}
```

结果：`showstatshifts` 许愿会显示 `+2 from Grassy Yurtmat's camouflage`。

### 9.9 状态效果（Effect）与 `GetEffectType()`

⚠️ **Wiki 的 `Modding:Effects` 是一个 `{{Stub}}`**。整页只讲了 `GetEffectType()` 和类型位掩码 —— **`Apply`、`Duration`、`DisplayName` 以及效果叠加机制在 Wiki 上完全没有文档**。这些必须从反编译的程序集里查。

"所有效果都继承一个名为 `GetEffectType()` 的虚方法。`GetEffectType()` 的结果是一个整数，表示一个位向量，其中每一位是一个标志，指明该效果是否属于某个类型。"

**机制位**（从右数第 n 位 → 类型 → 十进制值）：

| 位 | 类型 | 值 | 位 | 类型 | 值 |
| --- | --- | --- | --- | --- | --- |
| 1 | General | 1 | 9 | Dimensional | 256 |
| 2 | Mental | 2 | 10 | Chemical | 512 |
| 3 | Metabolic | 4 | 11 | Structural | 1024 |
| 4 | Respiratory | 8 | 12 | Sonic | 2048 |
| 5 | Circulatory | 16 | 13 | Temporal | 4096 |
| 6 | Contact | 32 | 14 | Neurological | 8192 |
| 7 | Field | 64 | 15 | Disease | 16384 |
| 8 | Activity | 128 | | | |

**类别位**：25 Minor = 16777216；26 Negative = 33554432；27 Removable = 67108864；28 Voluntary = 134217728。

**掩码**：Mechanism = `16777215`（位 1–24）；Class = `251658240`（位 25–28）；**Duration Indefinite = `9999`**（位 1、2、3、4、9、10、11、14）。

**适用性检查**（在每次调用 "Apply Event Effect" 时执行；被视为固体的对象总是返回 true）：

| 目标 | 以下位不适用 |
| --- | --- |
| **液体** | 6 或 11（Contact / Structural） |
| **气体** | 3、6 或 11（Metabolic / Contact / Structural） |
| **等离子体** | 3、6、7、10、11、12（Metabolic、Contact、Field、Chemical、Structural、Sonic） |

其它规则：`Mental` 不能施加到没有大脑的对象上；`Metabolic` 不能施加到没有胃的对象、气体或等离子体上；`Respiratory` 不能施加到没有胃的对象上；`Circulatory` 不能施加到不会流血的对象上；`Activity` 不能施加到没有身体的对象上。

**辅助方法**："`bool IsOfType()` —— 如果指定的任意一位为真就返回 true"；"`bool IsOfTypes()` —— 如果指定的**全部**位都为真才返回 true……这些检查通过对它们做按位 AND（`&`）运算完成。"

```csharp
// 取自 Wiki（原文如此）
public override int GetEffectType()
{
    return Effect.TYPE_MENTAL
        | EFFECT.TYPE_MINOR
        | EFFECT.TYPE_NEGATIVE
        | EFFECT.TYPE_REMOVABLE;
}
```

> ⚠️ 上面这段是 Wiki **原文照抄**，注意其中 `Effect.TYPE_MENTAL` 与 `EFFECT.TYPE_MINOR` **大小写不一致** —— 这是 Wiki 的笔误，抄的时候请统一。

### 9.10 启动钩子与缓存：代码该放在哪里

Wiki 给了一张很实用的表：

| 代码模式 | 何时运行 | 备注 |
| --- | --- | --- |
| **Mod-Sensitive Cache** | 主菜单首次加载时；活动模组列表变化时 | 与具体存档无关；玩家载入不同存档不会重新调用。模组配置变化时会**热重载** |
| **Blueprint Preload** | 与 Mod-Sensitive Cache 同时 | "运行时机相同，但加载框架不同" |
| **Game-Based Cache** | 选择"新游戏"后立即；载入已有存档时 | "**在玩家对象存在之前、世界生成之前**调用，所以如果你需要操作玩家，这个选项不合适" |
| **PlayerMutator** | 新游戏创建玩家之后 | "每个存档只运行一次。例如，你可以给玩家加一个承载你代码的自定义部件" |
| **AfterGameLoaded Hook** | 存档载入且玩家对象存在之后 | 用于读档后修改玩家（"相当于新游戏时的 PlayerMutator"） |
| **Harmony Injection** | 代码流程中的**任意时刻** | "灵活度最高……也最难、最技术化。多数模组不需要这个" |

**调用链与总体顺序**：

- Mod-sensitive cache 由 `XRL.ModManager.ResetModSensitiveStaticCaches()` 调用，触发时机：① 启动时编译玩家启用的脚本模组；② 活动模组配置变化时的热重载；③ 使用 **`reload` 许愿**时。
- Game-based cache 由 `XRL.Core.XRLCore.ResetGameBasedStaticCaches()` 调用，触发时机：选择 "New Game" 之后立即、以及载入已保存游戏之后立即（"**包括因预知（Precognition）而重载等场景**"）。
- **总体顺序：① Mod-sensitive cache → ② Pre-game cache 代码（含 Blueprint preload）→ ③ Game-based cache 代码。**
- Blueprint preload 由 `XRL.World.GameObjectFactory.CallLoadBlueprint()` 调用，"紧接在 mod-sensitive cache 代码处理完之后"。

**`[HasModSensitiveStaticCache]` 的处理语义**：带 `[ModSensitiveStaticCache]` 的字段重置为默认值（值类型）或**置为 null**（对象类型）；`[ModSensitiveStaticCache(true)]` 改为强制 `Activator.CreateInstance`。然后执行带 `[ModSensitiveCacheInit]` 的静态方法。"**对 `Reset()` 方法没有特殊处理。**"

```csharp
[HasModSensitiveStaticCache]
public static class Initialiser
{
    [ModSensitiveStaticCache]
    public static int Counter;   // 游戏启动时、以及模组配置变化时重置为 int 默认值

    [ModSensitiveStaticCache(true)]
    public static List<SolidColor> Colors = new List<SolidColor>();  // 重置为新的空 List<SolidColor>

    [ModSensitiveCacheInit]
    public static void MyModCacheResetCode()
    {
        // 游戏启动时、以及模组配置变化时调用
    }
}
```

**`[HasGameBasedStaticCache]` 的处理语义**：带 `[GameBasedStaticCache]` 的字段重置为默认值（值类型）或**通过 `Activator.CreateInstance` 新建实例**（对象类型；`[GameBasedStaticCache(CreateInstance = false)]` 改为置 null）。然后调用静态 `Reset()`（如果存在）。最后执行带 `[GameBasedCacheInit]` 的静态方法。

```csharp
[HasGameBasedStaticCache]
public static class Initialiser
{
    [GameBasedStaticCache]
    public static int Counter;  // 新游戏开始或读档时重置为 int 默认值

    public static int OtherCounter;  // 不会被自动重置（但你可以在 Reset() 里自己重置）

    public static void Reset()
    {
        // 新游戏开始或存档载入时调用 —— 不需要任何特性标签
    }

    [GameBasedCacheInit]
    public static void AdditionalSetup()
    {
        // 在 Reset() 之后调用
    }
}
```

> ⚠️ **`[PreGameCacheInit]` 的陷阱（原文）**："pre-game cache 方法**在每次新游戏或读档游戏之前都会运行**，**即使它们位于一个只有 `[HasModSensitiveStaticCache]` 特性的类里**。所以如果你要实现的 pre-game cache 方法只想在模组加载配置变化后才运行，要特别小心。"

**Blueprint preload**（"直接来自游戏代码 `XRL.World.Parts.TinkerItem`"）：

```csharp
namespace XRL.World.Parts
{
    [WantLoadBlueprint]
    public class TinkerItem : IPart
    {
        public override void LoadBlueprint()
        {
            if (this.CanBuild && string.IsNullOrEmpty(this.SubstituteBlueprint)
                && !this.ParentObject.HasTag("BaseObject"))
            {
                TinkerData tinkerData = new TinkerData();
                tinkerData.Blueprint = this.ParentObject.Blueprint;
                tinkerData.Cost = this.Bits;
                tinkerData.Tier = this.BuildTier;
                tinkerData.Type = "Build";
                tinkerData.Category = this.ParentObject.GetTag("TinkerCategory", "none");
                tinkerData.Ingredient = this.Ingredient;
                tinkerData.DisplayName = this.ParentObject.pRender.DisplayName;
                TinkerData.TinkerRecipes.Add(tinkerData);
            }
        }
    }
}
```

两个已文档化的陷阱：

- `LoadBlueprint()` "**在正常游戏过程中不会被调用**，所以适合放部件的一次性初始化代码（**但请记住，如果该部件存在于多个对象上，这个函数在蓝图预载过程中仍可能被调用多次**）"。
- 而**构造函数**"在正常游戏中每次创建对象时也会为真，所以一般不建议把蓝图预载逻辑放进类的构造函数"。

**修改玩家（新游戏）**：

```csharp
using XRL;       // 用于简写 XRL.PlayerMutator 和 XRL.IPlayerMutator
using XRL.World; // 用于简写 XRL.World.GameObject

[PlayerMutator]
public class MyPlayerMutator : IPlayerMutator
{
    public void mutate(GameObject player)   // 注意方法名是小写的 mutate
    {
        // 新游戏开始时修改玩家对象
        // 例如给玩家加一个自定义部件：
        player.AddPart<MyCustomPart>();
    }
}
```

> ⚠️ **"这个方法只在玩家开始新游戏时有效。如果玩家在已有存档上加载你的模组，`[PlayerMutator]` 代码永远不会被调用。"**

**修改玩家（读档）** —— 想让改动也作用于已有存档，必须配合上面那个一起用：

```csharp
using XRL;       // HasCallAfterGameLoadedAttribute / CallAfterGameLoadedAttribute
using XRL.Core;  // XRLCore
using XRL.World; // GameObject

[HasCallAfterGameLoadedAttribute]
public class MyLoadGameHandler
{
    [CallAfterGameLoadedAttribute]
    public static void MyLoadGameCallback()
    {
        // 每次载入存档时调用
        GameObject player = XRLCore.Core?.Game?.Player?.Body;
        if (player != null)
        {
            // 用 RequirePart 而不是 AddPart：只在玩家还没有该部件时添加。
            // 这样即使多次读档，你的部件也只会被添加一次。
            player.RequirePart<MyCustomPart>();
        }
    }
}
```

**调试输出**：

```csharp
XRL.Messages.MessageQueue.AddPlayerMessage("Hello world!")
UnityEngine.Debug.LogError("Hello world!")
```

"在模组文件初始构建期间发生的错误会记录在 `build-log.txt` 里。这些错误通常会导致模组无法构建，从而使玩家无法启用它。"

### 9.11 存档与序列化

**基础规则：**

- 必须的 using（Wiki 原文）：`using System;` 和 `using SerializeField = UnityEngine.SerializeField;`。
- "很多时候你只需要在类定义顶部加上 `[Serializable]` 特性，Qud 的引擎就会替你序列化所有 `public` 字段。具体来说，Qud 能正确序列化所有内建类型（如 `string`、`int` 等）以及它们的容器（如 `List<string>`）。"
- "注意 **`private`、`protected` 和 `static` 字段默认不会被序列化**……你需要给每个 private 或 protected 字段单独标记 `[SerializeField]`。**静态字段无法用这种方式序列化。**"

```csharp
using System;
using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [Serializable] // 这个特性让所有 public 字段自动被序列化
    public class MyCoolModPart : IPart
    {
        public float CoolnessRatio;  // 会被序列化！
        public int AbilityLevel;     // 也会被序列化！
        private string CurrentValue; // 因为是 private，这个字段「不会」被序列化 ——
                                     // 存档重载后它的值会被清空！使用时要小心。
        [SerializeField]
        private string SecretID;     // 虽然是 private，但因为标了 SerializeField，所以「会」被序列化！

        public void MyMethod()
        {
            //...
        }
    }
}
```

**⚠️ 对象字段不会自动序列化。** "如果你的类引入了对象类型或对象容器的新字段，比如 `GameObject` 字段……你**必须**为这些对象字段实现自定义序列化，否则存档重载后你的对象引用就会失效。"

**继承来的对象字段已经处理好了**："`IPart` 包含字段 `public GameObject _ParentObject`……`IPart.Write()` 里有保存游戏时序列化那个 GameObject 所需的特殊逻辑，`IPart.Read()` 里有反序列化它所需的逻辑……**这意味着如果你的类继承 `IPart`，你可以放心地认为 `this.ParentObject` 之类的父对象引用在你的代码里总是有效的。**"

> 设计建议："如果你能找到一种不用在代码里显式保存对象引用就能引用对象的方法，那一般最好避免创建那个字段。"

**自定义序列化**：把字段标记 `[NonSerialized]`，并重写 `Read` / `Write`。"如果你的类继承 `IComponent`（包括 `IPart` 和 `Effect` 等后代），你应该重写 `Read` 和 `Write` 方法……**这是游戏目前提供的唯一可用于序列化的虚方法。** 如果你需要在不是 `IComponent` 子类的类里序列化数据，就得去看 `SerializationReader` 和 `SerializationWriter` 类，自己实现序列化逻辑。" 小技巧："如果你用 `WriteGameObject` 或 `WriteGameObjectList` 函数，序列化 GameObject 字段或列表会特别简单。"

**游戏本体的实例（`Inventory`）**：

```csharp
namespace XRL.World.Parts
{
	[Serializable]
	public class Inventory : IPart
	{
        public override void Write(GameObject Basis, SerializationWriter Writer)
        {
            Writer.WriteGameObjectList(Objects);
            base.Write(Basis, Writer);
        }

        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            Reader.ReadGameObjectList(Objects);
            for (int num = Objects.Count - 1; num >= 0; num--)
            {
                if (Objects[num] == null)
                {
                    Objects.RemoveAt(num);
                }
            }
            base.Read(Basis, Reader);
        }

		[NonSerialized]
		public List<GameObject> Objects = new List<GameObject>();
	}
}
```

**已文档化的读写成员**：`Writer.WriteGameObjectList(...)`、`Writer.Write(value)`、`Writer.WriteNamedFields(this, GetType())`；`Reader.ReadGameObjectList(...)`、`Reader.ReadInt32()`、`Reader.ReadObject()`、`Reader.ReadString()`、`Reader.ReadNamedFields(this, GetType())`、`Reader.ModVersions["ExampleMod"]`（一个 `System.Version`）。

**什么会破坏存档（已文档化）：**

- "Qud 的存档数据使用**二进制块**……它在某种程度上也依赖某些东西不跨游戏和模组版本变化，所以它**很容易被某些类型的改动破坏**。"
- 在普通 `[Serializable]` 的 `IPart` 上增删字段："历史上这是一个重大问题，因为**替换部件中的字段可能永久损坏它的父对象**。"
- 好消息：Spring Molting 之后，"游戏能够移除它不认识的部件，所以禁用模组不会永久损坏所有受影响的对象。游戏也会自行备份以防存档损坏。" 但移除"会向用户暴露错误"。
- 重命名类是可以的（旧部件会作为未知部件被丢弃），但同样"会向用户暴露错误"，而且"在某些情况下，如果一个部件的功能对对象的运作至关重要，完全移除它可能是不可取的"。
- 在 `IScribed` 类里**不能改变已有字段的类型**。
- `Read` 的限制："**你不能在调用 `Read` 时移除一个部件。** 此外，在 `Read` 被调用的时刻，某些字段可能尚未初始化，其它对象/效果可能也还没被反序列化。因此，你通常应该只用 `Read` 来反序列化字段，**把实际的迁移推迟到之后某个时点**。"

**推荐的现代做法**：**207.69 版本引入了 `IScribedEffect`、`IScribedPart`、`IScribedSystem`**。"这些类的工作方式与它们非 'scribed' 的对应物相同，区别在于**你可以随意增删它们中的字段**。**强烈建议你在新代码中用这些新接口替代 `Effect` 或 `IPart`。**"

代价："前者（Scribed）在序列化时把字段连同字段名一起写入，而后者不写。因此 **`IScribed` 类会增大存档体积，并降低序列化/反序列化速度**……总的来说，只应该对那些**字段极多**、且**被附加到极多对象上**的 `IComponent` 考虑避免使用 `IScribed` 接口。"

**对于无法直接改继承的情况**（例如为了实现功能必须用 `IActivePart`），可以手动把组件"scribe 化"：

```csharp
public override void Write(GameObject Basis, SerializationWriter Writer)
{
    Writer.WriteNamedFields(this, GetType());
}

public override void Read(GameObject Basis, SerializationReader Reader)
{
    Reader.ReadNamedFields(this, GetType());
}
```

对于继承了已序列化状态的类（例如带有"几十个"字段的 `IActivePart`），已文档化的策略是：把你自己的**所有**字段标记 `[NonSerialized]`、手动反序列化它们，并**最后**才调用 `base.Read` / `base.Write`，好让父类的字段由基类实现处理，而不是对你的字段做反射。

**迁移模式**：读 `Reader.ModVersions["ExampleMod"]` → 按 `System.Version` 比较分支 → 只读该版本存在的字段 → 把真正的迁移放到 `AfterGameLoadedEvent` 处理函数里完成（那里也可以 `ParentObject.RemovePart(this)`）：

```csharp
public override void Read(GameObject Basis, SerializationReader Reader) {
    var modVersion = Reader.ModVersions["ExampleMod"];

    if (modVersion >= (new Version("0.3.0"))) {
        base.Read(Basis, Reader);
        return;
    }

    // 记录 MigrateFrom，这样我们就知道需要从旧版本迁移
    MigrateFrom = modVersion;

    // S1 在该部件的所有版本中都存在
    S1 = Reader.ReadString();

    // S2 是 v0.2.0 才加入的
    if (modVersion >= (new Version("0.2.0")))
        S2 = Reader.ReadString();
}
```

**其它钩子**：

- **`ReadError`** —— "可以在 `IComponent` 的子类上重写，用于处理反序列化期间抛出的异常。当版本检查不足时，这个函数可以作为兜底。"
- **`FinalizeRead`** —— "可以作为使用 `AfterGameLoadedEvent` 处理函数的替代方案，用来钩住对象反序列化的结尾。**对象完全反序列化之后，这个方法会在附着于该对象的所有部件和效果上被调用。**"
- 自定义数据容器可以实现 `IComposite`（`WantFieldReflection` + `Write(SerializationWriter)` / `Read(SerializationReader)`）。

> 关于 `ISerializable` 接口和 `Serialize` / `Deserialize` 方法：**Wiki 上没有 Qud 的这类 API**。已文档化的就是 `[Serializable]`、`[SerializeField]`、`[NonSerialized]` 以及 `Read` / `Write` 重写。

### 9.12 随机数：必须用游戏的 RNG

**硬性规定（来自 `Modding:Compatibility` 原文）**：

> **为避免冲突、并保持不同种子之间的一致性，不应该调用 `Stat.Random()` 和 `Stat.Rnd()`。最理想的是用 `GetSeededRandomGenerator()` 作为你模组的随机源。次佳是调用 `RandomCosmetic()` 或 `Rnd2()`。**

**种子的派生方式**（游戏内部原文）：

```csharp
Stat.Rand = new Random(Hash.String("Seed0" + (object) Seed));
Stat.Rnd  = new Random(Hash.String("Seed1" + (object) Seed));
Stat.Rnd2 = new Random(Hash.String("Seed2" + (object) Seed));
if (includeLifetimeSeeds)
    Stat.LevelUpRandom = new Random(Hash.String("Seed3" + (object) Seed));
Stat.Rnd4 = new Random(Hash.String("Seed4" + (object) Seed));
Stat.Rnd5 = new Random(Hash.String("Seed5" + (object) Seed));
```

| 生成器 | 用途 |
| --- | --- |
| `Rnd()` | "根据世界种子随机化并决定苏丹神器、村庄以及许多其它东西的主要函数。**在模组里使用这个生成器或 `Stat.Random`，可能导致同一个世界种子产生与本体不同的结果。**" |
| `Rnd2()` / `Rnd4()` / `Rnd5()` | "处理你在 Qud 里遇到的较小事件" |
| `LevelUpRandom()` | "用于决定升级时随机内容的 Random" |
| **`GetSeededRandomGenerator(string Seed)`** | **模组首选**。"如果你不想用游戏的种子、担心过度改变世界种子，有一个辅助函数返回一个经过哈希的新随机数。**这让你模组受世界种子影响，但不会改变本体自己的随机数调用。**" |

```csharp
public static Random GetSeededRandomGenerator(string Seed)
{
    if (XRLCore.Core.Game == null)
    {
        return new Random();
    }
    return new Random(Hash.String(XRLCore.Core.Game.GetWorldSeed(null) + Seed));
}
```

> "这里的 Seed 应遵循最佳实践约定，即 `YourName_YourMod`，以尽量避免重叠冲突。"

**注意**：直接调用 C# 内建 `Random` 类的 `Next()` 函数"会给你带来一些编译错误。**最佳实践是使用 `XRL/Rules/Stat.cs` 里的随机函数。**"

**返回值辅助函数**：

| 函数 | 等价于 |
| --- | --- |
| `Random(int low, int high)` | `Stat.Rnd()` |
| `TinkerRandom(int Low, int High)` | `Stat.Rnd4()` |
| `RandomCosmetic(int low, int high)` | `Stat.Rnd2()`（"最常用于获取粒子等视觉效果的随机角度"） |
| `SeededRandom(string Seed, int Low, int High)` | "基于 `Seed` 返回 `[Low, High]` 范围内的整数" |
| `GaussianRandom(float Mean, float StandardDeviation)` | 正态分布随机浮点 |

**推荐的模组本地随机数提供者**（结合 `[HasGameBasedStaticCache]` + `[GameBasedCacheInit]`，并把种子存进游戏状态）：

```csharp
using System;
using XRL;
using XRL.Core;
using XRL.Rules;

namespace MODNAME.Utilities
{
    [HasGameBasedStaticCache]
    public static class MODNAME_Random
    {
        private static Random _rand;
        public static Random Rand
        {
            get
            {
                if (_rand == null)
                {
                    if (XRLCore.Core?.Game == null)
                    {
                        throw new Exception("MODNAME mod attempted to retrieve Random, but Game is not created yet.");
                    }
                    else if (XRLCore.Core.Game.IntGameState.ContainsKey("MODNAME:Random"))
                    {
                        int seed = XRLCore.Core.Game.GetIntGameState("MODNAME:Random");
                        _rand = new Random(seed);
                    }
                    else
                    {
                        _rand = Stat.GetSeededRandomGenerator("MODNAME");
                    }
                    XRLCore.Core.Game.SetIntGameState("MODNAME:Random", _rand.Next());
                }
                return _rand;
            }
        }

        [GameBasedCacheInit]
        public static void ResetRandom()
        {
            _rand = null;
        }

        public static int Next(int minInclusive, int maxInclusive)
        {
            return Rand.Next(minInclusive, maxInclusive + 1);
        }
    }
}
```

用法：`MODNAME_Random.Next(1, 10);` 或 `someList.ShuffleInPlace(MODNAME_Random.Rand);`

### 9.13 命名参数

调用 Qud 的 C# API 时，**优先使用命名参数**来填可选参数。这个建议在 `Modding:Compatibility`、`Modding:Activated Abilities` 等多处反复出现。

例如 `XRL.UI.Popup.AskString()` 有 10 个参数：

```csharp
// namespace XRL.UI
// public class Popup
public static string AskString(
  string Message,
  string Default = "",
  string Sound = PROMPT_SOUND,
  string RestrictChars = null,
  string WantsSpecificPrompt = null,
  int MaxLength = 80,
  int MinLength = 0,
  bool ReturnNullForEscape = false,
  bool EscapeNonMarkupFormatting = true,
  bool? AllowColorize = null
)
```

不用命名参数时是这样：

```csharp
using XRL.UI;
var response = Popup.AskString(
  "How are you doing?",
  "Okay",
  Popup.PROMPT_SOUND,
  null,
  null,
  80,
  0,
  true,
  true,
  true
);
```

只写你想设置的可选参数时是这样：

```csharp
using XRL.UI;
var response = Popup.AskString(
  "How are you doing?",
  Default: "Okay",
  MaxLength: 80, // 注意：与当前默认值相同
  ReturnNullForEscape: true,
  AllowColorize: true
);
```

两个相关好处：

1. **灵活性与韧性。** 只指定你想设置的参数，你的代码就能适应默认值变化、以及许多常见的参数表变化（例如新增一个可选参数、或可选参数重新排序）。
2. **更合理的错误信息。** 如果参数表发生了无法自动解析的变化（例如你设置的某个参数被移除），错误信息通常会**包含你的参数名**，而不是索引（更糟的情况是继续编译但实参被传给了错误的参数）。
3. **文档价值。** 具名之后，未来的读者（包括你自己）能明白它的意图。

⚠️ **不要在命名参数之后再写位置参数！** 把 `void Foo(int A, string B, bool C)` 这样调用：`Foo(A: 0, "blah", C: false)`，一旦参数表重排就会产生难以理解的错误。

### 9.14 Harmony 补丁

**Wiki 的 `Modding:Harmony` 是一个 `{{Stub}}`**，但政策说得很清楚：

> **Harmony** 是一个用于动态修补 C# 代码的库。在 Caves of Qud 模组制作的语境下，它适合用来改变那些**没有通过游戏的模组接口显式暴露**的行为。**Harmony 已包含在游戏本体中，因此不需要任何外部模组即可使用。**

> 虽然 Harmony 补丁很强大，而且常常是修改行为最简便、最直接的方式，但它们**更容易与其它模组以及未来更新产生不兼容**，出问题时也更难调试排查。**Harmony 补丁应当被视为最后手段（last resort）** —— 只有当所需功能无法通过游戏已有的部件和事件基础设施、或其它类似方案实现时才使用。

**补丁类型的兼容性排序**（官方原文）：

> **Postfix 补丁通常最兼容，其次是"非阻断式"的 Prefix 补丁。会阻止主函数运行的 Prefix 补丁、以及修改函数 IL 代码的 Transpiler 补丁，往往更容易与其它模组冲突，除非它们是唯一选择，否则应该避免。**

**升级路径（很有价值的一条）**：

> Caves of Qud 开发团队更偏好数据驱动的行为，**很可能愿意为你的模组添加一个更好的钩子，而不是让你用 Harmony 补丁**。请到官方 Discord 服务器 `#modding` 频道联系 `@gnarf37` 或 `@armithaig`。

**示例**（"在你的 `.../Mods/ModName/` 目录里创建 `somefile.cs`"）：

```csharp
using HarmonyLib;

namespace YourMod.HarmonyPatches
{
    [HarmonyPatch(typeof(XRL.Messages.MessageQueue))]
    class YourPatch1
    {
        [HarmonyPrefix]
        [HarmonyPatch("Add")]
        static void Prefix(ref string Message)
        {
            Message = "{{chaotic|" + Message + "}}";
        }
    }
}
```

要点：`using HarmonyLib;`；类级 `[HarmonyPatch(typeof(目标类型))]`；方法级 `[HarmonyPatch("方法名")]`（字符串重载）；`[HarmonyPrefix]`；`static void Prefix(ref string Message)` 用 `ref` 改写实参。

**⚠️ Wiki 上完全没有文档的**：

- **需要哪个 NuGet 包 / DLL 引用。** 页面只说 Harmony 随游戏附带，没有给出包名、版本或 `HarmonyLib.dll` 的引用路径。唯一有文档的引用来源是游戏生成的 `Mods.csproj`（见 §9.3）。
- **显式的补丁入口点。** `Harmony.PatchAll`、`new Harmony(id).PatchAll()`、`Harmony.CreateAndPatchAll`、`[HarmonyPatchAll]`、手动 `harmony.Patch(...)` —— **在 Wiki 上全都没有提及**。唯一的示例依赖**特性驱动的自动补丁**，而 Wiki **从未说明这些特性是在何时、如何被应用的**。**这一点请务必自己对着反编译的 `Assembly-CSharp.dll` 或你所用 Harmony 版的文档确认，不要照猜。**
- **Transpiler 的示例代码。** Transpiler 只作为一种风险类别被提到，Wiki 上没有任何 `[HarmonyTranspiler]` / `CodeInstruction` 的样例。

**你本机的实测情况**（可作为 Harmony 确实在运行的旁证）：

`build_log.txt` 里出现：

```
Applying Harmony patches...
Success :)
```

`harmony.log.txt` 里能看到 IL 转储与补丁替换记录：

```
### Patch: virtual System.Void XRL.CharacterBuilds.Qud.UI.<SelectMenuOption>d__4::MoveNext()
### Replacement: static System.Void XRL.CharacterBuilds.Qud.UI.QudCustomizeCharacterModuleWindow+<SelectMenuOption>d__4::...MoveNext_Patch0(...)
```

其它实务要点：

- 引用 `0Harmony.dll`（位于 `CoQ_Data\Managed\`，931 KB）。
- 用了 Harmony 的模组**打开/关闭后必须重启游戏**（你机器上那个 "Reasonable Gameplay Options" 模组的描述里就写着这一点）。
- 补丁错误会写到 `harmony.log.txt`。

### 9.15 反编译查阅 API

- "**Caves of Qud 没有公开的源代码仓库。** 不过有若干第三方工具可以把 `.dll` 文件反编译成等价的 `.cs` 文件。由于反编译的性质，反编译结果与原版会有些不同：**变量、对象和方法的名字可能不同，而且不会有代码注释。**"
- **用 ILSpy 指向 `Assembly-CSharp.dll`**，它位于 **`QudLibPath`** 目录。你机器上的路径（该文件 **11.8 MB**）：

  ```
  D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll
  ```

- "注意反编译出来的代码通常会报错，因为它被剥离了原本的上下文。"
- Visual Studio **自带** ILSpy 的 .NET 反编译引擎（注意 Visual Studio **不是** Visual Studio **Code**）。Mac / Linux 开发者可以在 VS Code 里用 ILSpy 插件。
- **"你可以按 `F12` 跳转到所选对象、变量或方法的定义。如果定义在源码里，Visual Studio 会自动反编译并打开相关文件。"**

**API 命名空间全表**：`XRL.World`、`XRL.World.Parts`、`XRL.World.Parts.Mutation`、`XRL.World.Parts.Skill`、`XRL.World.ZoneParts`、`XRL.World.ObjectBuilders`、`XRL.World.AI`、`XRL.World.Text.Delegates`、`XRL.World.Text.Attributes`、`XRL.Core`、`XRL.Rules`、`XRL.UI`、`XRL.Messages`、`XRL.Wish`，以及 `ConsoleLib.Console` 和 `UnityEngine`。

---

## 10. 世界、区域与地图

### 10.1 概念模型

| 概念 | 大小 / 含义 |
| --- | --- |
| **Zone（区域）** | 游戏的一个"屏幕"，**80 × 25** 个格子 |
| **Cell（格子）** | 区域内的一个具体位置 |
| **World（世界）** | 一组区域的集合。默认世界是 `JoppaWorld`；其它有 `Tzimtzlum`、`Thin World`、`Interior`（载具内部）。技术定义："一组共享同一个 `IZoneFactory` 的区域，以及通过 `Worlds.xml` 指定的一些附加属性" |
| **Parasang（帕勒桑）** | **3 × 3** 个区域。世界地图上的**每一格就是一个帕勒桑**。左上角是 (0,0)，右下角是 (79,24) |

### 10.2 区域 ID（ZoneID）格式

```
WorldName.ParasangX.Y.ZoneX.Y.StrataZ
```

Wiki 的原文解释：

> 在脚本里常见到 `JoppaWorld.53.3.1.1.10` 这样的坐标格式。这个格式是 `WorldName.ParasangX.Y.ZoneX.Y.StrataZ`。上面那个坐标表示：在 JoppaWorld（Qud）里，世界地图上**从右数第 53 格**、**向下第 3 格**，位于该帕勒桑的**中心**。（Zone 范围是 0–2。）它在地表：**Z 层 10 是地表层**，Z 层 0 是 Resheph 之墓所在的位置。Z 层 50 就是地下 40 层。

- `ZoneX` / `ZoneY` 取值 **0–2**。
- `StrataZ` = 10 是地表；大于 10 是天空（如 `Level="5-9"` 是"Moon Stair 上方的天空"）。
- ⚠️ **深度上限**："游戏目前对区域定义施加了 **49 的深度上限**（在 `XRL.World.ZoneManager.GetZoneBlueprint` 中）。因此，如果你定义的区域的 `Level` 属性高于 49，**那些定义会被忽略**。"

**本体真实 ID 示例**：

| 地点 | ZoneID |
| --- | --- |
| Joppa | `JoppaWorld.11.22.1.1.10` |
| 六日高跷（Six Day Stilt） | `JoppaWorld.5.2.1.2.10` |
| Kyakukya | `JoppaWorld.27.20.1.1.10` |
| Ezra | `JoppaWorld.53.4.0.0.10` |
| Grit Gate | `JoppaWorld.22.14.1.0.13` |
| 苏丹陵墓 | `JoppaWorld.53.3.0.2.6`、`JoppaWorld.53.3.1.0.1` |

测试时可以直接 `wish goto:JoppaWorld.11.22.1.1.10` 传送过去。

### 10.3 `Worlds.xml` 的标签结构

> ⚠️ **更正**：`Worlds.xml` 里**没有 `<region>` 元素**。文档化的层级是：

```
<worlds> → <world> → <cell> → <zone> → ( <builder> | <postbuilder> | <population> | <map> | <music> | <widget> | <encounter> | <intproperty> | <boolproperty> )
```

| 标签 | 说明（Wiki 原文） |
| --- | --- |
| `<worlds>` | 根 |
| `<world>` | 如果这个 Name 的世界已经被定义过，会**自动与游戏定义合并**（当前主游戏世界是 `JoppaWorld`，所以想并入 Qud 世界就用它） |
| `<builder>` | 你指定的任何 builder 类会被加入 builder 列表，并在世界创建时执行。**必须是 `XRL.World.WorldBuilders` 命名空间里的类**。本体唯一使用的 builder 是 `JoppaWorldBuilder`。**Class 名以减号（`-`）开头会从已有世界定义中移除所有该类 builder** |
| `<cell>` | 如果 cell 的 Name 与游戏文件里已有的匹配，**会被完全覆盖**。cell 节点可以继承其它 cell 节点 |
| `<zone>` | 在这个世界格内定义指定区域或区域范围……游戏目前施加 49 的深度上限 |
| `<builder>`（zone 内） | 区域的 builder。一个区域可以有多个 builder，**依次施加** |
| `<postbuilder>` | 和 builder 类似，但在之后施加 |
| `<population>` | 用给定的种群表在该区域生成生物和物品 |
| `<map>` | 为该区域使用提供的 `.rpm` 地图文件 |

**真实的本体 cell 定义**（`MoonStairCell`，把你会用到的标签都用上了）：

```xml
<cell Name="MoonStairCell" Inherits="DefaultJoppaCell" ApplyTo="TerrainMoonStair">
  <zone Level="5-9" x="0-2" y="0-2" Name="sky above the Moon Stair" IndefiniteArticle="the" AmbientBed="Sounds/Ambiences/amb_bed_moonstair">
    <builder Class="Sky"></builder>
  </zone>
  <zone Level="10" x="0-2" y="0-2" Name="Moon Stair" IndefiniteArticle="the" AmbientBed="Sounds/Ambiences/amb_bed_moonstair">
    <builder Class="MoonStair"></builder>
    <builder Class="FactionEncounters" Population="GenericFactionPopulation"></builder>
    <music Track="Reflections of Ptoh" />
    <postbuilder Class="ZoneTemplate:MoonStair"></postbuilder>
  </zone>
  <zone Level="11-15" x="0-2" y="0-2" Name="subterranean stair">
    <builder Class="MoonStair"></builder>
    <builder Class="PossibleCryotube"></builder>
    <builder Class="FactionEncounters" Population="GenericFactionPopulation"></builder>
    <music Track="Reflections of Ptoh" />
    <postbuilder Class="ZoneTemplate:MoonStairCaves"></postbuilder>
  </zone>
</cell>
```

**由示例可见的属性**：

| 元素 | 见过的属性 |
| --- | --- |
| `<cell>` | `Name`、`Inherits`、`ApplyTo`、`Mutable`（`Mutable` 决定此处是否进行程序化生成） |
| `<zone>` | `Level`（单个或 `A-B`）、`x`、`y`（单个或 `0-2`）、`Name`、`NameContext`、`ProperName`、`IndefiniteArticle`、`AmbientBed`、`IncludeStratumInZoneDisplay` |

**其它子标签**：`<encounter Table="JoppaOutskirtsEncounters" Amount="minimum" />`、`<map ID="NewMapID" ClearBeforePlace="true" />`、`<widget Blueprint="AmbientLight" />`、`<boolproperty Name="JoinPartyLeaderPossible" Value="false" />`、`<intproperty Name="AmbushChance" Value="10" />`。

> **文档缺口**：Wiki 没有给出 `Worlds.xml` 的必需/可选属性表 —— 属性只能从示例里观察。而且 `Modding:Zone Templates` 这个页面**根本不存在**（红链），尽管 `<postbuilder Class="ZoneTemplate:X">` 到处都在用。

### 10.4 写一个可进入的新世界（最小完整例子）

`Worlds.xml`：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<worlds>
  <world Name="ZBWorld" ZoneFactory="ZBWorldZoneFactory" DisplayName="zone builder test world">
    <cell Name="ExampleCell">
      <zone Level="10" x="1" y="1" Name="test zone">
        <!-- 先留空 -->
      </zone>
    </cell>
  </world>
</worlds>
```

`ZoneFactories.cs`：

```csharp
namespace XRL.World.ZoneFactories {
    public class ZBWorldZoneFactory : IZoneFactory {
        public override bool CanBuildZone(ZoneRequest Request) => false;

        public override Zone BuildZone(ZoneRequest Request) {
            var zone = new Zone(80, 25);
            zone.ZoneID = Request.ZoneID;
            return zone;
        }

        public override void AddBlueprintsFor(ZoneRequest Request) {
            var cb = Blueprint.CellBlueprintsByName["ExampleCell"];
            Request.Blueprints.Add(cb.LevelBlueprint[1, 1, 10]);
        }

        public override void AfterBuildZone(Zone zone, ZoneManager zoneManager) {
            ZoneManager.PaintWalls(zone);
            ZoneManager.PaintWater(zone);
        }
    }
}
```

用 `wish goto:ZBWorld.40.12.1.1.10` 即可到达。

> **"你严格来说只需要实现 `BuildZone` 一个方法。"** `CanBuildZone` "决定我们是直接用 `BuildZone` 构建区域，还是间接地用 `GenerateZone` 和 `AddBlueprintsFor`"。

**`Plane` 与 `Protocol`**：同一个"维度"里的世界可以共享 `Plane`；`Protocol` 编码某个世界的属性，它可能在同一 plane 内共享，也可能不共享。

```xml
<!-- 取自本体的 Worlds.xml -->
<world Name="ThinWorld" ZoneFactory="ThinWorldZoneFactory" DisplayName="Thin World" Protocol="THIN"></world>

<world Name="Tzimtzlum" ZoneFactory="TzimtzlumWorldZoneFactory" DisplayName="Tzimtzlum" Plane="Tzimtzlum"></world>
```

**区域生成的实际执行顺序**（`Modding:Zone Builders`）：

1. 游戏识别玩家所在世界对应的 zone factory。
2. 运行 `CanBuildZone` 选择生成路径（`XRL.World.ZoneManager` 的 `GenerateFactoryZone`）：
   - 返回 true → 由 factory 的 `BuildZone` 创建区域。
   - 返回 false → `GenerateZone` 实例化 + `AddBlueprintsFor` 提供蓝图。
3. 多数 JoppaWorld 的情况走 `AddBlueprintsFor` 路线：游戏查找该区域对应的地形对象，并在 `Worlds.xml` 中搜索要应用的区域蓝图（`XRL.World.ZoneFactories.JoppaWorldZoneFactory` 的 `AddBlueprintsFor`）。
4. 然后遍历区域的 builders 并依次施加（`XRL.World.ZoneManager` 的 `ApplyBuilderToZone`）。

### 10.5 通用区域 builder 清单

| Builder | 说明 |
| --- | --- |
| `AddBlueprintBuilder` | 把 `Object` 参数指定的对象作为蓝图加入 |
| `AddWidgetBuilder` | 把对象加到 (0,0) 格。你在 `Worlds.xml` 里用 `<widget/>` 时就会加（如 `<widget Blueprint="Grassy" />`） |
| `Connecter` | （无描述） |
| `FactionEncounters` | 加入与从指定表采样的传奇生物的遭遇 |
| `MapBuilder` | 从 `.rpm` 文件加载地图；`ID` 或 `FileName` 必须匹配。由 `<map/>` 隐式应用 |
| `Music` | 加入音乐轨；由 `<music/>` 隐式加入 |
| `SolidEarth` | 用页岩填满整张地图（"挖空"式工作流） |
| `StairConnector` | （无描述） |
| `StairsDown` / `StairsUp` | 通往下一/上一 Z 层的楼梯；X/Y 可影响放置区域 |
| `TileBuilding` | （无描述） |

> ⚠️ 警告原文："**游戏已有的区域 builder 往往高度单体化，所以上面未列出的区域 builder 都不建议用来创建新类型的区域。**"

### 10.6 写自己的区域 builder

"`ZoneBuilderSandbox` 是创建新区域 builder 的主要接口。**你添加到游戏里的任何 builder 都会继承这个类。**" 多数只需要 `BuildZone`，它返回一个 bool 表示后续 builder 是否也应运行。

```csharp
namespace XRL.World.ZoneBuilders;

public class SolidEarth
{
    public bool BuildZone(Zone Z)
    {
        for (int i = 0; i < Z.Width; i++)
        {
            for (int j = 0; j < Z.Height; j++)
            {
                Z.GetCell(i, j).Clear();
                Z.GetCell(i, j).AddObject(GameObjectFactory.Factory.CreateObject("Shale"));
            }
        }
        return true;
    }
}
```

**设计规则**："**较早的 builder 应该定义区域的粗粒度细节，例如布局和总体几何形状。较晚的 builder 应专注于给区域添加细粒度细节（例如单个房间和遭遇）。**"

**可用的工具**：`EnsureAllVoidsConnected(Z, pathWithNoise: ...)`；`new FindPath(Z.GetCell(20,12), Z.GetCell(50,12))` 配合 `path.Steps`（需要 `using XRL.World.AI.Pathfinding;`）；`PlacePopulationInRegion(Z, region, "PigFarm")` 和 `PlacePopulationInRect`；`Cell.Clear`、`Cell.ClearWalls`、`Cell.RequireObject`、`ZoneBuilderSandbox.ClearRect`、`ZoneBuilderSandbox.PlaceHut`；`Zone.GetCells()`。

### 10.7 世界生成钩子

```csharp
using XRL.World.WorldBuilders;

namespace YourMod.YourNamespace
{
    // 游戏代码在 JoppaWorld 生成过程中会实例化这个类的一个实例
    [JoppaWorldBuilderExtension]
    public class YourJoppaWorldBuilderExtension : IJoppaWorldBuilderExtension
    {
        public override void OnBeforeBuild(JoppaWorldBuilder builder) { /* ... */ }
        public override void OnAfterBuild(JoppaWorldBuilder builder) { /* ... */ }
    }
}
```

`IWorldBuilderExtension` / `[WorldBuilderExtension]` 是同样的东西，但作用于**整体**世界生成。

**给世界加入"秘密地点"的文档化流程**：① 自己的 builder extension；② 在 `OnAfterBuild` 里用 `AddMutableEncounterToTerrain` 或 `popMutableLocationOfTerrain` 取一块随机的可变地形；③ 添加 zone builder；④ `AddSecret`；⑤ 可选：取得 `TerrainTravel` 部件并加一个 `EncounterEntry`。

```csharp
var zoneManager = The.ZoneManager;
zoneManager.AddZoneBuilder(zoneID, ZoneBuilderPriority.LATE, nameof(RoadNorthMouth));
zoneManager.AddZonePostBuilder(zoneID, nameof(AddObjectBuilder), "Object", zoneManager.CacheObject(creature));
zoneManager.SetZoneName(zoneID, "lair of My Creature", Article: "the", Proper: true);
zoneManager.SetZoneIncludeStratumInZoneDisplay(zoneID, false);
zoneManager.SetZoneProperty(zoneID, "NoBiomes", "Yes");
```

### 10.8 地图文件（`.rpm`）

> "RPM 文件是**静态地图内容**的存储。它们是**一种简单的 XML 格式**。你可以创建自己的 RPM 文件，或者通过在模组文件夹里使用**同名**的 `.rpm` 文件来'补丁'游戏自带的那些。"

- 格式：`.rpm`，XML。用**游戏内置的地图编辑器**制作。
- 位置：模组文件夹里**任意位置**（Wiki 例子：`YourRootModDirectory/RPM/MyMapFile.rpm`、`Path/To/YourNewMap.rpm`）。
- 一个 RPM 可以代表一个区域，也被用于世界地图（`QudWorldMap.rpm` 就是世界 `JoppaWorld` 的 `Map` 值）。

**地图 ID 的推导规则**：

> 地图文件通过它们的 `ID` 进行合并和解析。如果地图根元素上没有定义显式的 `ID` 属性，就会**根据 RPM 文件的名字和相对路径生成一个**。**分隔符、字母大小写和文件扩展名都会被忽略**（`XRL.EditorFormats.Map.MapFile.GetKey`）。

| 文件路径 | 生成的 ID |
| --- | --- |
| `StreamingAssets/Base/GritGate.rpm` | `GritGate` |
| `StreamingAssets/Base/preset_tile_chunks/JoppaSultanShrine_3x3.rpm` | `preset_tile_chunks_JoppaSultanShrine_3x3` |
| `YourRootModDirectory/RPM/MyMapFile.rpm` | `RPM_MyMapFile` |

**往已有地图里合并一个对象**（两个 Ctesiphus 的例子）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Map ID="Joppa" Width="80" Height="25" Load="Merge">
  <cell X="41" Y="7">
    <object Name="Ctesiphus" />
  </cell>
</Map>
```

`Load="Merge"` "告诉地图读取器把内容**追加**到该格，而不删除已经在那里的东西（例如地板砖或其它对象）"。

**替换世界地图上的一块地形**：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Map ID="QudWorldMap" Width="80" Height="25">
  <cell X="12" Y="22">
    <object Name="My_TerrainEastJoppa" />
  </cell>
</Map>
```

世界地图**每格只期望一个地形对象**；不写 `Load="Merge"` 就是告诉游戏替换该格内容。

**在 `Worlds.xml` 里注册地图**（两条等价路线）：

```xml
<worlds>
  <world Name="JoppaWorld" Load="Merge">

    <!-- Mutable 属性决定此处是否进行程序化生成 -->
    <cell Name="some name" Inherits="WatervineCell" ApplyTo="My_TerrainEastJoppa" Mutable="false">

      <!-- 这一段借用了 Joppa 的定义 -->
      <zone Level="10" x="0-2" y="0-2" Name="outskirts, Joppa" NameContext="Joppa">
        <builder Class="JoppaOutskirts" />
        <encounter Table="JoppaOutskirtsEncounters" Amount="minimum" />
      </zone>

      <zone Level="10" x="1" y="1" Name="SomeName" ProperName="true">
        <map FileName="Path/To/YourNewMap.rpm" /> <!-- 把这个地图文件放在模组文件夹里任意位置 -->
        <builder Class="Music" Track="MehmetsMorning" Chance="100" />
      </zone>
      <zone Level="10" x="1" y="2" Name="SomeOtherName" ProperName="true">
        <map ID="NewMapID" ClearBeforePlace="true" /> <!-- 与地图文件根节点上定义的 ID 匹配 -->
        <widget Blueprint="AmbientLight" />
      </zone>

      <!-- 还可以在这里加地下层等更多区域 -->
    </cell>

  </world>
</worlds>
```

> `<map FileName="YdFreehold.rpm" />` 和 `<map ID="YdFreehold" />` 的行为"完全相同"。C# 路线是 `zone.loadMap("YdFreehold.rpm");`。

### 10.9 地图编辑器的开启方式

> ⚠️ **文档化的顺序，一步都不能少：**

1. 启动 Qud。
2. 开启 Overlay UI（Options > Overlay UI > **Enable overlay user interface elements**）。
3. **允许鼠标输入**（Options > Overlay UI > **Allow mouse input**）—— "**跳过这一步地图编辑器就不会工作。**"
4. 回到主菜单。
5. 点击 **Modding Utilities**（右下角）。
6. 点击 **Map Editor**。
7. **New Map** 或 **Load Map**。

**操作**：Ctrl+点击 添加所选物品；Alt+点击 选中已有物品；点击拖拽 移动地图；Shift+点击 区域填充；Shift+拖拽+点击 选择区域。选中区域后，`X` 删除该类型的所有地砖，黄色的双圆按钮用调色板里的地砖替换该类型的所有地砖。**Ctrl+悬停在侧栏地砖上会显示它的完整 XML**（这是学地图格式的最快途径）。

### 10.10 内部区域（Interior Zones）

"**内部区域**是可以附着到对象上、并通过对象的 `enter` 动作进入的特殊区域。它们**可以完全用基于 XML 的模组构建**，（只要有足够的想象力）可以模拟载具、建筑以及更多东西。" 通常与 `Vehicle` 部件一起使用（魔像、圣殿机甲 mk Ia/Ib/II）。模组实例：Hearthpyre（帐篷/圆锥帐篷/蒙古包）、Mycogrigoric Alcoves（**纯 XML** 的太空前哨）。

**四个必需组件**：① 一个地图文件（`.rpm`）；② `Worlds.xml` 中 `Interior` 世界里的一个 cell；③ 一个用来附着区域的对象；④ 区域内部的一个出口对象。

```xml
<worlds>
  <world Name="Interior" ZoneFactory="InteriorWorldZoneFactory" ZoneFactoryRegex="^Interior" DisplayName="Inside" Plane="Inherit" Protocol="Inherit">
    <cell Name="TempleMechaMkI">
      <boolproperty Name="JoinPartyLeaderPossible" Value="false" />
      <zone Level="10" x="1" y="1" Name="Control pit" NameContext="Temple mecha mk I" AmbientBed="sfx_endgame_golem_int_lp" IncludeStratumInZoneDisplay="false">
        <builder Class="InteriorGround" />
        <builder Class="MapBuilder" FileName="TempleMechaMkIInterior.rpm" Width="5" Height="3" ClearBeforePlace="true" />
        <widget Blueprint="AmbientLight" />
        <intproperty Name="AmbushChance" Value="10" />
      </zone>
    </cell>
  </world>
</worlds>
```

> ⚠️ **内部地图必须从左上角格子（cell X=0, Y=0）开始**，"**即使它们的尺寸小于大多数区域使用的常规 80 宽 × 25 高**"。

```xml
<object Name="VehicleTemplarMech" Inherits="BaseRobot">
  <part Name="Interior" Cell="TempleMechaMkI" FallDistance="1" />
  <part Name="Vehicle" ChargeMinimum="1000" Type="TemplarMech" Autonomous="false" IsEMPSensitive="true" IsTechScannable="true" BindBlueprint="Purple Security Card" />
  <part Name="VehiclePilotPopulation" Blueprint="Templar Squire,Gunner-Knight Templar" />
  <part Name="VehicleMeleeInfiltration" />
  <part Name="VehicleSocketSeal" />
</object>
```

各部件作用：

| 部件 | 作用 |
| --- | --- |
| `Interior` | 用指定的 cell 给对象一个内部区域；`FallDistance="1"` 让里面的生物在载具被摧毁时坠落 |
| `Vehicle` | `Type` 会插入载具的 `VehicleRecord`（重塑壁龛用它）；`BindBlueprint` 被 `VehicleSeat` 用来决定玩家能否驾驶；`Autonomous="true"` 允许无人驾驶 |
| `VehiclePilotPopulation` | 默认驾驶员；可以用 `Table="..."` 代替 `Blueprint` |
| `VehicleMeleeInfiltration` | 允许玩家潜入载具 |
| `VehicleSocketSeal` | 除非玩家拥有该载具且它无人驾驶，否则阻止更换能量电池 |

**内部内容物**：出口 = 带 `InteriorPortal` 的对象（`MechExitHatch` 继承 `VehicleGolemExit`）；控制 = 带 `VehicleSeat` 的 `MechPilotSeat`；可选的 `EjectionSeat` **要求**同一格上有 `VehicleEjectionSlot` widget；带 `InteriorContainer` 的容器（`MechInteriorContainer`）给载具一个可交易库存。

```xml
<cell X="3" Y="1">
  <object Name="VehicleEjectionSlot"></object>
  <object Name="MarbleFloor"></object>
  <object Name="MechPilotSeat">
    <intproperty Name="InteriorRequired" Value="2" />
  </object>
</cell>
```

**内部重量**的三种文档化方案：① 给内部 `.rpm` 里每个对象都加 `<intproperty Name="InteriorRequired" Value="1" />`（缺点：除了手工编辑没有简单办法应用到整张地图）；② 定义携带该属性的近似蓝图副本（`<object Name="MechInteriorWall_InteriorRequired" Inherits="MechInteriorWall"><intproperty Name="InteriorRequired" Value="1" /></object>`）——在地图编辑器里更容易，但会有大量重复；③ 在 `Interior` 部件上设 `IgnoreWeight`（**207.76 版本加入**）：`<part Name="Interior" Cell="TempleMechaMkI" FallDistance="1" IgnoreWeight="true" />` —— "**完全忽略其内部任何对象的重量**"，但违背标准的负重行为。

**限制**："**截至 207.82 补丁，游戏不支持多区域内部空间。** 如果你想建造类似多区域内部空间的东西，需要自己 mod 一个世界。" **内部空间嵌套在内部空间里是可行的。**

### 10.11 任务（Quests）

结构：一个顶层 `<quest>` 标签（高级属性：所属阵营、日志成就等）加上若干 `<step>` 节点。

```xml
<quest
  Name="O Glorious Shekhinah!"
  Level="3"
  System="TravelToStiltSystem"
  Accomplishment="On the recommendation of a proselyte, you visited the merchant bazaar and grand cathedral at the Six Day Stilt."
  Hagiograph="=name= trekked through the salt pans, north and west, to the merchant bazaar and grand cathedral of the Six Day Stilt. There, the stiltfolk sang hymns in the sultan's honor."
  HagiographCategory="VisitsLocation">

  <step Name="Make a Pilgrimage to the Six Day Stilt" XP="1500">
    <text>Journey through the Great Salt Desert to visit the merchant bazaar and Mechanimist cathedral, where a proselyte asked you to make on offering of a trinket.</text>
  </step>
</quest>
```

多步骤示例：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<quests>
  <quest Name="Healing Balms for Nima Ruda" Factions="Joppa" Level="1"
    Reputation="25" Accomplishment="You assisted Nima Ruda in her apothecarial duties to the people of Joppa"
    Hagiograph="In the thickets of the salt marsh, =name= gave aid to the sick and wounded."
    HagiographCategory="DoesSomethingRad">

    <step Name="Get a starapple">
      <text>Fetch a starapple from Nima Ruda's starapple tree.</text>
    </step>

    <step Name="Create starapple jam">
      <text>Turn a starapple into starapple jam at a campfire.</text>
    </step>

    <step Name="Deliver the jam">
      <text>Deliver the starapple jam to Nima Ruda.</text>
    </step>
  </quest>
</quests>
```

**属性**：`<quest>` 支持 `Name`（任务的 ID，"通常与它的显示名相同"）、`Level`、`System`（`XRL.World.Quests` 里的一个 `IQuestSystem` 类）、`Factions`、`Reputation`、`Accomplishment`、`Hagiograph`、`HagiographCategory`；`<step>` 支持 `Name`、`XP`。

**测试**：用 `startquest:<任务名>` 许愿强行接取任务。

**驱动任务的两条路**：

| 动作 | 纯 XML | C# |
| --- | --- | --- |
| 接取 | 对话里的 `StartQuest` 部件生成器 | `The.Game.StartQuest("A Canticle for Barathrum");` |
| 完成步骤 | `CompleteQuestStep` 部件生成器；区域里 widget 上的 `QuestStepFinisher` 部件；生物上的 `FinishQuestStepWhenSlain` 部件 | `The.Game.FinishQuestStep(...)` |
| 失败步骤 | — | `The.Game.FailQuestStep(QuestName, QuestStep)` |
| 完成任务 | 对话里的 `FinishQuest` | `The.Game.FinishQuest(QuestName)` |
| 失败任务 | — | `The.Game.FailQuest(QuestName)` |

```xml
<node ID="CanticleAccept3">
  <text>Here you are. Now, go! Off with you! May you live long enough to do my bidding. Away, away!</text>
  <choice GotoID="End" StartQuest="A Canticle for Barathrum">
    <text>Farewell, Argyve.</text>
    <part Name="ReceiveItem" Blueprints="Droid Scrambler,Argyve's Data Disk" Identify="All" />
  </choice>
</node>
```

```xml
<node ID="PresentTheDisk">
  <text>Well done, =factionaddress:Barathrumites=. Present the disk.</text>
  <choice GotoID="InterpretSignal" CompleteQuestStep="Decoding the Signal~Return to Grit Gate|6000" FinishQuest="Decoding the Signal">
    <text>[Give Otho the disk]</text>
    <part Name="GritGateHandler" Rank="Journeyfriend" />
  </choice>
</node>
```

注意 `CompleteQuestStep` 的值格式是 `任务ID~步骤ID`，后面可以用 `|` 接奖励数值（如 `|6000`）。

`IQuestSystem` 的 C# 写法（"默认情况下，`IQuestSystem` 在它所属的任务完成后会被从游戏中移除"）：

```csharp
using System;

namespace XRL.World.Quests;

[Serializable]
public class TravelToStiltSystem : IQuestSystem
{
    public override void Register(XRLGame Game, IEventRegistrar Registrar)
    {
        Registrar.Register(ZoneActivatedEvent.ID);
    }

    public override bool HandleEvent(ZoneActivatedEvent E)
    {
        if (E.Zone.ZoneID == "JoppaWorld.5.2.1.1.10" || E.Zone.ZoneID == "JoppaWorld.5.2.1.2.10")
        {
            The.Game.FinishQuestStep("O Glorious Shekhinah!", "Make a Pilgrimage to the Six Day Stilt", -1, CanFinishQuest: true, E.Zone.ZoneID);
        }
        return base.HandleEvent(E);
    }
}
```

**设计建议**（本体 `Decoding the Signal` 的做法）：在交付步骤上**优先用 `FinishQuest` 而不是 `CompleteQuestStep`**，"因为到那个时间点时任务的某些步骤可能从未被完成过"（例如玩家是买来的果酱而不是自己做的）。

> ⚠️ **`QuestManager` 是管理任务推进的旧接口。虽然 Qud 里仍有基于它的代码，但它已经不再被使用了。**

### 10.12 变异（Mutations）

XML 部分（`Mutations.xml`）：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mutations>
  <category Name="Physical">
    <mutation Name="Udder" Cost="1" MaxSelected="1" Class="FreeholdTutorial_Udder" Exclusions="" Code="ea"></mutation>
  </category>
</mutations>
```

| 元素 | 含义 |
| --- | --- |
| `Name` | 在角色创建界面显示的名字 |
| `Class` | 用于实例化变异对象的 `.cs` 类 |
| `Cost` | 角色创建时的变异点数消耗 |
| `MaxSelected` | "目前只被 Unstable Mutation 使用……不清楚这个值设为大于 1 会不会给模组变异带来问题" |
| `Constructor` | （可选）传给类构造函数的字符串（或逗号分隔字符串）。例如 `GasGeneration` 被腐蚀性气体与催眠气体共用，通过 `Constructor="SleepGas"` / `"Confuse"` 区分。⚠️ 该页挂着 Missing-info 横幅说要"移除过时信息（例如 Constructor 字段）"，所以**可能已废弃** |
| `Exclusions` | （可选）互斥的变异 |
| `BearerDescription` | 供随机生成（村庄/历史）使用，例如 "the many-armed" |
| `Code` | 构造 Build Library 代码时使用。"不清楚是否真的存在'最佳实践'……应该避免使用本体变异已用的代码，但与其它模组的冲突可能无法避免。看起来这个代码可以长于 2 个字符，但这是未经检验的假设" |

**纯 XML 的一大胜利**：如果你**复用已有的类**，就能**零 C#** 造出一个全新的变异：

```xml
<mutations>
  <category Name="Physical">
    <mutation Name="Confusion Gas Generation" Cost="2" MaxSelected="1" Class="GasGeneration" Constructor="ConfusionGas" Exclusions="" BearerDescription="those who expel confusion gas" Code="zz"></mutation>
  </category>
</mutations>
```

**行为一律需要 C#**：类必须在 `XRL.World.Parts.Mutation` 命名空间、加 `[Serializable]`、最终继承 `BaseMutation`（"如果你有非常复杂的模组，不一定非要直接继承"）：

```csharp
namespace XRL.World.Parts.Mutation
{
    [Serializable]
    class FreeholdTutorial_Udder : BaseMutation
    {
        public override void Register(GameObject Object) { }
        public override string GetDescription() { return ""; }
        public override string GetLevelText(int Level) { string Ret = "You have udders.\n"; return Ret; }
        public override bool WantEvent(int ID, int cascade) { return base.WantEvent(ID, cascade) || ID == BeforeRenderEvent.ID; }
        public override bool HandleEvent(BeforeRenderEvent e) { /* ... */ return true; }
        public override bool FireEvent(Event E) { return base.FireEvent(E); }
        public override bool ChangeLevel(int NewLevel) { return true; }
        public override bool Mutate(GameObject GO, int Level) { return true; }
        public override bool Unmutate(GameObject GO) { return true; }
    }
}
```

成员职责：`GetDescription()` / `GetLevelText(int Level)` "被调用来生成给定等级下变异的描述"；`ChangeLevel(int)` "在变异等级发生任何变化时被调用"；`Mutate(GO, Level)` / `Unmutate(GO)` "在对象获得或失去该变异时被调用"；`Register` / `FireEvent` "`BaseMutation` 派生自 `Part`，所以常规的事件注册与处理函数都可使用"。

### 10.13 宠物（Pets）

> **"Caves of Qud 支持只用 XML 就给游戏加入新宠物。"**

> 技术上，要给游戏加入一个自定义宠物，**你唯一需要做的就是给某个生物的蓝图加上 `<tag Name="StartingPet" />`**。

```xml
<?xml version="1.0" encoding="utf-8" ?>
<objects>
  <object Name="Snapjaw Scavenger Pet" Inherits="Snapjaw Scavenger">
    <tag Name="StartingPet" />
  </object>
</objects>
```

**"这会让一个 `snapjaw scavenger` 宠物出现在角色定制菜单里。"**

可选增强（官方 Patreon 宠物用的就是这些）：`Pettable` 部件 + `PetResponse` 标签、独特贴图、`Story` 性质、独特对话脚本。

```xml
<objects>
  <object Name="Snapjaw Scavenger Pet" Inherits="Snapjaw Scavenger">
    <part Name="Pettable" />
    <property Name="Story" Value="My Snapjaw Scavenger Story" />
    <tag Name="PetResponse" Value="licks you,growls" />
    <tag Name="StartingPet" />
  </object>
</objects>
```

```xml
<books>
  <book ID="My Snapjaw Scavenger Story" Title="{{W|My Story Title}}">
    <page>
Write your story for your creature here!
    </page>
  </book>
</books>
```

- `PetResponse` 是"该宠物可以有的不同反应的**逗号分隔列表**"。
- `Pettable` "给它 `pet` 交互"。

> **文档缺口**：Wiki 上**没有**独立的 `Pet` 部件，也没有超出 `StartingPet` 之外的"可驯服"接口。其它宠物系统知识得从游戏文件或 `testpets` 许愿里挖。

---

## 11. 调试方法论

### 11.1 四个报错渠道

| 渠道 | 内容 |
| --- | --- |
| **模组管理器界面** | 某些错误会直接标在模组上并给出错误信息（点击模组查看） |
| **`build_log.txt`** | 加载期错误、依赖缺失、C# 编译警告 |
| **`Player.log`** | 运行期错误（搜 `MODERROR` / `MODWARN`） |
| **`harmony.log.txt`** | Harmony 补丁相关错误 |

### 11.2 建议开启的游戏选项

- **Modding > Enable Mods**（必须）
- **Debug > Show quickstart option during character generation** → 角色创建时出现 `_Quickstart` 选项，能极快地进游戏测试
- **Debug > Show error popups** → 错误立即弹窗（⚠️ 某些情况下会导致游戏卡死）
- **Debug > Show debug info on objects** → 在游戏里检视对象的内部数据

### 11.3 修改 → 生效的工作流

```
编辑 XML/C#
   ↓
回到主菜单 → "Installed Mod Configuration" → 按 r（Save and Reload）
   ↓
重启游戏（继续存档或新建存档）
   ↓
游戏中 wish 出你的对象
```

**热重载要点：**

- **XML 可以游戏内热重载**：`wish reload`，能显著缩短调试循环。
- **C# 改动必须重启游戏**（要重新编译）。
- ⚠️ **对象和区域一旦生成，就不再受蓝图控制**。改了蓝图后：
  - 改 `objects` / `bodies` / `populations` → 需要**重新 wish 一个新对象**。
  - 改 `worlds` / `populations` → 可能需要 `wish rebuild` 重建当前区域。
  - `conversations` 和 `books` → 一般**不需要**重新生成对象。
- 一个超实用的技巧：把待测蓝图标记成 `<tag Name="BaseObject" Value="*noinherit" />` 让基类不出现，再单独 wish 具体子类型。

### 11.4 `Player.log` 的两类错误

**语法错误（Syntactic）** —— XML 不合法：

```xml
<objects>
  <object Name="MySnapjaw">
</objects>
```

```
MODERROR [Snapjaw Mages!] - System.Exception: File: .../Test.xml, Line: 3:3
---> System.Xml.XmlException: The 'object' start tag on line 2 position 4 does not match
     the end tag of 'objects'. Line 3, position 3.
```

→ 错误信息说的是**闭合标签不匹配**，但真正的原因是**第 2 行的 `<object>` 没闭合**。XML 解析器不聪明，要"站在解析器的角度"倒推。

**语义错误（Semantic）** —— XML 合法但内容不对：

```xml
<object Name="MySnapjaw" Inherits="Snapjaw Scvenger">
```

```
MODERROR [Snapjaw Mages!] - blueprint "Snapjaw Scvenger" inherited by MySnapjaw not found
```

→ 这条很友好，直接告诉你拼错了。

**最麻烦的：完全没有报错。** 例如 `ColorString="O"` 应为 `ColorString="&amp;O"`，游戏照常运行，只是颜色变成了浅灰。

### 11.5 无报错类问题的排查手法

1. **用版本控制（git）**。`git diff` 能告诉你改了什么、在哪引入了 bug。
2. **反推相关部件**：颜色不对 → 查 `Render` 部件，因为贴图和颜色归它管。
3. **注意继承带来的覆盖**：如果 `Foo` 有 `RandomTile` 部件，`Bar` 继承 `Foo` 就也有；这会让 `Bar` 自己设的贴图被**静默覆盖**。
4. **开启 "Show debug info on objects"** 在游戏里直接看对象内部状态。

### 11.6 `MODWARN` 常见来源

- 使用了已废弃（deprecated）的类或方法。
- 在对象蓝图里用了已被改名的技能或变异。
- 对**不存在的**蓝图或种群表做 `Load="Merge"`（例如给玩家可能没装的 DLC 里的宠物做合并）。

Warnings 一般应该修，但有些是正当的（DLC 场景）。

---

## 12. 兼容性与"守规矩"

目的是让模组**与其它模组、与游戏未来更新、以及与自己旧版本存档**都能共存。

### 12.1 加前缀（最重要的一条）

给所有**需要唯一**的内部名字加一个大概率不重复的前缀。例如 `TrashMonks_`、`Pyovya_`。

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
  <object Name="TrashMonks_Cool New Sword" Inherits="Long Sword3">
    <!-- ... -->
  </object>
</objects>
```

**需要加前缀的东西**（含"内部标识符"与"玩家可见名"分别怎么设）：

| 数据类型 | 唯一标识符怎么设 | 玩家可见名怎么设 |
| --- | --- | --- |
| 解剖结构（anatomy） | `Name` 属性 | 无 |
| 躯体部位类型 [变体] | `Type` 属性 | `Description` 属性 |
| 事件（非 min-event） | 传给 `FireEvent` 的参数 | 无 |
| **对象蓝图** | `Name` 属性 | `Render` 部件 |
| 选项 | `ID` 属性 | `DisplayText` 属性 |
| **部件类** | 类名 | 仅主动部件：`NameForStatus` 字段 |
| **种群表** | `Name` 属性 | 无 |
| 任务 | `ID` 属性 | `Name` 属性 |
| 带种子的随机生成器 | 传给 `GetSeededRandomGenerator` 的参数 | 无 |
| 技能（"power"） | `Class` 属性 | `Name` 属性 |
| **Wish 命令** | 传给 `WishCommand` 的参数 | 无（玩家看到的就是内部名） |

类如果允许放在任意命名空间，用唯一命名空间名也可以。

### 12.2 只写你改的部分

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
  <object Name="Chain Mail" Load="Merge">
    <part Name="Armor" DV="2" />
  </object>
</objects>
```

**如果你发现自己在从本体整段整段复制 XML，那多半是不需要的。**

⚠️ **`Inherits` 和 `Load="Merge"` 一般不要同时使用。**

### 12.3 其它约定

- **随机函数**：不要用 `Stat.Random()` / `Stat.Rnd()`（见 9.8）。
- **属性值**：优先 `value` 而非 `sValue`（除非在做独特生物）。
- **命名参数**：调 C# API 时优先用（见 9.9）。
- **存档迁移**：优先 `IScribedPart` / `IScribedEffect` / `IScribedSystem`（见 9.7）。
- **Harmony**：只在没有别的办法时用。

### 12.4 模组被判"不兼容"怎么办

1. 修掉它引起的错误（见第 11 节调试方法论）。
2. 向创意工坊上传新版本。
3. 通过官方支持邮箱或 Caves of Qud Discord 的 `#modding` 频道**申请解除不兼容标记**。

---

## 13. 哪些东西没有 XML 接口

**必须用 C# 才能做**（或其 XML 接口只覆盖部分属性）：

| 内容 | 状态 |
| --- | --- |
| **液体（liquid）** | **完全没有 XML 接口**（`lang-experimental` 分支上有一个在开发）。必须写继承 `XRL.Liquids.BaseLiquid` 并带 `[IsLiquid]` 特性的 C# 类 |
| **状态效果（status effect）** | **完全没有 XML 接口** |
| **配方（recipe）** | **完全没有 XML 接口**，未来可能加入 |
| **Wish 命令** | **完全没有 XML 接口** |
| **变异（mutation）** | 有 XML 接口（`Mutations.xml`），但只覆盖部分属性。**行为必须 C#**；但**复用已有类即可纯 XML 造出新变异**（见 §10.12） |
| **技能（skill）** | 有 XML 接口，但只覆盖部分属性 |
| **物品改造（item mod）** | 有 XML 接口，但只覆盖部分属性 |
| **主动技能（activated ability）** | Wiki 上**只有 C# 用法**，没有 XML 结构文档 |
| **AI 行为** | 没有 XML 接口文档；逻辑写在 C# 里，挂钩 `AIGetOffensiveMutationList` 事件 |
| **自定义对话谓词/动作** | 需要 C# 的 `[ConversationDelegate]` 静态方法 |
| **世界/区域生成逻辑** | `Worlds.xml` 可声明，但生成算法要 C#（`IZoneFactory` / `ZoneBuilderSandbox`） |

### 液体最小范例（C#）

```csharp
using XRL.Liquids;
using XRL.World.Parts;

[IsLiquid]
public class MyLiquid : BaseLiquid {
    public MyLiquid () : base ("myliquid") {}
}
```

然后就可以用 XML 生成一滩它：

```xml
<objects>
    <object Name="MyLiquidPool" Inherits="Water">
        <part Name="LiquidVolume" MaxVolume="-1" Volume="10" StartVolume="10d10" InitialLiquid="myliquid-1000"></part>
    </object>
</objects>
```

现在 `wish MyLiquidPool` 就能造出来。⚠️ Wiki 上"修改已有液体"和"液体可实现哪些属性"两节**都还没写**。

---

## 14. 发布到 Steam 创意工坊

### 14.1 上传流程

1. 像平常一样在存档目录的 `Mods` 文件夹下把模组做成一个子目录。
2. 从 Caves of Qud 主菜单打开 **Modding Utilities**（主界面左下角）。
3. 选择 **Steam Workshop Uploader**。
4. 在列表里选中你的模组。
5. 点 **"Create Workshop Id for Mod..."** —— 你的模组就通过模组目录下的 `workshop.json` 与一个工坊条目关联了。此时可以在 Steam 上浏览到它（但内容是空的）。
6. 可选：填写标题、描述等字段。之后也可以在 Steam 上改。
7. 点 **"Upload Content..."** —— 模组文件夹的内容会被上传。
8. 玩家现在就能在 Steam 订阅、重启游戏、获得你的模组内容。

### 14.2 开发者提醒

> ⚠️ **如果你之后把模组上传到创意工坊并订阅了它，一定要把开发用的工作副本移到别处**，避免冲突。

### 14.3 `workshop.json` 字段

见 4.2 节。关键提示：**把 `Visibility` 手动固定为 `"2"`**，可避免游戏在每次推送更新时把模组自动改成私有、逼你去工坊页面手动改回来。

### 14.4 标签

尽量复用 Caves of Qud 工坊上**已有模组正在使用的标签**，方便玩家检索。可以自定义标签，但可发现性会差。

---

## 15. 完整最小可运行示例

以下是一个**可直接使用**的最小模组骨架，已按本文所有规则写好。把它放到：

```
C:\Users\16064\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\Alice_FirstMod\
```

### 15.1 `manifest.json`

```json
{
    "ID": "Alice_FirstMod",
    "Title": "我的第一个模组",
    "Description": "添加一只新的发光水蛭。",
    "Version": "0.1.0",
    "Author": "Alice",
    "Tags": "Creature",
    "PreviewImage": "preview.png"
}
```

### 15.2 `ObjectBlueprints/Creatures.xml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<objects>
  <!-- 基类：不在游戏里出现，仅用于被继承 -->
  <object Name="Alice_FirstMod_Glow Leech" Inherits="Leech">
    <tag Name="BaseObject" Value="*noinherit" />

    <part Name="Description"
          Short="A translucent =pronouns.personTerm= the length of your forearm, lit from within by a slow amber pulse. It regards =pronouns.objective= without fear." />
    <part Name="Render"
          DisplayName="{{Y|glow}} leech"
          Tile="Alice_FirstMod/glow_leech.png"
          ColorString="&amp;y"
          DetailColor="W" />

    <stat Name="Hitpoints" Value="8" />
    <stat Name="DV" Value="2" />

    <!-- 10% 概率携带 1-2 个发光球 -->
    <inventoryobject Blueprint="Floating Glowsphere" Number="1-2" Chance="10" />
  </object>

  <!-- 两个可实际出现的子类型。Jungle_Creatures 是本体真实存在的动态表（已核对） -->
  <object Name="Alice_FirstMod_Bright Leech" Inherits="Alice_FirstMod_Glow Leech">
    <part Name="Render" DisplayName="{{W|bright}} leech"
          Tile="Alice_FirstMod/glow_leech.png" ColorString="&amp;W" DetailColor="Y" />
    <tag Name="DynamicObjectsTable:Jungle_Creatures" />
  </object>

  <object Name="Alice_FirstMod_Dim Leech" Inherits="Alice_FirstMod_Glow Leech">
    <part Name="Render" DisplayName="{{K|dim}} leech"
          Tile="Alice_FirstMod/glow_leech.png" ColorString="&amp;K" DetailColor="y" />
    <tag Name="DynamicObjectsTable:Jungle_Creatures" />
  </object>
</objects>
```

### 15.3 `PopulationTables.xml`（可选：投放到刷新表）

> ⚠️ 这里引用的 `SnapjawParty0`、`SnapjawParty1` 等是**本体真实存在的种群表名**（分别对应 0~3 个 Snapjaw 的小队），已核对。你可以照这个模式把上面的水蛭挂到任意一张已有刷新表里。用之前请先在 `Base\PopulationTables.xml` 里搜一遍确认表名。

```xml
<?xml version="1.0" encoding="utf-8" ?>
<populations>
  <population Name="Alice_FirstMod_Leeches">
    <group Name="Leeches" Style="pickone">
      <object Number="1" Blueprint="Alice_FirstMod_Bright Leech" />
      <object Number="1" Blueprint="Alice_FirstMod_Dim Leech" />
    </group>
  </population>

  <population Name="SnapjawParty0" Load="Merge">
    <group Name="Creatures" Load="Merge">
      <table Name="Alice_FirstMod_Leeches" />
    </group>
  </population>
</populations>
```

注意这里三层 `Load="Merge"` 缺一不可：种群表本身、`group`、以及新增的 `<table>` 引用。

### 15.4 `Textures/Alice_FirstMod/glow_leech.png`

一张 **16 × 24** 的 PNG，只用三种颜色：黑色（→ 主色）、白色（→ 细节色）、透明（→ 背景色）。**不要画彩色。**

### 15.5 测试

1. 主菜单 → **Installed Mod Configuration** → 勾选模组 → 按 `r` 重载 → 重启游戏。
2. 新建存档时选 `_Quickstart`。
3. 游戏内按许愿键（先在按键设置里绑定），输入：

   ```
   Alice_FirstMod_Bright Leech
   ```

4. 如果没出现，去看 `Player.log` 搜 `MODERROR`。

**常用 Wish：**

| Wish | 作用 |
| --- | --- |
| `idkfa` | 上帝模式（无敌 + 穿透即秒杀） |
| `swap` | 与相邻生物交换身体 |
| `reload` | 重载 XML（不需重启） |
| `rebuild` | 重建当前区域（除玩家与同伴外全部重新生成） |
| `<蓝图ID>` | 生成该生物/物品 |
| `item:<物品ID>` | 生成物品 |
| `xp:25000` | 加 25000 经验 |
| `godmode` | 切换无敌 |
| `calm` | 安抚生物 |
| `checkpointon` | 切到 Roleplay 模式 |
| `curefungus` | 治愈真菌感染 |
| `rareliquids` | 生成稀有液体 |
| `fastforwardtomb` | 快进到食尸者之墓 |

---

## 16. 学习路线与资源

### 16.1 推荐学习顺序

1. **先玩一段时间的游戏。** 官方教程明确建议：熟悉了游戏内容，才能在造新东西时对照已有的生物和物品。
2. **读教程**：`Modding:Tutorial - Snapjaw Mages`。它从零开始，一路做到新生物 + 新物品 + 刷新表，**全程不需要 C#**。
   - 配套源码：<https://github.com/TrashMonks/Snapjaw-Mages>
   - 💡 **强烈建议翻这个仓库的 commit 历史** —— 每个提交对应教程的一步，可以精确看到模组是怎么一步步长出来的。
   - 另有一个较旧但仍有参考价值的 [Blue Ctesiphus 教程](https://steamcommunity.com/sharedfiles/filedetails/?id=1302696701)。
3. **读本体的 XML**：`Creatures.xml` 和 `Items.xml` 是你最好的老师。看到一个行为有意思的生物，就去读它的定义，反推部件作用。
4. **读别人的模组源码**（见 15.3）。
5. **需要新逻辑时再学 C#**，从 `IPart` + `Register` + `FireEvent` 开始。
6. **Harmony 放到最后**，且只在别无他法时。

### 16.2 按键页面索引（Wiki）

**核心概念**：Active Parts · Effects · Events · Grammar · Parts · Polish · Populations · Scripting · Turns/Segments/Actions · Wishes · XML

**生物与对象**：Activated Abilities · Bodies · Conversations · Creature AI · Giving Creatures Inventory Items · Inventory Actions · Missile Weapons · Mutations · Objects · Pets · StatShifter · Tiles · Vehicles

**区域与世界**：Interior Zones · Intro - Zones and Worlds · Maps · Worlds · Zone Builders · Zone Procedural Generation

**杂项**：Adding Code at Startup · Adding Code to the Player · Genotypes and Subtypes · Harmony · Key Mapping (Commands) · Liquids · Options · Quests · Randomness · Serialization (Saving/Loading) · Sounds

**资源**：Compatibility · Histographicnomicon · Mod Configuration · Code page 437 · Colors & Object Rendering · Creating a Workshop Mod · Installing a mod · Tutorial - Custom Player Tiles · Tutorial - Snapjaw Mages

### 16.3 可参考的开源模组（作者已授权复用）

| 仓库 | 涉及概念 |
| --- | --- |
| [TrashMonks/Snapjaw-Mages](https://github.com/TrashMonks/Snapjaw-Mages) | 教程配套：生物、物品、种群表 |
| [DeSevilla/my-qud-mods/monsters](https://github.com/DeSevilla/my-qud-mods/tree/main/monsters) | 生物、贴图（进阶：生物的 C# 脚本） |
| [Armithaig/hearthpyre](https://gitlab.com/Armithaig/hearthpyre) | C# 脚本、音效、贴图（大型模组范例） |
| [Ilysen/Jademouth](https://github.com/Ilysen/Jademouth) | 地图、任务、世界 |
| [Ilysen/Tealeaves](https://github.com/Ilysen/Tealeaves) | 对话、地图 |
| [kernelmethod/QudMods](https://gitlab.com/kernelmethod/qudmods/) | 多个模组；[LimbBall](https://gitlab.com/kernelmethod/qudmods/-/tree/main/Pets/LimbBall) 是宠物范例 |
| [librarianmage/FinderOfRuin](https://github.com/librarianmage/FinderOfRuin) | **Harmony**、选项 |
| [AsheIsAmazing/QudMods](https://github.com/AsheIsAmazing/QudMods/) | 多个模组 |
| [HeladoDeBrownie/Caves-Of-Qud-Minimods](https://github.com/HeladoDeBrownie/Caves-of-Qud-Minimods) | 多个小模组 |
| [gnarf37/qud-rhinoconaut](https://bitbucket.org/gnarf37/qud-rhinoconaut/src/master/) | 自定义预设、玩家贴图 |

**你本机也有现成范例**：`CavesOfQud\Mods\Freehold_Pet_Gloaming\`（宠物+对话）和 `Mods\supersoupsludge\`（基因型+贴图+起始装备），以及工坊目录 `workshop\content\333640\` 下的 44 个模组（含大量带 C# 脚本的），`ModAssemblies\` 里已有 50 个编译产物可供参考命名与结构。

### 16.4 求助渠道

- **Caves of Qud Discord** 的 `#modding` 频道
- **Kitfox Games Discord** 的 `#caves-of-qud` 频道
- Wiki 各页面的 talk page

### 16.5 工具清单

| 用途 | 推荐 |
| --- | --- |
| 文本/代码编辑器 | **VS Code**（多平台）、Sublime Text、Notepad++。需要 XML 和 C# 的**语法高亮**。（WordPad 和 Word **不行**，它们不是纯文本编辑器） |
| XML 结构化编辑 | [XiMpLe](https://www.ximple.cz/)（非商业免费），能防止漏写 `<` `/` 这类低级错误 |
| XML 语法检查 | 用 Firefox 或 Chrome 打开 XML 文件，会解析并报语法错误（Firefox 稍宽松但报错更具体） |
| 像素画编辑器 | [Piskel](https://www.piskelapp.com/)（全平台+网页）、[Aseprite](https://www.aseprite.org/)（有免费版）。GIMP 也可用但**要关掉自动抗锯齿** |
| 反编译查 API | **dnSpy** / **ILSpy** 打开 `Assembly-CSharp.dll` |
| 导出本体贴图 | [Brinedump](https://github.com/TrashMonks/brinedump) 模组，或 Unity 资源提取工具 |
| 批量切墙/栅栏贴图 | [ImageSlicer](https://bitbucket.org/bbucklew/imageslicer)（unormal 出品） |
| 版本控制 | **git**（调试无报错类问题的利器） |

### 16.6 别忘了这些游戏内资源

- **`Manual.xml`**：游戏内帮助文本，支持 `{{W|...}}` 富文本与 `~CmdXxx` 键位引用。你写到 `Books.xml`、对话、描述里的富文本用同一套语法。
- **`Mods.xml`**：物品改造定义。
- **`WishCommands.xml`**：本体许愿菜单项，只有 17 行 —— 是学 `<wishcommands>` 根类型的最小范例。

---

## 附录 A：颜色速查（完整调色板）

颜色定义在 `Display.txt` 里。**`&` 设置前景色，`^` 设置背景色。**

| 代码 | 色值 | 代码 | 色值 |
| --- | --- | --- | --- |
| `r` | `#a64a2e` | `R` | `#d74200` |
| `o` | `#f15f22` | `O` | `#e99f10` |
| `w` | `#98875f` | `W` | `#cfc041` |
| `g` | `#009403` | `G` | `#00c420` |
| `b` | `#0048bd` | `B` | `#0096ff` |
| `c` | `#40a4b9` | `C` | `#77bfcf` |
| `m` | `#b154cf` | `M` | `#da5bd6` |
| `k` | `#0f3b3a`（未命名） | `K` | `#155352` |
| `y` | `#b1c9c3` | `Y` | `#ffffff` |

在 XML 文件里 `&` 必须写成 `&amp;`，所以：

- 前景色：`ColorString="&amp;M"`
- 背景色：`^` 是普通字符，直接写 `ColorString="&amp;M^g"` = 亮品红字配绿底
- 教程里 `ColorString="&amp;O"` = 橙色前景

**自定义颜色**：在 `Display.txt` 里 `{ "colors":{ "X":"FFFFFF" } }`。**覆盖本体颜色**只需复用它的字符。

**富文本标记**（新标记语言，除 `Render` 部件的颜色串/细节色外，**所有渲染字符串都应使用它**）：

| 写法 | 效果 |
| --- | --- |
| `{{R\|fire}}` | 红色的 "fire" |
| `{{\|&RThis text is red}}` | 恢复之前的颜色 |
| `{{biomech\|Soupysludge Genotype}}` | 用名为 biomech 的模板渲染 |
| `{{[pattern] [type]\|text}}` | 匿名模板 |

模板类型：`solid`、`sequence`、`alternation`、`bordered`、`distribution`。例如 `{{R-R-R-R-R-M-M sequence|mumble mouth}}`（⚠️ 空格也计入序列位置）。

`StreamingAssets\Base\Colors.xml` 里有约 180 个命名模板（如 `gold`=`W`，`rainbow`=`r-R-W-G-B-b-m` 交替）。
两个特殊 shader（位于 `ConsoleLib.Console.MarkupShaders`）：`chaotic`（每字符随机）、`random`（每字符串随机），**每次查询前景色都会重新随机**，且只能用本体已有的颜色。

---

## 附录 B：代码页 437（CP437）符号

游戏字符串里的 `\u0000` 形式和 XML 里的 `&#x00;`（十进制 `&#000;` 也可）引用的是 **CP437 字符集，不是 Unicode**。映射表完整收录在 Wiki 的 `Modding:Code page 437`（对应 `XRL.UI.Sidebar.Codepage437Mapping`）。

常用符号：

| 符号 | 字符串写法 | 用途 |
| --- | --- | --- |
| ♥ | `{{r\|&#x3;}}` | 生命值 / 伤害 |
| → | `{{c\|&#x1A;}}` | 穿透 / 滑倒 |
| ♦ | `{{b\|&#x4;}}` | 护甲值 |
| ¢ | `{{C\|&#x9b;}}` | 义体信用楔 |

**间接转义**：`{{K|\t}}` 渲染出 ο（TAB = ASCII 9 → CP437 `\x09`）；`{{G|\a}}` 渲染出 •（BEL = ASCII 7 → CP437 `\x07`）。

细节：字符 124 渲染为 `|`（在 IBM 原版上是 ¦）；字符 0 是"空字符"；255 是"不换行空格"。
`RenderString` 保存的就是**关闭贴图模式时**使用的 CP437 字符。

---

## 附录 C：自定义玩家贴图 / 预设角色

⚠️ **更正一个常见误解**：Wiki 教程里**不存在** `ObjectBlueprints`、`PlayerTiles`、`Presets` 这些元素（"Presets" 只是游戏内菜单名）。真正的机制是 **`EmbarkModules.xml` + `QudPregenModule`**。

步骤：

1. 在 `Mods` 文件夹里建一个**名字独特**的模组文件夹（这个名字会出现在模组管理器里）。
2. 建 `Textures` 文件夹，放入 **16×24** 的 `.png`，**用独特的文件名**避免和别的模组冲突。
3. 写 `EmbarkModules.xml`：

```xml
<embarkmodules>
    <module Class="XRL.CharacterBuilds.Qud.QudPregenModule">
        <pregens>
            <pregen Name="TODO:Name" Genotype="TODO:Genotype" Tile="TODO:Tile" Foreground="TODO:Foreground" Detail="TODO:Detail" Background="k">
                <code>TODO:code</code>
                <description>
TODO:description
                </description>
            </pregen>
        </pregens>
    </module>
</embarkmodules>
```

字段含义：

| 字段 | 说明 |
| --- | --- |
| `Name` | 预设的显示名 |
| `Genotype` | `Mutated Human` 或 `True Kin`，或你的模组基因型 |
| `Tile` | 贴图文件名（官方示例用的是 `Tile="Creatures/sw_monad.bmp"`） |
| `Foreground` / `Detail` | 单字母颜色代码，替换黑/白部分。"通常看不到这个颜色，除非你关掉 'Color player's @ based on HP level'。但即使开着，你**离体或被克隆**时也能看到" |
| `Background` | 背景色，示例用 `k` |
| `<code>` | 导出的 build code |
| `<description>` | 支持标记语言和 CP437 转义，如 `{{c|&#249;}} I'm a monad :)` |

- 可以放多个 `<pregen>` 段。
- 想换 build 就用步骤图标往回翻 —— **"不要重新选一次预设，否则你做的改动会被重置。"**
- 第三方替代方案：Choose Your Fighter 模组。

## 附录 D：一句话总结每个核心概念

| 概念 | 一句话 |
| --- | --- |
| **数据模组** | 用 XML 定义新内容，本体也这么写 |
| **脚本模组** | 用 C# 加新逻辑，游戏自己编译你的 `.cs` |
| **部件（Part）** | 任何继承 `IPart` 的 C# 类，是 ECS 里的组件；XML 里用 `<part Name="..." />` 挂载 |
| **蓝图（Blueprint）** | 用 `<object Name="..." />` 定义的对象模板，用 Wish 输入 `Name` 即可生成 |
| **`Inherits`** | 继承另一个蓝图的全部数据 |
| **`Load="Merge"`** | 只写差异，与已有定义合并（`objects` 根下**不写就是替换**） |
| **`BaseObject` + `*noinherit`** | 让一个对象只作基类、不在游戏里出现 |
| **种群表** | 决定"什么怪在哪里刷"；静态表手写，动态表靠 `<tag Name="DynamicObjectsTable:X" />` 自动归类 |
| **三色贴图** | 只画黑白+透明，颜色由 `ColorString`/`TileColor`/`DetailColor` 在运行时赋上 |
| **Wish** | 游戏内控制台，模组开发的主要测试手段 |
| **`Player.log` / `build_log.txt`** | 运行期 / 加载期错误日志，调试第一站 |
| **前缀** | 给所有内部唯一名字加 `你的名字_模组名_` 前缀，避免冲突 |
| **Harmony** | 万能但脆弱的代码注入，最后手段 |

---

## 附录 E：脚本模组的 IDE 工程（可选，但强烈推荐）

游戏会自己编译 `.cs`，所以你**不需要**这个 csproj 也能跑。但**没有 IDE 就没有代码补全和即时查错**，写 C# 会痛苦很多。做法：

在模组目录下建一个 csproj（文件名随意，例如 `mymod.csproj`），照本体模板改写：

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<AssemblyName>mymod</AssemblyName>
		<PackageId>mymod</PackageId>
		<Authors>Alice</Authors>
		<TargetFramework>netstandard2.0</TargetFramework>
		<LangVersion>9</LangVersion>
		<GenerateAssemblyInfo>false</GenerateAssemblyInfo>
		<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
		<QudLibPath>D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\</QudLibPath>
		<DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
	</PropertyGroup>
	<ItemGroup>
		<Reference Include="$(QudLibPath)/Assembly-CSharp.dll" />
		<Reference Include="$(QudLibPath)/0Harmony.dll" />
		<Reference Include="$(QudLibPath)/UnityEngine.CoreModule.dll" />
		<Reference Include="$(QudLibPath)/UnityEngine.dll" />
		<Reference Include="$(QudLibPath)/mscorlib.dll" />
		<Reference Include="$(QudLibPath)/netstandard.dll" />
		<Reference Include="$(QudLibPath)/System.dll" />
		<Reference Include="$(QudLibPath)/System.Core.dll" />
		<Reference Include="$(QudLibPath)/Newtonsoft.Json.dll" />
	</ItemGroup>
</Project>
```

**四个必须注意的取值**（来自本体 `Mods.csproj.template.txt`，不要改）：

- `TargetFramework` = `netstandard2.0`
- `LangVersion` = `9`
- `DisableImplicitFrameworkReferences` = `true`
- `GenerateAssemblyInfo` / `GenerateTargetFrameworkAttribute` = `false`

**不要试图用这个 csproj 生成用于分发的 DLL。** 它只是给你 IDE 用的；实际运行的 DLL 由游戏编译到 `ModAssemblies\`。部分第三方模组作者会**把生成的 DLL 一起打包发布**（你机器上 `ModAssemblies` 里就有以 GUID 命名和以模组名命名的两类 DLL），但这不是必需流程。

**改完 `.cs` 后要重启游戏**（XML 可以 `wish reload` 热重载，代码不行）。

### C# 命名空间速查（从你机器上真实的模组源码里提取）

```csharp
using XRL;                    // 通用
using XRL.Core;               // XRLCore（游戏主循环、CurrentFrame 等）
using XRL.Rules;              // Stat、Rnd 等
using XRL.UI;                 // Popup 等界面
using XRL.World;              // GameObject、Cell、Event、IPart
using XRL.World.Parts;        // 所有部件
using XRL.World.AI;           // 行为/目标
using XRL.Language;           // 语法与文本生成
using XRL.Messages;           // 消息栏
using XRL.Wish;               // WishCommand / HasWishCommand
using XRL.World.Effects;      // 状态效果
```

---

*本文档依据 Caves of Qud 官方 Wiki（`Modding:Overview` 及其超链接页面）与游戏本体 2.0.211.56 的实际文件整理。Wiki 内容采用 CC BY-NC-SA 授权。*
