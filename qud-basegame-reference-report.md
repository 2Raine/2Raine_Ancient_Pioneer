# Caves of Qud Base-Game Data Mining Report

Game data root (read-only, never modified):
`D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base\`

Additional read-only source consulted (for code-driven behaviour that has no XML):
`D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll`
(inspected with the shipped `Trivial.Mono.Cecil.dll`; no files were written or changed).

File sizes (bytes):
| File | Bytes |
|---|---|
| ObjectBlueprints\Creatures.xml | 1,076,xxx (15,547 lines) |
| ObjectBlueprints\Items.xml | ~1.0 MB (12,692 lines) |
| ObjectBlueprints\Data.xml | — |
| Mutations.xml | 120 lines |
| Bodies.xml | — |
| Subtypes.xml | 332 lines |
| Genotypes.xml | 42 lines |
| EmbarkModules.xml | 471 lines |
| PopulationTables.xml | — |
| WorldTerrain.xml | — |
| Manual.xml | 558 lines |
| HiddenMutations.xml | 60 lines |
| Mods.xml | — |

---

## 1. Base creature object (`BaseObject` template) — Creatures.xml

`Creatures.xml:28` is the root template. It inherits `PhysicalObject`. It is tagged `<tag Name="BaseObject" Value="*noinherit" />` (line 71), which marks it as a template that is never spawned directly.

### 1a. FULL stat list with Min/Max/Value

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:28
  <object Name="Creature" Inherits="PhysicalObject">
    <part Name="Render" DisplayName="[Creature]" RenderString="@" RenderLayer="10" RenderIfDark="false" DetailColor="w" Tile="Assets_Content_Textures_Creatures_sw_farmer.bmp" />
    <tag Name="OverlayColor" Value="&amp;G^k" />
    <part Name="Physics" Organic="true" Takeable="false" Solid="false" Weight="201" Category="Creatures" />
    <part Name="Brain" Hostile="false" Wanders="true" Factions="Beasts-100" />
    <part Name="Body" Anatomy="Humanoid" />
    <part Name="Combat" />
    <part Name="Experience" />
    <part Name="Inventory" />
    <part Name="Mutations" />
    <part Name="Skills" />
    <part Name="ActivatedAbilities" />
    <part Name="RandomLoot" />
    <part Name="Springy" Factor="0.5" />
    <stat Name="Strength" ShortName="ST" Min="1" Max="9000" Value="16" />
    <stat Name="Agility" ShortName="AG" Min="1" Max="9000" Value="16" />
    <stat Name="Toughness" ShortName="TO" Min="1" Max="9000" Value="16" />
    <stat Name="Intelligence" ShortName="IN" Min="1" Max="9000" Value="16" />
    <stat Name="Willpower" ShortName="WI" Min="1" Max="9000" Value="16" />
    <stat Name="Ego" ShortName="EG" Min="1" Max="9000" Value="16" />
    <stat Name="Hitpoints" ShortName="HP" Min="0" Max="64000" Value="16" />
    <stat Name="Energy" ShortName="EN" Min="-100000" Max="100000" Value="0" />
    <stat Name="Speed" ShortName="SP" Min="1" Max="10000" Value="100" />
    <stat Name="MoveSpeed" ShortName="MS" Min="-1800" Max="195" Value="100" />
    <stat Name="AV" ShortName="AV" Min="0" Max="100" Value="0" />
    <stat Name="DV" ShortName="DV" Min="-100" Max="100" Value="0" />
    <stat Name="XP" ShortName="XP" Min="0" Max="2147483647" Value="0" />
    <stat Name="XPValue" ShortName="XPValue" sValue="*XP" Min="0" Max="2147483647" />
    <stat Name="SP" ShortName="SP" Min="0" Max="2147483647" Value="0" />
    <stat Name="MP" ShortName="MP" Min="0" Max="2147483647" Value="0" />
    <stat Name="AP" ShortName="AP" Min="0" Max="2147483647" Value="0" />
    <stat Name="MA" ShortName="MA" Min="-100" Max="2147483647" Value="0" />
    <stat Name="HeatResistance" ShortName="HeatResistance" Min="-100" Max="100" Value="0" />
    <stat Name="ColdResistance" ShortName="ColdResistance" Min="-100" Max="100" Value="0" />
    <stat Name="ElectricResistance" ShortName="ElectricResistance" Min="-100" Max="100" Value="0" />
    <stat Name="AcidResistance" ShortName="AcidResistance" Min="-100" Max="100" Value="0" />
    <part Name="Leveler" />
    <part Name="Commerce" Value="100" />
    <part Name="Stomach" />
    <part Name="Corpse" CorpseChance="0" BurntCorpseChance="100" BurntCorpseBlueprint="Ashes" />
    <removepart Name="BurnToAshesIfOrganic" />
    <stat Name="Level" ShortName="LV" Min="1" Max="10000" Value="1" />
    <tag Name="Role" Value="Unspecified" />
    <tag Name="BaseObject" Value="*noinherit" />
    <builder Name="Roboticized" ChanceOneIn="20000" />
    <tag Name="NeverStack" />
    <tag Name="Creature" />    
    <tag Name="PetResponse" Value="=subject.T= =verb:stare= at =object.t= blankly." />
    <tag Name="SimpleConversation" Value="Moon and Sun. Wisdom and will.~May the earth yield for us this season.~Peace, =player.formalAddressTerm=." />
    <xtagTextFragments Skin="skin" PoeticFeatures="shiny teeth,coarse hair,sunken eyes" YounglingNoise="*heh*" Activity="roaming around idly" VillageActivity="sleeping in our homes" NeedsItemFor="for my own collection,to slake my greed,because of its trade value" SacredThing="the act of procreating" ArableLand="arable land" ValuedOre="precious metals" />
    <stag Name="HardMaterial" Value="bone" />
  </object>
```

### 1b. `NaturalHealingRate` / healing stats — DOES NOT EXIST as a stat

**Explicit negative finding:** there is **no** `<stat Name="NaturalHealingRate" ...>` anywhere in `Base\*.xml` or `Base\ObjectBlueprints\*.xml`.

- A grep for `NaturalHealing` across all of `Base\` returns exactly **one** hit, and it is a *part*, not a stat:
  ```xml
  D:\...\Base\ObjectBlueprints\Creatures.xml:9219
  		<part Name="DisabledNaturalHealing" />
  ```
- A grep for `NaturalHealingRate` returns **no matches at all**.
- Natural healing is derived from the **Toughness** and **Willpower** stats. Verbatim from the genotype chargen help strings:
  ```xml
  D:\...\Base\Genotypes.xml:7
      <stat Name="Toughness" Minimum="10" Maximum="24" ChargenDescription="Your {{W|Toughness}} score determines your number of hit points, your natural healing rate, and your ability to resist poison and disease."/>
  D:\...\Base\Genotypes.xml:9
      <stat Name="Willpower" Minimum="10" Maximum="24" ChargenDescription="Your {{W|Willpower}} score modifies the cooldowns of your activated abilities, determines your ability to resist mental attacks, and modifies your natural healing rate."/>
  ```
  And in the in-game Help:
  ```xml
  D:\...\Base\Manual.xml:71
  regeneration rate, and your ability to resist poison and disease.
  D:\...\Base\Manual.xml:80
  point regeneration rate.
  ```

> **For the mod:** there is no healing stat to set. To change natural healing you must change Toughness/Willpower, or use the `Regeneration` mutation, or the `DisabledNaturalHealing` part.

### 1c. Typical playable-ish humanoid (Creatures.xml:766–800)

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:766
  <object Name="Humanoid" Inherits="Creature">
    <tag Name="BodyDisplayName" Value="Humanoid" />
    <part Name="Body" Anatomy="Humanoid" />
    <part Name="Brain" Factions="Humanoids-100" />
    <intproperty Name="Bleeds" Value="1" />
    <tag Name="Class" Value="human" />
    <tag Name="Species" Value="human" />
    <tag Name="RandomGender" Value="male,female" />
		<tag Name="PunchSound" Value="Sounds/Creatures/VO/sfx_humanoid_generic_vo_attack"/>
		<tag Name="DeathSounds" Value="Sounds/Creatures/VO/sfx_humanoid_generic_vo_die" />
		<tag Name="TakeDamageSound" Value="Sounds/Creatures/VO/sfx_humanoid_generic_vo_hurt"/>
		<tag Name="AmbientIdleSound" Value="Sounds/Creatures/VO/sfx_humanoid_generic_vo_idle"/>
    <tag Name="BaseObject" Value="*noinherit" />
    <tag Name="PrimaryLimbType" Value="Hand" />
    <tag Name="LairAmbientBed" Value="Sounds/Ambiences/amb_creature_humanoid_generic" />
  </object>
D:\...\Base\ObjectBlueprints\Creatures.xml:782
  <object Name="BaseHumanoid" Inherits="Humanoid">
    <part Name="Render" RenderString="h" />
    <part Name="Corpse" CorpseChance="90" CorpseBlueprint="Human Corpse" />
    <stat Name="Strength" sValue="14,1d3,(t)d1" />
    <stat Name="Agility" sValue="14,1d3,(t)d1" />
    <stat Name="Toughness" sValue="14,1d3,(t)d1" />
    <stat Name="Intelligence" sValue="14,1d3,(t)d1" />
    <stat Name="Willpower" sValue="14,1d3,(t)d1" />
    <stat Name="Ego" sValue="14,1d3,(t)d1" />
    <intproperty Name="Bleeds" Value="1" />
    <intproperty Name="Humanoid" Value="1" />
    <tag Name="BaseObject" Value="*noinherit" />
    <part Name="ConversationScript" ConversationID="Humanoids" ClearLost="true" />
    <tag Name="Humanoid" />
    <xtagTextFragments Skin="skin" PoeticFeatures="tall forehead,outstretched legs,eyes full of wonder" Activity="going about the tedious work of speaking to peers,hunting and gathering,wandering the salt dunes" VillageActivity="sleeping in our bedrolls and sitting in our chairs,lounging around,reading books,peeling and eating musa" NeedsItemFor="for my own collection,to slake my greed,because of its trade value,to inspire my art,to inspire my poetry,to be my muse,to help with farm work,for the games we play under the Beetle Moon" SacredThing="the act of procreating,philosophy,alcohol,grains of the earth,stone fruit,artistry,poetry,warm meals,bread and wine,pottery,painting" ArableLand="arable land,livestock,the bounty of the earth" ValuedOre="precious metals,artifacts,chrome,jasper,cybernetics,the nectar of the Eaters" />
    <tag Name="SimpleConversation" Value="Moon and Sun. Wisdom and will.~May the earth yield for us this season.~Peace, =player.formalAddressTerm=." />
    <tag Name="PrimaryLimbType" Value="Hand" />    		
    <tag Name="LairAmbientBed" Value="Sounds/Ambiences/amb_creature_humanoid_generic" />        
  </object>
```

`BaseTrueKin` (`Creatures.xml:1023`) is the only extra layer a vanilla playable humanoid adds:
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:1023
  <object Name="BaseTrueKin" Inherits="BaseHumanoid">
    <skill Name="Persuasion_RebukeRobot" />
    <tag Name="NonMutant" />
    <tag Name="Genotype" Value="True Kin" />
    <tag Name="HeroGenotype" Value="True Kin" />
    <tag Name="BaseObject" Value="*noinherit" />
  </object>  
```

**`sValue` mini-language:** `"14,1d3,(t)d1"` = baseline 14, plus `1d3`, plus `(tier)d1`. Other files use `(t-1)d2` for tier-minus-one. Verified examples: `Creatures.xml:98-103` (`BaseAnimal`) and `Creatures.xml:785-790` (`BaseHumanoid`).

---

## 2. The Player object blueprint — DOES NOT EXIST in any base XML file

**Explicit negative finding.** A grep for `Name="Player"` across the entire `Base\` tree returns **exactly one** match, and it is a faction, not an object:

```xml
D:\...\Base\Factions.xml:3
	<faction Name="Player" Visible="false" InitialPlayerReputation="1000"/>
```

A grep for the bare word `Player` in `ObjectBlueprints\*.xml` returns only 12 matches, none of which is a player object definition:
- `Creatures.xml:14168, 14955, 15354, 15375` — `Feelings="Player:0"` on `Brain` parts
- `Widgets.xml:274, 276` — `PlayerMuralController`
- `Widgets.xml:358` — `PlayerDeathAchievement`
- `HiddenObjects.xml:1971` — `Coda Player Statue`
- `HiddenObjects.xml:2223, 2234, 2241` — `Feelings="Player:0"`
- `Items.xml:7429` — `GlobalModChanceFactorOnPlayerEquip`

A grep for `Player` in the other candidate files (`ObjectBlueprints.xml`, `RootObjects.xml`) returns nothing. `ObjectBlueprints.xml` is an empty stub:

```xml
D:\...\Base\ObjectBlueprints.xml:1
<?xml version="1.0" encoding="utf-8"?>
<objects>
  
</objects>
```

**How the player object is actually created:** the player is instantiated *in code* from a genotype's `BodyObject`, and the code-side module responsible is named in `EmbarkModules.xml`:

```xml
D:\...\Base\EmbarkModules.xml:4
  <module Class="XRL.CharacterBuilds.Qud.QudSpecificCharacterInitModule"> <!-- responsible for initializing the default humanoid object during character builds (returns a "humanoid" object) -->
```

The genotype supplies the object to clone:

```xml
D:\...\Base\Genotypes.xml:4
  <genotype Name="Mutated Human" MutationPoints="12" StatPoints="44" AllowedMutationCategories="*" RandomWeight="90" DisplayName="Mutated Human" Subtypes="Callings" Class="" Tile="UI/sw_mutant.bmp" DetailColor="g" BodyObject="Humanoid" BaseHPGain="1-4" BaseSPGain="50" BaseMPGain="1" Species="human" IsMutant="true" CharacterBuilderModules="XRL.CharacterBuilds.Qud.QudCasteModule,XRL.CharacterBuilds.Qud.QudCyberneticsModule">
```

```xml
D:\...\Base\Genotypes.xml:20
  <genotype Name="True Kin" MutationPoints="0" StatPoints="38" AllowedMutationCategories="" CyberneticsLicensePoints="2" RandomWeight="10" DisplayName="True Kin" Subtypes="Castes" Class="" Tile="UI/sw_truekin.bmp" DetailColor="C" BodyObject="Humanoid" BaseHPGain="1-4" BaseSPGain="70" BaseMPGain="0" Species="human" IsTrueKin="true" CharacterBuilderModules="XRL.CharacterBuilds.Qud.QudCallingModule,XRL.CharacterBuilds.Qud.QudMutationsModule">
```

> **For the mod:** `BodyObject="Humanoid"` is the hook. To give a modded race a distinct body/blueprint, add a genotype in your mod's `Genotypes.xml` with `BodyObject="<YourCreatureBlueprint>"`.

---

## 3. Damage-attribute words used by melee / natural weapons

### 3a. Critical structural finding

In the **entire** base game, `Attributes=` on a `MeleeWeapon` part is used only **twice**, and both times the value is the single word `Bite`:

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:1457
    <part Name="MeleeWeapon" MaxStrengthBonus="1000" BaseDamage="1d2" Skill="Axe" Stat="Strength" Attributes="Bite" />
D:\...\Base\ObjectBlueprints\Creatures.xml:1531
    <part Name="MeleeWeapon" BaseDamage="1d3" Skill="ShortBlades" Stat="Strength" Slot="Face" Attributes="Bite" />
```

**`Slashing`, `Bludgeoning` do not exist anywhere** in `Base\*.xml` (grep: no matches). **`Piercing` exists exactly once**, and on a Projectile, not a melee weapon:

```xml
D:\...\Base\ObjectBlueprints\Items.xml:2494
		<part Name="Projectile" BasePenetration="8" BaseDamage="1d2" Attributes="Piercing" ColorString="&amp;y" PassByVerb="hurtle" />
```

Melee "damage type" in Qud is expressed by the **`Skill=`** attribute (`Axe`, `ShortBlades`, `Cudgel`, `LongBlades`, `Whip`, `Chain`), not by `Attributes`. `Attributes` carries *elemental / special* damage riders.

### 3b. Complete distinct word list (extracted programmatically from every `Attributes="..."` and `...Attributes="..."` in `ObjectBlueprints\*.xml`)

Damage-relevant words — these are the legal damage-attribute tokens:

| Token | Occurrences | Carried by (part) | Sample location |
|---|---|---|---|
| `Acid` | 3 | ElementalDamage, GasDamaging(DamageAttributes) | Creatures.xml:5234, 10017; PhysicalPhenomena.xml:440 |
| `Axe` | 1 | ThrownWeapon | Items.xml:2968 |
| `Bite` | 2 | **MeleeWeapon** | Creatures.xml:1457, 1531 |
| `Cold` | 4 | ElementalDamage, Projectile | Creatures.xml:6425; Items.xml:1788, 2687, 4091 |
| `Cosmic` | 1 | ElementalDamage | Creatures.xml:8709 |
| `Crushing` | 1 | DamageContents(DamageAttributes) | Furniture.xml:1606 |
| `Defoliant` | 2 | Projectile, GasDamaging | Items.xml:2258; PhysicalPhenomena.xml:549 |
| `Disintegrate` | 5 | Projectile | Creatures.xml:8784; Items.xml:1757, 1891, 2571, 2715 |
| `Dream` | 2 | Projectile, ElementalDamage | Creatures.xml:8800; HiddenObjects.xml:1720 |
| `Electric` | 6 | ElementalDamage, Projectile | Creatures.xml:3458, 3564, 8257; Items.xml:613, 2354, 2601 |
| `Exsanguination` | 1 | Projectile | Items.xml:2715 |
| `Extradimensional` | 1 | Projectile | HiddenObjects.xml:750 |
| `Fire` | 3 | Projectile | Items.xml:2233, 2680, 4062 |
| `Fungicide` | 2 | Projectile, GasDamaging | Items.xml:2284; PhysicalPhenomena.xml:563 |
| `Gas` | 4 | GasDamaging(DamageAttributes) | PhysicalPhenomena.xml:384, 440, 549, 563 |
| `Heat` | 5 | ElementalDamage | Creatures.xml:5219, 6040, 8282, 10023; Items.xml:2233 |
| `Laser` | 11 | Projectile | Creatures.xml:7961, 8865, 9410; Items.xml:1711, 1840, 2537, 2650, 2741, 6759; HiddenObjects.xml:750, 1720 |
| `Light` | 12 | Projectile | Creatures.xml:7060, 7961, 8162, 8865; Items.xml:4095 |
| `Mental` | 1 | Projectile | Items.xml:2803 |
| `Mutagenic` | 1 | Projectile | Creatures.xml:8902 |
| `NonDamaging` | 1 | ThrownWeapon | Items.xml:2994 |
| `NonPenetrating` | 4 | Projectile | Items.xml:1788, 1814, 2680, 2687 |
| `Normality` | 3 | Projectile, GasDamaging | Creatures.xml:8865; Items.xml:2329; PhysicalPhenomena.xml:521 |
| `Piercing` | 1 | Projectile | Items.xml:2494 |
| `Plasma` | 2 | Projectile, GasDamaging | Items.xml:1866; PhysicalPhenomena.xml:384 |
| `Psionic` | 1 | Projectile | Items.xml:2803 |
| `ShortBlades` | 13 | ThrownWeapon | Items.xml:670, 690, 709, 745, 768, 788, 802, 817, 833, 850, 871, 9649, 10674 |
| `Umbral` | 4 | ElementalDamage | Creatures.xml:5778, 9043; HiddenObjects.xml:42, 1720 |
| `Vorpal` | 3 | Projectile, ElementalDamage | Creatures.xml:9321; Items.xml:2377; HiddenObjects.xml:1720 |
| `AffectGas` | 1 | GasDamaging | PhysicalPhenomena.xml:563 |

Non-damage false positives from the same attribute namespace (ignore these): `goatfolk` (Creatures.xml:4606, `MapNoteAttributes`), `humanoid,settlement` (Creatures.xml:1867), `oboroqoru` (Creatures.xml:3898), `Mechanimist,SacredJoining` (Walls.xml:1426).

Verbatim examples of the carrier parts:

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:8255
  <object Name="QuatravoltGlider_Bite" Inherits="Bite">
    <part Name="MeleeWeapon" BaseDamage="0" />
    <part Name="ElementalDamage" Damage="4d4" Attributes="Electric" />
  </object>
```
```xml
D:\...\Base\ObjectBlueprints\Items.xml:2680
    <part Name="Projectile" BaseDamage="1d12+2" Attributes="Fire NonPenetrating" RenderChar="f" ColorString="&amp;R" PassByVerb="streak" />
```
```xml
D:\...\Base\ObjectBlueprints\Items.xml:2715
    <part Name="Projectile" BasePenetration="6" BaseDamage="1d8" Attributes="Exsanguination Disintegrate" ColorString="&amp;r" PassByVerb="screech" />
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:7060
    <part Name="Projectile" BasePenetration="9" BaseDamage="2d6" Attributes="Light" ColorString="&amp;W" PenetrateCreatures="true" PassByVerb="crackle" />
```
```xml
D:\...\Base\ObjectBlueprints\HiddenObjects.xml:1720
    <part Name="ElementalDamage" Damage="2d6" Attributes="Umbral Dream Vorpal Laser" />
```

Multi-word `Attributes` values are whitespace-separated token lists (e.g. `"Light Laser"`, `"Fire NonPenetrating"`, `"Exsanguination Disintegrate"`, `"Umbral Dream Vorpal Laser"`).

`DamageAttributes=` (the same token language, used by non-weapon parts) appears only 6 times:

```xml
D:\...\Base\ObjectBlueprints\PhysicalPhenomena.xml:384
    <part Name="GasDamaging" GasType="Plasma" Noun="plasma" MessageColor="&amp;W" CreatureDamageDivisor="100" DamageAttributes="Plasma Gas" TargetPart="Physics" ExcludeTag="Item" />
D:\...\Base\ObjectBlueprints\PhysicalPhenomena.xml:440
    <part Name="GasDamaging" GasType="Acid" Noun="acid" MessageColor="&amp;G" DamageAttributes="Acid Gas" TargetPart="Physics" ExcludeTag="Item"/>
D:\...\Base\ObjectBlueprints\PhysicalPhenomena.xml:521
    <part Name="GasDamaging" GasType="NormalityGas" Noun="normality gas" MessageColor="&amp;y" DamageAttributes="Normality" TargetPart="Extradimensional" TargetTag="Entropic" />
D:\...\Base\ObjectBlueprints\PhysicalPhenomena.xml:549
    <part Name="GasDamaging" GasType="Defoliant" Noun="defoliant" MessageColor="&amp;y" DamageAttributes="Defoliant Gas" TargetTag="LivePlant" TargetPart="PhotosyntheticSkin" AffectEquipment="true" AffectCybernetics="true" />
D:\...\Base\ObjectBlueprints\PhysicalPhenomena.xml:563
    <part Name="GasDamaging" GasType="Fungicide" Noun="fungicide" MessageColor="&amp;y" DamageAttributes="Fungicide Gas AffectGas" TargetTag="LiveFungus" TargetBodyPartCategory="Fungal" TargetEquippedTag="FungalInfection" TargetPart="GasFungalSpores" />
D:\...\Base\ObjectBlueprints\Furniture.xml:1606
    <part Name="DamageContents" Damage="3d4" DamageAttributes="Crushing" ChargeUse="200" IsEMPSensitive="false" WorksOnInventory="true" WorksOnEnclosed="true" />
```

---

## 4. Resistance stat semantics — STRONGEST EVIDENCE (in-game Help text)

The definitive statement is in the shipped in-game Help file `Manual.xml`. Verbatim:

```xml
D:\...\Base\Manual.xml:121
[[{{C|Acid Resist}}]]
Your acid resist is a measurement of how much acid damage you ablate. Your
base acid resist score is 0. At 100, you are immune to acid damage.

[[{{C|Cold Resist}}]]
Your cold resist is a measurement of how much cold damage you ablate and how
insulated you are from effects that reduce your temperature. Your base cold
resist score is 0. At 100, you are immune to cold damage and your
temperature cannot be reduced.

[[{{C|Electrical Resist}}]]
Your electrical resist is a measurement of how much electrical damage you
ablate and how resistive you are to electric current. Your base electrical
resist score is 0. At 100, you are immune to electrical damage and you do
not conduct electricity.

[[{{C|Heat Resist}}]]
Your heat resist is a measurement of how much heat damage you ablate and how
insulated you are from effects that increase your temperature. Your base
heat resist score is 0. At 100, you are immune to heat damage and your
temperature cannot be increased.
```

**Conclusions that are directly documented:**
- Base value is **0**.
- **100 = full immunity** to that damage type, *and* immunity to the associated temperature change (Cold/Heat) or to conduction (Electric). This is stated for all four resistance stats.
- Resistance scales linearly as "damage ablated": value = percent of incoming damage absorbed.

**What is NOT documented — mark UNCERTAIN:**
- The claim that `-100` = double damage is **NOT** stated anywhere I could find. I grepped `Manual.xml` for `resist|Resist|Resistance` (16 hits, all shown above or in the stat descriptions) and found no mention of negative resistance, extra damage, or vulnerability multipliers. Treat "-100 = double damage" as **UNCERTAIN / unverified**. What *is* verifiable is that negative values are legal and common (the base template allows `Min="-100"`), and that they are used on creatures that plausibly take extra damage from that element.

**Corroborating blueprint evidence:**

Immunity by 100 + explicit no-damage part — a glass phial is immune to all four elements:
```xml
D:\...\Base\ObjectBlueprints\Items.xml:12025
  <object Name="Phial" Inherits="WaterContainer">
    <part Name="LiquidVolume" MaxVolume="1" Volume="1" StartVolume="0" InitialLiquid="" ManualSeal="true" LiquidVisibleWhenSealed="true" />
    <part Name="Render" DisplayName="phial" ColorString="&amp;y" TileColor="&amp;y" DetailColor="k" Tile="Items/sw_vial.bmp" />
    <part Name="Physics" SpecificHeat="0" FreezeTemperature="-9999" BrittleTemperature="-9999" FlameTemperature="99999" VaporTemperature="9999" />
    <part Name="Description" Short="A short channel of glass for keeping precious liquids glints when turned." />
    <stat Name="Hitpoints" Value="1000" />
    <stat Name="HeatResistance" Value="100" />
    <stat Name="ColdResistance" Value="100" />
    <stat Name="AcidResistance" Value="100" />
    <stat Name="ElectricResistance" Value="100" />
    <part Name="NoDamage" />
```

A creature with resistance **200** also has the heat-focused mutation and a fire aura — consistent with 100+ being a stronger-than-immune buffer rather than a hard cap (note the *stat* Max is 100 but blueprints exceed it, so the Max is not enforced as a hard clamp):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:4820
  <object Name="Aloe Pyra" Inherits="BaseAloe">
    <part Name="Render" DisplayName="aloe pyra" Tile="Creatures/sw_aloe_solo.bmp" ColorString="&amp;y" DetailColor="R" Occluding="false" />
    <part Name="Description" Short="Mottled leaf-chutes are splayed out in a rosette. They sputter and smolder." />
    <stat Name="Level" Value="18" />
    <tag Name="Role" Value="Controller" />
    <stat Name="HeatResistance" Value="200" />
    <part Name="CrossFlameOnStep" Level="4" Length="12" Cooldown="10" />
  </object>
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5096
  <object Name="Fire Ant Queen" Inherits="BaseInsect">
    ...
    <mutation Name="HeatAbsorption" Level="5" />
    ...
    <stat Name="HeatResistance" Value="200" />
    <mutation Name="Wings" Level="6" CapOverride="6" />
    <tag Name="Species" Value="ant" />
    <tag Name="Gender" Value="female" />
    <tag Name="DynamicObjectsTable:Ruins_Creatures" />
  </object>
```
Negative resistance on a robot (the race trait that robots are weak to electricity):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:690
    <stat Name="ElectricResistance" Value="-50" />
    <stat Name="HeatResistance" Value="25" />
    <stat Name="ColdResistance" Value="25" />
```
Other verified negative values: `Creatures.xml:314` (`ElectricResistance -25`), `:450` and `:478` (`ColdResistance -25`), `:1060` (`HeatResistance -25` on `BasePlant`), `:1288-1289` (`AcidResistance -10`, `HeatResistance -10`), `:4414` (`ElectricResistance -10`), `:6314` (`ElectricResistance -20`), `Furniture.xml:2628,2639` (`ElectricResistance -50`).

**Weapon mods affecting resistance — negative finding.** `Mods.xml` declares Flaming/Freezing only as mod-table entries, with **no resistance numbers**:
```xml
D:\...\Base\Mods.xml:28
  <mod Part="ModFreezing" Tables="WeaponMods" Rarity="U" TinkerTier="2" CanAutoTinker="false" NoSparkingQuest="true" TinkerDisplayName="freezing" Value="1.2" TinkerCategory="melee weapons" />
D:\...\Base\Mods.xml:29
  <mod Part="ModFlaming" Tables="WeaponMods" Rarity="U" TinkerTier="2" CanAutoTinker="false" NoSparkingQuest="true" TinkerDisplayName="flaming" Value="1.2" TinkerCategory="melee weapons" />
```
The parts themselves are bare markers too — they do not touch HeatResistance/ColdResistance:
```xml
D:\...\Base\ObjectBlueprints\Items.xml:1471
    <part Name="ModFlaming" Tier="3" />
D:\...\Base\ObjectBlueprints\Items.xml:1480
    <part Name="ModFreezing" Tier="3" />
```
Their actual damage behaviour is code-side. Stat-boost style resistance changes instead use explicit parts:
```xml
D:\...\Base\ObjectBlueprints\Items.xml:6855
    <part Name="EquipStatBoost" Boosts="AcidResistance:40;ColdResistance:40;HeatResistance:40;ElectricResistance:40" IsTechScannable="false" />
D:\...\Base\ObjectBlueprints\Items.xml:9330
    <part Name="CyberneticsStatModifier" Stats="ElectricResistance:50" />
D:\...\Base\Subtypes.xml:72
        <stat Name="ColdResistance" Bonus="15" />
D:\...\Base\Subtypes.xml:126
        <stat Name="HeatResistance" Bonus="15" />
```

---

## 5. Immunity parts — every distinct usage pattern

### 5a. `EffectResistance` — 8 usages, 4 distinct `Values=` strings

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5586
    <part Name="EffectResistance" Values="Poison,PoisonGasPoison" />
D:\...\Base\ObjectBlueprints\Creatures.xml:5597
    <part Name="EffectResistance" Values="Sleep" />
D:\...\Base\ObjectBlueprints\Creatures.xml:5608
    <part Name="EffectResistance" Values="Stun,StunGasStun" />
D:\...\Base\ObjectBlueprints\Creatures.xml:5619
    <part Name="EffectResistance" Values="Confusion" />
D:\...\Base\ObjectBlueprints\Creatures.xml:7677
    <part Name="EffectResistance" Values="Poison,PoisonGasPoison" />
D:\...\Base\ObjectBlueprints\Creatures.xml:7687
    <part Name="EffectResistance" Values="Sleep" />
D:\...\Base\ObjectBlueprints\Creatures.xml:7697
    <part Name="EffectResistance" Values="Stun,StunGasStun" />
D:\...\Base\ObjectBlueprints\Creatures.xml:7707
    <part Name="EffectResistance" Values="Confusion" />
```

Full context for the first four (the "breathbeard" family — each is immune to its own breath):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5582
  <object Name="PoisonBreather" Inherits="BaseBreather">
    <part Name="Render" DisplayName="gallbeard" ColorString="&amp;g" />
		<part Name="Description" Short="Rolling scales tessellate =pronouns.possessive= husky, slouching form, and motes of noxious condensate stipple =pronouns.possessive= swollen chin." />
    <part Name="Corpse" CorpseBlueprint="PoisonBreatherCorpse" />
    <part Name="EffectResistance" Values="Poison,PoisonGasPoison" />
    <mutation Name="PoisonBreather" Level="5" />
  </object>
D:\...\Base\ObjectBlueprints\Creatures.xml:5593
  <object Name="SleepBreather" Inherits="BaseBreather">
    <part Name="Render" DisplayName="dreambeard" ColorString="&amp;w" />
		<part Name="Description" Short="Rolling scales tessellate =pronouns.possessive= husky, slouching form, and motes of dream condensate stipple =pronouns.possessive= swollen chin." />
    <part Name="Corpse" CorpseBlueprint="SleepBreatherCorpse" />
    <part Name="EffectResistance" Values="Sleep" />
    <mutation Name="SleepBreather" Level="5" />
  </object>
D:\...\Base\ObjectBlueprints\Creatures.xml:5604
  <object Name="StunBreather" Inherits="BaseBreather">
    <part Name="Render" DisplayName="stillbeard" ColorString="&amp;c" />
		<part Name="Description" Short="Rolling scales tessellate =pronouns.possessive= husky, slouching form, and motes of still condensate stipple =pronouns.possessive= swollen chin." />
    <part Name="Corpse" CorpseBlueprint="StunBreatherCorpse" />
    <part Name="EffectResistance" Values="Stun,StunGasStun" />
    <mutation Name="StunBreather" Level="5" />
  </object>
D:\...\Base\ObjectBlueprints\Creatures.xml:5615
  <object Name="ConfusionBreather" Inherits="BaseBreather">
    <part Name="Render" DisplayName="mazebeard" ColorString="&amp;B" />
		<part Name="Description" Short="Rolling scales tessellate =pronouns.possessive= husky, slouching form, and motes of dazzle condensate stipple =pronouns.possessive= swollen chin." />
    <part Name="Corpse" CorpseBlueprint="ConfusionBreatherCorpse" />
    <part Name="EffectResistance" Values="Confusion" />
    <mutation Name="ConfusionBreather" Level="5" />
  </object>
```

**Note the `Values` token convention:** the *base effect name* and the *gas-delivered variant name* are listed together, comma-separated, with no space: `Poison` + `PoisonGasPoison` → `"Poison,PoisonGasPoison"`; `Stun` + `StunGasStun` → `"Stun,StunGasStun"`. `Sleep` and `Confusion` have no separate gas-variant token.

### 5b. `ImmuneTo*` parts — 6 usages, 3 distinct part names

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5021
    <part Name="ImmuneToSleepGas" />
D:\...\Base\ObjectBlueprints\Creatures.xml:5798
    <part Name="ImmuneToConfusionGas" />
D:\...\Base\ObjectBlueprints\Creatures.xml:6519
    <part Name="ImmuneToConfusionGas" />
D:\...\Base\ObjectBlueprints\Creatures.xml:6520
    <part Name="ImmuneToSleepGas" />
D:\...\Base\ObjectBlueprints\Creatures.xml:11434
		<part Name="ImmuneToConfusionGas" />
D:\...\Base\ObjectBlueprints\Creatures.xml:11450
		<part Name="ImmuneToConfusionGas" />
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:14689
		<tag Name="ImmuneToFungus" />
```
Distinct part/tag names: **`ImmuneToSleepGas`**, **`ImmuneToConfusionGas`**, and the *tag* **`ImmuneToFungus`**.

### 5c. `ReflectDamage` — 2 usages

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6364
    <part Name="ReflectDamage" ReflectPercentage="5" />
D:\...\Base\ObjectBlueprints\Creatures.xml:8207
    <part Name="ReflectDamage" ReflectPercentage="100" />
```

Verbatim context (Quartz Baboon, 5%):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6358
    <stat Name="Strength" Boost="2" />
    <stat Name="AV" Value="5" />
    <stat Name="HeatResistance" Value="10" />
    <stat Name="ColdResistance" Value="10" />
    <stat Name="ElectricResistance" Value="10" />
    <stat Name="AcidResistance" Value="10" />
    <part Name="ReflectDamage" ReflectPercentage="5" />
    <tag Name="DynamicObjectsTable:Baboons" Value="{{{remove}}}" />
    <inventoryobject Blueprint="Large Stone" Number="5" />
    <inventoryobject Blueprint="Vicious_Baboon_Bite" Number="1" />
  </object>
```
Verbatim context (Mirror Bug, 100% — a full reflector):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:8200
  <object Name="Mirror Bug" Inherits="BaseInsect">
    <part Name="Render" DisplayName="mirror bug" Tile="Creatures/sw_bug.bmp" ColorString="&amp;y" TileColor="&amp;K" DetailColor="y" RenderString="i" />
    <part Name="Description" Short="It glints as it scuttles beneath an alighted exoskeleton of angled glass." />
    <stat Name="Level" Value="31" />
    <tag Name="Role" Value="Minion" />
    <stat Name="Hitpoints" Value="15" />
    <stat Name="AV" Value="3" />
    <part Name="ReflectDamage" ReflectPercentage="100" />
    <part Name="RefractLight" Chance="100" />
    <part Name="Swarmer" />
    <inventoryobject Blueprint="Mirror_Bug_Bite" Number="1" />
    <tag Name="Species" Value="ant" />
  </object>
```

### 5d. `NoDamage` — 37 usages (blanket effect immunity)

Distinct files: `Creatures.xml:1417` (`NaturalWeapon` base), `HiddenObjects.xml:979, 1057, 1622, 1681`, `Furniture.xml:3919, 3928, 6149, 6158, 6234`, `RootObjects.xml:30`, `Walls.xml:1357`, `PhysicalPhenomena.xml:43, 340(removepart), 625, 638, 662, 747`, `Widgets.xml:15`, `ZoneTerrain.xml:1273, 1433`, `Items.xml:1963, 3806, 3825, 3851, 3876, 3914, 3955, 3973, 4004, 4041, 4071, 7139, 9562, 9587, 9814, 12035, 12048, 12220`.
```xml
D:\...\Base\ObjectBlueprints\RootObjects.xml:27
  <object Name="CosmeticObject" Inherits="Object">
    <part Name="Render" DisplayName="[Object]" RenderString="?" RenderLayer="0" RenderIfDark="true" />
    <part Name="Physics" IsReal="true" Weight="-1" />
    <part Name="NoDamage" />
    <tag Name="NoEffects" />
    <tag Name="Cosmetic" />
    <tag Name="Gender" Value="neuter" />
    <tag Name="BaseObject" Value="*noinherit" />
    <tag Name="Immutable" />
  </object>
```

### 5e. `DisabledNaturalHealing` — 1 usage

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:9219
		<part Name="DisabledNaturalHealing" />
		<part Name="RulesDescription" Text="This creature doesn't heal naturally and must be repaired." />
```

### 5f. Parts you asked about that DO NOT EXIST (verified negative findings)

Grep patterns run across all of `Base\*.xml`:
- **`PoisonImmunity`** — no matches. (Immunity to poison is done via `<part Name="EffectResistance" Values="Poison,PoisonGasPoison" />` or the `Endurance_PoisonTolerance` skill.)
- **`ImmuneTo`** as a bare/standalone part — no matches. Only the three suffixed forms in 5b exist.
- **`Weakness`** / `Name="Weakness"` — no matches. **Does not exist.**
- **`Vulnerability`** — no matches. **Does not exist.** (Elemental weakness is expressed as a *negative* resistance stat, e.g. `ElectricResistance Value="-50"`.)
- **`DamageReduction`** — no matches as a part. **Does not exist.** (Damage mitigation is `AV`/`DV` stats and `Armor` parts.)
- **`PhysicalResistance`** — no matches. **Does not exist.** There is no physical-resistance stat in this version.
- **`MetalShell`** — no matches. **Does not exist** (see §10).

The only `...DamageReduction...`-like strings in the tree are unrelated cooking-unit IDs:
```xml
D:\...\Base\ObjectBlueprints\Data.xml:411
    <tag Name="Units" Value="CookingDomainReflect_UnitReflectDamageHighTier" />
D:\...\Base\ObjectBlueprints\Data.xml:421
    <tag Name="Units" Value="CookingDomainReflect_UnitQuills,CookingDomainReflect_UnitReflectDamage" />
```

---

## 6. Electrical Generation — every creature, and what stores the charge

### 6a. The mutation is declared in Mutations.xml (1 entry, no numbers)

```xml
D:\...\Base\Mutations.xml:16
    <mutation Name="Electrical Generation" Cost="4" MaxSelected="1" Class="ElectricalGeneration" Exclusions="" BearerDescription="those who generate electricity" Tile="Mutations/electrical_generation.bmp" />
```

Note there is **no level curve in XML** — `Mutations.xml` entries carry only `Name`/`Cost`/`Class`/`Variant`/`Exclusions`/`Tile`/`BearerDescription`. All numbers are code-side.

### 6b. All 4 creatures in Creatures.xml with the mutation — verbatim object blocks

**#1 — Electrofuge (`Creatures.xml:1907`), Level 2.** Accompanied by `AnimatedMaterialElectric`; **no** capacitor part.
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:1907
  <object Name="Electrofuge" Inherits="BaseSpider">
    <part Name="Render" DisplayName="electrofuge" Tile="creatures/sw_spider.bmp" RenderString="y" ColorString="&amp;W" DetailColor="w" />
    <part Name="Corpse" CorpseChance="10" CorpseBlueprint="Electrofuge Corpse" />
    <stat Name="AV" Value="2" />
    <stat Name="Level" Value="5" />
    <stat Name="Hitpoints" Value="18" />    
		<mutation Name="ElectricalGeneration" Level="2" />
    <part Name="Brain" Hostile="false" Factions="Arachnids-100" />
    <part Name="Description" Short="Gossamer hairs stand stiff in static charge across =pronouns.possessive= octet of Spindle legs, and =pronouns.subjective= =verb:crackle:afterpronoun= as =pronouns.subjective= =verb:crawl:afterpronoun=." />
    <inventoryobject Blueprint="Electrofuge_Bite" Number="1" />
    <tag Name="Role" Value="Lurker" />
    <stat Name="ElectricResistance" Value="50" />
		<part Name="AnimatedMaterialElectric" />
    <tag Name="DynamicObjectsTable:Ruins_Creatures" />
  </object>
  <object Name="Electrofuge_Bite" Inherits="Bite">
    <part Name="MeleeWeapon" BaseDamage="2d3" Skill="Axe" />
  </object>
```

**#2 — Naphtaali Sap (`Creatures.xml:3785`), Level 3.** Carries energy cells in inventory; **no** capacitor part.
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:3785
  <object Name="Naphtaali Sap" Inherits="BaseNaphtaali">
    <part Name="Render" DisplayName="Naphtaali sap" ColorString="&amp;W" Tile="Creatures/naphtaali_sap.bmp" />
    <stat Name="Hitpoints" Value="10" />
    <stat Name="Level" Value="7" />
    <tag Name="Role" Value="Lurker" />
    <inventoryobject Blueprint="@DynamicObjectsTable:EnergyCells:Tier2" Number="1" />
    <mutation Name="ElectricalGeneration" Level="3" />
  </object>
```
Its parent chain (`BaseNaphtaali` at 3728 → `BaseWoodsprog`) contains no charge part either:
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:3728
  <object Name="BaseNaphtaali" Inherits="BaseWoodsprog">
    <part Name="Render" DisplayName="Naphtaali" Tile="Creatures/naphtaali_forager.bmp" ColorString="&amp;g" DetailColor="w" />
    <part Name="Description" Short="A body time-fit to living in the smalls of spiral roots is kept warm by a layer of symbiotic moss-fur. Bright, scanning eyes peer out the selfsame mask of =pronouns.possessive= minyan, making =pronouns.possessive= face a blazing portrait of togetherness- laced, burned, and painted to point." />
    <part Name="Brain" Hostile="false" Factions="Naphtaali-100" />
    <part Name="ConversationScript" ConversationID="Naphtaali" />
    <part Name="AISelfPreservation" Threshold="20" />
    <stat Name="AV" Value="2" />
    <stat Name="Hitpoints" Value="10" />
    <part Name="Swarmer" />
    <tag Name="Culture" Value="Naphtaali" />
    <tag Name="BaseObject" Value="*noinherit" />
    <tag Name="DynamicObjectsTable:Naphtaali" />
  </object>
```

**#3 — Asphodelyte (`Creatures.xml:4828`), Level 7.** **No** capacitor part.
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:4828
  <object Name="Asphodelyte" Inherits="SapientMutatedFlower">
    <part Name="Render"  Tile="Creatures/sw_flower.bmp" TileColor="&amp;G" ColorString="&amp;G" DetailColor="r" RenderString="237" DisplayName="asphodelyte" />
    <part Name="Brain" Wanders="false" Hostile="false" Factions="Consortium-100,Flowers-100" />
    <stat Name="Level" Value="18" />
    <stat Name="Strength" sValue="23" />
    <stat Name="Agility" sValue="27" />
    <stat Name="Toughness" sValue="22" />
    <stat Name="Intelligence" sValue="16" />
    <stat Name="Willpower" sValue="16" />
    <stat Name="Ego" sValue="17" />
    <stat Name="Hitpoints" sValue="80" />
    <stat Name="AV" Value="6" />
    <part Name="Description" Short="A clan of contoured petals upfold to a florid bowl. Sharp current climbs =pronouns.possessive= filament and hops from anther to anther, while the dirt stutters under =pronouns.possessive= digging roots." />
    <part Name="FriendlyFireAmnestyDuringQuest" QuestName="Reclamation" />
    <tag Name="Role" Value="Minion" />
    <mutation Name="ElectricalGeneration" Level="7" />
    <mutation Name="Teleportation" Level="10" />
    <stat Name="ElectricResistance" Value="100" />
  </object>
```

**#4 — Quatravolt Glider (`Creatures.xml:8234`), Level 10 with `BaseChargePerTurnPercent="300"`.** **No** capacitor part. This is the only usage of the `BaseChargePerTurnPercent` XML attribute in the base game.
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:8234
  <object Name="Quatravolt Glider" Inherits="BaseEel">
    <part Name="Render" DisplayName="quatravolt glider" RenderString="224" Tile="Creatures\sw_eel_2.bmp" DetailColor="y" ColorString="&amp;W" />
    <part Name="Description" Short="=pronouns.Possessive= body is a clotted, finned cord that hovers and strums the air with magnetic force. Barbels crack out of =pronouns.possessive= mouth then sizzle and fry =pronouns.possessive= saliva." />
    <part Name="Brain" MaxKillRadius="6" />
    <stat Name="Level" Value="32" />
    <stat Name="AV" Value="8" />
    <stat Name="Ego" sValue="14" />
    <stat Name="HeatResistance" Value="20" />
    <stat Name="ColdResistance" Value="20" />
    <stat Name="ElectricResistance" Value="300" />
    <stat Name="AcidResistance" Value="20" />
    <stat Name="Hitpoints" Value="140" />
    <tag Name="Role" Value="Brute" />
    <removepart Name="Aquatic" />
    <removepart Name="SpawnWithLiquid" />
    <mutation Name="TemporalFugue" Level="7" />
    <mutation Name="ElectricalGeneration" Level="10" BaseChargePerTurnPercent="300" />
    <inventoryobject Blueprint="QuatravoltGlider_Bite" Number="1" />
    <tag Name="Polypwalking" />
    <tag Name="DynamicObjectsTable:PalladiumReef_Creatures" />
  </object>
  <object Name="QuatravoltGlider_Bite" Inherits="Bite">
    <part Name="MeleeWeapon" BaseDamage="0" />
    <part Name="ElementalDamage" Damage="4d4" Attributes="Electric" />
  </object>
```
(Line 8234's `<object>` opening tag is at `Creatures.xml:8234`; grepping `<object Name=` confirms the block runs to line 8254.)

### 6c. ANSWER: what part stores the charge? — **The mutation itself. There is NO capacitor part.**

Verified by IL inspection of `XRL.World.Parts.Mutation.ElectricalGeneration` in `Assembly-CSharp.dll`:

- The class declares its **own** charge field:
  `Int32 Charge`, `Int32 AdvancedCharge`, `Int32 BaseChargePerTurnPercent`, `Boolean ChargedThisTurn`
- It exposes the charge interface itself: `GetCharge()`, `AddCharge(Int32)`, `UseCharge(Int32)`, `GetMaxCharge()`, `GetChargePerTurn()`, plus `IChargeEvent` handling (`WantEvent` on `XRL.World.QueryChargeEvent::ID`, `TestChargeEvent::ID`, `UseChargeEvent::ID`).
- Its IL contains **no** reference to `XRL.World.Parts.Capacitor` or `XRL.World.Parts.Biocapacitor` anywhere. `Capacitor`/`Biocapacitor` are separate classes living in `Parts\Power Systems\`.
- The mutation's own level text says it *interacts with* capacitors rather than *being* one (string literals extracted from `GetLevelText`):
  - `"You accrue electrical charge that you can use and discharge to deal damage."`
  - `"Maximum charge: {{C|"` … `"}} charge per turn"`
  - `"Can discharge all held charge for 1d4 damage per "` … `" charge"`
  - `"Discharge can arc to adjacent targets dealing reduced damage, up to 1 target per "` … `" charge"`
  - `"EMP causes involuntary discharge (difficulty 18 Willpower save)"`
  - `"You can drink charge from energy cells and capacitors."`
  - `"You can provide charge to equipped devices that have integrated power systems."`

**MaxCharge — exact formula and constants** (from the compiled class's `const` fields and `GetMaxCharge` IL):

| Constant | Value |
|---|---|
| `BASE_MAX` | `2000` |
| `PER_LEVEL_MAX` | `2000` |
| `PER_TURN_PER_LEVEL_BASE` | `100` |
| `DISCHARGE_CHUNK` | `1000` |
| `DAMAGE_ABSORB_FACTOR` | `100` |
| `WILLPOWER_BASELINE` | `16` |
| `WILLPOWER_FACTOR` | `5` |
| `WILLPOWER_CEILING_FACTOR` | `5` |
| `WILLPOWER_FLOOR_DIVISOR` | `5` |
| `BaseChargePerTurnPercent` default | `100` |

`GetMaxCharge(Level)` IL is `2000 + (Level * 2000)`:
```
  ldc.i4       2000
  ldarg.0                          ; Level
  ldc.i4       2000
  mul
  add
  ret
```
So **MaxCharge = 2000 × (Level + 1)** — e.g. Level 2 → 6000, Level 3 → 8000, Level 7 → 16000, Level 10 → 22000.

`GetBaseChargePerTurn(Level, Percent)` IL is `(Level * 100 * Percent) / 100`.
`GetChargePerTurn(Level, Willpower, Percent)` full IL:
```
  ldarg.0                                  ; Level
  ldarg.2                                  ; Percent
  call  GetBaseChargePerTurn(Int32,Int32)
  stloc.0                                  ; base
  ldloc.0
  stloc.1                                  ; result = base
  ldarg.1                                  ; Willpower
  ldc.i4.s  16
  sub                                      ; W - 16
  ldc.i4.5
  mul                                      ; adj = (W - 16) * 5
  stloc.2
  ldloc.2
  brfalse.s  IL_001e                       ; if adj != 0 ...
  ldloc.1
  ldc.i4.s  100
  ldloc.2
  add
  mul
  ldc.i4.s  100
  div
  stloc.1                                  ; result = result * (100 + adj) / 100
  ldloc.1
  ldloc.0
  ldc.i4.5
  mul                                      ; base * 5
  call  Math.Min
  ldloc.0
  ldc.i4.5
  div                                      ; base / 5
  call  Math.Max                           ; clamp to [base/5, base*5]
  ret
```
I.e. **chargePerTurn = clamp(baseChargePerTurn × (100 + (Willpower−16)×5)/100, baseChargePerTurn/5, baseChargePerTurn×5)**. `BaseChargePerTurnPercent` (default 100, `300` on the Quatravolt Glider) scales the base.

### 6d. Creatures that DO have real charge-storage parts (for comparison / copy-paste)

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5252
  <object Name="Juice Sap" Inherits="BaseBat">
    <part Name="Render" DisplayName="juice sap" Tile="Creatures/sw_bat2.bmp" ColorString="&amp;W" TileColor="&amp;B" DetailColor="W" RenderString="b" />
    <part Name="Description" Short="Current jumps up the length of a needle proboscis, and the circut veins etched under =pronouns.possessive= wings gleam." />
    <part Name="Corpse" CorpseChance="20" CorpseBlueprint="Bat Corpse" />
    <part Name="Brain" Hostile="true" Factions="Winged Mammals-100" />
    <stat Name="Level" Value="16" />
    <stat Name="AV" Value="2" />
    <stat Name="Hitpoints" Value="20" />
    <stat Name="ElectricResistance" Value="20" />
    <tag Name="Role" Value="Minion" />
    <part Name="SapChargeOnHit" ChanceEach="60" Amount="800-1200" RequireDamageAttribute="Unarmed" />
    <part Name="Biocapacitor" ChargeRate="1000" MinimumChargeToExplode="100" ChargeDisplayStyle="" />
    <inventoryobject Blueprint="Juice_Sap_Bite" Number="1" />    
    <tag Name="DynamicObjectsTable:Ruins_Creatures" />
  </object>
```
→ **`Biocapacitor` with `ChargeRate="1000"`, no explicit `MaxCharge`.**

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5437
  <object Name="Traipsing Mortar" Inherits="BaseRobot">
    <part Name="Body" Anatomy="BipedalRobot" />
    <part Name="Render" DisplayName="{{c|traipsing mortar}}" RenderString="5" ColorString="&amp;R" Tile="Assets_Content_Textures_Creatures_sw_mortar.bmp" DetailColor="W" />
    <stat Name="Level" Value="15" />
    <stat Name="AV" Value="3" />
    <stat Name="Hitpoints" Value="20" />
    <part Name="Brain" Hostile="false" Hibernating="true" />
    <part Name="AIShootAndScoot" />
    <part Name="Description" Short="A mortar tube enameled with fire blister bounces on a gyroscopic mount between a pair of chrome-hooved kickers." />
    <inventoryobject Blueprint="TraipsingMortar_LaunchAssembly" Number="1" />
    <inventoryobject Blueprint="Mortar Tube" Number="1" />
    <inventoryobject Blueprint="HE Missile" Number="8-12" />
    <part Name="AmbientCollector" />
    <part Name="SolarArray" ChargeRate="20" />
    <part Name="BroadcastPowerReceiver" ChargeRate="10" />
    <part Name="Capacitor" MaxCharge="1000" ChargeRate="100" ChargeDisplayStyle="" />
    <part Name="FabricateFromSelf" ChargeUse="500" HitpointsPer="0" FabricateBlueprint="HE Missile" BatchSize="1" IsEMPSensitive="true" AIUseForAmmo="true" Cooldown="10-40" FabricateAlternateSource="debris and scraps" NameForStatus="MunitionsFab" />
    <skill Name="Cudgel" />
    <skill Name="HeavyWeapons" />
    <skill Name="HeavyWeapons_StrappingShoulders" />
    <skill Name="HeavyWeapons_Tank" />
    <tag Name="Species" Value="bipedal robot" />
    <tag Name="Role" Value="Artillery" />
    <tag Name="SeveredHeadBlueprint" Value="RobotHead3" />
    <tag Name="DynamicObjectsTable:Ruins_Creatures" />
    <tag Name="NoDropOnDeath" />
  </object>	
```
→ **`Capacitor` with `MaxCharge="1000" ChargeRate="100"`, fed by `SolarArray ChargeRate="20"` + `BroadcastPowerReceiver ChargeRate="10"`.**

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6253
  <object Name="Normality Bot" Inherits="BaseRobot">
    <part Name="Body" Anatomy="TreadedRobot" />
    <part Name="Render" DisplayName="anomaly extinguisher" RenderString="3" ColorString="&amp;y" Tile="Creatures/sw_scrapbot.bmp" DetailColor="K" />
    <part Name="Corpse" CorpseChance="100" CorpseBlueprint="NormalityGas60" />
    <stat Name="Level" Value="22" />
    <stat Name="AV" Value="6" />
    <stat Name="Hitpoints" Value="65" />
    <part Name="Brain" Hostile="false" Hibernating="true" PointBlankRange="true" />
    <part Name="FusionReactor" ChargeRate="1000" />    
    <part Name="Circuitry" StartCharge="1000" />
    <part Name="PointDefense" ChargeUse="1" MinRange="2" MaxRange="5" TargetExplosives="80" TargetThrownWeapons="90" TargetArrows="70" TargetSlugs="40" TargetEnergy="0" WorksOnEquipper="false" WorksOnSelf="true" UsesSelfEquipment="true" ShowComputeMessage="false" />
    <part Name="AIShootAndScoot" Duration="1d3+3" />
    ...
```
→ **`Circuitry StartCharge="1000"` + `FusionReactor ChargeRate="1000"`.**

Complete inventory of charge-storage / generation parts on creatures (`Creatures.xml`):
```xml
Creatures.xml:5263     <part Name="Biocapacitor" ChargeRate="1000" MinimumChargeToExplode="100" ChargeDisplayStyle="" />
Creatures.xml:5450     <part Name="SolarArray" ChargeRate="20" />
Creatures.xml:5452     <part Name="Capacitor" MaxCharge="1000" ChargeRate="100" ChargeDisplayStyle="" />
Creatures.xml:6262     <part Name="Circuitry" StartCharge="1000" />
Creatures.xml:8679     <part Name="Circuitry" StartCharge="10000" />
Creatures.xml:8825     <part Name="Circuitry" />
Creatures.xml:9371		<part Name="EnergyCell" SlotType="PowerCore" Charge="100000" MaxCharge="100000" />
Creatures.xml:9384		<part Name="EnergyCell" SlotType="PowerCore" Charge="500000" MaxCharge="500000" RechargeValue="250000" RechargeBit="M" />
Creatures.xml:9706     <tag Name="NoIntegratedHostCapacitor" />
Creatures.xml:13434    <part Name="Circuitry" />
```
Also referenced in `Creatures.xml:5451` (`BroadcastPowerReceiver ChargeRate="10"`), `Creatures.xml:6261` (`FusionReactor ChargeRate="1000"`).

**Sibling part classes confirmed present in the assembly:** `Capacitor`, `Biocapacitor`, `Circuitry`, `SolarArray`, `Flywheel`, `FusionReactor`, `BroadcastPowerReceiver`, `ChargeSink`, `EnergyCell`, plus charge-event parts `DischargeOnStep`, `DischargeOnHit`, `SapChargeOnHit`, `EquipCharge`, `IRechargeable`.

**XML attribute vocabulary for charge parts** (union of all observed usages in `Base\`): `Charge`, `MaxCharge`, `ChargeRate`, `StartCharge`, `ChargeUse`, `ChargeDisplayStyle`, `AltChargeDisplayStyle`, `AltChargeDisplayProperty`, `MinimumChargeToExplode`, `IsEMPSensitive`, `IsTechScannable`, `IsBioScannable`, `SlotType`, `RechargeValue`, `RechargeBit`, `NameForStatus`. `StartCharge` accepts dice/range syntax (e.g. `"0-10000"`, `"0-4x5000"`, `"0-20x1000"` in `Furniture.xml:2170, 2298`). `Capacitor` may be declared with `MaxCharge` omitted (`Furniture.xml:2205` `<part Name="Capacitor" StartCharge="20000" />`).

---

## 7. Regeneration mutation + natural healing

### 7a. The mutation definition (only XML occurrence)

```xml
D:\...\Base\Mutations.xml:30
    <mutation Name="Regeneration" Cost="4" MaxSelected="1" Class="Regeneration" Exclusions="" BearerDescription="those who regenerate" Tile="Mutations/regeneration.bmp" />
```

There is **no** `Regeneration` entry in `HiddenMutations.xml` (read in full, 60 lines — it contains 40 hidden physical mutations and 6 hidden mental ones; Regeneration is not among them). No level curve, no `<statline>` — the mutation is entirely code-driven (`Class="Regeneration"`).

### 7b. Every creature using it (16 usages) — all verbatim

```xml
Creatures.xml:2895     <mutation Name="Regeneration" />
Creatures.xml:3057     <mutation Name="Regeneration" />
Creatures.xml:3342     <mutation Name="Regeneration" />
Creatures.xml:4247     <mutation Name="Regeneration" />
Creatures.xml:5483     <mutation Name="Regeneration" />
Creatures.xml:6199     <mutation Name="Regeneration" Level="5" />
Creatures.xml:8431     <mutation Name="Regeneration" />
Creatures.xml:8694     <mutation Name="Regeneration" Level="10" />
Creatures.xml:9104     <mutation Name="Regeneration" Level="20" />
Creatures.xml:11337		<mutation Name="Regeneration" Level="10" />
Creatures.xml:13086    <mutation Name="Regeneration" Level="6" />
Creatures.xml:13988    <mutation Name="Regeneration" Level="3" />
Creatures.xml:14028    <mutation Name="Regeneration" Level="6" />
```
Note some usages omit `Level` entirely (`<mutation Name="Regeneration" />` at 2895, 3057, 3342, 4247, 5483, 8431) — Level defaults to 1.

Two verbatim contexts:
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:8692
    <stat Name="HeatResistance" Value="98" />
    <stat Name="ColdResistance" Value="98" />
    <mutation Name="Regeneration" Level="10" />
    <mutation Name="Teleportation" Level="1" />
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:9098
    <stat Name="Level" Value="45" />
    <stat Name="Hitpoints" Value="2000" />
    <stat Name="AV" Value="16" />
    <stat Name="DV" Value="-10" />
    <stat Name="Strength" Boost="4" />
    <part Name="Physics" Weight="40000" />
    <mutation Name="Regeneration" Level="20" />
    <stat Name="ElectricResistance" Value="50" />
    <stat Name="HeatResistance" Value="50" />
    <stat Name="ColdResistance" Value="50" />
    <stat Name="AcidResistance" Value="50" />
```

### 7c. Natural-healing-related XML (complete list)

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:9219
		<part Name="DisabledNaturalHealing" />
		<part Name="RulesDescription" Text="This creature doesn't heal naturally and must be repaired." />
```
```xml
D:\...\Base\ObjectBlueprints\Furniture.xml:2165
    <part Name="Enclosing" AVBonus="2" DVPenalty="8" ChargeUse="0" EnterSaveTarget="5" EnterSaveStat="Agility" EnterDamageChance="10" EnterDamageFailOnly="true" ExitSaveTarget="5" ExitSaveStat="Agility" ExitDamageChance="10" ExitDamageFailOnly="true" Damage="1d2+1" DamageBloodSplatterChance="25" IsEMPSensitive="true" NoDamageWhenDisabled="false" PeriodicEvent1="RegenTankRejuvenation" PeriodicEventTurns1="1" PeriodicEventUseGenericNotify1="true" PeriodicEventOnSelf1="true" PeriodicEvent2="RegenTankLimbRegeneration" PeriodicEventTurns2="10" PeriodicEventUseGenericNotify2="true" PeriodicEventOnSelf2="true" ShowGeneralInfoInShortDescription="false" NameForStatus="FluidCirculation" />
    <part Name="RegenTank" ChargeUse="100" MinTotalDrams="100" RejuvenationTriggerNotify="RegenTankRejuvenation" LimbRegenerationTriggerNotify="RegenTankLimbRegeneration" NameForStatus="RegenerationSystems" />
```
(The above is the "Regen Tank" furniture — a device that heals occupants, not a creature trait.)

Cooking-driven healing units (verbatim, for reference):
```xml
D:\...\Base\ObjectBlueprints\Data.xml:343
    <tag Name="Units" Value="CookingDomainRegenLowtier_BleedResistUnit,CookingDomainRegenLowtier_RegenerationUnit" />
D:\...\Base\ObjectBlueprints\Data.xml:345
    <tag Name="Actions" Value="CookingDomainLowtierRegen_HealToFull_ProceduralCookingTriggeredAction,CookingDomainLowtierRegen_StopBleeding_ProceduralCookingTriggeredAction,CookingDomainLowtierRegen_RemoveDebuff_ProceduralCookingTriggeredAction" />
D:\...\Base\ObjectBlueprints\Data.xml:353
    <tag Name="Units" Value="CookingDomainRegenHightier_RegenerationUnit" />
D:\...\Base\ObjectBlueprints\Data.xml:605
    <tag Name="Units" Value="CookingDomainPhotosyntheticSkin_RegenerationUnit" />
D:\...\Base\ObjectBlueprints\Data.xml:653
    <tag Name="Units" Value="CookingDomainRegenLowtier_BleedResistUnit,CookingDomainRegenLowtier_RegenerationUnit" />
```

The `Regeneration` mutation's numeric curve is **not** in XML — mark the exact HP/turn formula **UNCERTAIN**. What is certain: it is a 4-point physical mutation, `MaxSelected="1"`, implemented by `Class="Regeneration"`, taken by creatures at Levels 1–20, and it coexists freely with `DisabledNaturalHealing` being *absent* (the mech at 9219 is the only object in the game that disables natural healing).

---

## 8. Paralysis and the paralyzing stinger

### 8a. Mutation declarations — three Stinger variants

```xml
D:\...\Base\Mutations.xml:34
    <mutation Name="Stinger (Confusing Venom)" Cost="3" MaxSelected="1" Class="Stinger" Variant="Stinger Confusion" Exclusions="Stinger (Paralyzing Venom),Stinger (Poisoning Venom)" BearerDescription="those with stingers tipped with confusing venom" Tile="Mutations/stinger.bmp" />
D:\...\Base\Mutations.xml:35
    <mutation Name="Stinger (Paralyzing Venom)" Cost="4" MaxSelected="1" Class="Stinger" Variant="Stinger Paralysis" Exclusions="Stinger (Confusing Venom),Stinger (Poisoning Venom)" BearerDescription="those with stingers tipped with paralyzing venom" Tile="Mutations/stinger.bmp" />
D:\...\Base\Mutations.xml:36
    <mutation Name="Stinger (Poisoning Venom)" Cost="4" MaxSelected="1" Class="Stinger" Variant="Stinger Poison" Exclusions="Stinger (Confusing Venom),Stinger (Paralyzing Venom)" BearerDescription="those with stingers tipped with poisoning venom" Tile="Mutations/stinger.bmp" />
```

The `<mutation>` element takes a `Variant=` attribute to select the equipment blueprint that gets equipped.

### 8b. The Stinger natural-weapon blueprints — verbatim (Items.xml)

```xml
D:\...\Base\ObjectBlueprints\Items.xml:3870
  <object Name="Stinger" Inherits="MeleeWeapon">
    <part Name="Render" DisplayName="stinger" RenderString="*" Tile="items/sw_stinger.bmp" TileColor="&amp;w" DetailColor="G" />
    <part Name="Commerce" Value="0" />
    <part Name="Description" Short="A caudal barb-thing is tipped with a jewel of venomed dew." />
    <part Name="NaturalEquipment" />
    <part Name="NoBreak" />
    <part Name="NoDamage" />
    <part Name="Physics" Category="Natural Armor" IsReal="false" Weight="0" />
    <removepart Name="TinkerItem" />
    <intproperty Name="Natural" Value="1" />
    <tag Name="MutationEquipment" Value="Stinger" />
    <tag Name="ExcludeFromDynamicEncounters" />
    <tag Name="NoSparkingQuest" />
    <tag Name="NaturalGear" />
    <tag Name="VisibleAsDefaultBehavior" />
    <tag Name="ShowAsPhysicalFeature" />
    <part Name="MeleeWeapon" Skill="LongBlades" Slot="Tail" MaxStrengthBonus="0"/>
    <part Name="MaxPenetration" Max="1"/>
    <tag Name="WeaponIgnoreStrength" />
    <tag Name="HitSound" Value="Sounds/Abilities/sfx_ability_mutation_stinger_tailWhip"/>
    <tag Name="BaseObject" Value="*noinherit" />
  </object>
  <object Name="Stinger Poison" Inherits="Stinger">
    <part Name="Render" DetailColor="G" />
    <part Name="StingerPoisonProperties" />
    <tag Name="VariantName" Value="Stinger (Poisoning Venom)" />
  </object>
  <object Name="Stinger Paralysis" Inherits="Stinger">
    <part Name="Render" DetailColor="M" />
    <part Name="StingerParalysisProperties" />
    <tag Name="VariantName" Value="Stinger (Paralyzing Venom)" />
  </object>
  <object Name="Stinger Confusion" Inherits="Stinger">
    <part Name="Render" DetailColor="B" />
    <part Name="StingerConfusionProperties" />
    <tag Name="VariantName" Value="Stinger (Confusing Venom)" />
  </object>
```

**Key mechanics visible here:** the stinger is `Slot="Tail"`, `Skill="LongBlades"`, `MaxStrengthBonus="0"`, `WeaponIgnoreStrength`, and — critically — `<part Name="MaxPenetration" Max="1"/>`, i.e. it always penetrates at most 1 AV. The venom is a separate *properties* part (`StingerParalysisProperties` / `StingerPoisonProperties` / `StingerConfusionProperties`), **not** an `Attributes=` damage token.

### 8c. Creatures with a Stinger mutation — verbatim

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:2296
    <mutation Name="Stinger" Variant="Stinger Paralysis" Level="2" />
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:12878
    <mutation Name="Stinger" Level="1" Variant="Stinger Confusion" />
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:12908
    <mutation Name="Stinger" Level="1" Variant="Stinger Confusion" />
```
```xml
D:\...\Base\ObjectBlueprints\HiddenObjects.xml:2316
    <mutation Name="Stinger" Variant="Stinger Poison" Level="2" />
```

Full context for the paralyzing one (Scorpiock):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:2285
  <object Name="Scorpiock" Inherits="BaseScorpion">
    <part Name="Render" DisplayName="scorpiock" Tile="Creatures/sw_scorpion.bmp" RenderString="s" ColorString="&amp;w" DetailColor="g" />
    <part Name="Brain" Hostile="false" Factions="Arachnids-100" />
    <part Name="Description" Short="Motes of salt hang still on the notches of =pronouns.possessive= chitin and in the air above it. Behind, =pronouns.possessive= stinger coils in harmonic virtue." />
    <inventoryobject Blueprint="Scorpiock_Claws" Number="2" />
    <inventoryobject Blueprint="Scorpiock_Bite" Number="1" />
    <skill Name="Axe" />
    <skill Name="ShortBlades" />
    <skill Name="ShortBlades_Puncture" />
    <tag Name="Role" Value="Brute" />
    <mutation Name="Stinger" Variant="Stinger Paralysis" Level="2" />
    <tag Name="Species" Value="scorpion" />
  </object>
```

Full context for the confusion variant (Elder Irudad — a named NPC):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:12877
    <mutation Name="MultipleArms" Level="10" />
    <mutation Name="Stinger" Level="1" Variant="Stinger Confusion" />
    <mutation Name="Chimera" />
    <tag Name="Genotype" Value="Mutated Human Chimera" />
```

### 8d. The activated ability and its duration display

```xml
D:\...\Base\ActivatedAbilities.xml:1218
  <ability Command="CommandSting">
    <description>
      <p>You strike with your stinger, automatically hitting, penetrating, and applying your <stat Name="VenomType" /> venom.</p>      
      <br />
      <statline Name="StingerPen" DisplayName="Stinger penetration" />
      <statline Name="StingerDamage" DisplayName="Stinger damage" />
      <p><switch Name="VenomType">
        <case Value="paralyzing">Venom paralyzes opponents for <stat Name="Duration" />.</case>
        <case Value="confusing">Venom confuses opponents for <stat Name="Duration" />.</case>
        <case Value="poisonous">Venom poisons opponents for <stat Name="Duration" />.</case>
      </switch></p>
      <statline Name="Cooldown" />
    </description>  
    <UITile Tile="Mutations/stinger.bmp" Foreground="w" Detail="W" />
  </ability>
```
So the sting is an **activated ability** (`CommandSting`) that auto-hits and auto-penetrates, and the venom is applied as a *duration*-bearing effect whose length is the `Duration` stat.

### 8e. Duration curves — code-side (IL extracted from `Assembly-CSharp.dll`)

There are **no `Duration="..."` attributes** anywhere for paralysis in the XML. The durations are computed by the properties classes:

**`XRL.World.Parts.StingerParalysisProperties`** — `CreateEffect` builds the effect literally named `Paralyzed`:
```
  callvirt  XRL.World.Parts.IStingerProperties::GetDuration(System.Int32)
  call      XRL.Extensions::RollCached(System.String)
  ldc.i4.m1                                   ; -1
  newobj    System.Void XRL.World.Effects.Paralyzed::.ctor(System.Int32,System.Int32)
```
`GetDuration(Int32)` builds a dice string from the literals `3`, `7`, `3`, `1` with an `Int32::ToString` + `String::Concat` (i.e. a `"<Level>d<n>"` roll string) and `Math.Min` capping. The exact dice-string composition is **UNCERTAIN** — the constants are `3, 7, 3, 1` and the result is clamped by `Math.Min`, so the duration is capped at roughly 3 dice regardless of level.

**`XRL.World.Parts.StingerConfusionProperties`**:
```
CreateEffect:  GetDuration(Level) -> RollCached(...) ; ldc.i4.2 ; add ; newobj XRL.World.Effects.Confused::.ctor(Int32,Int32,Int32,String)
GetDuration:   ldc.i4.3 ; ldc.i4.s 14 ; ldc.i4.2 ; mul ; ldc.i4.3 ; div ; ldc.i4.2 ; add ; call Math.Min
```
i.e. confusion adds **+2** to the rolled duration, and `GetDuration` caps via `Math.Min(3, (14 * 2) / 3 + 2)` → cap 3.

**`XRL.World.Parts.StingerPoisonProperties`**:
```
CreateEffect: GetDuration(Level) -> RollCached ; GetIncrement(Level) ; newobj XRL.World.Effects.StingerPoisoned::.ctor(Int32, String, Int32, GameObject)
```
So poison uses a `StingerPoisoned` effect with a separate per-tick increment. `GetDuration`/`GetIncrement` bodies contain no numeric literals (values come from `ToString`/`Concat` of level-derived numbers) — exact curve **UNCERTAIN**.

**Effect names confirmed present in code:** `XRL.World.Effects.Paralyzed`, `XRL.World.Effects.Confused`, `XRL.World.Effects.StingerPoisoned`, `XRL.World.Effects.Asleep` (used as `Effect="Asleep"` in `Creatures.xml:1904`).

To make a creature start paralysed, the XML pattern is `SpawnWithEffect`:
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:1903
  <object Name="Sleeping Chromeling" Inherits="Chromeling">
    <part Name="SpawnWithEffect" Effect="Asleep" />
    <tag Name="ExcludeFromDynamicEncounters" />
  </object>
```

---

## 9. Bleeding — how it is implemented

### 9a. Negative finding
**`LiquidBleed` does not exist.** A grep for `Bleed` across all of `Base\*.xml` returns no `LiquidBleed` part. The keyword you want is **`BleedLiquid`** (a `<tag>`), plus the **`Bleeds`** `<intproperty>`.

### 9b. Parts that CAUSE bleeding — every distinct part, verbatim

**`BleedingOnHit`** (the standard on-hit bleed rider):
```xml
D:\...\Base\ObjectBlueprints\Items.xml:1379
    <part Name="BleedingOnHit" Amount="2-3" SaveTarget="35" Stack="true" />
D:\...\Base\ObjectBlueprints\Creatures.xml:5496
    <part Name="BleedingOnHit" Amount="1d1" SaveTarget="20" Stack="true" />
```
Full contexts:
```xml
D:\...\Base\ObjectBlueprints\Items.xml:1373
  <object Name="Sharpened Polyp" Inherits="BaseLongBlade">
    <part Name="Render" DisplayName="sharpened {{r|polyp}}" Tile="Items/sw_sharpened_polyp.bmp" ColorString="&amp;r" DetailColor="W" />
    <part Name="MeleeWeapon" MaxStrengthBonus="8" BaseDamage="2d6+1" Skill="LongBlades" />
    <part Name="Commerce" Value="50" />
    <part Name="Description" Short="Skeletal coral was torn off its reef-home and chewed to a cutting edge." />
    <part Name="Physics" Weight="4" FlameTemperature="99999" VaporTemperature="9999" />
    <part Name="BleedingOnHit" Amount="2-3" SaveTarget="35" Stack="true" />
    <part Name="CrumblesOnHit" Chance="50" />
    <tag Name="Tier" Value="7" />
		<stag Name="Contemporary" />
  </object>
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5493
  <object Name="Lamprey Bite" Inherits="Bite">
    <part Name="Render" DisplayName="{{B|psychic bite}}" />
    <part Name="MeleeWeapon" BaseDamage="1d8" Stat="Ego" Slot="Head" />
    <part Name="BleedingOnHit" Amount="1d1" SaveTarget="20" Stack="true" />
  </object>
```
Note `Amount` accepts both range (`"2-3"`) and dice (`"1d1"`) syntax; `SaveTarget` is the save difficulty; `Stack="true"` allows multiple bleed stacks.

**`CauseBleedingWhenDestroyed`**:
```xml
D:\...\Base\ObjectBlueprints\Items.xml:2788
    <part Name="CauseBleedingWhenDestroyed" Damage="2-4" SaveTarget="30" Stack="true" />
```

**`EngulfingBleeding`** (+ its companion `EngulfingHandOff`):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6391
    <part Name="EngulfingBleeding" Damage="1-2" />
D:\...\Base\ObjectBlueprints\Creatures.xml:8394
    <part Name="EngulfingBleeding" Damage="4-5" />
D:\...\Base\ObjectBlueprints\Creatures.xml:8395
    <part Name="EngulfingHandOff" SaveStat="Strength" SaveDifficultyStat="Strength" SaveTarget="30" BleedingDamageBonus="5-6" BleedingSavePenalty="5" />
```

**`Impaler`** with a bleed payload:
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:5327
    <part Name="Impaler" ClusterSize="1d6" Damage="2d10+5" BleedDamage="1d4" />
D:\...\Base\ObjectBlueprints\Creatures.xml:7314
    <part Name="Impaler" ClusterSize="1d6" Damage="2d10+5" BleedDamage="1d4" />
D:\...\Base\ObjectBlueprints\Creatures.xml:7333
    <part Name="Impaler" BleedSave="35" BleedDamage="4-7" Damage="25-35" DestroyOnStrike="true" NeedsToBeHidden="false" Message="{{R|You hear a crunch, and then =subject.t==subject.directionIfAny= =verb:explode=!}}" DamageMessage="from %t shrapnel." />
```

**`SaveModifier` against the `Bleeding` effect** (how to make a creature resist bleeding):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6707
    <part Name="SaveModifier" Vs="Bleeding" Amount="5" WorksOnSelf="true" WorksOnEquipper="false" />
D:\...\Base\ObjectBlueprints\Creatures.xml:6740
    <part Name="SaveModifier" Vs="Bleeding" Amount="15" WorksOnSelf="true" WorksOnEquipper="false" />
D:\...\Base\ObjectBlueprints\Creatures.xml:13136
    <part Name="SaveModifier" Vs="Bleeding" Amount="6" WorksOnSelf="true" WorksOnEquipper="false" />
D:\...\Base\ObjectBlueprints\Creatures.xml:14449
    <part Name="SaveModifier" Vs="Bleeding" Amount="6" WorksOnSelf="true" WorksOnEquipper="false" />
```
And the chargen equivalent (subtype save modifiers):
```xml
D:\...\Base\Subtypes.xml:18
          <savemodifier Vs="Bleeding" Amount="2" />
```
(identical at `Subtypes.xml:34, 49, 62`)

**Bleed-related activated ability statlines:**
```xml
D:\...\Base\ActivatedAbilities.xml:1342
      <statline Name="BleedDamage" DisplayName="Bleed damage" />
      <statline Name="BleedSave" DisplayName="Bleed save" />
```

**`LiquidProducer` with `Liquid="blood"`** (an exsanguination implant — note the `RequiresBodyPartCategory` gate):
```xml
D:\...\Base\ObjectBlueprints\Items.xml:2701
    <part Name="LiquidProducer" Liquid="blood" Rate="800" FillSelfOnly="true" IsTechScannable="true" IsEMPSensitive="true" WorksOnEquipper="true" WorksOnSelf="false" RequiresBodyPartCategory="Animal" NameForStatus="ExsanguinationMicrotubules" />
```

**`NoBleed`** (blanket bleed immunity, used on terrain):
```xml
D:\...\Base\ObjectBlueprints\ZoneTerrain.xml:26
    <part Name="NoBleed" />
```
(also `ZoneTerrain.xml:530, 611, 713, 757`)

### 9c. How a creature's blood liquid is specified

Two separate things:

1. **`<intproperty Name="Bleeds" Value="1" />`** — whether the creature bleeds at all. `Value="0"` disables it. Verified values:
   - `Value="1"`: `Creatures.xml:104, 176, 232, 264, 312, 345, 389, 448, 476, 521, 557, 611, 647, 706, 770, 791, 808, 1063, 1105, 8755, 11330`; `Items.xml:2793, 8569`; `Furniture.xml:3063, 3306, 3436`
   - `Value="0"`: `Foods.xml:567`; `Items.xml:8651, 8707, 8816, 8837, 8858, 8870`; `ZoneTerrain.xml:86, 103, 130, 257, 338, 368, 387`; `HiddenObjects.xml:2380`; `Creatures.xml:5194, 7319, 15329`

2. **`<tag Name="BleedLiquid" Value="<liquid>-<proportion>" />`** — *which* liquid. The value is a comma-separated weighted list; each entry is `liquidname-proportion` (proportions sum to 1000 in every observed case). **If `BleedLiquid` is absent, the creature bleeds the default liquid (blood)** — this is inferred from the fact that `Humanoid`/`BaseHumanoid` set only `Bleeds=1` with no `BleedLiquid`, while every non-blood creature explicitly overrides it. Mark the "defaults to blood" detail as **highly likely but not stated in XML** (the default lives in code).

Complete verbatim list of `BleedLiquid` declarations found:

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:390
    <tag Name="BleedLiquid" Value="slime-1000" />
    <tag Name="BleedColor" Value="&amp;g" />
    <tag Name="BleedPrefix" Value="{{g|slimy}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:612
    <tag Name="BleedLiquid" Value="ink-1000" />
D:\...\Base\ObjectBlueprints\Creatures.xml:707
    <tag Name="BleedLiquid" Value="oil-1000" />
    <tag Name="BleedColor" Value="&amp;K" />
    <tag Name="BleedPrefix" Value="{{K|oily}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:1064
    <tag Name="BleedLiquid" Value="sap-1000" />
    <tag Name="BleedColor" Value="&amp;W" />
    <tag Name="BleedPrefix" Value="{{W|sappy}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:1106
    <tag Name="BleedLiquid" Value="sap-1000" />
    <tag Name="BleedColor" Value="&amp;W" />
    <tag Name="BleedPrefix" Value="{{W|sappy}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:4880
    <tag Name="BleedLiquid" Value="convalessence-1000" />
    <tag Name="BleedColor" Value="&amp;C" />
    <tag Name="BleedPrefix" Value="{{C|luminous}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:7448
    <tag Name="BleedLiquid" Value="wine-400,acid-600" />
    <tag Name="BleedColor" Value="&amp;m" />
    <tag Name="BleedPrefix" Value="{{m|lush}} and {{G|acidic}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:8299
    <tag Name="BleedLiquid" Value="lava-1000" />
    <tag Name="BleedColor" Value="&amp;R" />
    <tag Name="BleedPrefix" Value="{{R|magmatic}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:8756
    <tag Name="BleedLiquid" Value="oil-1000" />
    <tag Name="BleedColor" Value="&amp;K" />
    <tag Name="BleedPrefix" Value="{{K|oily}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:8964
		<tag Name="BleedLiquid" Value="lava-1000" />
		<tag Name="BleedColor" Value="&amp;R" />
		<tag Name="BleedPrefix" Value="{{R|magmatic}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:9187
		<tag Name="BleedLiquid" Value="warmstatic-1000,water-500" />
		<tag Name="BleedColor" Value="&amp;Y" />
		<tag Name="BleedPrefix" Value="{{Y|entropic}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:10000
		<tag Name="BleedLiquid" Value="proteangunk-1000" />
		<tag Name="BleedColor" Value="&amp;c" />
		<tag Name="BleedPrefix" Value="{{c|soupy}}" />
D:\...\Base\ObjectBlueprints\Creatures.xml:11332
		<tag Name="BleedLiquid" Value="blood-900,gel-100" />
D:\...\Base\ObjectBlueprints\Creatures.xml:11518
		<tag Name="BleedLiquid" Value="proteangunk-1000" />
D:\...\Base\ObjectBlueprints\Items.xml:2794
    <tag Name="BleedLiquid" Value="blood-1000" />
D:\...\Base\ObjectBlueprints\Items.xml:8685
    <tag Name="BleedLiquid" Value="sap-1000" />
D:\...\Base\ObjectBlueprints\Furniture.xml:3064
    <tag Name="BleedLiquid" Value="gel-1000" />
D:\...\Base\ObjectBlueprints\Furniture.xml:3307
    <tag Name="BleedLiquid" Value="gel-1000" />
D:\...\Base\ObjectBlueprints\Furniture.xml:3437
    <tag Name="BleedLiquid" Value="gel-1000" />
```

**The three bleeding tags travel together:**
- `BleedLiquid` — the liquid + proportion list
- `BleedColor` — Qud colour code used for the splatter/message
- `BleedPrefix` — an adjective shown before the liquid name (e.g. blood becomes "oily oil")

Canonical verbatim example (a robot that bleeds oil):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:674
  <object Name="Robot" Inherits="Creature">
    ...
    <intproperty Name="Bleeds" Value="1" />
    <tag Name="BleedLiquid" Value="oil-1000" />
    <tag Name="BleedColor" Value="&amp;K" />
    <tag Name="BleedPrefix" Value="{{K|oily}}" />
    <tag Name="PetResponse" Value="=subject.T= =verb:beep= and =verb:boop=." />
    <stag Name="HardMaterial" Value="casing" />    
    <tag Name="PrimaryLimbType" Value="Hand" />
  </object>
```
And a plant that bleeds sap (`BasePlant`):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:1063
    <intproperty Name="Bleeds" Value="1" />
    <tag Name="BleedLiquid" Value="sap-1000" />
    <tag Name="BleedColor" Value="&amp;W" />
    <tag Name="BleedPrefix" Value="{{W|sappy}}" />
```
And the humanoid baseline (bleeds, liquid unspecified → default blood):
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:770
    <intproperty Name="Bleeds" Value="1" />
D:\...\Base\ObjectBlueprints\Creatures.xml:791
    <intproperty Name="Bleeds" Value="1" />
```

### 9d. Blood liquid on the Player blueprint
**Not applicable — the Player blueprint does not exist** (see §2). The player inherits from whichever creature `BodyObject=` names in `Genotypes.xml`, i.e. **`Humanoid`** (`Genotypes.xml:4` and `:20`). `Humanoid` sets `<intproperty Name="Bleeds" Value="1" />` (`Creatures.xml:770`) and **no `BleedLiquid`**, so the player bleeds the default liquid. To give a modded race a non-blood liquid, add the three `Bleed*` tags to your race's creature blueprint.

---

## 10. Deflection / metal detection

### 10a. `MetalShell` — DOES NOT EXIST

- Grep for `MetalShell` across all of `Base\*.xml`: **no matches.**
- Grep for `MetalShell` in the string table of `Assembly-CSharp.dll`: **no matches.**

The "becoming" mod's `MetalShell` is that mod's own invention, not a base-game part. The nearest base-game name collision is a *mine*, not a shell:
```xml
D:\...\Base\ObjectBlueprints\Items.xml:197
  <object Name="MineShell" Inherits="InorganicObject">
```
(an explosive mine casing — unrelated to deflection.)

### 10b. `<part Name="Metal" />` — 374 usages total; full object list

`Metal` is a code part (`XRL.World.Parts.Metal`, confirmed in the assembly). A related mod part `ModMetallized` also exists in code. Per-file counts: **Creatures.xml 41**, **Items.xml 243**, **Furniture.xml 78**, **Walls.xml 8**, **ZoneTerrain.xml 1**. Total **371** `<part Name="Metal" />` + 3 more that appear as `Metal` inside other contexts, i.e. ~374 grep hits.

**Creatures.xml — all 41 objects carrying `Metal`** (these are natural weapons and robot bodies):
`Robot`, `InorganicManipulator`, `Knob`, `Stud`, `Blunt End`, `MetalFist`, `MetalManipulator`, `Chromeling_Bite`, `Scrapbot_Scrapsaw`, `Scrapbot_Junkshovel`, `Waydroid_Shockrod`, `Drillbot_Drill`, `PlatedChromeling_Bite`, `Sawhander_Saw`, `Cloneling_Scalpel`, `Baetyl`, `Boosterbot_Claw`, `Point-Defense Laser`, `LightLock`, `Eaters' Crest`, `Winch_Lever`, `StripFly_Pincer`, `UrnPorter_AshShovel`, `Chrome Stilt`, `LightRondure`, `Dawning Ape`, `Naser Cannon`, `LeeringStalker_PneumaticPiston`, `MachinedEdge`, `ShoulderMountedLightCannon`, `GiganticMachineHull`, `PowerCore`, `Vertical Launcher`, `CherubimSpawn4A`, `CherubimSpawn4B`, `CherubimSpawn5A`, `CherubimSpawn5B`, `Tungsten Carbide Hammer-Fist`, `Tungsten Carbide Axe-Fist`, `Graftek_Tendril`, `Mindrone_Zap`

Three verbatim examples with their enclosing object:

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:674
  <object Name="Robot" Inherits="Creature">
    ...
    <part Name="Metal" />        <!-- line 684 -->
    <part Name="MaintenanceSystems" />
    <part Name="Physics" Organic="false" />
    <removepart Name="Springy" />
    <removepart Name="Stomach" />
    ...
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:1497
  <object Name="MetalFist" Inherits="NaturalWeapon">
    <part Name="Render" DisplayName="metal fist" Tile="Creatures/natural-weapon-fist.bmp" ColorString="&amp;c" />
    <part Name="Physics" Organic="false" />
    <part Name="MeleeWeapon" MaxStrengthBonus="1000" BaseDamage="1d3+1" Skill="Cudgel" Stat="Strength" />
    <part Name="Metal" />
    <tag Name="BaseObject" Value="*noinherit" />
    <tag Name="UndesirableWeapon" />
    <tag Name="ShowAsPhysicalFeature" Value="*delete" />
  </object>
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:1899
  <object Name="Chromeling_Bite" Inherits="Bite">
    <part Name="MeleeWeapon" BaseDamage="1d3" Skill="Axe" />
    <part Name="Metal" />
  </object>
```

**Items.xml — all 243 objects** (abridged to the distinct名单, in file order):
`MineShell`, `Grenade`, `Wrench`, `Mace2`, `Warhammer2`, `Steel War Hammer`, `Steel War Hammerth`, `Steel Hammer`, `Cudgel3`–`Cudgel8` (+`th` variants), `Maghammer`, `Stun Rod`, `Prayer Rod`, `Syphon Baton`, `Dagger`, `Desert Kris`, `Dagger2`, `Steel Kukri`, `Steel Dagger`, `Steel Utility Knife`, `Steel Potter's Knife`, `Steel Butcher Knife`, `Dagger3`–`Dagger8`, `ArmDagger4`, `Vibro Dagger`, `Gaslight Dagger`, `Gaslight Sword`, `Gaslight Chisel`, `Battle Axe`, `Hand Axe`, `Iron Vinereaper`, `Battle Axe2`, `Steel Battle Axe`, `Steel Battle Axeth`, `Steel Hand Axe`, `Opal-Pommeled Steel Axe`, `Steel Vinereaper`, `Battle Axe3`–`Battle Axe8` (+`th`), `Stun Whip`, `Long Sword`, `Two-Handed Sword`, `Long Sword2`, `Long Sword2th`, `Steel Long Sword`, `Steel Long Swordth`, `Long Sword3`–`Long Sword8` (+`th`), `Vibro Blade`, `Desert Rifle`, `Carbine`, `Sniper Rifle`, `Musket`, `Laser Rifle`, `Chain Laser`, `Eigenrifle`, `Freeze Ray`, `Hypertractor`, `Light Rail`, `Spaser Rifle`, `Phase Cannon`, `Electrobow`, `Turbow`, `Carbide Arrow`, `Folded Carbide Arrow`, `Chaingun`, `Grenade Launcher`, `Missile Launcher`, `HE Missile`, `Flamethrower`, `Defoliant Gas Pump`, `Fungicide Gas Pump`, `Mortar Tube`, `Normality Gas Pump`, `Arc Cannon`, `Linear Cannon`, `Blast Cannon`, `Swarm Rack`, `Borderlands Revolver`, `Semi-Automatic Pistol`, `Grappling Gun`, `Chain Pistol`, `Laser Pistol`, `Eigenpistol`, `Arc Winder`, `Nullray Pistol`, `Hand Rail`, `Di-Thermo Beam`, `Spaser Pistol`, `Space Inverter`, `Pump Shotgun`, `Combat Shotgun`, `Studded Leather Armor`, `Ring Mail`, `Chain Mail`, `Steel Plate Mail`, `Carbide Plate Armor`, `Gas Tumbler`, `Stasis Casque`, `Mechanical Wings`, `Gyrocopter Backpack`, `Strength Exo`, `Psiamp Backpack`, `Palladium Mesh Tabard`, `Gigantic Chassis Plate`, `Steel Helmet`, `Chain Coif`, `Headlamp`, `Miner's Helmet`, `Ganglionic Teleprojector`, `Psiamp Helmet`, `Mental Aggregator`, `Goggles`, `Mirrorshades`, `Night-vision Goggles`, `Gas Mask`, `Telescopic Monocle`, `Spectacles`, `Telemetric Visor`, `VISAGE`, `Night-Sight Interpolators`, `Dazzle Cheek`, `BaseUtilityBracelet`, `Blood-stained neck-ring`, `Structural Scanning Bracelet`, `Steel Gauntlets`, `Ulnar Stimulators`, `Chain Gauntlets`, `Carbide Gauntlets`, `Precision Nanon Fingers`, `Magnetized Boots`, `Chain Boots`, `Steel Boots`, `Carbide Boots`, `Spring Boots`, `Step Sowers`, `Rocket Skates`, `Ninefold Boots`, `3D Cobblers`, `Anti-Gravity Boots`, `Point-Defense Drone`, `Iron Buckler`, `Steel Buckler`, `Steel Shield`, `Carbide Shield`, `Fullerite Shield`, `Crysteel Shield`, `Flawless Crysteel Shield`, `Tread Guard`, `Stopsvaalinn`, `Nacham's Ribbon`, `Vaam's Lens`, `Dagasha's Spur`, `Kah's Loop`, `Flange from the Great Machine`, `Sail from the Great Machine`, `Gear from the Great Machine`, `Gauge from the Great Machine`, `BaseTierShield`, `BaseTierBody2_AV`, `BaseTierBody2_DV`, `BaseTierShield2`, `BaseTierBody3_AV`, `BaseTierHead3_AV`, `BaseTierHands3_AV`, `BaseTierFeet3_AV`, `BaseTierShield3`, `BaseTierBody4_AV`, `BaseTierHead4_AV`, `BaseTierHands4_AV`, `BaseTierFeet4_AV`, `BaseTierShield4`, `BaseTierShield5`, `CarbideFist`, `FulleriteFist`, `MotorizedTreads`, `GunRack`, `CrysteelFist`, `BaseCathedra`, `Energy Cell`, `Chem Cell`, `Fidget Cell`, `Solar Cell`, `Nuclear Cell`, `Liquid Fueled Energy Cell`, `Lead-Acid Cell`, `Combustion Cell`, `Thermoelectric Cell`, `Biodynamic Cell`, `Scrap`, `Tool`, `Basic Toolkit`, `Advanced Toolkit`, `Pickaxe`, `Nanopneumatic Jackhammer`, `Tattoo Gun`, `Neuro Animator`, `Droid Scrambler`, `BaseRecoiler`, `Bronze Ingot`, `Copper Nugget`, `Silver Nugget`, `SmallMetalTrinket`, `FoldingChair`, `Small Sphere of Negative Weight`, `StorageTank`, `Magnetic Bottle`, `Wire Strand`

**Furniture.xml — 78 objects:** `GritGateCommPanel`, `Striped Door`, `Wavy Door`, `Metal Door`, `BaseDoubleWavyDoor`, `BaseDoubleMetalDoor`, `Security Door`, `Troll Door`, `Door_GritGateRank1`, `Door_GritGateRank2`, `Crypt Door`, `Crypt Double Door N`, `Crypt Double Door S`, `Recoming Nook Door`, `Iron Gate`, `PowerLine`, `HighTechInstallation`, `Kiln`, `Induction Charging Station`, `Hydraulic Bubblething`, `Rock Tumbler`, `Lathe`, `Food Processor`, `Universal Charging Station`, `Glass Furnace`, `Glass Printer`, `Regen Tank`, `Psiamp Sarcophagus`, `CyberneticsTerminal2`, `ConveyorDrive`, `ConveyorPad`, `Reclamation Cist`, `Reshaping Nook`, `Medical Bed`, `Hyperbiotic Bed`, `Throne`, `Massage Chair`, `Ergomax Chair`, `Torture Chair`, `Hyperbiotic Chair`, `Gravchair`, `Metal Chest1`–`Metal Chest8`, `RustLocker`, `MedLocker`, `Locker`, `CyberneticsRack`, `BaseWedgeChest`, `Reliquary`, `Recoming Reliquary`, `Multicabinet`, `Brazier`, `Tall Brazier`, `Sconce`, `Torchpost`, `Unlit Torchpost`, `Techlight1`–`Techlight3`, `Full-Spectrum Techlight`, `Half Candelabra`, `Tomb Techlight1`, `Metal Table`, `Sleek Table`, `Ornate Table`, `Globe`, `Orrery`, `Clockthing`, `Sewing Machine`, `Iron Maiden`, `Village Monument Sculpture`, `Sarcophagus`

**Walls.xml — 8 objects:** `BaseWallCircuit`, `BaseWallCyber`, `BaseWallMetal`, `BaseWallTubing`, `BaseWallMainframe`, `BaseWallSecure`, `IronFence`, `Walltrap`
**ZoneTerrain.xml — 1 object:** `Palladium Strut`

### 10c. "Deflect projectiles" — `Deflect` DOES NOT EXIST, but `PointDefense` DOES

**Explicit negative finding:** a grep for `Deflect` across all `Base\*.xml` returns **no matches**, and a grep of the `Assembly-CSharp.dll` string table for `Deflect` also returns **zero** strings. There is no part named `Deflect`, `ProjectileDefense`, or `MissileDefense`.

**The existing mechanic is `PointDefense`.** Confirmed present in both XML and the assembly:
```
Assembly-CSharp.dll strings:
  PointDefense
  PointDefenseSystem
  PointDefenseInterceptEvent
  XRL.World.Parts|PointDefense
  Assets\XRL Application\World\Events\PointDefenseInterceptEvent.cs
```

All base-game XML usages, verbatim:

```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6263
    <part Name="PointDefense" ChargeUse="1" MinRange="2" MaxRange="5" TargetExplosives="80" TargetThrownWeapons="90" TargetArrows="70" TargetSlugs="40" TargetEnergy="0" WorksOnEquipper="false" WorksOnSelf="true" UsesSelfEquipment="true" ShowComputeMessage="false" />
```
```xml
D:\...\Base\ObjectBlueprints\Items.xml:6747
    <part Name="PointDefense" ChargeUse="1" MinRange="2" MaxRange="5" TargetExplosives="80" TargetThrownWeapons="90" TargetArrows="70" TargetSlugs="40" TargetEnergy="0" />
```

Full item context (the Point-Defense Drone — the closest thing to a "deflect projectiles" build):
```xml
D:\...\Base\ObjectBlueprints\Items.xml:6738
  <object Name="Point-Defense Drone">
    <part Name="Render" DisplayName="point-defense drone" Tile="Items/sw_point_defense_drone.bmp" ColorString="&amp;c" DetailColor="Y" />
    ...
    <part Name="EnergyAmmoLoader" ChargeUse="100" ProjectileObject="ProjectilePointDefenseLaser" />
    ...
    <part Name="PointDefense" ChargeUse="1" MinRange="2" MaxRange="5" TargetExplosives="80" TargetThrownWeapons="90" TargetArrows="70" TargetSlugs="40" TargetEnergy="0" />
    ...
  <object Name="ProjectilePointDefenseLaser" Inherits="TemporaryEnergyProjectile">
    <part Name="Projectile" BasePenetration="5" BaseDamage="1d10" Attributes="Light Laser" ColorString="&amp;B" PassByVerb="streak" />
```

The full creature-side setup (Normality Bot) pairs `PointDefense` with a matching weapon, an energy loader, `IntegratedPowerSystems`, and an event responder — this is the complete 5-part recipe to copy:
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6261
    <part Name="FusionReactor" ChargeRate="1000" />    
    <part Name="Circuitry" StartCharge="1000" />
    <part Name="PointDefense" ChargeUse="1" MinRange="2" MaxRange="5" TargetExplosives="80" TargetThrownWeapons="90" TargetArrows="70" TargetSlugs="40" TargetEnergy="0" WorksOnEquipper="false" WorksOnSelf="true" UsesSelfEquipment="true" ShowComputeMessage="false" />
```
```xml
D:\...\Base\ObjectBlueprints\Creatures.xml:6277
  <object Name="Point-Defense Laser" Inherits="NaturalWeapon">
    <part Name="Render" DisplayName="point-defense laser" ColorString="&amp;c" DetailColor="r" Tile="Items/sw_point_defense_laser.bmp" />
    <part Name="Physics" Weight="7" />
    <part Name="Commerce" Value="400" />
    <part Name="Armor" AV="0" DV="0" WornOn="Back" />
    <part Name="MissileWeapon" Skill="Pistol" AmmoChar="&amp;B&#15;" RangeIncrement="3" ShotsPerAction="1" AmmoPerAction="1" ShotsPerAnimation="1" AnimationDelay="7" WeaponAccuracy="12" NoWildfire="true" EnergyCost="0" FiresManually="false" />
    <part Name="EnergyAmmoLoader" ChargeUse="100" ProjectileObject="ProjectilePointDefenseLaser" />
    <part Name="IntegratedPowerSystems" RequiresEvent="HasPowerConnectors" />
    <part Name="RespondToEvent" EventHandled="UseForPointDefense" NameForStatus="TrackingDataProcessor" />
    <part Name="Description" Short="It's a robotic half-eye lidded by a furcate chassis and embellished with antenna lashes. A minuscule barrel pokes out from its pupil and vibrates. The inner lid is articulated, allowing the drone to scan for incoming projectiles and shoot them down." />
    <part Name="Examiner" Complexity="6" Difficulty="2" />
    <part Name="TinkerItem" Bits="0256" CanDisassemble="true" CanBuild="false" />
    <part Name="Metal" />
    <tag Name="Mods" Value="MissileWeaponMods,FirearmMods,CommonMods,PistolMods,ElectronicsMods,BeamWeaponMods" />
    <tag Name="Tier" Value="6" />
    <tag Name="MissileFireSound" Value="Sounds/Missile/Fires/Pistols/sfx_missile_laserPistol_fire" />
    <tag Name="ExcludeFromDynamicEncounters" Value="*noinherit" />
  </object>
```

**`PointDefense` attribute vocabulary** (union of both usages): `ChargeUse`, `MinRange`, `MaxRange`, `TargetExplosives`, `TargetThrownWeapons`, `TargetArrows`, `TargetSlugs`, `TargetEnergy`, `WorksOnEquipper`, `WorksOnSelf`, `UsesSelfEquipment`, `ShowComputeMessage`. The `Target*` values are percentages (0–100) of interception chance per missile category.

**`IntegratedPowerSystems`** — 7 usages, all identical: `Creatures.xml:6284, 7051, 8854, 9311`; `Items.xml:2342, 7217, 7241`, all `<part Name="IntegratedPowerSystems" RequiresEvent="HasPowerConnectors" />`.

---

## 11. EmbarkModules.xml — full structure

**File:** `D:\...\Base\EmbarkModules.xml`, 471 lines, root element `<embarkmodules>`.

### 11a. All `Class=` values in the file (module classes, in file order)

| Line | `<module Class="...">` |
|---|---|
| 4 | `XRL.CharacterBuilds.Qud.QudSpecificCharacterInitModule` |
| 8 | `XRL.CharacterBuilds.Qud.QudGamemodeModule` |
| 64 | `XRL.CharacterBuilds.Qud.QudChartypeModule` |
| 108 | `XRL.CharacterBuilds.Qud.QudBuildLibraryModule` |
| 115 | `XRL.CharacterBuilds.Qud.QudGenotypeModule` |
| 122 | `XRL.CharacterBuilds.Qud.QudPregenModule` |
| 222 | `XRL.CharacterBuilds.Qud.QudSubtypeModule` |
| 233 | `XRL.CharacterBuilds.Qud.QudMutationsModule` |
| 241 | `XRL.CharacterBuilds.Qud.QudAttributesModule` |
| 248 | `XRL.CharacterBuilds.Qud.QudCyberneticsModule` |
| 255 | `XRL.CharacterBuilds.Qud.QudBuildSummaryModule` |
| 262 | `XRL.CharacterBuilds.Qud.QudCustomizeCharacterModule` |
| 269 | `XRL.CharacterBuilds.Qud.QudChooseStartingLocationModule` |
| 464 | `XRL.CharacterBuilds.Qud.QudSpecificBootHandlersModule` |
| 468 | `XRL.CharacterBuilds.Qud.QudGameBootModule` |

**15 modules total.** Two of them are empty shells (4, 464, 468). Each UI-bearing module declares one or more `<window ID="Chargen/..." Prefab="..." Class="XRL.CharacterBuilds.Qud.UI.…ModuleWindow">` children. Distinct `Prefab=` values used: `HorizScroll`, `SwitchingScroller`, `CategoryMenus`, `HorizScrollerScroller`, `VertScroll`.

Window IDs / UI window classes:
| Module | Window ID | Prefab | Window Class |
|---|---|---|---|
| QudGamemodeModule | `Chargen/Modes` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudGamemodeModuleWindow` |
| QudChartypeModule | `Chargen/CharType` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudChartypeModuleWindow` |
| QudBuildLibraryModule | `Chargen/BuildLibrary` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudBuildLibraryModuleWindow` |
| QudGenotypeModule | `Chargen/ChooseGenotypes` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudGenotypeModuleWindow` |
| QudPregenModule | `Chargen/Pregens` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudPregenModuleWindow` |
| QudSubtypeModule | `Chargen/ChooseSubtypes` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudSubtypeModuleWindow` |
| QudSubtypeModule | `Chargen/ChooseSubtypesCategory` | `SwitchingScroller` | `XRL.CharacterBuilds.Qud.UI.QudSubtypeModuleCategoryWindow` |
| QudMutationsModule | `Chargen/Mutations` | `CategoryMenus` | `XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow` |
| QudAttributesModule | `Chargen/PickAttributes` | `HorizScrollerScroller` | `XRL.CharacterBuilds.Qud.UI.QudAttributesModuleWindow` |
| QudCyberneticsModule | `Chargen/Cybernetic` | `CategoryMenus` | `XRL.CharacterBuilds.Qud.UI.QudCyberneticsModuleWindow` |
| QudBuildSummaryModule | `Chargen/BuildSummary` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudBuildSummaryModuleWindow` |
| QudCustomizeCharacterModule | `Chargen/Customize` | `VertScroll` | `XRL.CharacterBuilds.Qud.UI.QudCustomizeCharacterModuleWindow` |
| QudChooseStartingLocationModule | `Chargen/ChooseStartingLocation` | `HorizScroll` | `XRL.CharacterBuilds.Qud.UI.QudChooseStartingLocationModuleWindow` |

### 11b. Data-bearing sections

- **`<modes>`** inside `QudGamemodeModule` (lines 14–61): 5 `<mode>` entries — `Tutorial`, `Classic`, `Roleplay`, `Wander`, `Daily` (the last has `Editable="False"`). Each has `<icon Tile=... Foreground=... Detail=.../>`, `<description>`, and `<stringgamestate Name="GameMode" Value="..."/>`. `Roleplay` and `Wander` also set `Checkpointing=Enabled`.
- **`<types>`** inside `QudChartypeModule` (starts line 70).
- **`<pregens>`** inside `QudPregenModule` (lines 128–219): 9 `<pregen>` entries.
- **`<locations>`** inside `QudChooseStartingLocationModule` (lines 275–461): `<location ID=... Name=... Location="GlobalLocation:...@x,y">` with `<description>`, `<stringgamestate>`, and a `<grid Position="rc" Tile=... Foreground=... Detail=... Background=.../>` minimap block.

The 9 `<pregen>` entries: `Praetorian Prime` (True Kin), `First Gardener` (True Kin), `First Child of the Hearth` (True Kin), `Marsh Taur` (Mutated Human), `Dream Tortoise`, `Gunwing`, `Star-Eye Esper`, `Firefrond`, `bzzzt`.

### 11c. One full `<pregen>` example, verbatim

`Praetorian Prime` — note the `<code>` child is a **base64 gzip blob** (a whole serialized build), which is how pregens are stored:

```xml
D:\...\Base\EmbarkModules.xml:122
  <module Class="XRL.CharacterBuilds.Qud.QudPregenModule">
    <window ID="Chargen/Pregens" Prefab="HorizScroll" Class="XRL.CharacterBuilds.Qud.UI.QudPregenModuleWindow">
      <name>Pregens</name>
      <title>:choose preset:</title>
    </window>

    <pregens>
      
    <!-- True Kin -->
      <pregen Name="Praetorian Prime" Genotype="True Kin" Tile="creatures/sw_praetorian_prime.bmp" Foreground="y" Detail="c" Background="k">
        <code>H4sIACEIKGEA/81U74vTQBD9fnD/wxL8mIZeLSJCP9T6AzmVei1VkPuwSYZkcbsbdmeVIP3fbyZNbfYKFgWlC6XNvDczL29m+/P6Soikklv4Ds4ra5IXIplk42wynmTTaZJ2eB6ULgeEGyKMe2xry6DBU/grPwvRleTTQ+u2AU76cvc+W9TSyQLBveSKPvsUSv68BWORaB+6hFTMvYdtrtvRYkUJTSo2+96zo7JULILG4GBmIKCTOhXLkGtV3EK7tt/AzEzQeq+xE1NKlCTjlzoKPeGev5EWy3pFBU6kHRtQvQOfS65dAHGrTEQ4sfCA7fY/dj35LyxchfwCHYxUnTewp3PBpZOA1in53xycIzqVBwR/YSY+Fnbex6VVBv0yuKKWHsq4H/uMDkyFNQHPB3mEzCulFbYEPIuBtQ1VbcDzRX+U884gaK0qMAW/xjRGPyutG/sD3Cn0urIcPMYO0+tQ2awaMEiM0dNhRwLuYCuVUaYicDyEcnrdeUPROOMfbs2izcEZQFVc2tqcKDu/N5qtG90MQx40FEjqB//w+xOtFDGP/VjUR1XVuFGd7dHUmWlDN9dhow7YSL7wDCVvJC3TEO5Hxef+j2bLX/fXV7sH0tV16+sGAAA=</code>
        <description>
&amp;c&#249;&amp;y Charge-based melee fighter
&amp;c&#249;&amp;y Starts with night vision
&amp;c&#249;&amp;y {{W|Most survivable}} starting build
        </description>
      </pregen>
```
(Continues with the other 8 pregens through line 217, then `</pregens>` at 219 and `</module>` at 220.)

**Attribute vocabulary on `<pregen>`:** `Name`, `Genotype` (matches `<genotype Name=` in `Genotypes.xml`), `Tile`, `Foreground`, `Detail`, `Background`; children `<code>` (base64 gzip build blob) and `<description>` (rich text with `&#249;` = • bullet and `{{W|...}}` markup).

**Attribute vocabulary on `<location>`:** `ID`, `Name`, `Location="GlobalLocation:<world>.<a>.<b>.<c>.<d>@<x>,<y>"`; children `<description>`, `<stringgamestate Name=... Value=...>`, `<grid Position="rc" Tile=... Foreground=... Detail=... Background=.../>`.

---

## 12. Subtypes.xml — full structure

**File:** `D:\...\Base\Subtypes.xml`, 332 lines, root element `<subtypes>`.

### 12a. All `<class ID=...>` values — exactly 2

| Line | `<class>` |
|---|---|
| 4 | `<class ID="Castes" ChargenTitle="choose caste" SingularTitle="caste" StatBoxDisplay="true">` |
| 179 | `<class ID="Callings" ChargenTitle="choose calling" SingularTitle="calling">` |

`Castes` is referenced by the True Kin genotype (`Subtypes="Castes"`, `Genotypes.xml:20`); `Callings` by Mutated Human (`Subtypes="Callings"`, `Genotypes.xml:4`).

### 12b. The full `<class ID="Callings" ...>` header, verbatim

```xml
D:\...\Base\Subtypes.xml:179
  <class ID="Callings" ChargenTitle="choose calling" SingularTitle="calling">
```
(with `</class>` closing at line 330; the file ends at 332 with `</subtypes>`)

For completeness the other class header verbatim:
```xml
D:\...\Base\Subtypes.xml:4
  <class ID="Castes" ChargenTitle="choose caste" SingularTitle="caste" StatBoxDisplay="true">
```

**`<class>` attribute vocabulary:** `ID`, `ChargenTitle`, `SingularTitle`, `StatBoxDisplay`. `Castes` additionally nests `<category Name=... DisplayName=...>` blocks (3 of them: `Ekuemekiyye`, `Ibul`, `Yawningmoon`); `Callings` is a flat list of 13 subtypes.

### 12c. Two verbatim `<subtype>` examples

**Example 1 — from `Castes`, the only subtype carrying `savemodifiers`:**
```xml
D:\...\Base\Subtypes.xml:8
      <subtype Name="Horticulturist" Gear="StartingGear_Horticulturist" Tile="creatures/caste_1.bmp" DetailColor="g">
        <stat Name="Intelligence" Bonus="3" />
        <skills>
          <skill Name="CookingAndGathering" />
          <skill Name="CookingAndGathering_Harvestry" />
          <skill Name="Axe" />
          <skill Name="Rifles" />
          <skill Name="Survival_JungleSurvival" />
        </skills>
        <savemodifiers>
          <savemodifier Vs="Bleeding" Amount="2" />
        </savemodifiers>
      </subtype>
```

**Example 2 — from `Callings`, showing the optional `Class=` and `<reputations>` children:**
```xml
D:\...\Base\Subtypes.xml:288
    <subtype Name="Warden" Gear="StartingGear_Warden" Tile="creatures/caste_22.bmp" DetailColor="w">
      <stat Name="Strength" Bonus="2" />
      <skills>
        <skill Name="LongBlades" />
        <skill Name="Shield" />
        <skill Name="Shield_Slam" />
        <skill Name="Rifles" />
        <skill Name="Pistol" />
      </skills>
      <reputations>
        <reputation With="Wardens" Value="300" />
      </reputations>
    </subtype>
```

**Example 3 — the only subtype with a non-empty `Class=` attribute (`Scholar`, `Class="ScholarSkills"`):**
```xml
D:\...\Base\Subtypes.xml:261
    <subtype Name="Scholar" Class="ScholarSkills" Gear="StartingGear_Scholar" Tile="creatures/caste_20.bmp" DetailColor="C">
      <stat Name="Intelligence" Bonus="2" />
      <skills>
        <skill Name="Tinkering" />
				<skill Name="Physic" />
        <skill Name="Physic_Nostrums" />
        <skill Name="CookingAndGathering_Harvestry" />
        <skill Name="Tactics" />
        <skill Name="Customs" />
      </skills>
    </subtype>
```

### 12d. Complete subtype inventory and the schema

`Castes` (13): `Horticulturist`, `Priest of All Suns`, `Priest of All Moons`, `Syzygyrior` (category *Ekuemekiyye*); `Artifex`, `Consul`, `Praetorian`, `Eunuch` (category *Ibul*); `Child of the Hearth`, `Child of the Wheel`, `Child of the Deep`, `Fuming God-Child` (category *Yawningmoon*).

`Callings` (13): `Apostle`, `Arconaut`, `Greybeard`, `Gunslinger`, `Marauder`, `Pilgrim`, `Nomad`, `Scholar`, `Tinker`, `Warden`, `Water Merchant`, `Watervine Farmer` — 12 listed plus `Pilgrim` = 13 total.

**Element/attribute vocabulary:**
- `<subtype Name= Gear= Tile= DetailColor= [Class=]>`
- `<stat Name= Bonus= />` — `Bonus` accepts negatives (e.g. `Greybeard` has `<stat Name="Strength" Bonus="-1" />` at line 204). Stat names used: the six attributes plus `ColdResistance` and `HeatResistance`.
- `<skills><skill Name=... /></skills>`
- `<reputations><reputation With=... Value=... /></reputations>` — faction name as used in `Factions.xml`.
- `<savemodifiers><savemodifier Vs=... Amount=... /></savemodifiers>`
- `<extrainfo>...</extrainfo>` — free text shown at chargen (supports `{{B|...}}` colour markup), e.g. line 258 `<extrainfo>Starts with a {{B|recycling suit}}</extrainfo>`.

Verbatim example with all optional blocks:
```xml
D:\...\Base\Subtypes.xml:246
    <subtype Name="Nomad" Gear="StartingGear_Nomad" Tile="creatures/caste_19.bmp" DetailColor="W">
      <stat Name="Toughness" Bonus="2" />
      <skills>
        <skill Name="Survival" />
        <skill Name="Survival_SaltDesertSurvival" />
        <skill Name="CookingAndGathering_Harvestry" />
        <skill Name="Survival_Trailblazer" />
        <skill Name="Endurance_Weathered" />
      </skills>
      <reputations>
        <reputation With="Issachari" Value="200" />
      </reputations>
      <extrainfo>Starts with a {{B|recycling suit}}</extrainfo>
    </subtype>
```

Note `Gear="StartingGear_*"` references population/gear tables that live in `PopulationTables.xml` — none of those blueprints are defined in `Subtypes.xml` itself.

---

## Cross-cutting notes for the mod author

1. **`<tag Name="BaseObject" Value="*noinherit" />`** is present on nearly every template. It marks the object as a template; the character builder's `BodyObject`/inheritance machinery relies on it.
2. **Inheritance is `<object Name="X" Inherits="Y">`**; single inheritance only, resolved in file + load order. `ObjectBlueprints.xml` at the `Base\` root is an empty `<objects>` stub and is where mod load order would be registered.
3. **Stat modification on a child uses `Value=` (absolute override), `Boost=` (additive integer), or `sValue=` (tier-scaled roll string).** All three appear on the same stat name in different objects.
4. **`<removepart Name="..." />`** removes an inherited part; used e.g. `removepart Name="Springy"` (`Creatures.xml:687`), `removepart Name="Stomach"` (`:688`), `removepart Name="Aquatic"` (`:8247`), `removepart Name="Capacitor"` (`Furniture.xml:1675`).
5. **Every number that matters for balance in this version lives in code**, not XML: mutation curves (`Regeneration`, `ElectricalGeneration` charge, stinger venom durations), weapon mod effects (`ModFlaming`/`ModFreezing`), and natural healing rate. XML only carries names, levels, and the flags. Plan the mod accordingly — either accept the code defaults or ship a Harmony patch (`0Harmony.dll` is present in `CoQ_Data\Managed`).
