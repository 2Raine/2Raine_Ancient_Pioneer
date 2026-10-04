# Caves of Qud Modding — Technical Research Note: Worlds, Zones, Content & Release

Sources: official Caves of Qud wiki (https://wiki.cavesofqud.com), pages listed at the end. All XML/JSON/C# snippets below are quoted from those pages (raw wikitext), except where a snippet is explicitly marked as reconstructed from prose. Anything the wiki does not document is marked **[UNDOCUMENTED]**.

---

## 1. Zones and worlds

### 1.1 Conceptual model (from `Modding:Intro - Zones and Worlds`)

- **Zone:** a "screen" of the game, consisting of an **80 x 25** rectangular grid of cells.
- **Cell:** a specific location within a zone. Objects typically occupy one cell at a time.
- **World:** a collection of zones. The default world players start in is **`JoppaWorld`**; other examples are **`Tzimtzlum`**, **`Thin World`**, and **`Interior`** (used to model vehicle interiors). More technically (footnote on the page): a collection of zones **sharing a common `IZoneFactory`** and some additional properties specified via `Worlds.xml`.
- **Parasang:** a **3 x 3 grid of zones**. Each cell of the world map corresponds to a parasang.
- The top-left parasang of the world map is **(0, 0)**; the world map goes to **(79, 24)** at the bottom right. (So the world map itself is 80 x 25 parasangs.)

### 1.2 Zone ID string format

Verbatim from the intro page:

> In scripting, it's common to see coordinates in the format `JoppaWorld.53.3.1.1.10`. This format is: `WorldName.ParasangX.Y.ZoneX.Y.StrataZ`. In the previous coordinate, it is in JoppaWorld (Qud), in the 53rd tile from the right on the world map, 3 tiles down, in the center of the parasang. (Zone ranges from 0-2.) It's on the surface: Z level 10 is the surface strata, Z level 0 being where Resheph's tomb is. Z level 50 would be 40 strata deep.

Key consequences:

- `ZoneX` / `ZoneY` range **0–2** (they index the 3 x 3 grid inside the parasang).
- `StrataZ` **10 = surface**; descending decreases Z (Z 0 ≈ Resheph's tomb); ascending from 10 goes *above* the surface (the `MoonStairCell` example below uses `Level="5-9"` for "sky above the Moon Stair").
- Zone definitions with `Level` **higher than 49 are ignored**: "The game currently applies a depth cap of 49 to zone definitions (in `XRL.World.ZoneManager.GetZoneBlueprint`)."
- Other real-world examples used in the wiki text: Joppa `JoppaWorld.11.22.1.1.10`, Six Day Stilt `JoppaWorld.5.2.1.2.10` (also `JoppaWorld.5.2.1.1.10`), Kyakukya `JoppaWorld.27.20.1.1.10`, Ezra `JoppaWorld.53.4.0.0.10`, Grit Gate `JoppaWorld.22.14.1.0.13`, Sultan tomb `JoppaWorld.53.3.0.2.6` / `JoppaWorld.53.3.1.0.1`.

### 1.3 `Worlds.xml` — exact tag structure

The wiki gives this table for `Worlds.xml` (verbatim descriptions):

| XML Tag | Description |
| --- | --- |
| `<worlds>` | (root) |
| `<world>` | auto merged with the game definition if the world with this Name was already defined (the primary game world currently is JoppaWorld, so you want to use that if you're trying to merge into Qud's world) |
| `<builder>` | Any builder class you specify gets added to the list of builders and executed as part of world creation. Must be a class in the `XRL.World.WorldBuilders` namespace. The only builder used by the base game is `JoppaWorldBuilder`. Begin a Class name with minus (`-`) to remove all builders with that class from existing world definition (this would probably be a bad idea though unless you're remaking the entire world by fully replacing JoppaWorldBuilder). |
| `<cell>` | Cell nodes are completely overwritten if they have a matching Name to one that already exists in the game files. Cell nodes can inherit from other cell nodes. |
| `<zone>` | Define the specified zone or range of zones within this world cell. For example, the Rustwell is a single world cell, which includes various zone definitions for the zones included within it's 3x3 parasang area and Z depths. The game currently applies a depth cap of 49 to zone definitions (in `XRL.World.ZoneManager.GetZoneBlueprint`). As a result, if you define a zone definitions with a `Level` attribute higher than 49, those definitions are ignored. |
| `<builder>` | Builders for zones. Zones can have multiple builders, which are applied in succession to generate the zone. |
| `<postbuilder>` | Postbuilders are similar to builders, but applied afterward. |
| `<population>` | Use the given population table to generate creatures and items in the zone. |
| `<map>` | Use the provided map (an `.rpm` file) for the zone. |

> **Note on the task brief:** there is **no `<region>` element** documented for `Worlds.xml`. The documented hierarchy is `<worlds> → <world> → <cell> → <zone> → (<builder> | <postbuilder> | <population> | <map> | <widget> | <music> | <encounter> | <intproperty> | <boolproperty>)`. Regions/terrain of the world map are modelled as *objects* in the world-map `.rpm` plus `ApplyTo` on `<cell>`, not as a `<region>` tag.

### 1.4 Minimal working world (starter code used by all zone-builder articles)

`Worlds.xml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<worlds>                                           
  <world Name="ZBWorld" ZoneFactory="ZBWorldZoneFactory" DisplayName="zone builder test world">
    <cell Name="ExampleCell">
      <zone Level="10" x="1" y="1" Name="test zone">
        <!-- Leave empty for now -->
      </zone>
    </cell>
  </world>
</worlds>
```

`ZoneFactories.cs`:

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

This world is reachable with the wish `goto:ZBWorld.40.12.1.1.10`.

### 1.5 Bare-bones new world blueprint + `Plane` / `Protocol`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<worlds>
  <world Name="MyWorld" ZoneFactory="MyWorldFactory" DisplayName="your personal world">
    <!--
    You can add custom world builders here, e.g.
    <builder Class="MyWorldBuilder" />
    -->
  </world>
</worlds>
```

Semantics per the wiki:

- Worlds **may share the same `Plane`** if they are in the same "dimension" (e.g. a world consisting of a map of the salt desert and `JoppaWorld`).
- `Protocol` encodes properties about a specific world which may or may not be shared across the same plane.

Real examples quoted from `Worlds.xml`:

```xml
<!-- Taken from Worlds.xml -->
<world Name="ThinWorld" ZoneFactory="ThinWorldZoneFactory" DisplayName="Thin World" Protocol="THIN"></world>

<world Name="Tzimtzlum" ZoneFactory="TzimtzlumWorldZoneFactory" DisplayName="Tzimtzlum" Plane="Tzimtzlum"></world>
```

### 1.6 `IZoneFactory` — full example (world map of jungle + Yd Freehold everywhere)

```csharp
namespace XRL.World.ZoneFactories {
    public class MyWorldFactory : IZoneFactory {

        public override Zone BuildZone(ZoneRequest Request) {
            Zone zone = new Zone(80, 25);
            zone.ZoneID = Request.ZoneID;

            if (Request.IsWorldZone) {
                zone.ForeachCell(delegate(Cell c) {
                    c.AddObject("TerrainJungle");
                });
                zone.DisplayName = "your world, world map";
                return zone;
            }

            zone.loadMap("YdFreehold.rpm");
            zone.DisplayName = "your personal world";
            return zone;
        }

        public override void AfterBuildZone(Zone zone, ZoneManager zoneManager) {
            ZoneManager.PaintWalls(zone);
            ZoneManager.PaintWater(zone);
        }
    }
}
```

Verified with `goto:MyWorld.40.12.1.1.10`. The **only** method a factory strictly needs to implement is `BuildZone`.

### 1.7 Using cells and zone blueprints

To defer generation to `Worlds.xml` cells, override two more members:

- `AddBlueprintsFor`, which adds zone blueprints appropriate to the zone that we're trying to generate.
- `CanBuildZone`, which determines whether we build a zone directly using `BuildZone` or indirectly with `GenerateZone` and `AddBlueprintsFor`.

`Worlds.xml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<worlds>
  <world Name="MyWorld" ZoneFactory="MyWorldFactory" DisplayName="your personal world">
    <cell Name="MyWorldCell">
      <zone Level="10" x="1" y="1" Name="my joppa" IncludeStratumInZoneDisplay="true">
        <map FileName="YdFreehold.rpm" />
        <music Track="MehmetsMorning" />
      </zone>
    </cell>
  </world>
</worlds>
```

```csharp
namespace XRL.World.ZoneFactories {
    public class MyWorldFactory : IZoneFactory {

        public override bool CanBuildZone(ZoneRequest Request) {
            // Use BuildZone only for the world map; otherwise, we use GenerateZone
            // and AddBlueprintsFor.
            return Request.IsWorldZone;
        }

        public override Zone BuildZone(ZoneRequest Request) {
            var zone = new Zone(80, 25);
            zone.ZoneID = Request.ZoneID;

            if (Request.IsWorldZone) {
                zone.ForeachCell(delegate(Cell c) {
                    c.AddObject("TerrainJungle");
                });
            }

            zone.DisplayName = "your personal world";
            return zone;
        }

        public override void AddBlueprintsFor(ZoneRequest Request) {
            // Normally we would use the fields of the ZoneRequest to figure out
            // what cell blueprint we should get. In this case, we just resort
            // to using the same blueprint for all cells.
            var cellBlueprint = Blueprint.CellBlueprintsByName["MyWorldCell"];
            var levelBlueprint = cellBlueprint.LevelBlueprint[1, 1, 10];
            Request.Blueprints.Add(levelBlueprint);
        }

        public override void AfterBuildZone(Zone zone, ZoneManager zoneManager) {
            ZoneManager.PaintWalls(zone);
            ZoneManager.PaintWater(zone);
        }
    }
}
```

### 1.8 Zone-build order at runtime (from `Modding:Zone Builders`)

When the player first enters a zone, the game builds it following these steps:

- First, it identifies the appropriate zone factory for the world that the player is in (`JoppaWorld`, `ThinWorld`, etc.).
- The game then runs `CanBuildZone` on the zone factory to determine the method that it should use to generate the zone (footnote: `XRL.World.ZoneManager`, method `GenerateFactoryZone`).
  - When it returns true, the zone factory calls the factory's `BuildZone` method to create the zone.
  - Otherwise, the zone factory calls `GenerateZone` to instantiate the zone and `AddBlueprintsFor`.
- In *most* cases in JoppaWorld, the game takes the `AddBlueprintsFor` route. In this case, the game looks up the terrain object for the zone and searches `Worlds.xml` for the zone blueprint that it should apply to the zone (`XRL.World.ZoneFactories.JoppaWorldZoneFactory`, method `AddBlueprintsFor`).
- The game then iterates over the zone builders defined by the blueprint and applies them successively to the new zone (`XRL.World.ZoneManager`, method `ApplyBuilderToZone`).

### 1.9 A real cell definition (`MoonStairCell`)

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

Observed attribute vocabulary on `<cell>`/`<zone>` (all documented by example, not by a table): `Name`, `Inherits`, `ApplyTo`, `Mutable`, `Level` (single or `A-B` range), `x`, `y` (single or `0-2` range), `NameContext`, `ProperName`, `IndefiniteArticle`, `AmbientBed`, `IncludeStratumInZoneDisplay`.

Additional child tags seen in `Modding:Maps`:

```xml
<zone Level="10" x="0-2" y="0-2" Name="outskirts, Joppa" NameContext="Joppa">
  <builder Class="JoppaOutskirts" />
  <encounter Table="JoppaOutskirtsEncounters" Amount="minimum" />
</zone>

<zone Level="10" x="1" y="1" Name="SomeName" ProperName="true">
  <map FileName="Path/To/YourNewMap.rpm" /> <!-- put this map file somewhere in your mod folder -->
  <builder Class="Music" Track="MehmetsMorning" Chance="100" />
</zone>
<zone Level="10" x="1" y="2" Name="SomeOtherName" ProperName="true">
  <map ID="NewMapID" ClearBeforePlace="true" /> <!-- match this with the ID defined on your map file's root node -->
  <widget Blueprint="AmbientLight" />
</zone>
```

And on `<cell>`: `<boolproperty Name="JoinPartyLeaderPossible" Value="false" />`; on `<zone>`: `<intproperty Name="AmbushChance" Value="10" />`.

### 1.10 Zone builders — generic builders provided by the game

| Builder name | Description (quoted) |
| --- | --- |
| `AddBlueprintBuilder` | Adds an object with a specified `Object` parameter as a blueprint. In general it is preferable to use zone templates and population tables to add (non-structural) objects to zones, so this is primarily useful when you need to dynamically add on a zone builder. For example: this builder is used to place the chest containing the Ruin of House Isner during world generation. |
| `AddWidgetBuilder` | Adds an object to the (0, 0) cell of a zone. Typically this is a `Widget` object (see `ObjectBlueprints/Widgets.xml`) although in theory it can be anything. An `AddWidgetBuilder` builder is added to the zone whenever you use the `<widget/>` tag in `Worlds.xml`. For example, `<widget Blueprint="Grassy" />` adds a `Grassy` widget (which paints the floor of the zone with grass). |
| `Connecter` | *(no description given)* |
| `FactionEncounters` | Adds encounters with legendary creatures sampled from a provided table. |
| `MapBuilder` | This builder loads a map from an .rpm file. The `ID` or `FileName` must match the `ID` or path of the map file. A MapBuilder is implicitly applied to a zone by the `<map/>` tag in Worlds.xml. |
| `Music` | Adds a music track to a zone. A Music zone builder is implicitly added by the `<music/>` tag in Worlds.xml. |
| `SolidEarth` | Fills the entire map with shale. This is useful when you wish to carve out the geometry of your zone (i.e., specify the empty space rather than specify the filled space). |
| `StairConnector` | *(no description given)* |
| `StairsDown` | Adds a set of stairs down to the next Z-level. You can specify X- and Y- coordinates to influence the region in which the stairs are placed. |
| `StairsUp` | Adds a set of stairs up to the previous Z-level. You can specify X- and Y- coordinates to influence the region in which the stairs are placed. |
| `TileBuilding` | *(no description given)* |

MapBuilder usage, verbatim:

```xml
<map FileName="YdFreehold.rpm" />
<!-- The below is identical in behaviour to the above. -->
<map ID="YdFreehold" />
```

Warning from the page: "In general, the game's existing zone builders tend to be highly monolithic, so any zone builders not listed above are not recommended for use in creating new types of zones. For new zones modders should favor a more modular architecture."

### 1.11 Writing a zone builder — `ZoneBuilderSandbox`

"`ZoneBuilderSandbox` is the primary interface for creating new zone builders. Any builders that you add to the game will inherit from this class."

Most zone builders only need to define `BuildZone`. It accepts a zone and returns a bool signalling whether subsequent zone builders should also run.

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

Design rule quoted from the page: "**earlier builders should define coarse-grained detail about a zone such as layout and general geometry. Builders that come later should be focused on adding fine-grained details (e.g. individual rooms and encounters) to the zone.**"

Key utilities:

- `EnsureAllVoidsConnected(Z, pathWithNoise: ...)` — creates paths between empty spaces (useful when carving a zone out of solid material).
- `new FindPath(Z.GetCell(20, 12), Z.GetCell(50, 12))` with `path.Steps` (`using XRL.World.AI.Pathfinding;`).
- `PlacePopulationInRegion(Z, region, "PigFarm")`; a more limited version is `PlacePopulationInRect`.
- Other: `Cell.Clear`, `Cell.ClearWalls`, `Cell.RequireObject`, `ZoneBuilderSandbox.ClearRect`, `ZoneBuilderSandbox.PlaceHut`.
- `Zone.GetCells()` is the modern way to iterate cells.

Example builder + population table (verbatim):

```csharp
using Genkit;
using System;
using System.Collections.Generic;

namespace XRL.World.ZoneBuilders {
    public class CreateCircularFarm : ZoneBuilderSandbox {
        public bool BuildZone(Zone Z) {
            var region = new List<Location2D>();

            foreach (var cell in Z.GetCells()) {
                var dist = cell.CosmeticDistanceTo(40, 12);
                if (dist > 6)
                    continue;

                cell.Clear();
                if (dist == 6)
                    cell.AddObject("BrinestalkStakes");
                else
                    region.Add(cell.Location);
            }

            PlacePopulationInRegion(Z, region, "PigFarm");

            // Add a little door
            Z.GetCell(30, 12).Clear();
            Z.GetCell(30, 12).AddObject("Brinestalk Gate");

            return true;
        }
    }
}
```

`PopulationTables.xml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<populations>
  <population Name="PigFarm">
    <group Name="Creatures" Style="pickeach">
      <object Blueprint="PigFarmer" />
      <object Blueprint="Herding Dog" />
      <object Blueprint="Farm Pig" Number="3-6" />
    </group>
  </population>
</populations>
```

### 1.12 Worldgen hooks (`Modding:Worlds`)

Two interfaces hook into world generation:

```csharp
using XRL.World.WorldBuilders;

namespace YourMod.YourNamespace
{
    //The game code instantiates an instance of this class during the JoppaWorld generation process
    [JoppaWorldBuilderExtension]
    public class YourJoppaWorldBuilderExtension : IJoppaWorldBuilderExtension
    {
        public override void OnBeforeBuild(JoppaWorldBuilder builder)
        {
            //The game calls this method before JoppaWorld generation takes place. JoppaWorld generation includes the creation of lairs, historic ruins, villages, and more.
        }

        public override void OnAfterBuild(JoppaWorldBuilder builder)
        {
            //The game calls this method after JoppaWorld generation takes place.
        }
    }
}
```

`IWorldBuilderExtension` is the same shape but "is called before and after the **overall** world generation process", with attribute `[WorldBuilderExtension]` and interface `IWorldBuilderExtension`.

Documented recipe for adding a secret at worldgen:

1. Create your own world builder extension class inheriting from (most likely) `IJoppaWorldBuilderExtension`.
2. In the `OnAfterBuild` method, grab a random mutable block of terrain using either `AddMutableEncounterToTerrain` or `popMutableLocationOfTerrain`.
3. Add zone builders to the zone.
4. Use `AddSecret` to add a corresponding secret to the location.
5. (Optional) If you want to make the secret findable while traversing the world map, get the `TerrainTravel` part on the block of terrain that you popped and add a new `EncounterEntry` to it.

```csharp
using XRL;
using XRL.World;
using XRL.World.WorldBuilders;
using XRL.World.ZoneBuilders;

namespace YourMod.YourNamespace
{
    [JoppaWorldBuilderExtension]
    public class YourJoppaWorldBuilderExtension : IJoppaWorldBuilderExtension
    {
        public override void OnAfterBuild(JoppaWorldBuilder builder)
        {
            var location = builder.popMutableLocationOfTerrain("Hills", centerOnly: false);
            var zoneID = builder.ZoneIDFromXY("JoppaWorld", location.X, location.Y);

            // Change these parameters as appropriate for the secret that you're
            // adding.
            // - The second parameter affects how the secret appears in the journal
            // - The third parameter affects which factions will sell the secret.
            // - The fourth parameter affects the category under which the secret
            //   shows up in the journal.
            // - The fifth parameter is the ID for the secret, which can be revealed
            //   using e.g. the revealsecret wish.
            var secret = builder.AddSecret(
                zoneID,
                "the location of Secret Creature",
                new string[2] { "lair", "robot" },
                "Lairs",
                "$myname_mymod_mysecret"
            );

            // Add zone builders to the zone.
            //
            // Each zone builder has a different priority (the integer parameter that
            // is passed in). Builders with lower priority run before builders with
            // higher priority.
            var zoneManager = The.ZoneManager;

            // Add some roads to the north and south
            zoneManager.AddZoneBuilder(zoneID, ZoneBuilderPriority.LATE, nameof(RoadNorthMouth));
            zoneManager.AddZoneBuilder(zoneID, ZoneBuilderPriority.LATE, nameof(RoadSouthMouth));

            // Add an object to the zone
            // Replace "Oboroqoru" with the object ID of the creature you want to add
            var creature = GameObject.Create("Oboroqoru");
            zoneManager.AddZonePostBuilder(zoneID, nameof(AddObjectBuilder), "Object", zoneManager.CacheObject(creature));

            // You can also set various properties on the zone, if you wish.
            zoneManager.SetZoneName(zoneID, "lair of My Creature", Article: "the", Proper: true);
            zoneManager.SetZoneIncludeStratumInZoneDisplay(zoneID, false);
            zoneManager.SetZoneProperty(zoneID, "NoBiomes", "Yes");
        }
    }
}
```

### 1.13 Disabling the world map, and making smaller world maps

You can disable world maps altogether by adding either the `"inside"` or `"SpecialUpMessage"` property to your zone. Ascending in a zone marked `"inside"` simply goes up to the next Z-level (used by interior zones and the Thin World); `"SpecialUpMessage"` is used by Tzimtzlum.

```csharp
// This gets called when creating a zone for which CanBuildZone == false
public override Zone GenerateZone(ZoneRequest Request, int Width, int Height) {
    var zone = new Zone(Width, Height);
    if (Request.ZoneID != null)
        The.ZoneManager.SetZoneProperty(Request.ZoneID, "inside", "1");
    return zone;
}

// This gets called after building zones for which CanBuildZone == true
public override void AfterBuildZone(Zone zone, ZoneManager zoneManager) {
    zone.SetZoneProperty("inside", "1");
    ZoneManager.PaintWalls(zone);
    ZoneManager.PaintWater(zone);
}
```

Smaller-than-80x25 world maps are a **known rough edge** — the wiki carries a "Missing Info" box: "The zero-width, zero-height zone hack in the code snippet below may have deficiencies that have not been observed yet. The game does not natively use world maps with dimensions < 80 x 25". Two techniques: place `InteriorVoid` objects as impassable/opaque world-map walls, and return `new Zone(0, 0)` when the player tries to cross the world boundary:

```csharp
public override bool CanBuildZone(ZoneRequest Request) {
    // Use BuildZone for the world map and for zones outside of the map's
    // boundaries.
    return Request.IsWorldZone
        || Request.WorldX < 37
        || Request.WorldX > 43
        || Request.WorldY < 10
        || Request.WorldY > 14;
}
```

### 1.14 Procedural generation primitives (`Modding:Zone Procedural Generation`)

- **`FastNoise`** — generic noise sampling: `SetSeed`, `SetNoiseType(FastNoise.NoiseType.SimplexFractal | Cellular)`, `SetFrequency`, `SetFractalType(FastNoise.FractalType.FBM)`, `SetFractalOctaves`, `SetFractalLacunarity`, `SetFractalGain`, `GetNoise(x, y, z)`; cellular extras `SetCellularDistanceFunction`, `SetCellularReturnType`, `SetCellularJitter`. Used by `Strata`, `MoonStair`, `RiverBuilder`, and the `CrystalGrassy` / `CrystalDirty` widgets.
- Zone-builder example that uses the parasang-relative coordinates: `int dX = (3 * Z.wX + Z.X) * 80; int dY = (3 * Z.wY + Z.Y) * 25;` — i.e. `Z.wX`/`Z.wY` are the parasang coordinates and `Z.X`/`Z.Y` the coordinates within the parasang.
- **`NoiseMap`** (`XRL.World.ZoneBuilders.Utility`) — a single algorithm: a 2-D grid of random values iteratively convolved with the 3x3 filter `{{1,3,1},{3,6,3},{1,3,1}}`. Constructor parameters in the wiki's example: `Z.Width, Z.Height, MaxDepth, SectorsWide, SectorsHigh, SeedsPerSector, MinSeedDepth, MaxSeedDepth, BaseNoise, FilterPasses, BorderWidth, CutoffDepth, nodes` where `nodes` is a `List<NoiseMapNode>`. (The page notes `MaxDepth` "doesn't actually do anything".)
- **Wave function collapse** — takes a colormap `.png` from the game's **`wavetemplates/`** directory (or a template inside your mod) and returns a locally-similar colormap. API: `new WaveCollapseFastModel(Template, 3, 40, 20, true, false, 8, 0)`, `wfcModel.Run(Stat.Random(int.MinValue, int.MaxValue), 0)`, `new ColorOutputMap(wfcModel)`, then per-pixel matching against `ColorOutputMap.BLACK / RED / MAGENTA / GREEN / BLUE / YELLOW`. Used for Tomb of the Eaters crypts and gardens, historic-site structures, and initial village structures.

### 1.15 Population tables (`Modding:Populations`)

Two systems: the legacy `EncounterTables.xml` and the preferred `ZoneTemplates.xml` + `PopulationTables.xml`.

- Both support `Load="Merge"`, which appends entries to the specified table instead of overwriting it.
- For populations, nested groups also need `Load="Merge"`:

```xml
  <population Name="GenericLairOwner" Load="Merge">
    <group Name="Options" Load="Merge">  <!-- Add the Load="Merge" attribute to each nested group, mimicking the base game's PopulationTables.xml structure -->
      <object Blueprint="WristbladeMerchant" Weight="20" /> <!-- Insert your new item! -->
    </group>
  </population>
```

- Encounter table merge:

```xml
<encountertables>
  <encountertable Name="Ammo 1" Load="Merge">
    <objects>
      <object Chance="100" Number="3d6" Blueprint="My New Ammo"></object>
    </objects>
  </encountertable>
</encountertables>
```

- Dynamic tables: `DynamicObjectsTable` (from `<tag Name="DynamicObjectsTable:Baboons" />` on objects), `DynamicInheritsTable` (all objects inheriting a blueprint; supports tier weighting, e.g. `DynamicInheritsTable:BaseLongBlade:Tier5` → weight 1000 at tier, 100 one tier away, 10 two tiers away, 1 otherwise), `DynamicSemanticTable` (intersection of `<stag Name="..."/>` categories; `DynamicSemanticTable:Medical,Furniture:4:6` weights by tier/tech tier near 4/6).
- Opt out of all dynamic tables with `<tag Name="ExcludeFromDynamicEncounters" />`.
- Debug wishes: `population:findblueprint:<blueprint>` and `population:generate:<table>#<amount>` (the latter accepts `DynamicObjectsTable`, `DynamicInheritsTable`, `DynamicSemanticTable` prefixes).
- Table entries seen in examples: `<group Name= Style="pickeach|pickone">`, `<object Blueprint= Number= Chance= Weight= Hint= />`, `<table Name= Number= Chance= Weight= />`, plus `{zonetier}` interpolation inside table names (`<table Name="Junk {zonetier}" Chance="15" />`).

### 1.16 Not documented / stubs on these pages

- **`Modding:Zone Templates` does not exist** — the intro page links it as a red link (`action=edit&redlink=1`). Zone templates are referenced only indirectly (e.g. `<postbuilder Class="ZoneTemplate:MoonStair">`).
- No `<region>` tag anywhere in the documented `Worlds.xml` schema.
- The tag-name table for `Worlds.xml` gives no required/optional attribute list; attribute names are only observable from examples.

---

## 2. Maps

### 2.1 `.rpm` file format

From `Modding:Maps`:

> RPM files are stores of static map content. They are a simple XML format. You can create your own RPM files, or "patch" the ones shipped with the game in your mod folders by using the same filename for the .rpm file.
>
> The in-game map editor loads and saves files in the .rpm XML format. It supports saving complete maps with all of the 80x25 cells included in the XML.

An RPM can represent one zone, but is also used for the world map (`QudWorldMap.rpm` — the world `JoppaWorld`'s `Map` value).

### 2.2 Map identity resolution (`ID`)

> Map files merge together and are resolved by their `ID`. If no explicit `ID` attribute is defined on the root element of the map, one is generated from the name and relative path of the RPM file. Separators, letter casing, and file extensions are ignored (`XRL.EditorFormats.Map.MapFile.GetKey`).

Documented examples:

- `StreamingAssets/Base/GritGate.rpm` → `GritGate`
- `StreamingAssets/Base/preset_tile_chunks/JoppaSultanShrine_3x3.rpm` → `preset_tile_chunks_JoppaSultanShrine_3x3`
- `YourRootModDirectory/RPM/MyMapFile.rpm` → `RPM_MyMapFile`

### 2.3 Merging into an existing map (the "Two Ctesiphus" example)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Map ID="Joppa" Width="80" Height="25" Load="Merge">
  <cell X="41" Y="7">
    <object Name="Ctesiphus" />
  </cell>
</Map>
```

`Load="Merge"` "tells the map reader to append the content to the cell without deleting what is already there (a floor tile, or other object for instance)."

### 2.4 Replacing a world-map terrain tile

```xml
<?xml version="1.0" encoding="utf-8"?>
<Map ID="QudWorldMap" Width="80" Height="25">
  <cell X="12" Y="22">
    <object Name="My_TerrainEastJoppa" />
  </cell>
</Map>
```

The world map expects only one terrain object per map cell; omitting `Load="Merge"` tells the game to **replace** the cell's contents. You then define the terrain object and attach a cell to it with `ApplyTo`:

```xml
<objects>
  <object Name="My_TerrainEastJoppa" Inherits="Terrain">
    <!-- sets a number or attributes such as the location of your custom tile (in a sub folder of your mod folder) and the colours to be applied to it -->
    <part Name="Render" 
      DisplayName="your display name here" 
      Tile="Terrain/yourcustomtile.png"  
      ColorString="&amp;G^k"  
      DetailColor="r" 
    /> 
    <part Name="Description" Short="your description here" />
    <tag Name="NoBiomes" Value="1" />
    <tag Name="OverlayColor" Value="&amp;W" />
  </object>
</objects>
```

```xml
<worlds>
  <world Name="JoppaWorld" Load="Merge">

    <!--The Mutable attribute sets whether or not procgen functions here -->
    <cell Name="some name" Inherits="WatervineCell" ApplyTo="My_TerrainEastJoppa" Mutable="false"> 
    
      <!-- this first part is borrowed from Joppa in this example -->
      <zone Level="10" x="0-2" y="0-2" Name="outskirts, Joppa" NameContext="Joppa">
        <builder Class="JoppaOutskirts" />
        <encounter Table="JoppaOutskirtsEncounters" Amount="minimum" />
      </zone>

      <zone Level="10" x="1" y="1" Name="SomeName" ProperName="true">
        <map FileName="Path/To/YourNewMap.rpm" /> <!-- put this map file somewhere in your mod folder -->
        <builder Class="Music" Track="MehmetsMorning" Chance="100" />
      </zone>
      <zone Level="10" x="1" y="2" Name="SomeOtherName" ProperName="true">
        <map ID="NewMapID" ClearBeforePlace="true" /> <!-- match this with the ID defined on your map file's root node -->
        <widget Blueprint="AmbientLight" />
      </zone>
      
      <!-- more zones such as underground could be added here -->

    </cell>

  </world>
</worlds>
```

### 2.5 The in-game Map Editor

Available as a utility from the main menu when the Overlay UI is enabled:

1. Start Qud
2. Enable the Overlay UI (Options > Overlay UI > Enable overlay user interface elements)
3. Enable mouse input (Options > Overlay UI > Allow mouse input) — "The map editor won't work if you skip this step"
4. Return to the main menu.
5. Click **Modding Utilities** in the lower right corner.
6. Click **Map Editor**.
7. Click **New Map** to start a new map design, or **Load Map** to load an existing RPM file.

Editing controls: search bar filters blueprints; **Ctrl+click** adds the selected item; **Alt+click** selects an existing item; click-and-drag moves the map; **Shift+click** flood fills; **Shift+drag+click** selects areas. With an area selected, the X button deletes all tiles of that type, and the yellow double-circle button replaces all tiles of that type with the palette tile. Holding **ctrl** while hovering a sidebar tile shows its full XML.

### 2.6 Where a mod's map file goes, and how the zone is told to use it

- Map files can live anywhere in the mod folder; the wiki's own example uses `YourRootModDirectory/RPM/MyMapFile.rpm` and `Path/To/YourNewMap.rpm`, and the 2025 Monster Mash example uses "RPM files in subdirectories using explicit ID attributes".
- The zone is told to use it in `Worlds.xml` in one of two ways:
  - `<map FileName="YdFreehold.rpm" />` / `<map FileName="Path/To/YourNewMap.rpm" />`, or
  - `<map ID="NewMapID" />` matching an explicit `ID` on the `.rpm` root element. Optional `ClearBeforePlace="true"`.
- Equivalently, in C#: `zone.loadMap("YdFreehold.rpm");`, or by adding the `MapBuilder` zone builder (`<builder Class="MapBuilder" FileName="..." Width="..." Height="..." ClearBeforePlace="true" />`).

### 2.7 Interior zones (`Modding:Interior Zones`)

**Definition:** "Interior zones are special zones that can be attached to objects and entered by using the object's `enter` action. They can be constructed purely using XML-based mods, and (with sufficient imagination) can simulate vehicles, buildings, and much, much more." In-game they are typically used with the `Vehicle` part (Golem; Templar mechs mk Ia/Ib/II). Mod examples: Hearthpyre (tents/tipis/yurts), Mycogrigoric Alcoves (purely-XML space outposts).

**Components needed to create an interior zone in-game:**

- a map file (`.rpm` created using the map editor) for the zone;
- a cell for the zone in the `Interior` world defined in `Worlds.xml`;
- an object that you want to attach the zone to; and
- an object (typically some kind of door, hatch, portal, etc.) that you have placed inside the zone that you will use as an exit.

**The `Interior` world declaration (verbatim from `Worlds.xml`):**

```xml
<worlds>
  <!-- ... -->
  <world Name="Interior" ZoneFactory="InteriorWorldZoneFactory" ZoneFactoryRegex="^Interior" DisplayName="Inside" Plane="Inherit" Protocol="Inherit">
    <!-- ... -->
    <cell Name="TempleMechaMkI">
      <boolproperty Name="JoinPartyLeaderPossible" Value="false" />
      <zone Level="10" x="1" y="1" Name="Control pit" NameContext="Temple mecha mk I" AmbientBed="sfx_endgame_golem_int_lp" IncludeStratumInZoneDisplay="false">
        <builder Class="InteriorGround" />
        <builder Class="MapBuilder" FileName="TempleMechaMkIInterior.rpm" Width="5" Height="3" ClearBeforePlace="true" />
        <widget Blueprint="AmbientLight" />
        <intproperty Name="AmbushChance" Value="10" />
      </zone>
    </cell>
    <!-- ... -->
  </world>
</worlds>
```

Note: **interior maps must start from the upper-left tile** (cell X=0, Y=0), "even if their dimensions are less than the normal 80 wide by 25 tall used by most zones".

**Attaching it to an object — the parts that make the Templar mech a vehicle:**

```xml
<object Name="VehicleTemplarMech" Inherits="BaseRobot">
  <!-- ... -->
  <part Name="Interior" Cell="TempleMechaMkI" FallDistance="1" />
  <part Name="Vehicle" ChargeMinimum="1000" Type="TemplarMech" Autonomous="false" IsEMPSensitive="true" IsTechScannable="true" BindBlueprint="Purple Security Card" />
  <part Name="VehiclePilotPopulation" Blueprint="Templar Squire,Gunner-Knight Templar" />
  <part Name="VehicleMeleeInfiltration" />
  <part Name="VehicleSocketSeal" />
  <!-- ... -->
</object>
```

- `Interior`: gives the object an interior zone using the named cell from `Worlds.xml`; `FallDistance="1"` makes creatures inside fall (and take fall damage) if the vehicle is destroyed.
- `Vehicle`: `Type` is inserted into the vehicle's `VehicleRecord` (used by the Reshaping nook); `BindBlueprint` is used by `VehicleSeat` to determine whether the player can pilot; `Autonomous="true"` allows operation without a pilot.
- `VehiclePilotPopulation`: default pilot; `Table="..."` may be used instead of `Blueprint`.
- `VehicleMeleeInfiltration`: allows players to infiltrate the vehicle.
- `VehicleSocketSeal`: prevents replacing the power cell unless the player owns the vehicle and it is unpiloted.

**Interior contents:** the exit is an object with the `InteriorPortal` part (`MechExitHatch` inherits `VehicleGolemExit`); the control mechanism is `MechPilotSeat` with `VehicleSeat`, plus optional `EjectionSeat` which **requires a `VehicleEjectionSlot` widget on the same cell**; a container object with `InteriorContainer` (`MechInteriorContainer`) gives the vehicle an inventory to trade from.

```xml
<cell X="3" Y="1">
  <object Name="VehicleEjectionSlot"></object>
  <object Name="MarbleFloor"></object>
  <object Name="MechPilotSeat">
    <intproperty Name="InteriorRequired" Value="2" />
  </object>
</cell>
```

**Interior weight handling** — three documented options:

1. Annotate each object in the interior map with the `InteriorRequired` int property:
```xml
<cell X="0" Y="0">
  <object Name="MechInteriorWall">
    <intproperty Name="InteriorRequired" Value="1" />
  </object>
</cell>
```
   Downside: no simple way to add this property to every object in a map outside of manually editing the `.rpm`.
2. Define near-copies of blueprints that carry the property:
```xml
<objects>
  <object Name="MechInteriorWall_InteriorRequired" Inherits="MechInteriorWall">
    <intproperty Name="InteriorRequired" Value="1" />
  </object>
</objects>
```
   Easier to work with from the map editor, but requires a lot of up-front duplication.
3. Set `IgnoreWeight` on the `Interior` part (added in **207.76**), which makes the vehicle *completely ignore* interior weight:
```xml
<part Name="Interior" Cell="TempleMechaMkI" FallDistance="1" IgnoreWeight="true" />
```

**Limitations:** "As of patch 207.82, multi-zone interiors are *not* supported by the game. If you wish to construct something similar to a multi-zone interior, you will need to mod in your own world." Nesting interiors inside one another *is* possible (a spaceship whose control room contains a door to the engine room's zone, etc.).

### 2.8 Placing a custom village (XML-only dynamic generation)

Not a dedicated page. `Modding:Intro - Zones and Worlds` documents the XML-only path for dynamic zone generation:

- `<builder Class="FactionEncounters" Population="GenericFactionPopulation">` for legendary/faction encounters.
- The `MapChunkPlacement` part: "This can be attached to a widget object to have it spawn objects from a map file when the widget is placed in a zone." The Mycogrigoric Alcoves design pattern is "to define many different widgets, each of which uses a different map chunk, and then spawn these widgets from a population table to randomly generate different maps."
- **Known bug (as of 207.80):** "`MapChunkPlacement` (and related parts, such as `DeployWith`) is bugged for interior zones" (issue #9582). Workarounds are described as "possible, albeit tedious".
- In-game wishes useful for authoring village-like content: `makevillage` ("Build a village in the current zone"), `villageprops` ("List properties of current village"), `testpets`, `rivertest`, `roadtest`.

---

## 3. Quests and conversations

### 3.1 `Quests.xml`

Each quest is broken down into:

- a top-level `<quest>` tag defining high-level properties, including the faction that assigned it, journal accomplishments added upon completing the quest, etc., and
- `<step>` nodes that define individual steps of the quest.

Verbatim example (the "O Glorious Shekhinah!" quest):

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

Fuller example with multiple steps, `Factions`, `Reputation`, `System`:

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

**Testing:** "Once a quest has been defined in `Quests.xml` it can be obtained in-game. You can use the `startquest:[quest name]` wish to artificially grant you the quest for testing purposes."

Documented `Quest`-related XML attributes: `Name` (the quest's ID, "usually the same as its display name"), `Level`, `System`, `Factions`, `Reputation`, `Accomplishment`, `Hagiograph`, `HagiographCategory`, and on `<step>`: `Name`, `XP`.

### 3.2 `IQuestSystem` (the C# quest driver)

"A common component for many (but not all) quests is custom, per-quest systems that advance a player's progression through the quest as they achieve various objectives. An `IQuestSystem` can be used to subscribe to many different events on the player... **By default, `IQuestSystems` are removed from the game once their attached quest is completed.**"

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

    // GetInfluencer is primarily used to determine who should be referenced when
    // naming an item. If the player successfully rolls an item naming opportunity
    // upon completing the quest, they will be able to name their item after the
    // culture of either Wardens Esther or Tszappur.
    public override GameObject GetInfluencer()
    {
        if (50.in100())
        {
            return GameObject.FindByBlueprint("Wardens Esther");
        }
        return GameObject.FindByBlueprint("Tszappur");
    }
}
```

Additional overridables shown in the second example: `RegisterPlayer(GameObject Player, IEventRegistrar Registrar)`, `HandleEvent(TookEvent E)`, `Start()`. Property `QuestID` is available on the system.

> **IQuestSystem versus QuestManager:** "`QuestManager` is the old interface that used to manage the progression of quests, and while code based on it is still present in Caves of Qud, it is no longer used. Most of the use cases for `QuestManager` can now be handled better through `IQuestSystem`."

Full example system:

```csharp
using System;

namespace XRL.World.Quests {
    [Serializable]
    public class HealingBalmsSystem : IQuestSystem {
        public virtual string AppleStepID => "Get a starapple";
        public virtual string JamStepID => "Create starapple jam";

        public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar) {
            Registrar.Register(TookEvent.ID);
        }

        public override bool HandleEvent(TookEvent E) {
            if (E.Item.Blueprint == "Starapple")
                The.Game.FinishQuestStep(QuestID, AppleStepID);
            if (E.Item.Blueprint == "Starapple Preserves")
                The.Game.FinishQuestStep(QuestID, AppleStepID);
            return base.HandleEvent(E);
        }


        public override GameObject GetInfluencer() {
            return GameObject.FindByBlueprint("Nima Ruda");
        }

        // When the quest first starts, we check whether the player already has
        // a starapple and/or starapple jam.
        public override void Start() {
            if (The.Player.Inventory.HasObject("Starapple"))
                The.Game.FinishQuestStep(QuestID, AppleStepID);
            if (The.Player.Inventory.HasObject("Starapple Preserves"))
                The.Game.FinishQuestStep(QuestID, JamStepID);
        }
    }
}
```

### 3.3 Starting, finishing and failing quests — XML-only vs. scripting

| Action | XML-only | C# |
| --- | --- | --- |
| Start a quest | `StartQuest` part generator in a conversation | `The.Game.StartQuest("A Canticle for Barathrum");` |
| Complete a step | `CompleteQuestStep` part generator; `QuestStepFinisher` part on a widget placed in a zone; `FinishQuestStepWhenSlain` part on a creature | `The.Game.FinishQuestStep(...)` |
| Fail a step | — | `The.Game.FailQuestStep(QuestName, QuestStep)` |
| Finish a quest | `FinishQuest` in a conversation | `The.Game.FinishQuest(QuestName)` |
| Fail a quest | — | `The.Game.FailQuest(QuestName)` |

Conversation-based examples (verbatim):

```xml
<node ID="CanticleAccept3">
  <text>                                    
    Here you are. Now, go! Off with you! May you live long enough to do my bidding. Away, away!
  </text>
  <choice GotoID="End" StartQuest="A Canticle for Barathrum">
    <text>Farewell, Argyve.</text>
    <part Name="ReceiveItem" Blueprints="Droid Scrambler,Argyve's Data Disk" Identify="All" />
  </choice>
</node> 
```

```xml
<node ID="PresentTheDisk">
  <text>
    Well done, =factionaddress:Barathrumites=. Present the disk.
  </text>
  <choice GotoID="InterpretSignal" CompleteQuestStep="Decoding the Signal~Return to Grit Gate|6000" FinishQuest="Decoding the Signal">
    <text>[Give Otho the disk]</text>
    <part Name="GritGateHandler" Rank="Journeyfriend" />
  </choice>
</node>
```

`FinishQuestStepWhenSlain` body (shows the `Clean`, `RequireQuest`, `GameState` pattern):

```csharp
using System;
namespace XRL.World.Parts;

[Serializable]
public class FinishQuestStepWhenSlain : IPart
{
    public string Quest;
    public string Step;
    public string GameState;
    public virtual bool Clean => true;

    // ...

    public virtual void Trigger()
    {
        if (GameState != null)
        {
            The.Game.SetIntGameState(GameState, 1);
        }
        if (!The.Game.TryGetQuest(this.Quest, out var Quest))
        {
            if (!RequireQuest)
            {
                return;
            }
            Quest = The.Game.StartQuest(this.Quest);
        }
        The.Game.FinishQuestStep(Quest, Step);
        if (Clean)
        {
            ParentObject.RemovePart(this);
        }
    }
}
```

Design note from the page: prefer `FinishQuest` over `CompleteQuestStep` on the delivery step of a fetch quest, "because some steps of the quest may never have been completed at that point in time."

### 3.4 Conversation XML

"Conversations are trees of XML loaded from `Conversations.xml` and usually executed from a `ConversationScript` part on a game object."

**Attaching a conversation to a creature** (this is the documented mechanism — `ConversationID` on the `ConversationScript` part must reference a `<conversation ID="...">` template):

```xml
<!-- ObjectBlueprints.xml-->
<objects>
  <object Name="Snapjaw Pal" Inherits="Snapjaw">
    <part Name="ConversationScript" ConversationID="FriendlySnapjaw" />
  </object>
</objects>
```

```xml
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

**Basic tags (verbatim table):**

| XML Tag | Description |
| --- | --- |
| `<conversation>` | Single conversation template typically containing `<node>` and `<start>` elements, linked to a `ConversationScript` via its `ID`. |
| `<node>` | Collection of `<text>` from the Speaker's point of view, along with a range of `<choice>` for the Player to respond with. Setting `AllowEscape="false"` prevents the player from exiting the dialogue window early. (`XRL.World.Conversations.Node`) |
| `<start>` | Special variant of `<node>` that can be selected when starting a conversation. For backwards compatibility, a `<node>` with an ID of "Start" will behave similarly. |
| `<choice>` | Collection of `<text>` from the Player's point of view, commonly defines a `Target` `<node>` to navigate to if selected. The `Target` attribute has two special values: `Start` and `End`, which will return to the beginning of the conversation or end it, respectively. For backwards compatibility, the `GotoID` attribute will behave similarly to `Target`. |
| `<text>` | Contains a block of text to display for an element, multiple of these can be defined and randomly selected from if valid. Additional text nodes can be recursively defined within other text nodes, allowing groups of text to use the same conditions. For backwards compatibility, delimiting the text with `~` characters will behave similarly to multiple text nodes. |
| `<part>` | Reference to a C# class that inherits from `IConversationPart`. Any attributes defined here will be inserted into the fields & properties of the part, if possible. Anything defined as a child element of the part can be loaded with custom C# behavior. |

> **Note on the task brief's tag list:** the tags `<setvar>`, `<if>`, `<wish>`, and `<goto>` are **not** documented on the current `Modding:Conversations` page. The modern equivalents are:
> - `<goto>` → `Target` (with legacy `GotoID` accepted for backwards compatibility);
> - `<if>` → predicate *delegates* as XML attributes (e.g. `IfHaveQuest="..."`, `IfReputationAtLeast="Loved"`), or `<part Name="ChangeTarget" .../>` / `IPredicatePart` subclasses;
> - `<setvar>` → the state-setting action delegates (`SetStringState`, `SetIntState`, `SetBooleanState`, `SetIntConversationState`, …) and `SetStringProperty` / `SetIntProperty`;
> - `<wish>` → no documented conversation tag; testing custom content is done via the wish console (`conv:<id>`, `convnode:<id>:<startnode>[:<speakerblueprint>]`, `startquest:<quest name>`, etc.), and `FireEvent` / `IfCommand` are used to drive custom behavior.

**Merging:** "If multiple elements with the same `ID` are defined within the same scope, a merge will occur by default where the properties of the latter element overwrite those of the former. If an explicit ID isn't defined, one will be created based on other attributes. You can alter the conflict behavior of an element by setting a `Load` attribute with valid values of: "Merge", "Replace", "Add", or "Remove"."

```xml
<conversation ID="FriendlySnapjaw">
  <node ID="SnappyNoise">
    <text>gnnnnnnn.</text> <!-- ID is "Text" -->
    <text>beh. mmmf.</text> <!-- ID is "Text2" -->
    <text>mmnnn!</text> <!-- ID is "Text3" -->
    <choice Target="LibDink">Thank you.</choice>  <!-- ID is "LibDinkChoice" -->
  </node>
</conversation>

<conversation ID="FriendlySnapjaw"> <!-- Will merge with above conversation -->
  <node ID="SnappyNoise">  <!-- Will merge with "SnappyNoise" node -->
    <text>gra! gra! gra!</text> <!-- ID is "Text" and will merge -->
    <text Cardinal="3">gra! gra! gra!</text> <!-- ID is "Text3" and will merge -->
    <choice Target="End">Live and drink.</choice> <!-- ID is "EndChoice" and will not merge -->
  </node>
</conversation>
```

**Inheritance:**

```xml
<conversation ID="FriendlySnapjaw">
  <start ID="SnappyNoise">
    <text>gnnnnnnn.</text>
    <choice Target="LibDink">Thank you.</choice>
  </start>
</conversation>

<conversation ID="ExcitedSnapjaw" Inherits="FriendlySnapjaw"> <!-- Inherits SnappyNoise -->
  <node ID="SnappyBye">
    <text>gra! gra! gra!</text>
    <choice Target="End">Live and drink.</choice>
  </node>
</conversation>

<conversation ID="AngryArconaut">
  <start ID="Grumpy">
    <text>I hate things.</text>
    <choice Inherits="ExcitedSnapjaw.SnappyBye.EndChoice" /> <!-- Inherits "Live and drink." -->
  </start>
</conversation>
```

"By default, every conversation inherits from `BaseConversation`, which holds the definitions of common elements to all conversations like trade and the water ritual." `Inherits` accepts a comma-separated list; the current element takes precedence over what it inherits from.

**Distribution:**

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

**Delegates** — "Predicates which control whether an element is accessible, and Actions which perform some task when the element is selected. After the Deep Jungle update these are now for the most part agnostic as to what their parent element is."

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

Naming convention: an inverse predicate can be invoked with `IfNot` (e.g. `IfNotHaveItem`); a Speaker delegate with `IfSpeaker`/`SetSpeaker`; both combined give `IfSpeakerNot`. Predicates and actions accept logic expressions with parentheses and `AND`, `OR`, `NOT`, e.g. `IfHaveActiveQuest="(Quest1 AND Quest2)"`.

**Custom delegates:**

```csharp
[HasConversationDelegate] // This is required on the surrounding class to reduce the search complexity.
public static class DelegateContainer
{
    // A predicate that receives a DelegateContext object with our values assigned, this to protect mods from signature breaks.
    [ConversationDelegate(Speaker = true)]
    public static bool IfHaveItem(DelegateContext Context)
    {
        // Context.Value holds the quoted value from the XML attribute.
        // Context.Target holds the game object.
        // Context.Element holds the parent element.
        return Context.Target.HasObjectInInventory(Context.Value);
    }
}
```

**Full delegate catalogue (as documented):**

*Predicates:* `IfHaveQuest`, `IfHaveActiveQuest`, `IfFinishedQuest`, `IfFinishedQuestStep` (value format `"Quest ID~Step ID"`), `IfHaveObservation`, `IfHaveObservationWithTag`, `IfHaveSultanNoteWithTag`, `IfHaveVillageNote`, `IfHaveState`, `IfTestState` (format `"ID Operator Value"`), `IfHaveConversationState`, `IfHaveText`, `IfLastChoice`, `IfCommand`, `IfReputationAtLeast` (`Loved`/`Liked`/`Indifferent`/`Disliked`/`Hated`), `IfTime` (time ticks or ranges like `"325-1000"`), `IfLedBy` (`"*"`, `"Player"`, or a blueprint ID), `IfZoneHaveObject`, `IfZoneID` (prefix match, e.g. `"JoppaWorld.5.2"` for the whole Stilt), `IfZoneName`, `IfZoneLevel` (`"10-15"`), `IfZoneTier`, `IfZoneWorld`, `IfUnderstood`, `IfIn100`, `IfGenotype`, `IfSubtype`, `IfTrueKin`, `IfMutant`, `IfHaveItem`, `IfHaveItemDescendsFrom`, `IfWearingBlueprint`, `IfHaveBlueprint`, `IfHavePart`, `IfHaveTag`, `IfHaveProperty`, `IfHaveTagOrProperty`, `IfHaveLiquid` (`"water"` or `"sludge:64"`), `IfLevelLessOrEqual`.

*Actions:* `AwardXP` (prefix `!` suppresses the XP popup), `FinishQuest`, `FireEvent`, `FireSystemsEvent`, `SetStringState`, `SetIntState`, `AddIntState`, `SetBooleanState`, `ToggleBooleanState`, `SetStringProperty`, `SetIntProperty`, `SetStringConversationState`, `SetIntConversationState`, `SetBooleanConversationState`, `RevealObservation`, `RevealMapNote`, `GiveLiquid`, `UseLiquid`, `SetLeader`, `Notify`.

*Part generators:* `StartQuest` (adds a `QuestHandler` part with `Start`), `CompleteQuestStep` (adds `QuestHandler` with `Step` and `"QuestID~StepID"`), `GiveItem` (adds `ReceiveItem`), `TakeItem`.

**Parts (`IConversationPart`):** `AddSlynthCandidate`, `ChangeTarget`, `GiveArtifact`, `GiveReshephSecret`, `IPredicatePart`, `LibrarianGiveBook`, `PaxInfectLimb`, `QuestHandler`, `ReceiveItem`, `RequireReputation`, `SpiceContext`, `Tag`, `TakeItem`, `TextFilter`, `TextInsert`, `Trade`, `VillageContext`, `WaterRitualRandomMutation`.

Notable part parameters:

- `QuestHandler`: `QuestID`, `StepID`, `XP`, `Action` ∈ {`Start`, `Step`, `Finish`, `Complete`}. Example: `<part Name="QuestHandler" Action="Step" QuestID="Fetch Argyve a Knickknack" StepID="Return to Argyve" XP="75" />`
- `ReceiveItem`: `Blueprints` (comma separated), `Identify` (comma separated, or `*`/`All`), `Mods` (dice roll), `Pick`, `FromSpeaker`.
- `TakeItem`: `Blueprints`, `IDs`, `Amount` (dice roll or `*`/`All`), `Unsellable`, `ClearQuest`, `Destroy`.
- `RequireReputation`: `Faction`, `Level`.
- `TextFilter`: `FilterID` ∈ {`Angry`, `Corvid`, `WaterBird`, `Fish`, `Frog`, `Leet`, `Lallated`, `Weird`, `Cryptic Machine`}, `Extras`, `ProtectFormatting`.
- `TextInsert`: `Prepend`, `Spoken`, `NewLines`.
- `Tag`: `<part Name="Tag">{{g|[begin trade]}}</part>`
- `WaterRitualRandomMutation`: `Category` (base game: `Physical`, `Mental`).

**Parts in your own namespace:**

```xml
<conversation ID="JoppaZealot">
  <part Name="SpiceContext" />
  <start ID="OrphanOfTheSalt">
    <text>
      Blah! Orphan of the salt! Blooh!
      <part Name="TextInsert" Spoken="false" NewLines="2" Text="[Press Tab or T to open trade]" />
    </text>
    <choice Target="End">
      <text>You intrigue me. I will go to the Six Day Stilt for no particular reason.</text>
      <part Name="QuestHandler" QuestID="O Glorious Shekhinah!" Action="Start" />
    </choice>
  </start>
</conversation>
```

"If you use a period within the part's name, it's assumed you are specifying your own namespace and won't be required to place your part within `XRL.World.Conversations.Parts`. You can optionally declare a `Namespace` on the root `<conversations>` element, and concatenated sub-namespaces on each `<conversation>`."

Minimal conversation part:

```csharp
public class SnapjawLaugh : IConversationPart
{
    public override bool WantEvent(int ID, int Propagation)
    {
        return base.WantEvent(ID, Propagation)
               || ID == PrepareTextEvent.ID
            ;
    }

    public override bool HandleEvent(PrepareTextEvent E)
    {
        E.Text.Append("\n\nehehehehe!");
        return base.HandleEvent(E);
    }
}
```

**Conversation events** propagate *up* the element tree (event bubbling), and propagation is separated by perspective (Speaker vs. Listener). Documented events: `IsElementVisibleEvent`, `GetTextElementEvent`, `PrepareTextEvent`, `DisplayTextEvent`, `ColorTextEvent`, `GetChoiceTagEvent`, `EnteredElementEvent`, `EnterElementEvent`, `GetTargetElementEvent`, `LeaveElementEvent`, `LeftElementEvent`, `HideElementEvent`, `PredicateEvent`. Default registration perspective can be overridden:

```xml
<conversation ID="EventfulSnapjaw">
  <part Name="SpiceContext" Register="All" /> <!-- Registers for Speaker events by default, but overrides with both -->
  <start ID="TasterOfTheSalt">
    <part Name="SnapjawLaugh" /> <!-- Registers for Speaker events -->
    <text>mmmg. salt.</text>
    <text>tasty.</text>
    <choice Target="End">
      <text>Salt responsibly, friend.</text>
      <part Name="ReceiveItem" Blueprints="EmptyWaterskin" /> <!-- Registers for Listener events -->
    </choice>
  </start>
</conversation>
```

**Choice ordering:** `Priority` (integer; higher appears closer to the top; default 0), `Before="WaterRitualChoice"`, `After="WaterRitualChoice"`.

**Production tip from the page:** "For extensive conversation design in mods that use a lot of conversations, some modders have recommended using a tool such as Twine to map out your conversation logic."

---

## 4. Mutations, liquids, sounds, wishes, pets, genotypes

### 4.1 Mutations

**XML surface (`Mutations.xml`)** — "First, include a mutations.xml in your mod that defines a new mutation."

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mutations>
  <category Name="Physical">
    <mutation Name="Udder" Cost="1" MaxSelected="1" Class="FreeholdTutorial_Udder" Exclusions="" Code="ea"></mutation>
 </category>
</mutations>
```

A second, gas-generating example (XML-only mutation, no new C# at all):

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mutations>
  <category Name="Physical">
    <mutation Name="Confusion Gas Generation" Cost="2" MaxSelected="1" Class="GasGeneration" Constructor="ConfusionGas" Exclusions="" BearerDescription="those who expel confusion gas" Code="zz"></mutation>
  </category>
</mutations>
```

`<mutation>` elements (verbatim list from the page): **Name**, **Class**, **Cost**, **MaxSelected**, **Constructor** *(optional)*, **Exclusions** *(optional)*, **BearerDescription**, **Code**.

- **Name** — name of the mutation as it appears in the character creation screen.
- **Class** — the name of the .cs Class object used to instantiate your mutation object.
- **Cost** — mutation points when selected during character creation.
- **MaxSelected** — max copies selectable at character creation. "Currently this is only used for Unstable Mutation... It is not clear if this value can be set to more than 1 for a modded mutation without causing some problems."
- **Constructor** — a string (or comma-delimited string) passed to the mutation class constructor; e.g. GasGeneration is shared between Corrosive Gas Generation and Sleep Gas Generation with different constructor args. The page carries a `{{Missing info|Remove obsolete info (e.g., Constructor field)...}}` banner, so treat `Constructor` as possibly obsolete.
- **Exclusions** — mutations considered mutually exclusive, e.g.:

```xml
<mutation Name="Stinger (Confusing Venom)" Cost="3" MaxSelected="1" Class="Stinger" Constructor="Confuse" Exclusions="Stinger (Paralyzing Venom),Stinger (Poisoning Venom),Wings" BearerDescription="those with stingers tipped with confusing venom" Code="bx"></mutation>
```

- **BearerDescription** — used by random generation algorithms for villages and history (e.g. "the many-armed").
- **Code** — used when constructing a Build Library code. "It is unclear if there is really a 'best practice' for codes. Probably one should avoid using the codes used by base game mutations, but conflict with other mods may be inevitable. It would appear that this code can be longer than 2 characters, but that is an untested hypothesis."

**What requires C#:** everything behavioural. The class must be in namespace `XRL.World.Parts.Mutation`, marked `[Serializable]`, and ultimately descend from `BaseMutation` (not necessarily directly).

```csharp
using System;
using System.Collections.Generic;
using System.Text;

using XRL.Rules;
using XRL.Messages;
using ConsoleLib.Console;

namespace XRL.World.Parts.Mutation
{
    [Serializable]
    class FreeholdTutorial_Udder : BaseMutation
    {
        public override void Register(GameObject Object)
        {
        }

        public override string GetDescription()
        {
            return "";
        }

        public override string GetLevelText(int Level)
        {
            string Ret = "You have udders.\n";
            return Ret;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == BeforeRenderEvent.ID;
        }

        public override bool HandleEvent(BeforeRenderEvent e)
        {
            if (ParentObject.IsPlayer())
            {
                if (ParentObject.pPhysics != null && ParentObject.pPhysics.CurrentCell != null)
                {
                    ParentObject.pPhysics.CurrentCell.ParentZone.AddLight(ParentObject.pPhysics.CurrentCell.X, ParentObject.pPhysics.CurrentCell.Y, Level, LightLevel.Darkvision);
                }
            }
            return true;
        }

        public override bool FireEvent(Event E)
        {
            return base.FireEvent(E);
        }

        public override bool ChangeLevel(int NewLevel)
        {
            return true;
        }

        public override bool Mutate(GameObject GO, int Level)
        {
            return true;
        }

        public override bool Unmutate(GameObject GO)
        {
            return true;
        }
    }
}
```

Documented `BaseMutation` members and their roles:

- `GetDescription()` and `GetLevelText(int Level)` — "called to generate the descriptive for a given level of the mutation".
- `ChangeLevel(int NewLevel)` — "called any time the mutation changes level".
- `Mutate(GameObject GO, int Level)` / `Unmutate(GameObject GO)` — "called on an object when it gains or loses the mutation".
- `Register(GameObject Object)` / `FireEvent(Event E)` — "BaseMutation derives from Part, so the typical event registration and handling functions are available".

The page reproduces the full decompiled `FlamingHands` mutation as a reference implementation (activated ability registration via `AddMyActivatedAbility("Flaming Hands", "CommandFlamingHands", "Physical Mutation", ...)`, AI usability via `AIGetOffensiveMutationList`, equipment generation via `GeneratesEquipment()` + `CommandForceEquipObject`, `CleanUpMutationEquipment`, `ComputeDamage`). `Modding:Overview` summarizes: "mutation | no | `Modding:Mutations` | There's an XML interface, but it covers only some properties."

### 4.2 Liquids

`Modding:Overview` states flatly: "liquid | **no** | `Modding:Liquids` | **There's no XML interface at all, but one is incoming on the `lang-experimental` branch.**"

`Modding:Liquids` (a stub):

> A new liquid is implemented by a C# class that inherits from `XRL.Liquids.BaseLiquid` and has the `XRL.World.Parts.IsLiquid` attribute.

```csharp
using XRL.Liquids;
using XRL.World.Parts;

[IsLiquid]
public class MyLiquid : BaseLiquid {
    public MyLiquid () : base ("myliquid") {}
}
```

"This creates a liquid with largely uninteresting properties whose internal identifier is `myliquid`. The easiest way is to see it in-game is to spawn it in some container":

```xml
<objects>
    <object Name="MyLiquidPool" Inherits="Water">
        <part Name="LiquidVolume" MaxVolume="-1" Volume="10" StartVolume="10d10" InitialLiquid="myliquid-1000"></part>
    </object>
</objects>
```

"Now you can wish it into existence with `MyLiquidPool`."

**Undocumented on this page:** "Modifying an existing liquid" — "This section has yet to be written." "Properties that a liquid can implement" — "This section has yet to be written."

### 4.3 Sounds

> Sounds can be added in the `/sounds` folder of a mod. Supported types: wav, mp3, aiff, ogg

Windows example path for a mod called "MyCoolMod" (note the capitalized `Sounds\` in the path, vs. lowercase in the prose):

```
C:\Users\Brian\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\MyCoolMod\Sounds\mysound1.wav
```

Exposure points: "Sounds are currently exposed on the `AmbientSoundGenerator` part, `OpenSound` and `CloseSound` tag of the `Door` part and `ReloadSound` tag of the `MagazineAmmoLoader` part."

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
   <object Name="Desert Rifle" Load="Merge">
       <tag Name="ReloadSound" Value="mysound1"></tag>
   </object>
</objects>
```

```xml
<object Name="Argyve" Load="Merge">
  <part Name="AmbientSoundGenerator" Sounds="Clink1,Clink2,Clink3,Spark1,Spark2"></part>
</object>
```

Supported sound tags: `ReloadSound` (MagazineAmmoLoader on gun reload), `OpenSound` (Door part, open), `CloseSound` (Door part, close).

Other sound file names documented on the page (base-game assets used as defaults): `SplashStep1` (walking through a liquid), `WoodDoorOpen`, `WoodDoorClose`, `FirearmReload`, `sub_bass_mouseover1` (default button mouseover), `Clink1,Clink2,Clink3,Spark1,Spark2` (Argyve's hut), `Chop1,Chop2` (watervine farmer).

**Music** is added via the `<music Track="..."/>` zone tag / `Music` zone builder (`<builder Class="Music" Track="MehmetsMorning" Chance="100" />`), and zone ambience via the `AmbientBed` attribute (`AmbientBed="Sounds/Ambiences/amb_bed_moonstair"`). Note the full asset path style used for ambiences vs. the bare filename style used for `mysound1`. Music/SFX are classed as data-mod-able in the overview table: "music/SFX | yes | (no page) | These can be added to location and item data, respectively."

### 4.4 Wishes

> To create a wish, you define a `WishCommand` attribute on a public method. This method should either be void or bool. **The enclosing class must also have the `HasWishCommand` attribute**

```csharp
using XRL.Wish;

[HasWishCommand]
public class MyWishHandler
{
  // Handles "testwish:foo" or "testwish foo" as a wish command
  [WishCommand(Command = "testwish")]
  public static bool TestWishHandler(string rest)
  {
     Popup.Show("Matched: " + rest);
     // if we dont return true, other wishes will also parse this wish message
     return true;
  }

  // Handles "testwish" with nothing else! (no string param)
  [WishCommand(Command = "testwish")]
  public static void TestWishHandler()
  {
     Popup.Show("Matched it the short way");
     // if we return void, it assumes we handled it
  }

  // showing of non-static also!
  public int count = 0;

  // no command -- uses the method name by default!
  [WishCommand]
  public void inc()
  {
    Popup.Show(count++);
  }

  [WishCommand]
  public void dec()
  {
    Popup.Show(count--);
  }

  [WishCommand(Regex = @"other fancy match \d things"]
  public void Handle(System.Text.RegularExpression.Match match)
  {
    Popup.Show(match.Groups[0].ToString());
  }

}
```

*(The last attribute line is quoted exactly as it appears on the wiki; it is missing a closing parenthesis in the source page — treat it as a wiki typo, not a syntax guide.)*

"The regular expression passed to the attribute is parsed using **case insensitive** matching."

**Exact attribute spelling to quote:** `[HasWishCommand]` on the class; `[WishCommand]` / `[WishCommand(Command = "...")]` / `[WishCommand(Regex = @"...")]` on the method. There is no `[WishCommand]`-style class attribute other than `HasWishCommand`.

**Testing content in-game:** wish console is bound by default to **Ctrl+W**; rebind via Key Mapping → `Debug` section → `Wish`. Usage notes from the `Wishes` page:

- "Most wish commands are strictly case-sensitive." Example: `dismember:Tail` dismembers your tail, `Dismember:Tail` spawns a schematics drafter.
- Unmatched input triggers a **Levenshtein-distance** lexicographic search over object blueprints and quests (`XRL.Wish.WishSearcher`, method `SearchForWish`); matching an object blueprint spawns it, matching a quest starts it. (The spawning section also lists mutations and zones as search targets: object → spawn, mutation → grant, quest → start, zone → teleport.)
- "A very common problem that occurs when a wish goes wrong is that your character will gain an undesirable mutation with a name that is somewhat similar to your wish command. If this happens to you, use the `mutationbgone` wish."
- "**A note for modders:** Unless otherwise mentioned, the logic for each wish is handled in the game's `Wishing` class." (`XRL.World.Capabilities.Wishing`)
- `reload` — "Reload game files … This reloads the game assets, such as `ObjectBlueprints.xml`, from all active mods. Almost everything is reloaded, but some things are not: Object tile images will not update until the game client is reopened. **Changes to `Worlds.xml` will not affect an existing save. A new one must be started.**"

**Built-in wish list (mod-relevant extracts)** — full list at `Wishes`; the categories and the entries most useful for testing world/zone/content mods:

- *Special modes:* `calm` (turn off AI), `cool` (turn off cooldowns), `idkfa` (god mode). `ExploreZone` mode is not accessible through a wish (bind it via Key Mapping instead).
- *Going places:* `blink`, `crossintobright`, `godown:<n>`, `gates`, `goclam`, `go:<specifier>` (`shuglair`, `shug`, `agolgot`, `rermadon`, `qas`, `qon`, `qasqon`), `goto:<zone ID>` (e.g. `goto:JoppaWorld.11.22.1.1.10`), `gosecret:<secret ID>` (e.g. `gosecret:$beylah`), `hydropon`, `returntoqud`, `sultantomb1`, `sultantomb6`, `thinworld`, `thinworldx`, `where?`, `where`, `xy`, `zone:<id>`.
- *Quests:* `questdebug`, `startquest:<quest name>`, `quest:<quest name>`, `finishqueststep:<quest name>:<step name>`, `completequest:<quest name>`, `stage2`…`stage11`, `postgolem`, `startbattle`, `starfreight:spindle`, `sherlock`, `hindrenawardtest`, `finishallquests`, `othowander1`, `markofdeath`, `markofdeath?`.
- *Journal secrets:* `filljournal`, `reshephgospel`, `reveal1sultanhistory`, `revealobservations`, `sultanhistory`, `sultanreveal`, `villagereveal`, `revealsettlements`, `revealmapnotes`, `revealsecret:<secret id>` — with a table of predetermined secret IDs (`$beylah`, `$skrefcorpse`, `$glowpadmerchant`, `$ruinofhouseisner`, `~kindrish`, `$recomingnook`, `$mamonvillage`, `$oboroqorulair`, `$hydropon`, `$agolgotlair`, `$shugruithlair`, `$shugruithmouth`, `$rermadonlair`, `$bethsaidalair`, `$qasqonlair`, `$greatmachineartifact`).
- *Spawning things:* `allbox`, `bits[:<amount>]`, `clone`, `cryotube`, `datadisk`, `factionencounter:<FactionName>` (only "properly" works with factions defined in `GenericFactionPopulation`), `implant:...`, `item:<blueprint>:<number>`, `makevillage`, `maxmod`, `minime`/`eviltwin`, `modify:<blueprint>:<mod>`, `object:<id>`, `placeobjecttest:<ObjectID>` (exact, case-sensitive; spawns 200), `playerlevelmob`, **`population:findblueprint:<blueprint>`**, **`population:generate:<table>#<amount>`**, `randomitems`, `randomrelic:<n>`, `relic`, **`rivertest`**, **`roadtest`**, `slynthasterisk`, `smartitem:<id>`, `spawn:<object>`, `sultanrelics`, `sultantest:<attributes>`, `testhero:<base type>`.
- *Removing things:* `destroy`/`obliterate`, `deathgeno:<name>`, `geno:<name>`.
- *Conversations:* `conv:<id>[:<speakerblueprint>]`, `convnode:<id>:<startnode>[:<speakerblueprint>]`.
- *Miscellaneous (authoring/debug):* `a:<word>`, `auditblueprints`, `beguile`, `bodyparttypes`, `bpxml:<blueprint id>`, `copy`, `day`, `dismiss`, `dude`, `factionsheeter`, `find:<blueprint>`, `findduplicaterecipes`, `groundliquid`, `hasblueprintbeenseen:<blueprint>`, `mazetest`, `night`, `objdump`, `pluralize:<word>`, `powergrid` / `powergriddebug` / `powergridruin`, `purgeobjectcache!`, **`rebuild`** (rebuild the current zone), `rebuildbody:<asAnatomy>`, `regionalize`, **`reload`**, `restock`, `seed:<n>`, `sheeter`, `showcooldownminima`, `showintproperty:<prop>`, `showstringproperty:<prop>`, `soundlog`, `sultanmuralwalltest`, `swap`, `test437`, `testcardinal`, `testendmessage`, `testmarkup`, `testobjects`, `testordinal`, `testpets`, `testpop`, `testrig`, `testsifrah`, `teststringbuilder`, **`testzoneparse`**, `togglementalshields`, `topevents`, `tunneltest`, `villageprops`, `wavetilegen`, `weather`, `websplat`, **`zonebuilders`** ("Shows the ZoneBuilder classes used for the current zone").
- *Unlisted-function wishes of interest:* `gamestate:<key>`, `getstringgamestate:<key>`, `getboolgamestate:<key>`, `setstringgamestate:<key>:<value>`, `setboolgamestate:key:True/False`, `reputation:<faction>:<value>`, `opinion:<faction>`, `blueprint`, `what`, `pushgameview <view>`, `zoneconnections`, `freezezones`, `clearfrozen`, **`ensurevoids`** ("Makes pathways to any inaccessible areas of the current zone"), `svardymstorm`, `stage9`…`stage10`, `tombbeta`*, `slynthquest`.

The page also notes `setstringgamestate:GameMode:Roleplay` converts a game to Roleplay mode, and every page-level claim is marked `{{As of patch inline|2.0.201.50}}`.

### 4.5 Pets

"**Caves of Qud provides support for modding in new pets into the game using just XML.**"

> Technically, the only thing that you need in order to mod in a custom pet is to add `<tag Name="StartingPet" />` to a creature's blueprint.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<objects>
  <object Name="Snapjaw Scavenger Pet" Inherits="Snapjaw Scavenger">
    <tag Name="StartingPet" />
  </object>
</objects>
```

"This will cause a `snapjaw scavenger` pet to appear in the character customization menu."

Optional (but used by official Patreon pets): `Pettable` part + `PetResponse` tag, a unique tile, the `Story` property, and a unique conversational script.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<objects>
  <object Name="Snapjaw Scavenger Pet" Inherits="Snapjaw Scavenger">
    <part Name="Pettable" />
    <tag Name="PetResponse" Value="licks you,growls" />
    <tag Name="StartingPet" />
  </object>
</objects>
```

`PetResponse` is "a comma-separated list of the different responses that the pet can have".

```xml
<?xml version="1.0" encoding="utf-8" ?>
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
<?xml version="1.0" encoding="utf-8" ?>
<books>
  <book ID="My Snapjaw Scavenger Story" Title="{{W|My Story Title}}">
    <page>
Write your story for your creature here!
    </page>
  </book>
</books>
```

> **Note:** the page does **not** document a `Pet` *part* or a "tameable" surface beyond `StartingPet`; it references the `Pettable` part, `PetResponse` tag, and (for vanilla) `testpets` ("Test all possible village pet objects"). Anything else about the pet system is **[UNDOCUMENTED]** on this page.

### 4.6 Genotypes and subtypes

"**Genotypes and subtypes can be created with only some basic XML files. No scripting is required.**"

**`Genotypes.xml`** — `<genotype>` attributes (verbatim list):

- `Name`: the name of the genotype.
- `MutationPoints`: the number of mutation points (for purchasing mutations in the mutation selection screen) that the genotype should start with.
- `StatPoints`: the number of stat points that the genotype should have available for allocation.
- `BaseHPGain`, `BaseSPGain`, `BaseMPGain`: the number of hitpoints, skill points, and mutation points that the genotype should gain per level.
- `AllowedMutationCategories`: the mutation categories (as defined by the `category` tags in `Mutations.xml`) that the genotype should be able to pick up.
- `RandomWeight`: the likelihood of picking this genotype when random creating a character.
- `DisplayName`: how the name of the genotype should be displayed in the character creation UI.
- `Subtypes`: the name of the subtype class in `Subtypes.xml` that should be used for this genotype's subtypes.
- `Tile`, `DetailColor`: the tile for the genotype and its color, as displayed in the character creation UI.
- `BodyObject`: the game object blueprint that should be used for this genotype's body.
- `Species`: the species of a character with this genotype (used by morphogenetic and a handful of other things).
- `IsMutant`: whether or not characters with this genotype are considered mutants (used by e.g. tonics when determining effects).
- `CharacterBuilderModules`: unused (issue #12181).

Child tags: `<stat>` (starting value for a particular statistic), `<skills>` (skills granted by default on game start), `<reputations>` (starting faction reputations; preferred over a `Reputation="..."` attribute on `<genotype>`), `<extrainfo>` (additional info under the genotype in the selection screen).

**`Subtypes.xml`** — tags:

- `<class>`: defines a class of subtypes belonging to a single genotype (e.g. `ID="Callings"` = Mutated Human callings).
- `<category>`: optional, purely aesthetic subdivision (used by True Kin castes for arcologies).
- `<subtype>`: high-level properties of a single subtype. Attributes: `Class` (defaults to the parent `<class>` node), `Gear` (a population table for initial inventory), `BodyObject`, `Tile`, `DetailColor`, `BaseMPGain`, `BaseSPGain`, `BaseHPGain`, `Species`, `CyberneticsLicensePoints`, `Constructor`.
- `<stat>` (bonuses), `<skills>` (starting skills), `<savemodifiers>` (save bonuses vs. negative effects), `<reputations>` (starting reputation bonuses), `<extrainfo>` (extra text under the subtype description).

`<class>` also takes `ChargenTitle` and `SingularTitle` in the example: `<class ID="Snapjaws" ChargenTitle="choose calling" SingularTitle="calling">`.

**Chargen / preset integration:** the genotype must have `Subtypes` pointing at the `<class ID>` in `Subtypes.xml`, and the class's subtypes reference `Gear` population tables; the genotype's `Tile`/`DetailColor`/`DisplayName`/`RandomWeight` drive the chargen screens. `Modding:Overview`: "preset character | yes | `Modding:Tutorial_-_Custom_Player_Tiles` | Most properties of interest can be defined with XML." Custom subtype naming requires a `Naming.xml` with namestyles assigned to those subtypes (naming is assigned on a subtype level rather than by genotype) — flagged `{{Stub}}`.

Full worked example (Snapjaw genotype) from the page — genotype XML with `Subtypes="Snapjaws"`, `BodyObject="SnapjawBody"`, `Species="snapjaw"`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<genotypes>
  <genotype Name="Snapjaw" MutationPoints="12" StatPoints="44" AllowedMutationCategories="*"
    RandomWeight="10" DisplayName="Snapjaw" Subtypes="Snapjaws" Class="" Tile="Assets_Content_Textures_Creatures_sw_snapjaw.bmp"
    DetailColor="R" BodyObject="SnapjawBody" BaseHPGain="1-4" BaseSPGain="50" BaseMPGain="1" Species="snapjaw"
    IsMutant="true" CharacterBuilderModules="">

    <!-- Stats -->
    <stat Name="Strength" Minimum="10" Maximum="24" ChargenDescription="Your {{W|Strength}} score determines how effectively you penetrate your opponents' armor with melee attacks, how much damage your melee attacks do, your ability to resist forced movement, and your carry capacity."/>
    <!-- ... five more stats ... -->

    <!-- Skills -->
    <skills>
      <skill Name="Tactics_Run" />
      <skill Name="Survival_Camp" />
    </skills>

    <!-- Reputation -->
    <reputations>
      <reputation With="Snapjaws" Value="475" />
    </reputations>

    <extrainfo>Mutations</extrainfo>
    <extrainfo>Starts with Heightened Smell</extrainfo>
    <extrainfo>Moderate starting attributes</extrainfo>
    <extrainfo>-600 reputation with {{C|the Putus Templar}}</extrainfo>
    <extrainfo>0 reputation with {{C|snapjaws}}</extrainfo>
  </genotype>
</genotypes>
```

Body object (note `<mutation Name="HeightenedSmell" Level="1" />` directly inside a blueprint, and the sound tags):

```xml
<objects>
  <!-- Custom body object for the snapjaw genotype -->
  <object Name="SnapjawBody" Inherits="Humanoid">
    <!-- Define the sounds the player should make -->
    <tag Name="AmbientIdleSound" Value="Sounds/Creatures/VO/sfx_creature_animal_snapjaw_vo_idle"/>
    <tag Name="PunchSound" Value="Sounds/Creatures/VO/sfx_creature_animal_snapjaw_vo_attack"/>
    <tag Name="DeathSounds" Value="Sounds/Creatures/VO/sfx_creature_animal_snapjaw_vo_die" />
    <tag Name="TakeDamageSound" Value="Sounds/Creatures/VO/sfx_creature_animal_snapjaw_vo_hurt"/>
    <tag Name="LairAmbientBed" Value="Sounds/Ambiences/amb_creature_snapjaw" />
    <!-- Snapjaws have Heightened Smell by default. -->
    <mutation Name="HeightenedSmell" Level="1" />
  </object>
</objects>
```

Subtypes (abridged; three subtypes with `<stat Name="..." Bonus="2" />` and `<skills>` blocks), plus `PopulationTables.xml` defining `StartingGear_SnapjawCommon`, `StartingGear_SnapjawScavenger`, `StartingGear_SnapjawBrute`, `StartingGear_SnapjawShotgunner` with `<group Name="Items" Style="pickeach">`, nested `<table Name="..."/>`, and `<object Blueprint="..." Number="..."/>` entries. Note the documented gotcha: "We have to explicitly give each snapjaw subtype the `Snapjaw_Bite` natural equipment, otherwise they won't have it."

---

## 5. Release, packaging, compatibility

### 5.1 `manifest.json` (root of mod folder; all config files are optional)

"By convention, all JSON keys will be referred to using the casing used in the game's code. However, **the configuration file keys are case-insensitive**."

| Field | Description (quoted / condensed) | Properties |
| --- | --- | --- |
| **ID** | Primary identifier of the mod, used as the mod's key in the mod manager and to resolve it as a dependency of another mod. "Try to restrict the ID to alphanumeric characters, as it is also used as a symbol for preprocessor directives when compiling." | Falls back to `config.json`'s `ID` field. |
| **LoadOrder** | "Whole number indicating the mod's load priority in ascending order: smaller values load before larger ones. E.g. a mod with a loadorder of `-1` loads before one with `1`. **Obsolete in favor of `Dependencies` with build 210.**" | Falls back to `config.json`'s `LoadOrder` field. |
| **Title** | Title of the mod, displayed in the mod manager. | Accepts color shaders. Falls back to `workshop.json`'s `Title`, then to `manifest.json`'s `ID`. |
| **Description** | Short description of the mod, displayed in the mod manager. | Accepts color shaders. |
| **Tags** | "Comma-delimited list of tags, only for display in the mod manager and has no effect on tags used in the workshop." | Falls back to `workshop.json`'s `Tags`. |
| **Version** | Mod's version, displayed in the mod manager. | |
| **Author** | Creator(s) of the mod. | Accepts color shaders. |
| **PreviewImage** | "Relative path to an image used as an icon for the mod in the manager, recommended size 512x512. The largest Caves of Qud displays on default scale is 128x128, but if also used as the steam workshop preview image, that can display at up to 435x435 on the 'Most Popular Items' front page." | Falls back to `workshop.json`'s `ImagePath`. |
| **Dependencies** | "ID-Version pair collection of mods that are required to load before this one. **New in build 210.**" | Accepts version ranges. |
| **Dependency** | "Shorthand to declare a single mod dependency of any version, mutually exclusive with `Dependencies` field. **New in build 210.**" | |
| **LoadBefore** | "A single or array of mod IDs that this mod should attempt to load earlier than. **New in build 210.**" | |
| **LoadAfter** | "A single or array of mod IDs that this mod should attempt to load later than. **New in build 210.**" | |
| **Directories** | "Array of directory objects that can be loaded conditionally depending on game version or other mods in the load order. **New in build 210.**" | Falls back to loading the root directory if undefined. |

**`Directories` entry fields:** `Paths` (array, relative to mod root; deduplicated; case-sensitive on some OSes; prohibited from escaping the mod directory; recursive), `Path` (shorthand, mutually exclusive with `Paths`), `Version` (range matched against `XRLGame.MarketingVersion` — "the bright number in the bottom right of the main menu"; default `*`), `Build` (range matched against `XRLGame.CoreVersion` — "the dark number prefixed with 'build'"; default `*`), `Dependencies` / `Dependency` (optional deps; influence load order but don't prevent the mod from loading), `Exclusions` / `Exclusion`, `Options` (single or array of option requirement-specification strings; "the XML file containing the referenced options is required to have `Option` somewhere in its file name").

**Version ranges (verbatim):**

- `*`: Matches any version number and will thus always proceed.
- `1.0.*`: Matches a version greater or equal to `1.0.0`, but less than `1.1.0`.
- `2.0.208 - 3.0.0`: Matches a version greater or equal to `2.0.208`, and less or equal to `3.0.0` (i.e. an inclusive closed interval).
- `2.0.209.52 - *`: Matches a version greater or equal to `2.0.209.52`.
- `>3.5`: Matches a version greater or equal to `3.6.0`.
- `>=2 <5`: Matches a version greater or equal to `2.0.0`, but less than `5.0.0`.
- `^0.5.2 || 7.2.1`: Matches a version greater or equal to `0.5.2`, but less than `0.6.0`. **Or** a version equal to `7.2.1`.

**Example `manifest.json`** (from the wiki's Page, taken from the Snapjaw Mages tutorial and modified to demonstrate more options):

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
        {
            "Paths": [ "/Common/", "/Assets/Textures/" ]
        },
        {
            "Path": "/Old/",
            "Build": "<2.0.209.43"
        },
        {
            "Paths": [ "/NewCS/", "/NewXML/" ],
            "Build": ">=2.0.209.43"
        },
        {
            "Path": "/GooeyAddon/",
            "Version": ">=1.0.0",
            "Options": [ "OptionSnapjawMage_AddGooeyIck == Yes", "OptionSound != No" ],
            "Dependencies": {
                "Momo_CyberneticGenders": "^2.*",
                "IckySounds": ">=999.7.1.X"
            }
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

### 5.2 `workshop.json`

| Field | Description | Properties |
| --- | --- | --- |
| **WorkshopId** | The mod's unique ID on the workshop. | |
| **Title** | Title of the mod, displayed on the workshop. | |
| **Description** | Description of the mod, displayed on the workshop. | Accepts Steam formatting tags. |
| **Tags** | Comma-delimited list of tags, displayed on the workshop. | Pre-defined tags can be filtered on. |
| **Visibility** | Stringed integer: `"0"` private, `"1"` friends-only, `"2"` public. | |
| **ImagePath** | "Relative path to an image used as an icon for the mod in the workshop, recommended size 512x512." | |

```json
{
  "WorkshopId": 708258860,
  "Title": "Snapjaw Mages",
  "Description": "[h1]Snapjaw Mages[/h1]\n\nThis mod adds the new [b]Snapjaw Mage[/b] creature to Caves of Qud.",
  "Tags": "Creatures",
  "Visibility": "2",
  "ImagePath": "Preview.png"
}
```

From `Modding:Creating a Workshop Mod` (an older, slightly different example — note `"Preview.png"` vs `"Preview.png"`/`"preview.png"` casing across pages):

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

Field guidance quoted from the Creating a Workshop Mod page:

- **Tags:** "one or more categories that your mod falls under, separated by commas (for instance, `"World,Settlement"`). You can define your own tags but if at all possible, try to use some of the same tags used by items currently in the Caves of Qud workshop so that it's easier for people to find your mod."
- **Notes:** "Inside workshop.json, if the Visibility variable is manually set to `"2"`, the mod will always remain public. This prevents the game from making all updated mods private by default, forcing you to change visibility on the workshop page itself every time an update is pushed."

### 5.3 `modconfig.json` (texture settings) and `config.json` (obsolete)

```json
{
  "shaderMode": 0,
  "textureWidth": 16,
  "textureHeight": 24
}
```

- `ShaderMode`: `0` = default texture shader; `1` = true color texture shader (note: "This currently only works for textures rendered in the world. Most UI elements do not support this mode.")
- `TextureWidth` / `TextureHeight`: width/height of the mod's textures, in pixels.
- `config.json`: "was obsoleted in version 2.0.201.44 and was subsumed by the `manifest.json` file."

### 5.4 Uploading to the Workshop (step-by-step, verbatim)

1. Create your mod as normal as a subdirectory of the save directory `\Mods` folder (see file locations for exact paths for your operating system)
2. Open the Modding Utilities from the Caves of Qud overlay main menu (home screen, lower left)
3. Select the steam workshop uploader
4. Select your mod from the list
5. Click the "Create Workshop Id for Mod..." button. Your mod will now be associated with a workshop entry via a workshop.json file in the mod directory. You can now browse your item on steam workshop, though it will be empty. *(the page carries a dated bug note here about needing to re-click the mod in the list after create)*
6. Optionally fill out the title, description and other fields. These can be edited in steam later, if you'd like.
7. Click the Upload Content... button. The contents of your mod folder will be uploaded.
8. Now users can Subscribe to your mod in Steam and restart Caves of Qud and get your mod content!

### 5.5 Installing a mod (player-facing paths and mod-manager UI)

**Steam Workshop:** subscribe on the [Caves of Qud Workshop page](https://steamcommunity.com/app/333640/workshop/). "**Note:** This section applies only to running the game through Steam. Even if you obtained the game through Steam, **running it from outside of Steam will cause it not to load any Steam Workshop mods.**"

**Offline / manual mods folder** ("the `Mods` folder in the game's user data directory, which will exist as long as the game has correctly booted up at least once"):

- Windows – `C:\Users\Username\AppData\LocalLow\Freehold Games\CavesOfQud\`
- Linux – `/home/Username/.config/unity3d/Freehold Games/CavesOfQud/`
- Mac – `Users/Username/Library/Application Support/com.FreeholdGames.CavesOfQud/`

"**Note:** If you are using the Itch desktop client's sandbox feature, the directory may be under a user account other than the one you log in with."

**Install steps:** unzip into `Mods`; each mod's files must be contained in a separate folder; "**The folder name for an individual mod doesn't matter – the game loads content from any folder in the `Mods` directory.**"

```yaml
<Caves of Qud App Directory>
    Mods
        SampleMod1
            CodeFile.cs
            ObjectBlueprints.xml
            Textures
                character_tile.png
        SampleMod2
            CodeFile1.cs
            CodeFile2.cs
            ObjectBlueprints.xml
            Mutations.xml
        SampleMod3
            ObjectBlueprints.xml
```

**Other acquisition routes:** Nexus Mods (Manual Download → `.zip`), GitHub ("Clone or download" → "Download ZIP"), Bitbucket ("Downloads" → "Download repository"), and SteamCMD: `login anonymous`, then `workshop_download_item 333640 MOD_ID` (app id 333640; e.g. Blue Ctesiphus = `708258860`).

**Workshop-vs-offline ID conventions:** the offline folder name is irrelevant to loading; the Workshop association is the `WorkshopId` string/int in `workshop.json`, and the `manifest.json` `ID` is the mod-manager/dependency key. The Overview page warns: "While working on your mod, it will exist as a folder in the 'offline' mods location… **If you later upload it to the Steam Workshop and subscribe to it, make sure to move your working copy elsewhere to avoid conflicts.**"

**Mod manager UI:** the mod manager is available from the main menu; it displays `Title`, `Description`, `Tags`, `Version`, `Author` and `PreviewImage`, and "will also note errors on each mod by clicking on it". There is also a global option "Allow scripting mods", which "disables" script mods (i.e. C# code).

### 5.6 Compatibility guidance

**Prefixing.** "Certain internal names must be unique, usually because they're used as a lookup key… you should pick a prefix that's likely to be unique to you and add it to the front of any unique identifiers."

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
  <object Name="TrashMonks_Cool New Sword" Inherits="Long Sword3">
    <!-- other stuff goes here -->
  </object> 
</objects>
```

The "inexhaustive table of things whose names should be prefixed", with internal-identifier attribute vs. player-facing name attribute:

| type of data | how to set its unique identifier | how to set its player-facing name |
| --- | --- | --- |
| anatomy | `Name` attribute | N/A |
| body part type [variant] | `Type` attribute | `Description` attribute |
| event (non-min-event) | argument passed to `FireEvent` | N/A |
| object blueprint | `Name` attribute | `Render` part |
| option | `ID` attribute | `DisplayText` attribute |
| part class | class name | active parts only: `NameForStatus` field |
| population table | `Name` attribute | N/A |
| quest | `ID` attribute | `Name` attribute |
| seeded random generator | argument passed to `GetSeededRandomGenerator` | N/A |
| skill (or "power") | `Class` attribute | `Name` attribute |
| wish | argument passed to `WishCommand` | N/A; the internal name is seen by the player |

"For classes that are allowed to be in any namespace, using a unique namespace name instead is acceptable."

**Overriding base objects safely (merging).** "When merging onto existing XML data, such as object blueprints, there's no need to copy entire blocks of XML from the base game. Only specify the parts that you want to *change* and leave out everything else. **For object blueprints, you generally should not include an `Inherits` attribute at the same time as `Load="Merge"`.**"

```xml
<?xml version="1.0" encoding="utf-8"?>
<objects>
  <object Name="Chain Mail" Load="Merge">
    <part Name="Armor" DV="2" />
  </object> 
</objects>
```

**Random functions.** "To avoid conflicts and to keep consistency between seeds, `Stat.Random()` and `Stat.Rnd()` should not be called. The ideal is to use `GetSeededRandomGenerator()` for your mod's randomness. The next best is calling `RandomCosmetic()` or `Rnd2()`."

**Stats.** "Prefer using `value` over `sValue`, unless you're creating a unique creature. When loading ObjectBlueprints, the code loads both `sValue` and `value` into a stat, then if `sValue` is set, prefers using that to set a stat over `value`."

**Named arguments** for Qud's C# API (robustness against parameter-list changes), e.g.:

```csharp
using XRL.UI;
var response = Popup.AskString(
  "How are you doing?",
  Default: "Okay",
  MaxLength: 80, // note: same as current default
  ReturnNullForEscape: true,
  AllowColorize: true
);
```

"**Avoid including positional arguments after named ones!**"

**Save migration.** "you should prefer using `IScribedPart`, `IScribedEffect`, and `IScribedSystem` over `IPart`, `Effect`, and `IGameSystem`, respectively." If you can't inherit from one of these (e.g. `IActivePart`), implement:

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

**Load order.** Two mechanisms: the (now obsolete) `manifest.json` `LoadOrder` integer (ascending; smaller loads first), and the build-210 `Dependencies` / `Dependency` / `LoadBefore` / `LoadAfter` fields, which are the modern way to express ordering and requirements. Per-`Directories` `Dependencies` are *optional* and "do influence the load order, but won't prevent the mod from loading overall".

**Harmony** is the last resort: "Harmony can insert code at arbitrary places unrestricted by any interfaces the game provides. This has the possibility of playing havoc with other mods and with future changes to the base game, so only use Harmony as a last resort!" `Modding:Harmony` is the linked page.

### 5.7 How incompatible Workshop mods get flagged (`Modding:Overview#My_mod_was_marked_incompatible`)

Verbatim:

> If you received a notice that your Steam Workshop mod was marked incompatible with the game:
>
> 1. Fix any errors it causes. (See Debugging.)
> 2. Upload a new version to the Steam Workshop.
> 3. Request that your mod be unmarked as incompatible through either the support email (support@freeholdgames.com) or the Caves of Qud Discord's #modding channel.

Related debugging surface:

- **Load time:** "when the game loads all the content from all enabled mods. You can find load errors and warnings in the **build log**. The mod manager, available from the main menu, will also note errors on each mod by clicking on it."
- **Run time:** "pretty much any time after load time… in the **player log**. To see them immediately as they happen, enable 'Show error popups' under the debug options. Note that there are some situations where this option can softlock your game."

### 5.8 Fit and finish before publishing (`Modding:Polish`, a stub)

- **Article grammar:** "If you've created an object with a proper name, give it `<xtagGrammar Proper="true" />` to keep the game from improperly putting an article before it. (E.g., 'The Argyve takes 1 damage from your pyrokinesis!')."
- **Zoom levels:** "Use the mouse wheel to zoom in and out."
- **Modern vs. classic UI:** to access the classic UI, go to Options, ensure "Show advanced options" is checked, and uncheck "Enable modern UI" under UI. "The modern and classic UIs behave differently when reading a book. Ensure your books are paginated and broken into lines such that the classic UI doesn't cut off their text."
- **Text graphics (tiles off):** "go to Options, ensure 'Show advanced options' is checked, and uncheck 'Enable tile graphics'. Ensure all your objects that may appear as tiles have distinctive symbols assigned to them using the `Render` part's `RenderString` attribute."

### 5.9 `Modding:Histographicnomicon` (stub)

> The **Histographicnomicon** is part of the mod toolkit and **randomly generates sultan histories**.
>
> Main Menu > Modding Utilities > **Histographicnomicon**

The page carries a `{{Cleanup}}` note: "It's not clear that this tool warrants a complete article on its own. This might be better off as a part of an article on history procgen during world building." No configuration format or output format is documented. **[UNDOCUMENTED beyond the above.]**

---

## Appendix: pages that are stubs, missing, or explicitly incomplete

| Page | Status |
| --- | --- |
| `Modding:Zone Templates` | **Does not exist** (red link from `Modding:Intro - Zones and Worlds`). Zone templates (`<postbuilder Class="ZoneTemplate:X">`) are used in examples but have no article. |
| `Modding:Liquids` | Stub. "Modifying an existing liquid" and "Properties that a liquid can implement" are both marked as not yet written. |
| `Modding:Interior_Zones` | Stub banner present, though the body is substantial (weights, mechs, limitations). |
| `Modding:Polish` | Stub. |
| `Modding:Histographicnomicon` | Stub + cleanup banner. |
| `Modding:Genotypes_and_Subtypes` | "Custom naming conventions" is a stub; the worked example is flagged as missing info on snapjaw naming conventions. |
| `Modding:Mutations` | Missing-info banner: "Remove obsolete info (e.g., Constructor field), update info on variants". |
| `Modding:Populations` | Cleanup banner: "Everything about encounter tables is out of date." |
| `Modding:Creating_a_Workshop_Mod` | Has a cleanup banner ("Is the bug mentioned in step 6 still relevant?") and a missing-info banner that the *older* revision lacked info on `workshop.json` structure (now covered on `Modding:Mod Configuration`). |
| `Modding:Quests` | Complete; note that `QuestManager` is documented as obsolete in favour of `IQuestSystem`. |
| `Modding:Conversations` | Very complete; `<setvar>` / `<if>` / `<wish>` / `<goto>` tags are **not** part of the documented surface. |

## Sources

1. https://wiki.cavesofqud.com/wiki/Modding:Intro_-_Zones_and_Worlds
2. https://wiki.cavesofqud.com/wiki/Modding:Worlds
3. https://wiki.cavesofqud.com/wiki/Modding:Zone_Builders
4. https://wiki.cavesofqud.com/wiki/Modding:Zone_Procedural_Generation
5. https://wiki.cavesofqud.com/wiki/Modding:Maps
6. https://wiki.cavesofqud.com/wiki/Modding:Interior_Zones
7. https://wiki.cavesofqud.com/wiki/Modding:Quests
8. https://wiki.cavesofqud.com/wiki/Modding:Conversations
9. https://wiki.cavesofqud.com/wiki/Modding:Mutations
10. https://wiki.cavesofqud.com/wiki/Modding:Liquids
11. https://wiki.cavesofqud.com/wiki/Modding:Sounds
12. https://wiki.cavesofqud.com/wiki/Modding:Wishes
13. https://wiki.cavesofqud.com/wiki/Modding:Pets
14. https://wiki.cavesofqud.com/wiki/Modding:Genotypes_and_Subtypes
15. https://wiki.cavesofqud.com/wiki/Modding:Compatibility
16. https://wiki.cavesofqud.com/wiki/Modding:Creating_a_Workshop_Mod
17. https://wiki.cavesofqud.com/wiki/Modding:Installing_a_mod
18. https://wiki.cavesofqud.com/wiki/Modding:Polish
19. https://wiki.cavesofqud.com/wiki/Modding:Histographicnomicon

Additional pages consulted (linked `Modding:` pages, plus the wish reference): `Modding:Mod Configuration`, `Modding:Populations`, `Modding:Overview`, `Wishes`.
