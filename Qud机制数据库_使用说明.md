# Caves of Qud 机制数据库 —— 使用说明

> 第一阶段产出：把游戏本体的**物品功能实现机制**拆解成可查询、可组装的数据库。
> 数据来源：游戏本体 XML（44 个文件、5.3 MB）+ `Assembly-CSharp.dll`（7,841 个类型）。
> 已核对游戏版本 **2.0.211.56**。

---

## 1. 它解决什么问题

模组里做一个物品，本质不是"抄一段 XML"，而是**组合机制**。以霜冻射线（freeze ray）为例，
它其实是 4 个机制 + 一个**独立蓝图**上的 3 个机制：

```
Freeze Ray (Items.xml:1767)
├── EnergyAmmoLoader   ChargeUse="500"  ProjectileObject="ProjectileFreezeRay"   ← 电量消耗在这
├── EnergyCellSocket   SlotType="EnergyCell"                                      ← 电池插槽
├── MissileWeapon      Skill="Rifle" ShotsPerAction="1" WeaponAccuracy="0"        ← 怎么开火
├── Commerce/Physics/Render/Description/Examiner/TinkerItem/Metal                 ← 呈现与交易
└── 指向 → ProjectileFreezeRay (Items.xml:1786)  ← 投射物是另一个蓝图
    ├── Projectile            BaseDamage="1d4" Attributes="Cold NonPenetrating"  ← 投射物属性
    ├── TemperatureOnHit      Amount="-190"                                      ← 命中降温
    └── TemperatureOnEntering Amount="-190"                                      ← 途经降温
```

所以数据库的**基本单位是一个部件 = 一个机制**，每个机制回答三件事：

1. **能写哪些属性** —— 完整列表
2. **每个属性是什么类型、默认在哪定义** —— 含从父类继承来的
3. **本体是怎么用的** —— 真实取值 + 出处 `文件:行号`

### 让它准确的关键发现

**XML 属性名 = 部件类上的 C# 公共字段/属性名。** 已实测验证：

| XML 写法 | 对应代码 |
| --- | --- |
| `ChargeUse="500"` | `EnergyAmmoLoader` ← 继承自 `IActivePart` |
| `ProjectileObject="..."` | `EnergyAmmoLoader.ProjectileObject` |
| `BasePenetration="4"` | `Projectile.BasePenetration` |
| `ShotsPerAction="1"` | `MissileWeapon.ShotsPerAction` |
| `Short="..."` | `Description.Short`（**属性**，字段是 `_Short`） |
| `Anatomy="Spider"` | `Body.Anatomy`（**属性**） |

这个对应关系让我们能把数据侧（XML）和代码侧（DLL）**连起来**，从而给出**类型**和**真实取值**，
而不是靠猜。当前实测吻合率 **99.71%**。

---

## 2. 数据库规模（实测）

| 项目 | 数量 |
| --- | --- |
| 机制条目 | **2,432** |
| ├─ 对象部件 `part` | 1,371 |
| ├─ 状态效果 `effect` | 397 |
| ├─ 区域生成器 `zonebuilder` | 237 |
| ├─ 技能 `skill` | 173 |
| ├─ 变异 `mutation` | 156 |
| ├─ 对话部件 `convpart` | 61 |
| ├─ 液体 `liquid` | 28 |
| └─ 区域部件 `zonepart` | 9 |
| 属性指纹总数 | **27,302** |
| ├─ ✅ 本体 XML 真用过 | 1,705 |
| ├─ ⚠️ 代码里有、XML 没见过 | 21,294 |
| └─ ⛔ 运行时内部状态 | 4,303 |
| 蓝图条目（对象/技能/变异/躯体/改造…） | **5,727** |
| 部件使用记录 | 16,252 |
| 有本体真实用例的机制 | 956 |
| **成员（方法 + 属性）** | **47,325**（方法 44,472 / 属性 2,853） |
| **事件绑定** | **10,728** |
| └─ 唯一事件名 | 1,041 |
| **技能 power → C# 实现 桥接** | **123 条，100% 定位成功** |
| XML ↔ C# 属性吻合率 | **100.00%（0 个不吻合）** |
| 反编译源码 | 5,438 个 `.cs` 文件 / 24.2 MB |

---

## 3. 怎么用

需要一个 Python。所有命令在 `_tools\` 目录下执行。

```powershell
$py = "C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
$q  = "D:\caves of qud 模组制作\_tools\qud.py"

# 总览
& $py $q stats

# ① 看一个机制的完整数据表（最常用）
& $py $q mech EnergyAmmoLoader
& $py $q mech Projectile
& $py $q mech MissileWeapon --all        # --all 连未用过的代码字段也列出

# ② 看一个物品由哪些机制拼成（含可粘贴 XML）
& $py $q item "Freeze Ray" --xml
& $py $q item "Chain Mail" --xml

# ③ 反查：哪些机制接受某个属性
& $py $q attr ChargeUse
& $py $q attr BasePenetration

# ④ 看某部件在本体的全部用法（最真实的配置范围）
& $py $q use MeleeWeapon --limit 15

# ⑤ 按名字搜机制
& $py $q find charge
& $py $q find ammo

# ⑥ 列出某类别的机制
& $py $q list part --filter ammo
& $py $q list mutation

# ⑦ 【新】看机制"怎么运作" —— 方法实现 + 事件绑定
& $py $q code EnergyAmmoLoader
& $py $q code EnergyAmmoLoader --method GetChargePerAction   # 只看某个方法
& $py $q code Projectile --body 80                          # 方法体多显示几行

# ⑧ 【新】事件绑定：列出 / 反查
& $py $q events                       # 最常被监听的事件 TOP 30
& $py $q events EndTurnEvent          # 哪些机制监听这个事件
& $py $q events GetShortDescriptionEvent

# ⑨ 【新】在方法体里搜代码（找实现逻辑）
& $py $q impl ChargeUse               # 谁在算充能消耗
& $py $q impl BasePenetration         # 穿透怎么算
& $py $q impl Cooldown

# ⑩ 【新】技能树 → power → C# 实现（含花费、属性要求、前置）
& $py $q skills                       # 列出所有技能树
& $py $q skills Axe                   # 看斧系技能树全貌
& $py $q skills Cudgel
```

### 输出里的图例

| 标记 | 含义 | 建议 |
| --- | --- | --- |
| `[OK]` | **本体 XML 真的用过这个属性** | 放心照抄，并附带真实取值分布 |
| `[TRY]` | 代码里有这个字段，但本体 XML 从没用过 | 可以试，但你是第一个；注意类型 |
| `[SKIP]` | 运行时内部状态（`LastStatus`、`Cached...` 等） | 写进 XML 无意义 |

### 短名歧义（重要）

有 **8 个短名对应两个不同的类**，查询时会要求你消歧，而不是随便挑一个：

| 短名 | 两个身份 |
| --- | --- |
| `Shield` | `XRL.World.Parts.Shield`（部件） / `XRL.World.Parts.Skill.Shield`（技能） |
| `StairsDown`、`StairsUp` | `XRL.World.Parts.*`（部件） / `XRL.World.ZoneBuilders.*`（区域生成器） |
| `NightVision` | `XRL.World.Parts.Mutation.NightVision` / `XRL.World.Parts.NightVision` |
| `Music`、`RuinPowerGrids` | `XRL.World.Parts.*` / `XRL.World.ZoneBuilders.*` |
| `LifeDrain`、`Spectacles` | `XRL.World.Effects.*` / `XRL.World.Parts.*` |

遇到歧义时这样指定：

```powershell
& $py $q mech "XRL.World.Parts.StairsDown"      # 用全名
& $py $q mech Shield --kind part                 # 或用类别
& $py $q mech Shield --kind skill
```

### 换一种方式查询：直接开 SQLite

```powershell
& $py -c "import sqlite3;c=sqlite3.connect(r'D:\caves of qud 模组制作\qud_db\qud_mechanisms.sqlite');print(c.execute('SELECT part,attr,type FROM mech_attr WHERE attr=\"ChargeUse\"').fetchall())"
```

| 数据库 | 表 | 用途 |
| --- | --- | --- |
| `qud_mechanisms.sqlite` | `mechanisms` | 每个机制一行（**以 `full` 全名为唯一键**），`json` 列是完整数据表 |
| | `mech_attr` | 每个机制的每个属性一行（`full,part,attr,type,declared_in,safety,observed`） |
| | **`mech_method`** | **每个方法一行，含完整方法体**（`full,part,name,ret,args,line,body`） |
| | **`mech_event`** | **每个事件绑定一行**（`full,part,event,role,is_min`）；`role` ∈ register/want/handle/fire |
| | `mech_alias` | 短名 → 所有同名全名（消歧用） |
| | **`power_class`** | **技能 power → C# 实现桥接**（`skill,power,class,resolved,method_count,event_count`） |
| | `orphan_part` | XML 出现过但找不到同名类的部件名 |
| `qud_blocks.sqlite` | `blocks` | 每个蓝图条目一行，`json` 是完整拆解 |
| | `part_usage` | 每个 `<part>` 出现一行（反查"谁用了这个部件"） |
| | `part_attr` | 每个部件属性出现一行（**自动补全用**） |
| | `stat_usage` | 每个 `<stat>` 出现一行 |
| | `power_attr` | 技能 power 的属性（`Class`/`Cost`/`Minimum`/`Prereq`） |

**事件绑定的 `role` 含义**（对应你写代码时的选择）：

| role | 对应代码 | 含义 |
| --- | --- | --- |
| `register` | `Registrar.Register("X")` / `RegisterPartEvent(this,"X")` | 注册监听（字符串事件或 MinEvent） |
| `want` | `WantEvent(...) \|\| ID == XEvent.ID` | 声明想要某个 MinEvent |
| `handle` | `HandleEvent(XEvent E)` | 实际处理函数 |
| `fire` | `E.ID == "X"` | 该机制自己触发的事件 |

### 健康度自检

```powershell
& $py $q stats              # 含 XML <-> C# 吻合率
& $py $q stats --verbose    # 列出所有对不上的机制
```

游戏更新后跑一次这个，如果吻合率从 99.71% 明显下跌，说明有部件被改名了 ——
这比等模组出错再回头查要早得多。

---

## 4. 实战：从零做一个"电击射线"

用它来演示数据库怎么替代猜测。

**第 1 步：找一个结构最近的现成物品。**

```
& $py $q item "Freeze Ray" --xml
```

得到 11 个部件的完整配方（见本文开头）。

**第 2 步：搞清楚"电量"这个机制到底怎么写。**

```
& $py $q mech EnergyAmmoLoader
```
输出（节选）：
```
--- 属性：本体 XML 实际使用过 (5) ---
  OK   ChargeUse            int      [IActivePart]      取值: 100(9x), 0(4x), 1000(4x), 500(3x)
  OK   ProjectileObject     string   [EnergyAmmoLoader] 取值: ProjectilePointDefenseLaser(2x), ProjectileLaserRifle(2x), ...
```

于是你知道：**电量消耗 = `ChargeUse`，而且它定义在父类 `IActivePart` 上**（所以每个耗电部件都能用）。
`ChargeUse` 的真实取值范围是 0/100/500/1000 —— 这就是本体的平衡区间。

**第 2b 步：搞清楚这个数字到底怎么被用的。**

光知道 `ChargeUse="500"` 还不够 —— 你需要知道**一次射击实际扣多少电**。这就要看实现：

```
& $py $q code EnergyAmmoLoader --method GetChargePerAction
```
```
--- int GetChargePerAction()   src:614 ---
    MissileWeapon part = ParentObject.GetPart<MissileWeapon>();
    return ChargeUse * part.AmmoPerAction;
```

**一次射击耗电 = `ChargeUse` × 同一对象上 `MissileWeapon.AmmoPerAction`。**
霜冻射线的 `ChargeUse="500"` 配 `AmmoPerAction="1"`，每射一次扣 500 充能。
把 `AmmoPerAction` 改成 3，耗电就变成 1500 —— 这个关系**看 XML 永远看不出来**。

想找"还有谁在算充能"，用代码搜索：

```
& $py $q impl ChargeUse
```

**第 3 步：搞清楚投射物属性。**

```
& $py $q mech Projectile
```
```
  OK   BaseDamage            string   取值: 1d6(10x), 1d8(8x), 0(8x)
  OK   BasePenetration       int      取值: 4(9x), 0(8x), 5(7x)
  OK   Attributes            string   取值: Light Laser(8x), Disintegrate(4x), Light(3x)
  OK   PassByVerb            string   取值: streak(11x), hum(6x), flicker(4x)
  SKIP Launcher             object   ← 运行时字段，别写
```
`Attributes` 是空格分隔的效果标签（`Cold NonPenetrating`、`Light Laser`）。

**第 4 步：搞清楚伤害类型/附加效果怎么挂。**

霜冻射线不是靠 `Projectile` 造成寒冷，而是额外挂了 `TemperatureOnHit` / `TemperatureOnEntering`。
这说明**"造成寒冷"是一个独立机制**，不是投射物参数：

```
& $py $q find temperature
```

**第 5 步：写你自己的版本。**

```xml
<object Name="Alice_ElectricRay" Inherits="BaseRifle">
  <part Name="Render" DisplayName="{{W|electric}} ray" Tile="items/sw_raygun.bmp"
        ColorString="&amp;W" DetailColor="Y" />
  <part Name="Physics" UsesTwoSlots="true" Weight="25" />
  <part Name="MissileWeapon" Skill="Rifle" AmmoChar="ER" ShotsPerAction="1"
        AmmoPerAction="1" ShotsPerAnimation="1" WeaponAccuracy="0" />
  <part Name="Commerce" Value="700" />
  <part Name="EnergyAmmoLoader" ChargeUse="500"
        ProjectileObject="Alice_ElectricRay_Projectile" />
  <part Name="EnergyCellSocket" SlotType="EnergyCell" />
  <part Name="Examiner" Complexity="5" />
  <part Name="TinkerItem" Bits="12345" CanDisassemble="true" CanBuild="true" />
  <part Name="Metal" />
  <tag Name="Tier" Value="5" />
  <tag Name="Mods" Value="MissileWeaponMods,FirearmMods,CommonMods,RifleMods,ElectronicsMods,BeamWeaponMods" />
</object>

<object Name="Alice_ElectricRay_Projectile" Inherits="TemporaryProjectile">
  <part Name="Render" DisplayName="{{W|energy beam}}" ColorString="&amp;W" />
  <part Name="Projectile" BaseDamage="1d4" Attributes="Electric NonPenetrating"
        ColorString="&amp;W" PassByVerb="crackle" />
  <!-- 附加效果换成电击类，用 find 找 -->
</object>
```

**每一个属性名、每一个取值，都能在数据库里追到本体出处。**

---

## 5. 已知限制（诚实清单）

第一阶段是**数据侧 + 字段级代码侧**，以下是明确没做的：

| 限制 | 影响 | 现状 |
| --- | --- | --- |
| **✅ 已解决：方法体实现逻辑** | ~~看不到"这个部件具体怎么算伤害"~~ | **已解决**。反编译 5,438 个 `.cs`，索引 **47,325 个成员**（含完整方法体）。用 `qud.py code` 看实现，`qud.py impl` 搜逻辑 |
| **✅ 已解决：部件↔事件绑定** | ~~不知道某部件监听哪些事件~~ | **已解决**。索引 **10,728 条绑定 / 1,041 个唯一事件**。用 `qud.py events` 查询 |
| **✅ 已解决：XML↔C# 吻合率** | ~~少数属性对不上~~ | **已解决：100.00%（0 个不吻合）**。根因是**同名异类属性错配**（见下文），不是解析问题 |
| **✅ 已解决：属性/方法类型解析** | ~~`et:0x00`~~ | **已解决**。属性签名比字段多一个参数计数字节，需用 `property_sig()`。**0 / 27,302 残留** |
| **✅ 已解决：技能 power → 实现** | ~~技能 Class 没和 C# 关联~~ | **已解决**。`power_class` 表，**123 条 100% 定位**。用 `qud.py skills Axe` |
| **✅ 已解决：SDK / 反编译工具链** | ~~无法反编译~~ | **已解决**。.NET SDK 8.0.425 + ilspycmd 9.1.0.7988 |
| **✅ 已解决：短名歧义** | ~~同名不同类被覆盖~~ | **已解决**。全名做主键 + `mech_alias` 消歧 |
| **40 个类型确实没有任何成员** | 1.9% 的机制查询会显示"无成员" | 已核实是**真的空类**：`public class CookingDomainCold_UnitCryokinesis : ProceduralCookingEffectUnitMutation<Cryokinesis> {}` —— 纯继承标记，逻辑全在泛型基类里。**不是解析漏失** |
| **方法体是反编译结果，非原始源码** | 变量名可能被改写、无注释 | 官方无公开源码仓库；逻辑等价，但**不要假定变量名与官方一致** |
| **21,220 个 `[TRY]` 属性** | 数量巨大，多数在 XML 里没用过 | 已分级标注，实际可用的是那 1,705 个 `[OK]` |
| **XML 里的 17 处非法字符引用被丢弃** | 那些是 CP437 字形码 | 已在提取时记录（`blocks.jsonl` 的 `_meta` 行），不影响结构 |

### 一个值得记录的坑：同名异类的属性错配

有 **8 个短名对应两个不同的类**（`Shield` 既是部件又是技能；`StairsDown` 既是部件又是区域生成器）。
XML 只用短名标识部件，所以我最初把**两个类的 XML 用法归到了同一个短名下** ——
结果 `XRL.World.ZoneBuilders.StairsDown`（只吃 `x`/`y`）被"凭空赋予"了部件的
`Connected`/`PullDown`/`Levels` 等属性，然后在诊断里报告成"这 8 个属性代码里找不到"。

**修正方法**：一条 XML 属性只有在目标类**真的能承载它**（字段/属性存在）时才算数。
修完之后吻合率从 99.71% 变成 **100.00%** —— 也就是说之前那 0.29% 的"不吻合"，
有相当一部分其实是我自己的归因错误，而不是游戏数据的问题。
被过滤掉的属性会记录在 `observed_foreign` 里，用 `mech --all` 可以看到。

---

## 6. 文件清单

| 文件 | 说明 |
| --- | --- |
| `qud_db\qud_mechanisms.sqlite` | **机制库**（主查询对象）：机制 + 属性 + 方法 + 事件 |
| `qud_db\mechanisms.jsonl` | 同上，JSON 行格式（便于 diff） |
| `qud_db\qud_blocks.sqlite` | **蓝图库** |
| `qud_db\blocks.jsonl` | 同上，JSON 行格式 |
| `qud_db\csharp.jsonl` | 反编译源码的类型/方法/事件，JSON 行格式 |
| `qud_src\` | **反编译出的 5,438 个 `.cs` 源文件**（24.2 MB），可直接全文检索 |
| `_tools\qud.py` | **查询工具**（见第 3 节） |
| `_tools\extract_blocks.py` | 蓝图拆解器（XML → 块） |
| `_tools\index_mechanisms.py` | 机制索引器（DLL 字段/属性 + XML 用法 → 数据表） |
| `_tools\index_csharp.py` | C# 源码索引器（方法体 + 事件绑定） |
| `_tools\dump_metadata.py` | .NET 元数据读取器（被上面复用） |

---

## 7. 怎么重新生成（游戏更新后）

```powershell
$py   = "C:\Users\16064\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
$dn   = "$env:USERPROFILE\.dotnet\dotnet.exe"
$ilspy= "$env:USERPROFILE\.dotnet\tools\ilspycmd.exe"
$base = "D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base"
$dll  = "D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll"
$out  = "D:\caves of qud 模组制作\qud_db"
$src  = "D:\caves of qud 模组制作\qud_src"
$T    = "D:\caves of qud 模组制作\_tools"

# 1) 拆蓝图（XML）
& $py "$T\extract_blocks.py" --base $base --outdir $out

# 2) 建机制索引（DLL 字段/属性 × XML 用法）
& $py "$T\index_mechanisms.py" --dll $dll --blocks "$out\qud_blocks.sqlite" --outdir $out

# 3) 反编译整程序集（约 36 秒）
Remove-Item $src -Recurse -Force -ErrorAction SilentlyContinue
& $ilspy -p -o $src --nested-directories $dll

# 4) 索引方法体与事件绑定（约 5 秒，可重复运行）
& $py "$T\index_csharp.py" --src $src --db "$out\qud_mechanisms.sqlite" --outdir $out

# 5) 自检
& $py "$T\qud.py" stats
```

脚本对游戏更新有一定韧性：

- **四个脚本全部幂等** —— 每个都先 DROP 自己拥有的表再重建，重复运行数字完全不变。
  （这一点是踩过坑才做到的：早期版本用 `CREATE TABLE IF NOT EXISTS` 但不清表，
  重跑一次就把 `blocks` 从 5,727 变成 11,454、`power_class` 从 123 变成 246。）
- **各脚本只碰自己拥有的表** —— `qud_mechanisms.sqlite` 被两个脚本共享，
  机制索引器只 DROP `mechanisms`/`mech_attr`/`mech_alias`/`orphan_part`/`power_class`，
  绝不删除数据库文件，所以不会毁掉 `index_csharp.py` 的产物。
  跑完会打印 C# 索引行数；若显示 0 或"不存在"，再跑一次 `index_csharp.py` 即可。
- XML 的非法字符引用会被自动修复并记录
- `Assembly-CSharp.dll` 元数据表结构若偏移，脚本会**报错而不是产生垃圾数据**
- 第 5 步的吻合率若不是 100%，说明有部件被改名、或新增了同名类

### 重建顺序（有依赖关系，别打乱）

```
extract_blocks.py      → qud_blocks.sqlite        （独立）
index_mechanisms.py    → 机制表 + power_class      （依赖 qud_blocks.sqlite）
index_csharp.py        → mech_method/mech_event   （依赖 qud_src/，独立于上面两步）
```

`index_mechanisms.py` 建 `power_class` 需要 `mech_method` 已存在，所以**首次**要从上到下跑；
C# 索引已存在时，只重跑前两个也没问题。

---

## 8. 一个完整的挖掘示例：霜冻射线的耗电到底怎么算

把全部能力串起来走一遍。目标：**搞清"一次射击扣多少电"，并知道这个数字能怎么调。**

```powershell
# 1) 物品由哪些机制拼成
& $py $q item "Freeze Ray"
#    EnergyAmmoLoader  ChargeUse="500" ProjectileObject="ProjectileFreezeRay"

# 2) 这个机制有哪些可写属性（附本体真实取值）
& $py $q mech EnergyAmmoLoader
#    OK ChargeUse  int [IActivePart]  取值: 100, 0, 1000, 500

# 3) 这个数字怎么被用的？看实现
& $py $q code EnergyAmmoLoader --method GetChargePerAction
#    MissileWeapon part = ParentObject.GetPart<MissileWeapon>();
#    return ChargeUse * part.AmmoPerAction;   <-- 一次射击 = ChargeUse × AmmoPerAction

# 4) 它监听什么事件（决定代码何时运行）
& $py $q code EnergyAmmoLoader
#    注册: GenerateIntegratedHostInitialAmmo / ...
#    处理: CheckReadyToFireEvent, LoadAmmoEvent, CommandReloadEvent ...

# 5) 全局搜索相关逻辑
& $py $q impl ChargeUse
```

**结论**：一次射击耗电 = `ChargeUse` × `MissileWeapon.AmmoPerAction`。
霜冻射线是 500 × 1 = 每发 500。想改成高耗能连射，把 `AmmoPerAction` 调到 2 就是每发 1000 ——
**这个乘法关系只看 XML 永远看不出来。**

再比如做一个斧系技能，可以直接看整棵技能树的实现：

```powershell
& $py $q skills Axe              # 7 个 power，各自的 C# 类与前置
& $py $q code Axe_Dismember      # Dismember 的实现（17 个成员）
& $py $q events EndTurnEvent     # 哪些机制在回合结束时做事
```

---

## 9. 下一步可做的

原缺口清单已全部完成（见第 5 节的 ✅ 项）。若要继续深化，候选方向：

1. **装配模板生成器** —— 按"我要一个耗电的远程武器"这类**意图**直接生成成套 XML，
   而不是逐个查机制再手工拼。
2. **C# 代码块骨架** —— 从反编译源码提取 `IPart` / `Effect` / `IActivePart` 的标准骨架
   （构造函数、`Register`、`WantEvent`、`HandleEvent`），供直接填充。
3. **把 `mech_event` 变成分诊表** —— 现在能列出"哪些机制监听某事件"，
   下一步可用于回答"我要实现这个效果，应该挂哪个事件"。
4. **物品平衡参考** —— 用 `part_attr` 的取值分布统计各 tier 的数值区间，
   做新物品时对标本体的平衡。
