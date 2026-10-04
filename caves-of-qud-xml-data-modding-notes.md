# Caves of Qud — Data Modding (XML) Technical Research Note

Source: official Caves of Qud wiki (https://wiki.cavesofqud.com). Pages fetched and read:
`Modding:XML`, `Modding:Objects`, `Modding:Tiles`, `Modding:Bodies`, `Modding:Populations`,
`Modding:Colors_%26_Object_Rendering`, `Modding:Code_page_437`,
`Modding:Tutorial_-_Custom_Player_Tiles`, `Modding:Parts`, `Modding:Grammar`,
plus three linked pages: `Modding:Giving_Creatures_Inventory_Items`, `Modding:Conversations`,
`Modding:Activated_Abilities`.

All 13 pages returned HTTP 200 with article bodies; none were empty or erroring.

**Transcription caveat:** when the wiki's syntax-highlighted code blocks are converted to plain
text, closing angle brackets are rendered as `\>` and some `=`/`[` characters gain a backslash
(e.g. `Load="Merge"\>`). In this note those artifacts are normalized to real XML (`>`). Every
snippet is otherwise quoted as it appears on the wiki.

**Coverage caveat (read this first):** the wiki is substantially incomplete for data modding.
Sections 1–4 below mark explicitly, per topic, where the wiki documents nothing. The largest
gaps: there is no documented table of `<object>`-level attributes (only `<object>` *child* tags),
no documentation of `<stats>`/`<skills>`/`<inventory>`/`<equipment>` wrapper blocks, no
`After`/`Before` load-ordering attributes for files or objects, no `Extends`, no
`<goto>`/`<setvar>`/`<if>`/`<wish>` conversation tags, and no Grammar XML file format.

---

## 1. XML data modding fundamentals

### 1.1 Root elements and what they define

From `Modding:XML`: "Caves of Qud describes much of its data using XML format—object blueprints,
population tables, conversations, activated abilities, genders, etc." A mod's XML files are loaded
at game boot. "The game determines what to do with the data from the type of its root element."

Root elements that appear on the fetched pages (this is the complete set the wiki actually shows,
not an exhaustive engine list):

| Root element | Defines | Source page |
| --- | --- | --- |
| `<objects>` | Object blueprints (creatures, items, furniture, corpses, base templates) | XML, Objects |
| `<bodies>` | `bodyparttypes`, `bodyparttypevariants`, `anatomies` | Bodies |
| `<conversations>` | Conversation templates keyed by `ID` | Conversations |
| `<populations>` | Population tables (`population` / `group` / `object` / `table`) | Populations |
| `<encountertables>` | Legacy encounter tables (`encountertable` / `objects` / `object`) | Populations |
| `<embarkmodules>` | Character-creation presets (`module` / `pregens` / `pregen`) | Custom Player Tiles |

The XML page also names `genders` and `activated abilities` as XML-described data, and quotes a
snippet "from the base game's `ActivatedAbilites.xml`" (the wiki spells it both ways) containing
a `<p>` element with an embedded `<stat Name="SwoopCrashChance" />` — evidence that some root
types allow inline markup elements inside text. **The wiki does not give the root element name or
attribute surface for the abilities or genders files.**

### 1.2 `Load` strategies and merge semantics

From `Modding:XML` (`Load` Strategies) — the default and the merge rules are **root-type
dependent**:

- **Most root types:** "when 'duplicate' entries are found for the same data—i.e., those using the
  same `Name` or `ID` value depending on the type—the default action is to merge the data together
  into a single entry."
- **`objects` — exception, and the #1 pitfall:** "For the `objects` root, `object` data is not
  merged by default but instead replaced by the latest entry to be loaded. In order to merge onto
  the existing data instead of replacing it, a `Load="Merge"` attribute must appear in the object
  element's opening tag. Thus the example from earlier doesn't create a new blueprint called
  `Ctesiphus`, but instead augments the existing one."
- **`populations` — second exception:** "although the default is to merge almost everywhere,
  elements *within* a `population` usually represent new items altogether. If instead
  `Load="Merge"` is specified on such an element, the properties of an existing entry can be
  changed." In practice this means **nested groups need their own `Load="Merge"`**; the wiki's own
  example carries the comment: `<!-- Add the Load="Merge" attribute to each nested group, mimicking the base game's PopulationTables.xml structure -->`.

`Load` values documented on the XML page:

| Value | Semantics |
| --- | --- |
| *(absent)* | Root-type default (see above): merge for most roots, **replace** for `objects`. |
| `Merge` | Merge onto the existing entry instead of replacing it. |
| `Remove` | "deletes an entry altogether." |
| `Replace` | "overwrites the existing entry with the new one." |

`Modding:Conversations` gives a slightly different, **explicitly enumerated** value list for
conversation elements: "You can alter the conflict behavior of an element by setting a `Load`
attribute with valid values of: "Merge", "Replace", "Add", or "Remove"." `Add` is documented only
there; the XML or Objects pages do not describe `Add`.

A separate, unrelated `Load` value exists for `<mixin>`: `Load="Fill"` (see §1.4).

### 1.3 `After` / `Before` load-ordering attributes

**No file-level or object-level `After`/`Before` load-ordering attributes are documented on any
fetched page.** The XML page's load section discusses only `Load`; mod load order itself is
controlled by `manifest.json`, referenced as "specific instructions in
`manifest.json`" but not documented on these pages.

The only `Before`/`After` attributes the wiki documents are **conversation choice ordering**
attributes on `<choice>` (`Modding:Conversations`, "Choice ordering"):

| Attribute | Description |
| --- | --- |
| `Priority` | "An integer priority that that specifies where a choice should appear; choices with a higher priority appear closer to the top. By default all choices are given a priority of zero unless explicitly specified…" |
| `Before` | "Place a choice before another choice with the specified ID, e.g., `Before="WaterRitualChoice"`." |
| `After` | "Place a choice after another choice with the specified ID, e.g. `After="WaterRitualChoice"`." |

Do not conflate these with mod load order.

### 1.4 Where files live, and how they are discovered

- Vanilla data: `%game directory%\CoQ_Data\StreamingAssets\Base\ObjectBlueprints`
  (`Modding:Objects`).
- Mod override: "These object definitions may be extended or replaced via an `ObjectBlueprints.xml`
  file placed in your mod's root directory, `%appdata%\Caves of Qud\Mods\[your mod name]\ObjectBlueprints.xml`."
- **Discovery rule (important):** "By default (in the absence of specific instructions in
  `manifest.json`), the game loads as XML **any file anywhere in the mod folder whose name ends
  with `.xml`**." So the file name is not semantically required to be `ObjectBlueprints.xml`; the
  root element decides what the data is. Other pages still show conventional names
  (`PopulationTables.xml`, `EncounterTables.xml`, `Bodies.xml`, `Conversations.xml`,
  `Display.txt` for colors, `modconfig.json`, `display.txt`, `EmbarkModules.xml`), and the custom
  player tiles tutorial notes of `EmbarkModules.xml`: "(The extension _must_ be `.xml` and not
  `.txt`.)"
- Binary assets live under a `Textures` folder in the mod root (see §3).

### 1.5 Inheritance, mixins, and `Name` vs `ID`

`Inherits` **is** documented:

- On `<object>`, from verbatim base-game snippets: `<object Name="Shrewd Baboon" Inherits="Baboon">`
  and `<object Name="Robot" Inherits="Creature">`.
- On conversations (`Modding:Conversations`, "Inheritance"): "you can inherit their properties with
  the `Inherits` attribute. By default, every conversation inherits from `BaseConversation`, which
  holds the definitions of common elements to all conversations like trade and the water ritual.
  The attribute can also take a comma separated list, meaning you can inherit and merge the
  properties of multiple parent elements together. Unlike merging, the properties of the current
  element have precedence over those it is inheriting from." Elements can also be referenced
  across conversations by dotted path: `<choice Inherits="ExcitedSnapjaw.SnappyBye.EndChoice" />`.

`<mixin>` — "Adds a 'mixin' to an object. This can be used in place of (or in addition to)
inheritance to automatically copy parts and tags to an object from another object." Documented
attributes:

- **Name (string):** blueprint used in mixin
- **Include (string):** comma-delimited string of elements to include
- **Exclude (string):** comma-delimited string of elements to exclude
- **Priority (int):** mixin priority (lower comes earlier)
- **Load (string):** "the string "Fill" indicates whether to add the mixin before or after normal
  inheritance, where defining this attribute with the value "Fill" will add the mixin _before_, and
  defining this attribute with any other value (or not defined at all) will add the mixin _after_.
  Use of this attribute treats the parent object as though its inheritance root is the mixin."

`Extends` — **not documented anywhere on the fetched pages.** Only `Inherits` and `<mixin>` appear.

`Name` vs `ID` conventions as documented:

- Objects, bodies, anatomies, body part types/variants, populations, encounter tables: identified
  by **`Name`** (`<object Name=…>`, `<anatomy Name=…>`, `<population Name=…>`,
  `<bodyparttype Type=…>` uses `Type`/`Name` in different places — see §4.1). Blueprint references
  elsewhere use the attribute **`Blueprint=`**, `Inherits=`, or `Name=` (for `<table>`, `<part>`).
- Conversations and conversation elements: identified by **`ID`** (`<conversation ID=…>`,
  `<node ID=…>`, `<start ID=…>`), and are referenced from objects through the
  `ConversationScript` part's `ConversationID` attribute.
- The XML page frames the identity attribute generically as "the same `Name` or `ID` value
  depending on the type."

Auto-generated IDs (conversations only): "If an explicit ID isn't defined, one will be created
based on other attributes." The worked example shows `<text>` children becoming `Text`, `Text2`,
`Text3` and a `<choice Target="LibDink">` becoming `LibDinkChoice` — and shows that a later
`<text Cardinal="3">` collides with the implicit ID `Text3` and therefore merges.

---

## 2. Objects (creatures, items, etc.)

### 2.1 Anatomy of an `<object>` definition — what the wiki actually documents

**The wiki has no attribute table for `<object>` itself.** The only `<object>`-level attributes
appearing in any fetched snippet are `Name`, `Load`, and `Inherits`. Attributes such as `Level`,
`Difficulty`, `Tag`, `Gender`, `Genotype`, `Faction`, `Brain`, `Stats`, `Skills`, `Inventory`,
`Parts`, `Tags`, `Builder` are **not documented as `<object>` attributes** on these pages; the
engine expresses these through child elements and parts instead. Treat the following task-list
attributes as undocumented and requiring decompilation or base-game XML reading:

`ID`, `Tag`, `Level`, `Difficulty`, `Gender`, `Genotype`, `Faction`, `Skills`, `Stats`,
`Properties`, `Parts`, `Tags` — **none documented at object level**.
`DisplayName`, `Render`, `Color`, `Tile`, `TileColor`, `DetailColor` — documented, but as
**attributes of the `Render` part**, not of `<object>`. `Inventory` and `Body` are parts.
`Builder` exists as a child tag but is undescribed (§2.6).

The minimal documented shape is therefore:

```xml
<objects>
    <object Name="Ctesiphus" Load="Merge">
        <part Name="Render" ColorString="&amp;B" />
    </object>
</objects>
```

### 2.2 Child XML tags supported by `<object>`

Verbatim table from `Modding:Objects` ("Supported XML Tags and Functions"):

| XML Tag | Description |
| --- | --- |
| `<part>` | "Indicates that this object should load the part with the specified name. A parts is any C# class that inherits `IPart`. You don't need to know how to code to add a part though, there are a lot of useful parts already available in the base game that you can steal from other objects." |
| `<mutation>` | "If an existing mutation is redefined, the game merges the new attributes you define with existing attributes on the mutation, overwriting them if they already exist." |
| `<builder>` | *(no description given)* |
| `<skill>` | "If an existing skill is redefined, the game merges the new attributes you define with existing attributes on the mutation, overwriting them if they already exist." *(sic — the wiki says "mutation")* |
| `<inventoryobject>` | "Adds an object to a creature's inventory. `Number` can be specified to say how many of the object the creature should have. In addition, a blueprint can be prefixed with `@` to sample the inventory objects from a population table, e.g. `Blueprint="@DynamicObjectsTable:EnergyCells:Tier{ownertier}"`." |
| `<stat>` | *(no description given)* |
| `<property>` | *(no description given)* |
| `<intproperty>` | *(no description given)* |
| `<xtag>` | *(no description given)* |
| `<tag>` | "The code includes a method to remove an existing tag, by redefining it with `Value="*delete"`. However, this appears to currently be broken because it does not work in combination with Load="Merge" or on inherited tags." |
| `<stag>` | "Adds an object to a dynamic semantic table." |
| `<mixin>` | See §1.5. |

Removal tags: "The effects of most objects tags can be reverted using a `remove*` tag. For
instance, `<removepart Name="..." />` will remove a part from an object blueprint;
`<removemutation Name="..." />` can be used to remove a mutation from a creature." Concrete
example: `<removepart Name="Corpse" />`.

Observed usage forms from verbatim snippets:

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

```xml
<object Name="Snapjaw" Load="Merge">
  <part Name="Corpse" CorpseChance="90" CorpseBlueprint="Snapjaw Corpse" />
</object>
```

```xml
<tag Name="ExcludeFromDynamicEncounters" />
<tag Name="PaintedWall" Value="wall_plant" />
<tag Name="InventoryPopulationTable" Value="MyNewSnapjawPopulationTableWhatever" />
<stag Name="Medical" />
```

So `<stat>` takes `Name` + `Value` and/or `Boost`; `<property>` takes `Name` + `Value`; `<tag>`
takes `Name` (+ optional `Value`), and a "tag" name may embed a colon-qualified table key;
`<stag>` takes `Name`.

### 2.3 Parts: how `<part>` works and how to discover valid names/properties

`Modding:Parts`: "A part can be added to an object by adding the `<part/>` XML tag to it."

```xml
<objects>
  <object Name="Snapjaw Scavenger" Load="Merge">
    <part Name="MyPart" />
  </object>
</objects>
```

Attribute values map onto C# fields: given `public string Foo = "hello, world!";` in `MyPart`,
"you could do something like `<part Name="MyPart" Foo="goodbye!" />`". Parts can be attached to
zones too:

```xml
<zone Level="5" x="0" y="2" Name="Tomb of *Sultan1Name*" NameContext="the Tomb of the Eaters" DisableForcedConnections="Yes">
  <map FileName="Sultan1SW.rpm"></map>
  <builder Class="FlagInside"></builder>
  <music Track="Music/Deeper Eaters" />
  <part Name="AmbientStabilization" Strength="40" />
</zone>
```

**How a modder discovers valid part names/properties (as documented):** the wiki's only guidance is
(a) that parts are C# classes in `XRL.World.Parts` inheriting `IPart`, (b) "there are a lot of
useful parts already available in the base game that you can steal from other objects", and (c)
`Modding:Activated_Abilities`' Further Reading, which says "you can also see how any ability was
implemented by decompiling your game and looking at the decompiled code." The two wiki parts
catalogues are explicitly "(incomplete)" / "(non-exhaustive)". There is no schema file or
auto-generated part index documented.

Selected documented parts (abbreviated; quotes are the wiki's own descriptions):

*General*

- `Description` — attrs: `Short`. "Affects the description of an object when you look at it."
- `Render` — attrs: `DisplayName`, `RenderString`, `RenderLayer`, `RenderIfDark`, `DetailColor`,
  `ColorString`. "Change the color and tile used to render an object, as well as the name of the
  object as it appears in-game."
- `DeployWith` — `Blueprint, PreferredDirection, SameCell, SolidOkay, SeepingOkay, Chance, CarryOverOwner`.
- `Food` — `Message, Satiation, Thirst, Healing`.
- `Interesting` — `Key, IfPartOperational, DisplayName, Preposition, Radius, EvenIfInvisible, TranslateToLocation, IconTile, IconRenderString, IconColorString, IconTileColor, IconDetailColor`.
- `RandomColors` — `DetailColor, TileColor`; "should be comma-separated lists of colors."
- `RandomTile` — `Tiles`; "a comma-separated list".

*Creature / corpse*

- `Body` — `Anatomy`. "The body type used by a creature."
- `Brain` — `Hostile, Calm, Factions`. "Used to specify a creature's default hostility state, as
  well as the factions that it belongs to."
- `Corpse` — `CorpseBlueprint, CorpseRequiresBodyPart, BurntCorpseBlueprint, BurntCorpseRequiresBodyPart, VaporizedCorpseBlueprint, VaporizedCorpseRequiresBodyPart`.
- `ConversationScript` — `ConversationID, Quest, PreQuestConversationID, InQuestConversationID, PostQuestConversationID, ClearLost, Filter`.
- `Butcherable` — `OnSuccessAmount, OnSuccess`.
- `Consumer` — `Chance, WeightThresholdPercentage, SuppressCorpseDrops, Message, FloatMessage`.
- `Followers` — `Table`. `GenerateName` — `SpecialType, NamingContext`. `GivesRep` — `repValue`.
- `MentalShield`; `SocialRoles` — `Roles`; `Titles` — `Primary, Ordinary`.

*Item / furniture*

- `Animated` — `ChanceOneIn`; `BootSequence` — `BootTime, VariableBootTime, ReadoutInName, …`;
  `Chat` — `Says, ShowInShortDescription`; `Commerce` — `Value`;
  `CyberneticsBaseItem` — `Slots, Cost, BehaviorDescription`;
  `EnergyCellSocket` — `SlotType, SlottedType, ChanceSlotted, ChanceFullCell, ChanceDestroyCellOnForcedUnequip`;
  `Examiner` — `Complexity, Difficulty, Unknown, Alternate, Understanding`;
  `LiquidVolume` — `InitialLiquid, MaxVolume, StartVolume, ManualSeal, LiquidVisibleWhenSealed`;
  `MutationOnEquip` — `Level, ClassName, Constructor, Describe`;
  `TinkerItem` — `Bits, CanDisassemble, CanBuild, Ingredient, SubstituteBlueprint, RepairCost, RustedRepairCost`.

*Materials / physics*

- `Interior` — `Cell, WX, WY, X, Y, Z, FallDistance, CarriedWeight, Unique`;
  `Metal`; `Physics` — `Weight, Conductivity, FlameTemperature, VaporTemperature, FreezeTemperature, BrittleTemperature, Solid, IsReal, Owner, Takeable, Category`;
  `Springy` — `Factor`.

*Armor / weapons / projectiles*

- `Armor` — `AV, DV, MA, Acid, Elec, Cold, Heat, Strength, Agility, Toughness, Intelligence, Ego, Willpower, ToHit, SpeedPenalty, SpeedBonus, CarryBonus, WornOn`;
  `MeleeWeapon` — `MaxStrengthBonus, BaseDamage, Skill, Stat, Slot`;
  `MissileWeapon` — `AnimationDelay, ShotsPerAction, AmmoPerAction, ShotsPerAnimation, AimVarianceBonus, WeaponAccuracy, MaxRange, VariableMaxRange, AmmoChar, NoWildfire, bShowShotsPerAction, FiresManually, ProjectilePenetrationStat, SlotType, EnergyCost, RangeIncrement, Modifier, Skill`;
  ammo loaders `BioAmmoLoader`, `CooldownAmmoLoader`, `EnergyAmmoLoader`, `LiquidAmmoLoader`,
  `MagazineAmmoLoader`; `Projectile` — `BasePenetration, StrengthPenetration, PenetrateCreatures, PenetrateWalls, Quiet, BaseDamage, ColorString, Attributes, PassByVerb, RenderChar`.

*Misc* — `Harvestable` — `DestroyOnHarvest, OnSuccess, OnSuccessAmount, RipeColor, RipeTileColor, RipeDetailColor, UnripeColor, UnripeTileColor, UnripeDetailColor, StartRipeChance`.

### 2.4 `<stats>`, `<skills>`, `<inventory>`, `<equipment>` blocks

**None of these wrapper elements are documented on the fetched pages.** What *is* documented:

- Stats are singular `<stat Name="…" Value="…" />` / `<stat Name="…" Boost="…" />` children of
  `<object>` — verbatim examples above (`AV = 3`, `Intelligence +1`, `Hitpoints = 20`).
- Skills are singular `<skill>` children of `<object>`; the tag table and the XML page both note
  that redefining an existing skill/mutation **merges** attributes, overwriting duplicates. No
  attributes are listed.
- A creature's starting gear uses `<inventoryobject>`:
  "Adds an object to a creature's inventory. `Number` can be specified to say how many of the object
  the creature should have. In addition, a blueprint can be prefixed with `@` to sample the
  inventory objects from a population table, e.g. `Blueprint="@DynamicObjectsTable:EnergyCells:Tier{ownertier}"`."
  `Modding:Giving_Creatures_Inventory_Items` gives the concrete form:

  ```xml
  <inventoryobject Blueprint="Lives1"></inventoryobject>
  ```

- Random starting gear, modern method (same page): add a tag and supply the matching table:

  ```xml
  <tag Name="InventoryPopulationTable" Value="MyNewSnapjawPopulationTableWhatever"></tag>
  ```

  "…and add the matching population table via a populationtables.xml file in your mod."

- Legacy/deprecated method, quoted verbatim: "Builder classes directly on the inventory part:
  `<part Name="Inventory" Builder="InventoryChestJunk3or4"></part>`".

There is **no documented `<inventory>` block with `Chance`/`Number`/`Object` attributes.** Those
attribute names do exist, but on population-table `<object>` entries and encounter-table
`<object>` entries (§4.2), not in an object blueprint. Do not conflate the two.

### 2.5 Behaviour and AI

Documented XML surfaces for behaviour:

- `Brain` part attributes `Hostile`, `Calm`, `Factions`.
- `ConversationScript ConversationID="…"` (+ quest-gated conversation IDs).
- AI-relevant parts are named but not specified in XML, e.g. "`Consumer` … (along with the
  `SlowDangerousMovement` and and `AIWanderingJuggernaut` parts)." **`Modding:Creature AI` was not
  among the pages fetched; the fetched pages do not document `<brain>`/AI XML attributes beyond
  the `Brain` part.**
- The real AI hook is C#: `Modding:Activated_Abilities` shows abilities registering
  `"AIGetOffensiveMutationList"` and implementing AI logic in `FireEvent`; the same page says the
  AI event "allows you to implement logic for AI to use this activated ability as an attack in
  combat." There is no XML interface documented for custom AI.

### 2.6 Spawning the object in the world

- **Populations / dynamic tables** are the documented path (§4.2). Key tags on the object:
  `<tag Name="DynamicObjectsTable:Baboons" />`, `<stag Name="Medical" />`,
  `<tag Name="ExcludeFromDynamicEncounters" />`, `<tag Name="InventoryPopulationTable" Value="…" />`.
  The Populations page warns: "Some dynamic population tables are created based on the base type of
  the object, so new objects will begin appearing immediately in areas generated with dynamic
  population tables unless they are tagged with `ExcludeFromDynamicEncounters`."
- **`Builder`**: listed as a supported `<object>` child tag with an empty description. The only
  concrete `builder` element on the fetched pages is inside a zone:
  `<builder Class="FlagInside"></builder>`. **The object-level `<builder>` syntax is undocumented
  here.**
- **Wishes / debug loop** (`Modding:XML`, Debugging): "XML can be reloaded during gameplay by
  wishing `reload`"; "testing changes to blueprints may require **wishing for another copy of an
  object** or wishing `rebuild` to regenerate the current zone (everything visible in the main game
  view except the player and their companions)." **The exact wish syntax for spawning a blueprint is
  not given on the fetched pages.** Documented population wishes are
  `population:findblueprint:<blueprint>` and `population:generate:<table>#<amount>`.
- Critical caveat, verbatim: "objects and zones in particular, once generated, are no longer
  beholden to their blueprints." Also: `rebuild` "may be necessary after editing `worlds` or
  `populations`"; testing `objects`, `bodies`, or `populations` changes "may require spawning a new
  object after editing"; "`conversations` and `books`, on the other hand, generally do not require
  spawning new objects."

### 2.7 Representative verbatim snippets

Merge a new colour into an existing blueprint (`Modding:Objects`):

```xml
<objects>
    <object Name="Ctesiphus" Load="Merge">
        <part Name="Render" ColorString="&amp;B"></part>
    </object>
</objects>
```

Remove a part and add a corpse drop:

```xml
<object Name="Snapjaw" Load="Merge">
  <part Name="Corpse" CorpseChance="90" CorpseBlueprint="Snapjaw Corpse" />
</object>
```

A new creature inheriting an existing one, tagged into a dynamic table:

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

From `Modding:XML`, showing inline substitution elements inside text:

```xml
<p>There is a <stat Name="SwoopCrashChance" />% chance you crash to the ground (doubled if your attack is blocked by a shield).</p>
```

---

## 3. Tiles and rendering

### 3.1 Folder layout, file naming, supported formats

- A tile is placed "in a `Textures` subdirectory of your mod. Additional subdirectories can be
  included." Example path:
  `%appdata%\Caves of Qud\Mods\[your mod name]\Textures\creatures\new_tile.png`
- "**Only .png files are supported.**"
- Wall/fence atlases default to the `Textures/Tiles/` path and **default to a `.bmp` extension**
  (overridable — §3.4). The player-tiles tutorial's own working example references
  `Tile="Creatures/sw_monad.bmp"`. **The wiki is internally inconsistent about `.bmp` vs `.png`;
  the explicit rule stated is PNG-only for mod-supplied tiles, with `.bmp` remaining the painted-tile
  default.**
- Resolution: the tutorial specifies **16×24** px for player tiles. Tile size is otherwise
  overridable globally with `display.txt` in the mod base directory:

  ```json
  {
      "tiles":{
          "width":"24",
          "height":"24"
      }
  }
  ```

### 3.2 Colour semantics of a tile image (3-colour scheme)

"The default tiles and shaders used in Caves of Qud use 3-color tiles. Black, non-transparent
pixels are painted with the foreground color, white non-transparent pixels are painted with the
detail color and transparent pixels are painted with the background color."

- **Black** `#000000` → foreground colour
- **White** `#ffffff` → detail colour
- **Transparent** (alpha 0) → background colour, which "will change to the background color of the
  game, known as **viridian**."

**4th colour:** "there are a handful of tiles that use a 4th color, usually RGBA(124, 101, 44, 255).
When rendered inside the game, a special formula is performed to create an weighted mix of the tile
and detail color, with weight based on the 4th color's R channel (the first number). a 4th color
that has 255 red would show up the same as the detail color."

**True-colour tiles:** include `modconfig.json` in the mod root:

```json
{
  "shadermode":"1"
}
```

"Truecolor tiles will be shaded as their natural color, with the background color blended in at
1-alpha."

### 3.3 How `Render`, `Tile`, `TileColor`, `DetailColor`, `ColorString`, `Color` interact

Verbatim `Render` part from `Modding:Tiles`:

```xml
<part Name="Render" Tile="items/sw_spray.bmp" DisplayName="&amp;ySpray&amp;r-&amp;ya&amp;r-&yBrain" ColorString="&amp;G" TileColor="&amp;G" DetailColor="r" RenderString="012" RenderLayer="5"></part>
```

- "Where `TileColor` represents the the black part of the unfiltered image, and `DetailColor` will
  recolor the white part."
- `ColorString` — "contains the foreground and (optionally) the background string for the ascii and
  tiles. Examples: `&B` or `&B^r`".
- `TileColor` — "is identical to ColorString, but applies only to tiles. **If this is not specified,
  a tile falls back to using the ColorString.**"
- `DetailColor` — "changes the 'third' color used only for tiles, not the ascii. DetailColor is
  always just a single character. Example: `g`".
- "Note that if a detail color is not specified, it will default to the background viridian color."
- `RenderString` holds the ASCII glyph(s); in the example, `RenderString="012"` (three CP437
  characters, see §3.5). `RenderLayer` is a z-ordering integer.
- Display-name colouring is inline in `DisplayName`:

  ```xml
  <part Name="Render" DisplayName="&amp;Cb&amp;Be&amp;ba&amp;cd&amp;Ce&amp;Bd&amp;y bracelet" ColorString="&amp;C"></part>
  ```

- `RandomColors` part takes comma-separated `DetailColor`/`TileColor` lists; `RandomTile` takes a
  comma-separated `Tiles` list.

**A new tile for a creature/item** is therefore exactly the mod-merge pattern:

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
    <object Name="Mehmet" Load="Merge">
        <part Name="Render" Tile="creatures/new_mehmet.png"></part>
    </object>
</objects>
```

### 3.4 Painted tiles (walls, fences, liquids)

- Walls and liquids: filename suffix `-00000000`, one bit per adjacent cell, "starting from the
  Northern cell and working clockwise". Example: a horizontal wall segment gets `-00100010`.
  "To properly create a complete wall or liquid that can be rendered in all possible configurations,
  you must have **256** individual wall sprites."
- Fences: suffix `_nsew` (N/S/E/W only, letters always in that order). A lone fence uses `_`.
  "you must have **16** individual fence sprites."
- Root filename tags: `<tag Name="PaintedWall" Value="wall_plant" />` or `PaintedFence`.
- Atlas path override: `<tag Name="PaintedWallAtlas" Value="YourCustomPath" />` /
  `PaintedFenceAtlas`; default is `Textures/Tiles/`.
- Extension override: `<tag Name="PaintedWallExtension" Value=".png" />` / `PaintedFenceExtension`;
  default is `.bmp`.
- `PaintWith`: "if you've got a pillar or mural segment that should be painted to appear connected
  to similar walls around it, you can add this tag to all of the walls involved. The specific value
  of the tag doesn't matter as long as it's the same for all walls involved." Example:
  `<tag Name="PaintWith" Value="MainframeWalls" />`.
- Tooling: the ImageSlicer utility by unormal generates the full set from one 5×5 reference image;
  "You must manually create an `Output` directory in the same location as the executable/project."
  The wiki warns the "Watermaker and Wallmaker (legacy) options probably won't work anymore".

### 3.5 Code page 437 and ASCII rendering

- "there are certain symbols that are used in the game strings that take the form of `\u0000`, where
  `0` is any digit in hexadecimal. In XML, this also takes the form of `&#x00;` … This code does
  **not** represent the character in unicode, rather the **code page 437** on old IBM pcs."
  Decimal escapes `&#000;` are also used in the base game's XML.
- Mapping table is reproduced in full on the wiki (`XRL.UI.Sidebar.Codepage437Mapping`), e.g.
  `\x03`/`&#x3;` = ♥, `\x04` = ♦, `\x1a`/`&#x1A;` = →, `\x9b` = ¢, `\xdb` = █, `\xc4` = ─.
- Documented usage table (XML form):

  | In game | String | Usage |
  | --- | --- | --- |
  | ♥ | `{{r|&#x3;}}` | HP or Damage |
  | → | `{{c|&#x1A;}}` | Penetration, or slipping |
  | ♦ | `{{b|&#x4;}}` | Armor value |
  | ¢ | `{{C|&#x9b;}}` | Cybernetics credit wedge |

- Indirect escapes: `{{K|\t}}` renders ο (Dodge) because TAB is ASCII 9 → CP437 `\x09`;
  `{{G|\a}}` renders • (blocked-attack blip) because BEL is ASCII 7 → CP437 `\x07`.
- **Relation to tile mapping:** the wiki does not document any `Tile`-value ↔ CP437 coupling. The
  link that *is* documented is `RenderString`, which holds CP437 characters used when tiles are
  disabled (see the industrial-fan `Render` override in §3.6, which sets `E.RenderString` while
  nulling `E.Tile`).
- Notable gotchas flagged by the wiki itself: the page carries a "Missing Info" banner —
  "More testing needed. Are there other codes that do not follow 437 conventions? If the intended
  437 symbol is used for these irregular symbols, does it still work?" Also: code point 124 `|`
  renders as `|` in CoQ "although it appeared as ¦ on original IBM computers"; code 0 is
  "?Empty Character" and 255 is "?non breaking space".

### 3.6 Dynamic rendering via `Render()` / `RenderEvent` and `AnimatedMaterialGeneric`

C#-only dynamic override (verbatim):

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

Wiki warning, quoted: "Be careful about overriding `Render`! This is an event that is called very
often by the game. An inefficiently-written `Render` method can slow down the player's game
significantly."

The XML-only alternative for animation is the `AnimatedMaterialGeneric` part:

```xml
<part Name="AnimatedMaterialGeneric"
  AnimationLength="20"
  LowFrameOffset="1"
  HighFrameOffset="1"
  TileAnimationFrames="0=Tiles2/sw_fan_1.bmp,5=Tiles2/sw_fan_2.bmp,10=Tiles2/sw_fan_3.bmp,15=Tiles2/sw_fan_4.bmp"
  RequiresOperationalActivePart="Fan" />
```

"`AnimationLength` tells us how long the full animation should last… `TileAnimationFrames` tells us
the actual frames that should appear… After twenty ticks, we loop back around to 0."

The `RenderEvent` API also exposes `E.ApplyColors("&C", ICON_COLOR_PRIORITY)` and
`E.RenderEffectIndicator("!", "Abilities/abil_berserk.bmp", "&R", "R", 45, 55)` (from the
`SpaceTimeVortex` and `Berserk` examples).

### 3.7 Colours: names, specification, and custom colours

Colours are defined in `Display.txt`. "`&` means to set the foreground color, `^` means to set the
background color." Full documented palette:

| Code | Name | Hex | | Code | Name | Hex |
| --- | --- | --- | --- | --- | --- | --- |
| r | dark red / crimson | `#a64a2e` | | k | *(unnamed)* | `#0f3b3a` |
| R | red / scarlet | `#d74200` | | K | dark grey / black | `#155352` |
| o | dark orange | `#f15f22` | | y | grey | `#b1c9c3` |
| O | orange | `#e99f10` | | Y | white | `#ffffff` |
| w | brown | `#98875f` | | g | dark green | `#009403` |
| W | gold / yellow | `#cfc041` | | G | green | `#00c420` |
| b | dark blue | `#0048bd` | | B | blue / azure | `#0096ff` |
| c | dark cyan / teal | `#40a4b9` | | C | cyan | `#77bfcf` |
| m | dark magenta / purple | `#b154cf` | | M | magenta | `#da5bd6` |

"In XML files, such as ObjectBlueprints.xml, the ampersand (&) must be replaced by the encoded
ampersand (`&amp;`)." So `&` + code becomes `&amp;` + code, while background `^` + code is written
literally:

| Prefix | Text | Xml |
| --- | --- | --- |
| Foreground | `&` + code | `&amp;` + code |
| Background | `^` + code | `^` + code |

So `ColorString="&M^g"` = "foreground bright magenta with a background color of green".

**Custom colours** — add `Display.txt` to your mod:

```json
{
    "colors":{
		"X":"FFFFFF"
    }
}
```

"replace `X` with the character you want to represent your new color, and `FFFFFF` with the hex code
of the new color. If you want to overwrite a vanilla color, add a new color that uses the same
character as the vanilla color you want to overwrite." Multiple entries are allowed.

**Colour markup language:** "there is new system which parses text in double curly braces:
`{{color|text}}`." Leaving out the colour restores the previous colour:
`"{{|&RThis text is red}} and this will be whatever the color was before red"`. Guidance: "every
string for rendering, except the color string/detail color on render part should use the new Markup
language." Templates come from `StreamingAssets\Base\Colors.xml` — the wiki lists ~180 named
templates with a "Type" of sequence / alternation / solid / bordered (e.g. `gold` = `W`,
`crysteel` = `y-y-K-g-g-K-y-y` alternation, `rainbow` = `r-R-W-G-B-b-m` alternation).
Two shaders are special-cased in `ConsoleLib.Console.MarkupShaders`: `chaotic` ("Gives each
character a random color") and `random` ("Gives the entire string a random color"); "the colors are
re-rolled every time the game gets the foreground color of the text", and "both of these shaders
only use vanilla colors".

Anonymous templates: `{{[color pattern delimited by -] [type]|[text]}}`, e.g.
`{{R-R-R-R-R-M-M sequence|mumble mouth}}` — with the wiki's own warning: "spaces in text count
toward the position in the sequence. The 'm' in 'mouth' here will be red, not magenta, as the blank
space is in the 'magenta' space." Types: `solid`, `sequence`, `alternation`, `bordered`,
`distribution` ("Colors each character using a random color from the pattern. Not used by any of
the vanilla templates.").

### 3.8 Custom player tiles / presets (tutorial)

The tutorial's mechanism is **`EmbarkModules.xml` with a `QudPregenModule`**, not
`ObjectBlueprints.xml`/`PlayerTiles`/`Presets` elements — **those element names are not present on
the page**; "Presets" appears only as the in-game menu where the preset shows up.

Requirements: a `.png` tile at **16×24**, a build code from character creation ("Export Code to
Clipboard" on the build summary step), and a text editor. Steps:

1. Locate the `Mods` folder ("'offline' mods" on the wiki's file locations page).
2. "Create a directory inside that folder that will contain your mod data. Name it something
   distinctive, such as `[Your Name]'s Custom Preset`. This name will appear in the in-game mod
   manager."
3. "Inside your new mod folder, make a folder called `Textures` and put your tile in it. Give your
   tile a distinctive file name so that it doesn't conflict with other mods' tiles."
4. Create `EmbarkModules.xml` in the mod folder (extension must be `.xml`), containing:

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

Field semantics, quoted:

- `Name`: "whatever you want the preset to be called on the character creation menu."
- `Genotype`: "Write either `Mutated Human` or `True Kin`, or the appropriate modded genotype if
  you're using one."
- `Tile`: "Write the file name of your tile. For instance, if you put a file called `My Tile.png`
  into the `Textures` folder, write `My Tile.png`." (The working example instead uses a
  subdirectory-qualified path with a `.bmp` extension: `Tile="Creatures/sw_monad.bmp"`.)
- `Foreground`: "the single-letter color code for the foreground color you want … (the one that will
  replace black). Usually, you won't see this color unless you disable the 'Color player's @ based
  on HP level.' option. However, even with that option on, you can see it when you vacate your body
  or get cloned."
- `Detail`: "Likewise, but for the detail color (the one that will replace white)."
- `Background`: set to `k` in both the template and the example.
- `<code>`: the exported build code (a long base64 blob).
- `<description>`: "Write whatever text you want to describe the preset on the character creation
  menu." The example shows colour markup and CP437 escapes inside it:
  `{{c|&#249;}} I'm a monad :)`.

Multiple presets: "add more `pregen` sections just like the first one." A working example is given
verbatim on the page. To use a different build: select the preset, then navigate back via the step
icons and recreate the character — "Don't reselect a preset or else it will reset any changes
you've made." The tutorial also points to the third-party **Choose Your Fighter** mod for tiles not
tied to build codes.

---

## 4. Bodies, populations, parts, grammar

### 4.1 `<bodies>`

`Bodies.xml` "is organized into three main sections": `bodyparttypes` ("the core kinds of body
parts"), `bodyparttypevariants` ("body part sub-types that are more or less interchangeable with
other parts of the same main type"), and `anatomies` ("actual structures of body parts. These are
assigned to objects in ObjectBlueprints.xml").

An object gets an anatomy via the `Body` part:

```xml
<part Name="Body" Anatomy="Spider" />
```

Body part types are declared under `<bodyparttypes>` **inside the `<bodies>` root**:

```xml
<bodies>
  <bodyparttypes>
    <!-- ... -->
    <bodyparttype Type="Head" LimbBlueprintProperty="SeveredHeadBlueprint" LimbBlueprintDefault="GenericHead" Mortal="true" Appendage="true" UsuallyOn="Body" Branching="Lateral,Longitudinal,Vertical,Stratal" ChimeraWeight="3" />
    <!-- ... -->
  </bodyparttypes>

  <!-- Variants and anatomies are defined below -->
  <!-- ... -->
</bodies>
```

(Note the rendering: the prose says "defined under the `<bodypartytypes>` tag" — a typo for
`<bodyparttypes>`, which is what the snippet uses.)

Documented `<bodyparttype>` attributes (the wiki leaves several descriptions blank):

| Attribute | Description |
| --- | --- |
| `Abstract` | "Marks a body part type as abstract. This is used to provide equipment slots that don't actually map to physical limbs, e.g. floating nearby slots, thrown weapon, and missile weapon slots. An abstract body part cannot be chosen as a primary limb." |
| `Appendage` | "Marks a limb type as an appendage, allowing it to be severed (note that abstract body part types are non-severable by default)." |
| `Branching`, `Category`, `Contact`, `DefaultBehavior`, `ImpliedBy`, `ImpliedPer`, `Integral`, `Plural`, `UsuallyOn` | *(blank in wiki)* |
| `ChimeraWeight` | "Weight affecting the probability that the limb type is selected when generating a new limb via the Chimera morphotype." |
| `Description` | "Overrides the name of the body slot as it appears in the inventory menu." |
| `DescriptionPrefix` | "Adds a prefix to the name of the body slot… For example, the `Back` slot has a `DescriptionPrefix` of `Worn on`…" |
| `Extrinsic` | "Marks a body part type as being extrinsic to a creature's body; for example, the robo-hands and robo-arms granted by helping hands… prevents that body part from being set as a primary limb and prevents it from dropping an object when severed." |
| `LimbBlueprintProperty` | "The tag to look for to determine the object blueprint that should be used when this limb is severed." Verbatim usage: `<tag Name="SeveredHandBlueprint" Value="RobotHand" />` inside `<object Name="Robot" Inherits="Creature">`. |
| `LimbBlueprintDefault` | "The default blueprint that should be used for a severed version of the limb when one isn't explicitly specified." |
| `Mobility` | "Marks the limb as providing mobility, and gives a weight… a limb with `Mobility="10"` contributes ten times as much… as a limb with `Mobility="1"`." |
| `Mortal` | "Marks a body part as mortal, which has implications for skills like Amputate Limb and Decapitate." |
| `Name` | "The name of the part type. Should be unique." |
| `NoArmorAveraging` | "Disable averaging of AV across body parts of this type." |

Variants: "Outside of any properties that you explicitly define for a variant, these are
functionally equivalent to their original type." `VariantOf` "specifies the original part type that
this variant belongs to; `Type` is the unique name of the variant."

```xml
<bodyparttypevariant VariantOf="Hand" Type="Tentacle" DefaultBehavior="SoftManipulator" Mobility="1" UsuallyOn="Body" />
```

Anatomies are "a nested collection of `<part>` tags". Verbatim bird anatomy:

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

`<anatomy>` attributes: `BodyCategory` (blank), `BodyMobility` ("Weighs the creature's body slot when
computing the total mobility… used by some creatures, such as creatures with the `Snake` or `Slug`
anatomies, that don't have any foot slots by default"), `BodyType` ("You can use this to replace the
default `Body` slot for a creature with a custom type variant"), `Category` (blank), `FloatingNearby`,
`ThrownWeapon`. Verbatim example:

```xml
<anatomy Name="BipedalRobot" Category="Mechanical" ThrownWeapon="Middle Hardpoint">
  <part Type="Control Unit">
    <part Type="Sensor Array" />
  </part>
  <part Type="Chassis" />
  <part Type="Hardpoint" Laterality="Right" />
  <part Type="Hardpoint" Laterality="Left" />
  <part Type="Feet" />
</anatomy>
```

`<part>` inside `Bodies.xml` attributes: `Abstract`, `Category`, `Contact`, `DefaultBehavior`,
`DependsOn`, `Extrinsic`, `IgnorePosition`, `Integral`, `Laterality`, `Mass`, `Mobility`, `Mortal`,
`Plural`, `RequiresLaterality`, `RequiresType`, `SupportsDependent`, and `Type` ("The name of the
body part type or type variant that should be added (for example: `Hand`, `Smooth Face`, etc.)").
`Laterality` "allows players to distinguish limbs of the same type from one another."

Note: `Modding:Bodies` is flagged as a **stub** by the wiki.

### 4.2 `<populations>`

"There are two primary population systems in Caves of Qud, the legacy `EncounterTables.xml` system,
and the `ZoneTemplates.xml`+`PopulationTables.xml` system. Encounter tables are an older system and
we prefer to use zone templates + population tables these days, but much of the game is still
populated via `EncounterTables.xml`."

The page carries two cleanup banners: "**Everything about encounter tables is out of date.**"

**Load semantics here** (verbatim): "both the `EncounterTables.xml` and the `PopulationTables.xml`
support use of the `Load="Merge"` attribute to merge content into existing tables defined by the
base game files. **This will append entries to the specified table instead of overwriting it.**"

Legacy encounter table merge:

```xml
<encountertables>
  <encountertable Name="Ammo 1" Load="Merge">
    <objects>
      <object Chance="100" Number="3d6" Blueprint="My New Ammo"></object>
    </objects>
  </encountertable>
</encountertables>
```

Population table merge (note the nested-group requirement):

```xml
  <population Name="GenericLairOwner" Load="Merge">
    <group Name="Options" Load="Merge">  <!-- Add the Load="Merge" attribute to each nested group, mimicking the base game's PopulationTables.xml structure -->
      <object Blueprint="WristbladeMerchant" Weight="20" /> <!-- Insert your new item! -->
    </group>
  </population>
```

Full worked example (a Six Day Stilt vendor), quoted:

```xml
  <!-- Merge new YoyoWinderTent into the game's StiltTents population table -->
  <population Name="StiltTents" Load="Merge">
    <group Name="Types" Load="Merge">
      <table Name="YoyoWinderTent" Weight="4" />
    </group>
  </population>

  <!-- Define the YoyoWinderTent -->
  <population Name="YoyoWinderTent">
    <group Name="Contents" Style="pickeach">
      <object Blueprint="YoyoWinder" Number="1" Hint="Interior" />
      <table Name="YoyoWinderTentContents" />
    </group>
  </population>
  <population Name="YoyoSigns">
    <group Name="Items" Style="pickone">
      <object Blueprint="YoyoSign1" Number="1" Hint="OutsideDoor:1" />
      <object Blueprint="YoyoSign2" Number="1" Hint="OutsideDoor:1" />
      <object Blueprint="YoyoSign3" Number="1" Hint="OutsideDoor:1" />
    </group>
  </population>
  <population Name="YoyoWinderTentContents">
    <group Name="Contents" Style="pickeach">
      <object Blueprint="Torchpost" Number="1" Hint="InsideCorner" />
      <table Name="YoyoSigns" />
      <object Blueprint="Torchpost" Number="1-2" Hint="OutsideDoor:2" />
      <object Blueprint="YoyoWinder Workbench" Number="1-2" Hint="AlongInsideWall" />
      <object Blueprint="Woven Basket" Number="1" />
      <object Blueprint="YoyoWinder Workbench" Number="1" />
      <object Blueprint="Yoyo Oil Pitcher" Number="1-2" Hint="AlongInsideWall" />
      <object Blueprint="Yoyo String Plastifer 3" Number="1" Chance="15" />
      <object Blueprint="Yoyo String Elastyne 3" Number="1" Chance="35" />
      <object Blueprint="Yoyo String Elastyne 5" Number="1" Chance="20" />
      <object Blueprint="Chest" Number="1-2" Hint="AlongInsideWall" />
    </group>
  </population>
```

Documented population XML surface, as evidenced above (the wiki gives **no attribute reference
table** for these elements — the semantics below are inferred by the wiki's own prose, not stated
in a spec): `<population Name>` → `<group Name Style>` → `<object Blueprint Number Chance Weight Hint>`
and `<table Name Weight Chance Number Hint>`. Observed `Style` values: `pickeach`, `pickone`.
`Number` accepts a range (`"1-2"`) and dice notation (`"3d6"`). `Chance` is a percentage. `Hint`
takes placement hints (`Interior`, `InsideCorner`, `OutsideDoor:1`, `OutsideDoor:2`,
`AlongInsideWall`, `AlongWall`).

**Task-list attributes that are NOT documented on the fetched Populations page:** `Region`, `Zone`,
`Level`, `Selectors`, and `<choice>` entries inside `<table>`. There is no `<table>` containing
`<choice>` children on this page at all — `<table>` is a *reference* to another population table by
`Name`. If a `<choice>`-based table form exists in the engine, the wiki does not document it here.
Zone/level placement of populations is handled by `ZoneTemplates.xml`, which the page names but does
not document.

**Dynamic tables** — three construction methods, quoted:

1. `DynamicObjectsTable` — "contains all objects that have been tagged as belonging to that table",
   via `<tag Name="DynamicObjectsTable:Baboons" />`.
2. `DynamicInheritsTable` — "contains all objects that inherit from another object", e.g.
   `<table Name="DynamicInheritsTable:Tool" Chance="15" />`. "A `DynamicInheritsTable` can be
   weighted based on the tier of an item by appending `:Tier<zonetier>`. For example,
   `DynamicInheritsTable:BaseLongBlade:Tier5` will give a weight of 1000 to items of the specified
   tier, a weight of 100 to items one tier above or below that, a weight of 10 to items two tiers
   above or below that, and a weight of 1 to all other items." The page tabulates the resulting
   weights for long blades (e.g. fullerite long sword = 1000, folded carbide = 100, iron = 1).
3. `DynamicSemanticTable` — "can generate any object within an intersecting list of categories",
   e.g.:

```xml
<table Chance="80" Number="1-8" Name="DynamicSemanticTable:Medical,Furniture:4:6" Hint="AlongWall" />
```

"`DynamicSemanticTable:Medical,Furniture:4:6` is a dynamic table which includes objects that have
both `<stag Name="Medical" />` and `<stag Name="Furniture" />` in their definition. The `4:6` bit
will weight objects more heavily as their tier gets closer to 4 or their tech tier gets closer to 6."

Exclusion: `<tag Name="ExcludeFromDynamicEncounters" />` — "especially helpful when creating unique
or rare NPCs and items… also useful when defining base objects, who serve as a template on which to
construct other objects but should not themselves appear in-game."

Debug wishes: `population:findblueprint:<blueprint>` — "shows the probability of a particular
blueprint appearing at least once for every population table"; `population:generate:<table>#<amount>`
— "generates a given population table the specified number of times, and then shows the frequency of
various blueprints among the generated objects." "Both wishes are compatible with dynamic tables."

Template placeholders: `{zonetier}` (`<table Name="Junk {zonetier}" Chance="15" />`) and
`{ownertier}` (in the `@DynamicObjectsTable:EnergyCells:Tier{ownertier}` blueprint form).

### 4.3 Parts (`Modding:Parts`)

"Parts are a core element of how creatures, objects, zones, and more are implemented in the game. In
essence, a 'part' can be thought of as a bit of code attached to an object, consisting primarily of
some data for that object, and some event handlers."

Examples of parts doing core work: `Description` implements the look description; `Render`
implements the tile; `Mutations` stores mutations "and individual mutations are also parts in and of
themselves"; `CallToArmsScore` is a zone part; `OmonporchBattlePart` is a part added to the player.

Part type hierarchy documented (non-exhaustive):

- `IPart` — "the most generic kind of part that can be added to an object."
  - `BaseMutation` — "a part used to represent a single mutation."
  - `IActivePart` — "used to represent active parts. This encompasses parts that are only activated
    under certain conditions to perform various behaviors." (See `Modding:Active Parts`.)
  - `IGrenade` — grenade behaviours.
  - `IModification` — item mods.
  - `IPlayerPart` — "a part that follows the player around even after changing bodies."
  - `IPoweredPart` — "a more specific variant of `IActivePart`… objects and abilities that require
    power, e.g. via energy cells."
  - `IScribedPart` — "a special type of part, primarily intended to be used by modders, that makes
    it easier to add or remove fields between mod versions."
- `IZonePart` — "a part that can be applied to a zone."

XML surface: `<part Name="MyPart" />`, `<part Name="MyPart" Foo="goodbye!" />`; removal with
`<removepart Name="..." />`. Parts are added to objects and to zones with the same tag.

C# contract for a new part (verbatim, including the wiki's own comment):

```csharp
using System;

namespace XRL.World.Parts
{
    // Almost all parts will want to have [Serializable] attached to
    // them, because that will allow the game to convert the part
    // to data when saving the game. In some rare circumstances, you
    // can mark a part [NonSerialized] to make sure that the game
    // _does not_ save the part.
    [Serializable]
    public class MyPart : IPart
    {
        public string Foo = "hello, world!";

        public override bool WantEvent(int ID, int cascade)
        {
            return ID == GetShortDescriptionEvent.ID
                || base.WantEvent(ID, cascade);
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            E.AddMark(Foo, DescriptionBuilder.ORDER_ADJUST_EXTREMELY_EARLY);
            return base.HandleEvent(E);
        }
    }
}
```

Data-only parts are discouraged: "this is a use case for which you would typically attach a
`<tag/>`, `<property/>`, or an `<intproperty/>` to an object." **`Modding:Parts` is flagged as a
stub**, and its part list is explicitly non-exhaustive.

### 4.4 Grammar

`Modding:Grammar` is **short and flagged as a stub**. It documents only token syntax, not grammar
XML files:

**Term format** (verbatim table):

| format | output | description |
| --- | --- | --- |
| `=pronouns.personTerm=` | human | returns in all lowercase |
| `=pronouns.PersonTerm=` | Human | returns with first letter capitalized |

**Pronouns** — "Written as `=pronouns.(pronounterm)=`". Documented terms, with the wiki's own
examples:

| name | example |
| --- | --- |
| subjective | "SHE went to the store." |
| objective | "You are waterbonded with HIM." |
| possessive OR possessiveAdjective | "HER desert rifle rusted." |
| substantivePossessive | "Kindrish is HERS." |
| reflexive | "He can only blame HIMSELF for his character's death." |
| indicativeProximal | THIS artifact is too complex for you to decipher. |
| indicativeDistal | THAT evidence was found next to Keh's bedroll. |
| personTerm | What one of this gender is referred to. |
| immaturePersonTerm | What an immature/young person of this gender is called. |
| formalAddressTerm | What you politely call someone of this gender. |
| offspringTerm | What the offspring of this gender are called. |
| siblingTerm | What a sibling of this gender is referred to. |
| parentTerm | What a parent of this gender is called. |

**Verbs** — "Written as `=verb:(word)[:afterpronoun]=` where `:afterpronoun` is optional. If
afterpronoun is set, it is based off of the object's pronoun set."

**What `Modding:Grammar` does NOT document:** there is no `Grammar` XML file format, no root
element, no article handling, no gender-declaration syntax, no `=subject.verb=` token, and no
`=name=` token. The page's only other content is a link to the non-modding `Gender and Pronouns`
page.

Other grammar-ish tokens documented elsewhere on the fetched pages (`Modding:Conversations`):

- `=subject.name=` — named in the `PrepareTextEvent` description: "This precedes the standard
  variable replacements like `=subject.name=` and allows setting a new Subject and Object."
- `=spice.commonPhrases.sacred.!random=` — replaced by the `SpiceContext` part, "e.g. replacing
  `=spice.commonPhrases.sacred.!random=` with `sanctified`."
- `=village.sacred=` — replaced by the `VillageContext` part, "e.g. replacing `=village.sacred=`
  with `the act of procreation`."
- `=mutation.name=` — used in the `WaterRitualRandomMutation` part's child text:
  `<part Name="WaterRitualRandomMutation" Category="Physical">You gain =mutation.name=.</part>`

So on the fetched pages, `=subject.…=` is attested for `name`; `=pronouns.…=` and `=verb:…=` are the
documented pronoun/verb surfaces. Any other `=subject.verb=`-style token is **not** documented.

### 4.5 Conversations (`Modding:Conversations`)

"Conversations are trees of XML loaded from `Conversations.xml` and usually executed from a
`ConversationScript` part on a game object. The most common elements are the Node and the Choice: a
node is a piece of text spoken by the creature you're interacting with, coupled with a list of
choices for the player to respond with."

Barebones verbatim example:

```xml
<!-- ObjectBlueprints.xml-->
<objects>
  <object Name="Snapjaw Pal" Inherits="Snapjaw">
    <part Name="ConversationScript" ConversationID="FriendlySnapjaw" />
  </object>
</objects>

<!-- Conversations.xml-->
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

"(Note that the outer `conversations` tag is required.)"

XML tag reference (verbatim descriptions):

| XML Tag | Description |
| --- | --- |
| `<conversation>` | "Single conversation template typically containing `<node>` and `<start>` elements, linked to a `ConversationScript` via its `ID`." |
| `<node>` | "Collection of `<text>` from the Speaker's point of view, along with a range of `<choice>` for the Player to respond with. Setting `AllowEscape="false"` prevents the player from exiting the dialogue window early." |
| `<start>` | "Special variant of `<node>` that can be selected when starting a conversation. For backwards compatibility, a `<node>` with an ID of "Start" will behave similarly." |
| `<choice>` | "Collection of `<text>` from the Player's point of view, commonly defines a `Target` `<node>` to navigate to if selected. The `Target` attribute has two special values: `Start` and `End`, which will return to the beginning of the conversation or end it, respectively. For backwards compatibility, the `GotoID` attribute will behave similarly to `Target`." |
| `<text>` | "Contains a block of text to display for an element, multiple of these can be defined and randomly selected from if valid. Additional text nodes can be recursively defined within other text nodes, allowing groups of text to use the same conditions. For backwards compatibility, delimiting the text with `~` characters will behave similarly to multiple text nodes." |
| `<part>` | "Reference to a C# class that inherits from `IConversationPart`. Any attributes defined here will be inserted into the fields & properties of the part, if possible. Anything defined as a child element of the part can be loaded with custom C# behavior." |

Merging/inheritance/distribution are covered in §1.5. Distribution:

```xml
<conversation ID="FriendlySnapjaw">
  <start ID="SnappyHello">
    <text>heeeelo!</text>
  </start>
  <start ID="SnappyNoise">
    <text>gnnnnnnn.</text>
  </start>
  <choice Target="End">Live and drink.</choice> <!-- Added to both start nodes -->
  <choice GiveItem="Dagger" Distribute="SnappyNoise" Qualifier="ID">It is time to grill cheese.</choice>
</conversation>
```

"`Distribute` attribute normally takes a list of element types, but if `Qualifier="ID"` is
specified, a list of IDs can be provided. Choices that are defined as children under a conversation
will propagate to all start nodes by default."

**Delegates** — "Unique to conversations are their delegate attributes such as
`IfHaveQuest="What's Eating the Watervine?"` or `GiveItem="Joppa Recoiler"`. These are distinguished
between two types: Predicates which control whether an element is accessible, and Actions which
perform some task when the element is selected. After the Deep Jungle update these are now for the
most part agnostic as to what their parent element is."

```xml
<conversation ID="FriendlySnapjaw">
  <start ID="FurFriend" IfHavePart="ThickFur"> <!-- Hidden if player doesn't have thick fur -->
    <text>ooohh. pretty...</text>
    <text IfReputationAtLeast="Loved">deheh. like you. hohohoho.</text> <!-- Hidden if not Loved by speaker's faction -->
    <choice Target="End" IfReputationAtLeast="Loved" GiveItem="Dagger">I like you too.</choice> <!-- Gives the player a dagger if selected-->
    <choice Target="End">Thank you.</choice>
  </start>
</conversation>
```

Delegate mechanics: "An Inverse predicate can be invoked with `IfNot` to negate its value. A Speaker
delegate can be invoked with `IfSpeaker`/`SetSpeaker`… Predicate and Action delegates accept logic
expressions, using parenthesis alongside `AND`, `OR` and `NOT`. For instance,
`IfHaveActiveQuest="(Quest1 AND Quest2)"` will only succeed if the player has both aforementioned
quests active at the same time."

Documented predicates include: `IfHaveQuest`, `IfHaveActiveQuest`, `IfFinishedQuest`,
`IfFinishedQuestStep` (`"Quest ID~Step ID"`), `IfHaveObservation`, `IfHaveObservationWithTag`,
`IfHaveSultanNoteWithTag`, `IfHaveVillageNote`, `IfHaveState`, `IfTestState`
(`"ID Operator Value"`, e.g. `"SlynthSettlementFaction = Joppa"`), `IfHaveConversationState`,
`IfHaveText`, `IfLastChoice`, `IfCommand`, `IfReputationAtLeast` (Loved/Liked/Indifferent/Disliked/
Hated), `IfTime`, `IfLedBy`, `IfZoneHaveObject`, `IfZoneID`, `IfZoneName`, `IfZoneLevel`,
`IfZoneTier`, `IfZoneWorld`, `IfUnderstood`, `IfIn100`, `IfGenotype`, `IfSubtype`, `IfTrueKin`,
`IfMutant`, `IfHaveItem`, `IfHaveItemDescendsFrom`, `IfWearingBlueprint`, `IfHaveBlueprint`,
`IfHavePart`, `IfHaveTag`, `IfHaveProperty`, `IfHaveTagOrProperty`, `IfHaveLiquid`,
`IfLevelLessOrEqual`.

Documented actions include: `AwardXP`, `FinishQuest`, `FireEvent`, `FireSystemsEvent`,
`SetStringState`, `SetIntState`, `AddIntState`, `SetBooleanState`, `ToggleBooleanState`,
`SetStringProperty`, `SetIntProperty`, `SetStringConversationState`, `SetIntConversationState`,
`SetBooleanConversationState`, `RevealObservation`, `RevealMapNote`, `GiveLiquid`, `UseLiquid`,
`SetLeader`, `Notify`, plus the part generators `StartQuest`, `CompleteQuestStep`, `GiveItem`,
`TakeItem`.

**Important gap:** the task asked about conversation tags `<goto>`, `<setvar>`, `<if>` and
`<wish>`. **None of these are documented on the fetched wiki page.** The documented equivalents are
the `Target`/`GotoID` attributes, the `Set*State`/`Set*ConversationState`/`Set*Property` action
delegates, and the `If*` predicate delegates.

Documented `<part>`s usable inside conversations: `AddSlynthCandidate`, `ChangeTarget`,
`GiveArtifact`, `GiveReshephSecret`, `IPredicatePart`, `LibrarianGiveBook`, `PaxInfectLimb`,
`QuestHandler`, `ReceiveItem`, `RequireReputation`, `SpiceContext`, `Tag`, `TakeItem`, `TextFilter`,
`TextInsert`, `Trade`, `VillageContext`, `WaterRitualRandomMutation`. Examples:

```xml
<part Name="QuestHandler" Action="Step" QuestID="Fetch Argyve a Knickknack" StepID="Return to Argyve" XP="75" />
<part Name="ReceiveItem" Pick="true" Mods="1" Blueprints="Long Sword4,Cudgel4,Dagger4,Battle Axe4" Identify="All" />
<part Name="RequireReputation" Faction="Snapjaws" Level="Loved" />
<part Name="TextFilter" FilterID="Lallated" Extras="*growl*,*whine*" />
<part Name="TextInsert" Spoken="false" NewLines="2">[Press Tab or T to open trade]</part>
<part Name="Tag">{{g|[begin trade]}}</part>
```

Namespace resolution: "If you use a period within the part's name, it's assumed you are specifying
your own namespace and won't be required to place your part within
`XRL.World.Conversations.Parts`. You can optionally declare a `Namespace` on the root
`<conversations>` element, and concatenated sub-namespaces on each `<conversation>`."

Conversation events propagate **up** the element tree (Choice → Node → Conversation), unlike object
events which cascade down, and are split by perspective (Speaker / Listener): "By default parts will
register for the perspective they are placed in, but can be overriden with the `Register` attribute"
(e.g. `<part Name="SpiceContext" Register="All" />`). Events: `IsElementVisibleEvent`,
`GetTextElementEvent`, `PrepareTextEvent`, `DisplayTextEvent`, `ColorTextEvent`,
`GetChoiceTagEvent`, `EnteredElementEvent`, `EnterElementEvent`, `GetTargetElementEvent`,
`LeaveElementEvent`, `LeftElementEvent`, `HideElementEvent`, `PredicateEvent`.

Custom delegates require C#: "It's possible to add your own delegates for you to use in XML by
adding a `[ConversationDelegate]` attribute to a static method in C#… Depending on the return type
it will either be registered as a predicate (bool) or action (void), and variants of the delegate
will automatically be created." Verbatim example:

```csharp
[HasConversationDelegate] // This is required on the surrounding class to reduce the search complexity.
public static class DelegateContainer
{
    [ConversationDelegate(Speaker = true)]
    public static bool IfHaveItem(DelegateContext Context)
    {
        return Context.Target.HasObjectInInventory(Context.Value);
    }
}
```

### 4.6 Activated abilities (`Modding:Activated Abilities`)

**This page documents no XML surface at all — abilities are C#-only on the fetched material.**
"Any part can add activated abilities, including mutations, skills, or even equipment… All parts add
abilities through the same interface." Registration happens in code:

```csharp
public override bool AddSkill(GameObject GO)
{
  ActivatedAbilities part = GO.GetPart("ActivatedAbilities") as ActivatedAbilities;
  if (part != null)
  {
    this.ActivatedAbilityID = part.AddAbility("Slam [&Wattack&y]", "CommandCudgelSlam", "Skill", -1, false, false, "You make an attack with a cudgel at an adjacent opponent at +1 penetration. ...", "-", false, false);
    this.Ability = part.AbilityByGuid[this.ActivatedAbilityID];
  }
  return true;
}
```

"The important fields to note in the `AddAbility` function call are the first 3. This tells the game
object to add a new ability called "Slam[attack]", and to fire the "CommandCudgelSlam" event when it
is used. This ability will show up under the "Skill" category in the ability menu. This function
returns a Guid…" Hook points: `AddSkill`/`RemoveSkill` for skills, `Mutate`/`UnMutate` for
mutations, `OnEquipped`/`OnUnequipped` for equipment. Listener registration:

```csharp
public override void Register(GameObject Object)
{
  Object.RegisterPartEvent((IPart) this, "CommandCudgelSlam");
  Object.RegisterPartEvent((IPart) this, "AIGetOffensiveMutationList");
  base.Register(Object);
}
```

Timing: "The `UseEnergy` call sets the amount of time the ability takes to activate. 1000 energy is
equivalent to 1 turn… 2000 energy would be 2 turns, and 500 would be half a turn. Free actions do not
need to call this function." Cooldowns: "10 cooldown is equivalent to 1 turn (at 16 willpower)."

The only XML evidence for abilities anywhere in the fetched set is the `Modding:XML` snippet from
the base game's `ActivatedAbilites.xml`:

```xml
<p>There is a <stat Name="SwoopCrashChance" />% chance you crash to the ground (doubled if your attack is blocked by a shield).</p>
```

So: **the wiki documents abilities as a C# feature; it does not document an ability XML file
schema.**

---

## 5. Practical gotchas

### 5.1 The `Load="Merge"` pitfalls

1. **`<object>` replaces by default.** Omitting `Load="Merge"` on an object that already exists
   "will replace the named object entirely" — silently destroying every part, stat, tag and skill on
   the vanilla blueprint. This is stated on both `Modding:XML` and `Modding:Objects`.
2. **Nested population groups need their own `Load="Merge"`.** Merging the `<population>` alone is
   not enough; the wiki's own example annotates this.
3. **`<tag Value="*delete">` is broken in combination with merge/inheritance**: "this appears to
   currently be broken because it does not work in combination with Load="Merge" or on inherited
   tags." Use `<removepart>` / `<removemutation>` for parts and mutations; there is no documented
   working tag-removal path.
4. **Merge vs inheritance precedence differs.** For `Load="Merge"`, "the properties of the latter
   element overwrite those of the former" (conversations). For `Inherits`, "the properties of the
   current element have precedence over those it is inheriting from."
5. **Implicit conversation IDs can collide unexpectedly.** `<text>` → `Text`, `Text2`, `Text3`; a
   later `<text Cardinal="3">` merges into `Text3`. Set explicit IDs when you mean to add, not
   merge.

### 5.2 Generated objects ignore later blueprint edits

"objects and zones in particular, once generated, are no longer beholden to their blueprints." The
documented workflow: wish `reload` for XML; wish `rebuild` for zones ("everything visible in the
main game view except the player and their companions"); spawn a fresh copy of the object after
editing `objects`, `bodies`, or `populations`; `conversations` and `books` generally need neither.
A `rebuild` may also be needed after editing `worlds` or `populations`.

### 5.3 Encoding

- The XML declaration `<?xml version="1.0" encoding="utf-8"?>` is recommended verbatim and "the only
  accepted XML version is 1.0". **But:** "Historically, Qud has interpreted text using code page 437
  even if the XML declaration stated UTF-8. Because of this, some characters such as `áéíóú` will
  render incorrectly by default. To signal that the data is actually UTF-8, it's necessary to
  include an `Encoding="utf-8"` attribute on the root element (`<objects Encoding="utf-8">`). This
  functionality is currently only available on the `lang-experimental` branch." Practical
  consequence: on stable, non-ASCII text in XML should be expressed as CP437 numeric entities
  (`&#x…;` / `&#…;`), not as UTF-8 literals.
- **Escaping:** `&` must be written `&amp;` in attributes — `ColorString="&amp;B"` has the value
  `&B`. Other documented entities: `&lt;` `<`, `&gt;` `>`, `&quot;` `"`.
- **Structural:** "There can be only one root element; anything after the root element will
  silently fail to parse." Missing closing tags are a classic error; "Firefox and Google Chrome will
  both load and parse XML and indicate syntax errors. Between the two, Firefox is slightly more
  permissive (meaning it will miss certain types of mistake, such as missing closing tags) but gives
  slightly more specific error messages." XiMpLe is recommended for structured editing.

### 5.4 File placement and discovery

- Any `.xml` file anywhere in the mod folder is loaded as XML unless `manifest.json` says otherwise;
  the root element decides the data type. A wrong-named file is therefore not a failure mode, but a
  wrong root element silently makes the data unusable.
- Colour definitions go in `Display.txt` (not `.xml`); tile size override goes in `display.txt`
  (lowercase) — the wiki uses two different-cased file names for two different purposes. True-colour
  mode goes in `modconfig.json`. Presets go in `EmbarkModules.xml`.
- Only `.png` is supported for mod tiles, per the explicit rule; painted wall/fence atlases still
  default to `.bmp` and to `Textures/Tiles/`.
- Painted walls need all 256 suffix variants and fences all 16 — a partial set renders incorrectly.

### 5.5 Case sensitivity, required attributes, crash-on-missing

**The fetched wiki pages do not state any rules about case sensitivity of element names, attribute
names, or values, and do not identify any attribute whose absence crashes the game.** Case is
consistent in every example (`Load`, `Name`, `ID`, `Target`, `Blueprint`, `Chance`, `Number`,
`Weight`, `Hint`, `Style`, `Anatomy`, `ConversationID`, `Register`, `Distribute`, `Qualifier`,
`VariantOf`, `Laterality`, `Type`), so the safe assumption is case-sensitivity, but this is an
inference from examples, not documented policy. Likewise, no page lists "required" attributes; the
only structurally required things the wiki calls out are the outer `<conversations>` tag, a single
root element, and the `.xml` extension for `EmbarkModules.xml`.

### 5.6 What has NO XML interface (C# required) — as explicitly documented

- **Custom parts.** Any new part is a C# class inheriting `IPart` (or `IZonePart`,
  `IConversationPart`, …). The wiki's stance is that you usually don't need to write one: "there are
  a lot of useful parts already available in the base game that you can steal from other objects."
- **Custom conversation delegates.** `[ConversationDelegate]` on a static C# method; the XML uses
  the resulting delegate name.
- **Conversation parts.** `<part>` inside conversations references "a C# class that inherits from
  `IConversationPart`"; "Anything defined as a child element of the part can be loaded with custom
  C# behavior."
- **Activated abilities.** Entirely C# on the fetched page (`AddAbility`, `RegisterPartEvent`,
  `FireEvent`, `UseEnergy`, cooldowns).
- **Dynamic rendering.** `Render(RenderEvent E)` overrides are C#; the documented XML-only
  substitute is the `AnimatedMaterialGeneric` part, which covers animation but not conditional tiles.
- **AI.** No XML interface is documented for AI behaviour; AI logic is written in the ability/part's
  `FireEvent` for `AIGetOffensiveMutationList`.
- **Grammar files.** No XML root or file format for grammar is documented, so it is not possible
  from the fetched material to say whether grammar rules are writable in XML at all;
  `Modding:Grammar` covers only the `=pronouns.…=`, `=verb:…=` token syntax used inside text.
- **Zone templates / regions.** `ZoneTemplates.xml` is named as the modern population mechanism, but
  its schema is not documented on `Modding:Populations`.

### 5.7 Documentation-quality warnings carried by the pages themselves

- `Modding:Populations`: two cleanup banners, "**Everything about encounter tables is out of
  date.**" and an unintegrated Steam-thread note.
- `Modding:Bodies`, `Modding:Parts`, `Modding:Grammar`,
  `Modding:Giving_Creatures_Inventory_Items`: flagged as **stubs**.
- `Modding:Code_page_437`: "Missing Info — More testing needed."
- `Modding:Objects`: the part list is explicitly "(incomplete)", and several rows in the child-tag
  and part tables have **no description at all** (`<builder>`, `<stat>`, `<property>`,
  `<intproperty>`, `<xtag>`, and many `Bodies.xml` attributes).
- `Modding:Tiles`: the tile-download link is "somewhat outdated"; the recommended alternatives are a
  Unity Asset Extractor or the Brinedump mod.

---

### Primary sources

- https://wiki.cavesofqud.com/wiki/Modding:XML
- https://wiki.cavesofqud.com/wiki/Modding:Objects
- https://wiki.cavesofqud.com/wiki/Modding:Tiles
- https://wiki.cavesofqud.com/wiki/Modding:Bodies
- https://wiki.cavesofqud.com/wiki/Modding:Populations
- https://wiki.cavesofqud.com/wiki/Modding:Colors_%26_Object_Rendering
- https://wiki.cavesofqud.com/wiki/Modding:Code_page_437
- https://wiki.cavesofqud.com/wiki/Modding:Tutorial_-_Custom_Player_Tiles
- https://wiki.cavesofqud.com/wiki/Modding:Parts
- https://wiki.cavesofqud.com/wiki/Modding:Grammar
- https://wiki.cavesofqud.com/wiki/Modding:Giving_Creatures_Inventory_Items
- https://wiki.cavesofqud.com/wiki/Modding:Conversations
- https://wiki.cavesofqud.com/wiki/Modding:Activated_Abilities
