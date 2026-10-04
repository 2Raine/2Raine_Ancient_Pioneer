# Caves of Qud — C# Scripting & Harmony Modding: Technical Research Note

Compiled from the official Caves of Qud wiki (`wiki.cavesofqud.com`, MediaWiki `Modding:` namespace). All code blocks are quoted **verbatim** from the wiki pages listed in *Sources* at the end; nothing has been invented. Where the wiki does not document a topic, it is explicitly marked **[UNDOCUMENTED ON THE WIKI]**. Verbatim typos from the wiki are preserved and flagged where they could bite you.

---

## 1. Script mods: mechanics and entry points

### 1.1 How `.cs` files are loaded

From `Modding:Scripting` (the target of the `Modding:C_Sharp_Scripting` redirect — note that the URL `/wiki/Modding:C_Sharp_Scripting` redirects to `/wiki/Modding:Scripting`, so both of the requested pages are the same article):

- "Caves of Qud is programmed in the **C Sharp v9.0** language". (The article is titled *Scripting* but is what the wiki links to as *C Sharp Scripting*.)
- "Caves of Qud loads a majority of its content via **reflection**. You may add new files to its database of reflectable objects by including `.cs` files in your mod. **These files are compiled at runtime into an assembly**, meaning that adding new C# code is technically as simple as including `.cs` files somewhere in your mod directory."
- Security / consent model: "C Sharp mods run with the full privileges of the base game. As such, they represent a security risk if any mod were to include malicious code. **Whenever a C Sharp mod is changed, users are required to approve the mod to run before it can be loaded.** The user will be prompted with a mod approval popup before creating or loading a save file. **If the mod is not approved none of its files will be loaded, including files other than `.cs` files.**"

From `Modding:Overview` (file structure):

- Files that go directly inside the mod folder, at top level: `manifest.json` and `workshop.json`.
- Files that can be nested anywhere within the mod folder:
  - "Any `.xml` files – The file may be called anything as long as it ends in `.xml`. What kind of data it represents is determined by what the outermost tag is, e.g., `<objects>…</objects>`."
  - "**Any `.cs` files - Just like `.xml` files, the filename before the extension does not matter.**"
  - "`Textures/`* – This is where all images to be used as tiles go."

So the **filename rule is: there is no filename rule** — any `*.cs` file anywhere under the mod directory is picked up. The mod's identity comes from `manifest.json`'s `ID`, and per `Modding:Mod_Configuration` the `ID` "is also used as a symbol for preprocessor directives when compiling" (this is the documented way to `#if` on mod presence). Files can also be selected per game version via the `Directories` array in `manifest.json` (`Paths`/`Path` recursively loaded, filtered by `Version` / `Build` / `Dependencies` / `Exclusions` / `Options`), which is how a mod ships "separate content/scripts for different game versions".

"Mods can be divided into three 'levels'": **data mods** (XML), **script mods** (C#), and among script mods **Harmony mods**. "Turning off the 'Allow scripting mods' option disables these [script mods]."

Required settings, from the Visual Studio setup guide in `Modding:Scripting`:

> Launch Caves of Qud. In the settings menu, navigate to the "Modding" section and enable **"Enable Mods (restart required.)"**, **"Select enabled mods on new game."**, and **"Allow scripting mods. Scripting mods may contain malicious code!"**.

Note the interaction with the approval model: the per-mod approval popup is separate from the global "Allow scripting mods" toggle, and denying approval for a mod suppresses *all* of that mod's files, not just its code.

### 1.2 Mod entry points: what actually exists

**[UNDOCUMENTED ON THE WIKI] `IMod`, `IModPart`, `Mod`.** Full-text wiki searches (`srnamespace=*`) for `IModPart`, `interface IMod`, `IEventRegistration`, `OnGameInit`, and `GameInit` return **zero hits**. The wiki documents **no** `IMod`-style mod interface and **no** `[OnGameInit]` attribute. Mods do not have a single documented entry-point class; instead the game scans for **attribute-tagged** classes and **reflection-discovered** `IPart`/`Effect` subclasses. Do not assume these names exist.

What the wiki *does* document, with exact names:

| Attribute / interface | Purpose / hook signature | Source page |
|---|---|---|
| `[PlayerMutator]` + `XRL.IPlayerMutator` | `void mutate(GameObject player)` — instantiated after the player object is created and assigned, on **New Game only** | Adding Code to the Player |
| `[HasModSensitiveStaticCache]` + `[ModSensitiveStaticCache]` / `[ModSensitiveStaticCache(true)]` / `[ModSensitiveCacheInit]` | Static-field reset + static init method, run when active script mods are compiled, on hotload, and on the `reload` wish | Adding Code at Startup |
| `[HasGameBasedStaticCache]` + `[GameBasedStaticCache]`, `[GameBasedStaticCache(CreateInstance = false)]`, static `Reset()`, `[GameBasedCacheInit]` | Per-new-game / per-save-load reset and init | Adding Code at Startup |
| `[PreGameCacheInit]` | Method-level attribute; works on a class carrying *either* cache attribute | Adding Code at Startup |
| `[WantLoadBlueprint]` on an `XRL.World.IPart` subclass | Requests blueprint preload; enables `IPart.LoadBlueprint()` | Adding Code at Startup |
| `[HasCallAfterGameLoadedAttribute]` + `[CallAfterGameLoadedAttribute]` | Static callback after a **save is loaded** and the Player exists | Adding Code to the Player |
| `[HasOptionFlagUpdate(Prefix="…")]` + `[OptionFlag]` / `[OptionFlag("Name")]` / `[OptionFlagUpdate]` | Bind XML options to static fields/properties | Options |
| `[HasWishCommand]` + `[WishCommand(Command = "…")]` / `[WishCommand(Regex = "…")]` | Register debug/console wishes | Wishes |
| `[HasVariableReplacer]` + `[VariableReplacer]` / `[VariableObjectReplacer]` / `[VariablePostProcessor("name")]` | Custom `=variable=` text replacers | Spring Molting Moddability (user page) |
| `XRL.World.IPart` | Base part class; components (pieces of code attached to an object) | Parts |
| `XRL.World.Parts.IActivePart`, `IPoweredPart`, `IGrenade`, `IModification`, `IPlayerPart`, `XRL.World.IScribedPart` | Specialized part base classes | Parts |
| `XRL.World.ZoneParts.IZonePart` | Part applied to a zone rather than an object | Parts |
| `IEventHandler`, `IModEventHandler<T>` | Register/handle MinEvents from arbitrary classes (not just parts/effects) | Events / Spring Molting |
| `IQuestSystem` | `public override void Register(XRLGame Game, IEventRegistrar Registrar)` — documented in the Quests page search snippet | (see §3.4) |

`XRL.World.Parts.IPlayerPart` is documented as: "a part that follows the player around even after changing bodies. This is usually used to implement player-specific behavior that is independent of the body that the player is in." (`Modding:Parts`). This is the closest documented thing to a "PlayerPart" entry point.

### 1.3 Where the code runs: the documented startup/cache model

`Modding:Adding_Code_at_Startup` gives this canonical table (verbatim):

| Code Pattern | When the Code Runs | Additional Notes |
|---|---|---|
| **Mod-Sensitive Cache** | When the main menu first loads; when the list of active mods changes | Not specific to a particular game and won't be called again if the player loads a different save. Runs once when the main menu loads during executable startup; called again if the approved/active mods change while running (the new configuration is "hotloaded"). |
| **Blueprint Preload** | When the main menu first loads; when the list of active mods changes | Runs at the same time as Mod-Sensitive Cache code, but with a different load framework. |
| **Game-Based Cache** | Immediately after the New Game option is selected; when an existing save is loaded | Called every time the game context changes. "Code is called **before the player object exists and before world generation**, so this option is not suitable if you need to manipulate the player." |
| **PlayerMutator** | After player is created for a New Game | Runs only once when a New Game starts. "Useful if you want your code to run only once per save file. For example, you might add a custom part to the player that holds your code." |
| **AfterGameLoaded Hook** | After a save game is loaded and the Player object exists | For modifying the player after a save load ("similar to PlayerMutator for new games"). |
| **Harmony Injection** | *At any time in the code flow* | "the most flexible method by far… However, it is also the most difficult and technical of the options. Most mods do not need this." |

Documented invocation chain and ordering:

- Mod-sensitive cache is invoked by `XRL.ModManager.ResetModSensitiveStaticCaches()`, called (a) when the player's active scripting mods are compiled on startup, (b) during a hotload when the active mod configuration changes, (c) when the **`reload` wish** is used.
- Game-based cache is invoked by `XRL.Core.XRLCore.ResetGameBasedStaticCaches()`, called immediately after selecting "New Game" and immediately after loading a saved game "(including scenarios such as a reload due to Precognition)".
- Overall order: **1. Mod-sensitive cache → 2. Pre-game cache code (including Blueprint preload) → 3. Game-based cache code.**
- Blueprint preload is invoked by `XRL.World.GameObjectFactory.CallLoadBlueprint()`, "immediately after mod-sensitive cache code is processed."

Processing semantics (in documented order):

- `[HasModSensitiveStaticCache]` classes: static fields marked `[ModSensitiveStaticCache]` are re-initialized to default value (value types) or **set to null** (object types); `[ModSensitiveStaticCache(true)]` forces `Activator.CreateInstance` instead. Then static methods marked `[ModSensitiveCacheInit]` are invoked. "there is no special handling for a `Reset()` method."
- `[HasGameBasedStaticCache]` classes: `[GameBasedStaticCache]` fields are reset to default (value types) or to a **new instance via `Activator.CreateInstance`** (object types; `[GameBasedStaticCache(CreateInstance = false)]` sets null instead). Then a static `Reset()` method is invoked if present. Then static methods marked `[GameBasedCacheInit]` are invoked.

Verbatim examples:

```csharp
[HasModSensitiveStaticCache]
public static class Initialiser
{
    [ModSensitiveStaticCache]
    public static int Counter; // Reset to default int value at game startup and whenever mod configuration changes

    [ModSensitiveStaticCache(true)]
    public static List<SolidColor> Colors = new List<SolidColor>(); // Reset to a new empty List<SolidColor> at game startup and whenever mod configuration changes

    [ModSensitiveCacheInit]
    public static void MyModCacheResetCode()
    {
        // Called at game startup and whenever mod configuration changes
    }
}
```

```csharp
[HasGameBasedStaticCache]
public static class Initialiser
{
    [GameBasedStaticCache]
    public static int Counter; // Reset to default int value whenever a new game is started or a save is loaded

    public static int OtherCounter; // Value is NOT automatically reset (though you could reset it in your Reset() method)

    public static void Reset()
    {
        // Called whenever a new game is started or a save is loaded - no attribute tag is needed
    }

    [GameBasedCacheInit]
    public static void AdditionalSetup()
    {
        // Called after Reset()
    }
}
```

Caveat the wiki calls out explicitly for `[PreGameCacheInit]`: "pre-game cache methods are run **before every new game or loaded game**, even if they are inside a class with only the `[HasModSensitiveStaticCache]` attribute. So be particularly careful if you're implementing a pre-game cache method that you only want to run after the mod loadout changes."

Blueprint preload example, "straight from the game code (`XRL.World.Parts.TinkerItem`)":

```csharp
namespace XRL.World.Parts
{
    [WantLoadBlueprint]
    //...
    public class TinkerItem : IPart
    {
        //...
        public override void LoadBlueprint()
        {
            if (this.CanBuild && string.IsNullOrEmpty(this.SubstituteBlueprint) && !this.ParentObject.HasTag("BaseObject"))
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
        //...
    }
}
```

Two documented hazards for preload code: `LoadBlueprint()` "is not called during normal gameplay, so it's a good place to put one-time initialization code for the part (However, keep in mind that this function might still be called multiple times during the blueprint preload process, if the part is present on more than one object)"; and the class constructor "will also be true whenever an object is created during normal gameplay, so it's generally not recommended to put blueprint preload logic into the class constructor."

### 1.4 Registering and attaching new parts

Namespace and class name are the registration mechanism. Parts live in `XRL.World.Parts`; mutations live in `XRL.World.Parts.Mutation` and must descend from `BaseMutation`; zone parts use `XRL.World.ZoneParts.IZonePart`.

**Class name ⇄ XML name convention** (from `Modding:Parts` and `Modding:Compatibility`): the XML `<part Name="…"/>` attribute matches the **C# class name**. Also:

- `<object Name="…">` — blueprint ID (`Name` attribute).
- `<part Name="MyPart" Foo="goodbye!" />` — part *fields* are settable from XML by attribute name (the example sets a public field `Foo`).
- `<mutation Name="Udder" … Class="FreeholdTutorial_Udder" …>` — mutations bind XML→C# through the **`Class`** attribute, not `Name`.
- Skills: `Class` attribute for the identifier, `Name` for the player-facing name.
- Options: `ID` attribute for the identifier, `DisplayText` for display.
- Zones/Worlds: `<part Name="AmbientStabilization" Strength="40" />` inside a `<zone>` element.

Prefixing is a documented requirement, not a suggestion: "Pick a prefix that's likely to be unique to you, and add it to the front of any names that need to be unique, such as object blueprint IDs and **part class names**." `Modding:Compatibility` tabulates the identifier field per data type, including "**part class** → class name" and "**event (non-min-event)** → argument passed to `FireEvent`". The wiki's own examples use prefixes like `Alice_Dog Pig`, `TrashMonks_Cool New Sword`, `Pyovya_SnapjawMage`, `ModName_CommandListener`, `MODNAME_Random`.

A minimal part (verbatim from `Modding:Parts`):

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
    }
}
```

```xml
<objects>
  <object Name="Snapjaw Scavenger" Load="Merge">
    <part Name="MyPart" />
  </object>
</objects>
```

Attachment API shown on the wiki: `player.AddPart<MyCustomPart>()` (PlayerMutator/Key Mapping examples), `player.RequirePart<MyCustomPart>()` ("RequirePart will add the part only if the player doesn't already have it"), `gameObject.TryGetPart<MyPart>(out var part)`, `GO.GetPart("ActivatedAbilities") as ActivatedAbilities`, `GO.GetPart<Body>()`, `ParentObject.RemovePart(this)`, `Object.RequirePart<Titles>()`, `ParentObject.HasEffect("Cudgel_SmashingUp")`, `User.pPhysics.CurrentCell.HasObjectWithPart("PlantProperties")`, `GameObject.Create("Snapjaw Scavenger")` / `GameObject.create("Ghostly Flames")` (both spellings appear in wiki code), and `ObjectBlueprints.xml` as the file the game iterates for blueprint preload.

`XRL.World.Parts.ActivatedAbilities` is the engine part that owns activated abilities (see §4.3).

---

## 2. Player and startup code

### 2.1 `[PlayerMutator]` — New Game only

`Modding:Adding_Code_to_the_Player`:

> You can use the `[PlayerMutator]` attribute and `IPlayerMutator` interface to modify the player object before the game begins, immediately after the player `GameObject` is first created.
>
> **This method works only when the player starts a New Game.** If a player loads your mod on an existing save, `[PlayerMutator]` code is never called.

Any class tagged with the `PlayerMutator` attribute is instantiated after the player object is created and assigned, and the `mutate` method is called with the player as the parameter. Verbatim sample:

```csharp
  using XRL; // to abbreviate XRL.PlayerMutator and XRL.IPlayerMutator
  using XRL.World; // to abbreviate XRL.World.GameObject
  
  [PlayerMutator]
  public class MyPlayerMutator : IPlayerMutator
  {
      public void mutate(GameObject player)
      {
          // modify the player object when a New Game begins
          // for example, add a custom part to the player:
          player.AddPart<MyCustomPart>();
      }
  }
```

(Note the lowercase method name `mutate` and the non-generic attribute usage in the wiki sample; the class also implements `IPlayerMutator`.)

### 2.2 `[CallAfterGameLoadedAttribute]` — save load only

> You can use the `[HasCallAfterGameLoadedAttribute]` and `[CallAfterGameLoadedAttribute]` attributes to modify the player object whenever a save game is loaded. This method can be useful if you want your mod changes to apply even to existing save games (rather than just new games).
>
> Note that this method works **only** on loaded saves - if you want your code to also run on a new game, you must combine this method with the PlayerMutator method described above.

```csharp
using XRL; // for HasCallAfterGameLoadedAttribute and CallAfterGameLoadedAttribute
using XRL.Core; // for XRLCore
using XRL.World; // for GameObject
  
[HasCallAfterGameLoadedAttribute]
public class MyLoadGameHandler
{
    [CallAfterGameLoadedAttribute]
    public static void MyLoadGameCallback()
    {
        // Called whenever loading a save game
        GameObject player = XRLCore.Core?.Game?.Player?.Body;
        if (player != null)
        {
            player.RequirePart<MyCustomPart>(); //RequirePart will add the part only if the player doesn't already have it. This ensures your part only gets added once, even after multiple save loads.
        }
    }
}
```

Accessor pattern for the player object (used repeatedly across pages): `XRLCore.Core.Game.Player.Body`, with null-safe chaining `XRLCore.Core?.Game?.Player?.Body`. Other documented globals: `The.Player`, `The.Game` (used with `IEventRegistrar.Register`).

### 2.3 Caveats about saves

- `[PlayerMutator]` never fires for an existing save → a part added only this way is absent for players who had a save before installing the mod. Combine with §2.2.
- Inside `[CallAfterGameLoadedAttribute]`, use `RequirePart<T>()` rather than `AddPart<T>()`, "This ensures your part only gets added once, even after multiple save loads." The wiki does not say what `AddPart` does when the part already exists **[UNDOCUMENTED: `AddPart` duplicate semantics]**.
- "Since the game approaches 1.0… modders need to be able to perform migrations without relying on a major game update" (`Modding:Serialization`). Historically "replacing fields in a part could permanently corrupt its parent object"; post-Spring-Molting "the game is able to remove parts that it doesn't know, so disabling a mod doesn't permanently corrupt all impacted objects. The game will also back itself up to help prevent save corruption." But removal "will surface errors to the user."
- Game-based cache runs *before* the player exists and before world generation — do not touch the player there.

### 2.4 Debug output

```csharp
XRL.Messages.MessageQueue.AddPlayerMessage("Hello world!")
```

```csharp
UnityEngine.Debug.LogError("Hello world!")
```

"Errors that occur during the initial building-in of your mod files are logged in `build-log.txt`. These errors will usually cause a mod to be unable to build, which prevents it from being enabled by the player."

---

## 3. Events

`Modding:Events` opens with a self-declared gap, which matters for the whole section:

> **Missing info:** This page does not describe event priorities, `IEventRegistrar`, or registering to listen to events on another GameObject.

It also says: "Due to the layer cake nature of the game's development over more than a decade, there are multiple different types of [events] that largely (but not completely) cover all the same use cases." The three-part model is: (1) a part listens for specific events / registers its intent; (2) something fires the event, dispatching it with details to all interested listeners; (3) the listening part handles it.

**[UNDOCUMENTED ON THE WIKI]** — there is **no event catalogue**: the page ends with `<!-- TODO: Insert event table -->`, and the requested `IEventRegistration` interface and `MinEvent<C>` generic pattern do not appear anywhere on the wiki (0 search hits). The documented counterparts are `IEventRegistrar` (§3.4) and `ModPooledEvent<T>`/`ModSingletonEvent<T>`/`IModEventHandler<T>` (§3.5). A `CommandEvent` class is likewise not documented — keybinds arrive as plain string events (§3.2, §6.2).

### 3.1 String events — listening

> To listen for a string event you typically override the **`Register`** method of the base **`IPart`** or **`Effect`** class.
> This is executed **once** when the IPart/Effect is added to the object and subsequently serialized.
> Overriding the **`AllowStaticRegistration`** method to return true alters this behaviour: registrations are no longer serialized and **`Register` is re-executed every save load**.
> It is however possible to register for an event *anywhere* as long as you have an instance of both an IPart/Effect and a GameObject.

```csharp
public override void Register(GameObject Object, IEventRegistrar Registrar)
{
	// Listen for when the GameObject in obj is awarded XP.
	Registrar.Register("AwardXP");

	// The otherwise identical method call used for when [this] is an Effect.
	Registrar.Register("AwardXP");

	// Call the base Register method that we overrode.
	base.Register(Object, Registrar);
}
```

The comment "The otherwise identical method call used for when [this] is an Effect" is verbatim but tautological in the wiki; the intent is that `Effect` exposes the same `Register` shape.

### 3.2 String events — firing and handling

```csharp
public void AwardPlayerXP() {
	// Get the player GameObject.
	GameObject player = XRLCore.Core.Game.Player.Body;

	// Create a new "AwardXP" Event, specifying an "Amount" parameter with the integer value 50.
	// You can keep declaring staggered parameters or leave them out entirely.
	// Event.New(ID, [PrmName1, PrmVal1, PrmName2, PrmVal2, PrmName3, ...])
	Event awardXP = Event.New("AwardXP", "Amount", 50);

	// You can also set the parameters one by one instead of passing them to the constructor.
	awardXP.SetParameter("Amount", 50);

	// Fire the event on the player.
	player.FireEvent(awardXP);
}
```

```csharp
public override bool FireEvent(Event E)
{
	// If the ID of our event is "AwardXP"...
	if (E.ID == "AwardXP") {
		// Get the parameter we specified either in the constructor or using SetParameter.
		int amount = E.GetIntParameter("Amount");

		// Add the amount to this GameObject's experience.
		this.ParentObject.Statistics["XP"].BaseValue += amount;
	}

	// Return the result of the base FireEvent that we overrode, which returns a literal true.
	// Should you instead return false further event processing will stop, meaning no Parts and Effects after get the chance to handle the event.
	return base.FireEvent(E);
}
```

**Cancellation semantics for all event families: returning `false` from the handler stops further processing.** Other documented `Event` members: `E.ID`, `E.SetParameter(name, value)`, `E.GetParameter<T>(name)`, `E.GetIntParameter(name)` / `E.GetIntParameter(name, default)`, `E.GetGameObjectParameter(name)`, `E.SetSilent(true)`, `E.AddAICommand(...)`, `E.AddMark(...)`. Other documented firing sites use `Event.New("CommandForceEquipObject", 0, 0, 0)` (positional placeholder params) and `GameObject.FireEvent(Event.New("CommandForceUnequipObject", "BodyPart", firstPart))`.

### 3.3 Min events — listening, firing, handling

Min events "are implemented as a separate C# class for each type of event. They were introduced with the Tomb of the Eaters update (V200)."

Listening uses `WantEvent`, "executed **every time** an event is fired so keep it lean… If you need to dynamically listen for another event it's recommended to do your dynamic logic elsewhere and flip a boolean variable for the WantEvent."

```csharp
// Listens for one event always.
public override bool WantEvent(int ID, int cascade) {
	// Check if the ID parameter matches one of the events we want, in this case ZoneActivatedEvent.
	// The base WantEvent of IPart/Effect will always return false.
	return base.WantEvent(ID, cascade) || ID == ZoneActivatedEvent.ID;
}


// Flip this boolean based on some logic condition elsewhere.
bool wantEndTurn = false;

// Listens for an event dynamically.
public override bool WantEvent(int ID, int cascade) {
	if(ID == ZoneActivatedEvent.ID)
		return true;
		
	if(wantEndTurn && ID == EndTurnEvent.ID)
		return true;

	return base.WantEvent(ID, cascade);
}
```

Firing (note the wiki's own `<!-- TODO -->` that the common static `MinEvent.Send()` method is not yet covered):

```csharp
public void GivePlayerDrams() {
	// Get the player GameObject.
	GameObject player = XRLCore.Core.Game.Player.Body;

	// Retrieve a GiveDramsEvent from the pool, this is the preferred way to get an event instance.
	GiveDramsEvent E = GiveDramsEvent.FromPool();
	// Some events have overloaded FromPool to set properties of the event on retrieval.
	E = GiveDramsEvent.FromPool(Actor: player, Drams: 50);
	// You can still set properties directly.
	E.Liquid = "water";
	// Yet other events do not have an accessible FromPool method, in which case you'll have to instantiate your own.
	E = new GiveDramsEvent() {
		Actor = player,
		Drams = 50
	};

	// Fire the event on the player.
	player.HandleEvent(E);
}
```

Handling — one override per event type, or a single `HandleEvent(MinEvent E)` that dispatches by `E.ID`/type:

```csharp
// A volume/container of liquid, like a puddle or flagon.
LiquidVolume liquid = new LiquidVolume();

// This method handles the GiveDramsEvent.
public override bool HandleEvent(GiveDramsEvent E) {
	liquid.GiveDrams(E.Liquid, ref E.Drams, E.Auto);

	// Just like old events, returning false prevents further event processing.
	// Here we have no more liquid left to give so there's no reason to continue.
	if (E.Drams <= 0)
		return false;

	return true;
}

// This method handles the FrozeEvent.
public override bool HandleEvent(FrozeEvent E) {
	E.Object.DisplayName = "&CFrozen Object";
	return true;
}

// It's still possible to override the base HandleEvent and compare the ID or type like old events.
// This is more useful for when you're cascading events carte blanche to sub-objects, see the Cascading section below.
public override bool HandleEvent(MinEvent E) {
	if (!base.HandleEvent(E)) {
		return false;
	}
	
	if (E.ID == GiveDramsEvent.ID) {
		return HandleEvent(E as GiveDramsEvent);
	} else if (E is FrozeEvent) {
		return HandleEvent(E as FrozeEvent);
	}

	return true;
}
```

Event IDs are static ints on the event class: `ZoneActivatedEvent.ID`, `EndTurnEvent.ID`, `GiveDramsEvent.ID`, `AfterGameLoadedEvent.ID`, `GetShortDescriptionEvent.ID`, etc.

### 3.4 Cross-object registration, priorities, and `IEventRegistrar`

Documented on the user page *Spring Molting Moddability* (linked from `Modding:Serialization`), which is also the source the Events page's "missing info" banner points at:

> Prior to this patch MinEvents were restricted to only being usable within their hardcoded cascade level… Event handlers can now register to MinEvents **from external event sources** that are outside their cascade range, with an **optional ordering** determining the precedence it has over other handlers. For example a global game system can listen for events directly on the player, or the member of a party can listen for events from their leader.

```csharp
using XRL.UI;

namespace XRL.World.Parts
{

    public class ExamplePart : IPart
    {

        // IEventRegistrar is also new with this update, it contains some state to make the most common registrations less repetitive
        // Most importantly it can both register and unregister for events as needed by the game, when an object goes out of scope for example
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            // Listen for when the parent object gains a level, after most handlers
            Registrar.Register(AfterLevelGainedEvent.ID, EventOrder.LATE);
            // Listen for when the player dies, before most handlers
            // Notably this does not follow the player should they change bodies, and should update the registration with AfterPlayerBodyChangeEvent
            Registrar.Register(The.Player, BeforeDieEvent.ID, EventOrder.VERY_EARLY);
            // Listen for when the game changes active zones
            Registrar.Register(The.Game, ZoneActivatedEvent.ID);
        }
        
        public override bool HandleEvent(AfterLevelGainedEvent E)
        {
            Popup.Show($"{ParentObject.an()} gained a level!");
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeDieEvent E)
        {
            // Make player immortal.
            return false;
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

So the documented `IEventRegistrar` surface is:

- `Registrar.Register(string eventID)` / `Registrar.Register(int eventID)` — self (string events and MinEvents respectively; `IQuestSystem`'s snippet uses `Registrar.Register(ZoneActivatedEvent.ID)`).
- `Registrar.Register(int eventID, EventOrder order)` — self, with priority (`EventOrder.LATE`, `EventOrder.VERY_EARLY` documented).
- `Registrar.Register(GameObject source, int eventID[, EventOrder order])` — listen to events on another object (`The.Player`, `The.Game`).

`IEventHandler` lets *any* class (not just parts/effects) register and handle MinEvents:

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

        public bool WantEvent(int ID, int Cascade)
        {
            return ID == AfterDieEvent.ID;
        }

        public bool HandleEvent(AfterDieEvent E)
        {
            XRL.UI.Popup.Show("You died!");
            return true;
        }

    }

}
```

An alternative to per-registration ordering is part priority: "A higher priority is placed earlier in the part list, receiving events before lower priorities… This affects the cascade order of any events to that part, but also the order in which it is serialized."

```csharp
    public class ExamplePart : IPart
    {
        // A higher priority is placed earlier in the part list, receiving events before lower priorities
        public override int Priority => PRIORITY_HIGH;
```

### 3.5 Custom MinEvents (modded events)

> To create your own MinEvent it's recommended to derive from `ModPooledEvent<T>` as it implements the event's pooling and dispatch for you, but there's also `ModSingletonEvent<T>` which is a slightly simpler alternative when the event has no state or cannot intersect with itself.
> Any handlers of the event will need to implement `IModEventHandler<T>` where `T` is your custom event.

Verbatim (identical on both `Modding:Events` and the Spring Molting page):

```csharp
using XRL.World;

namespace ExampleMod
{

    public class ExampleEvent : ModPooledEvent<ExampleEvent>
    {

        public static readonly int CascadeLevel = CASCADE_EQUIPMENT | CASCADE_EXCEPT_THROWN_WEAPON;

        public string Value;

        // A static method that fires your event,
        // this isn't strictly necessary but is how the game prefers to organize it
        public static string GetFor(GameObject Object)
        {
            var E = FromPool();
            Object.HandleEvent(E);

            return E.Value;
        }

        // Resets the event before it's returned to the pool
        public override void Reset()
        {
            base.Reset();
            Value = null;
        }

        // How far our event will cascade,
        // this example will cascade to equipped items
        public override int GetCascadeLevel()
        {
            return CascadeLevel;
        }

    }

    public class ExampleHandler : IPart, IModEventHandler<ExampleEvent>
    {

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                   || ID == ExampleEvent.ID
                ;
        }

        public bool HandleEvent(ExampleEvent E)
        {
            E.Value = "Handled!";
            return true;
        }

    }

}
```

### 3.6 Cascading

"The **cascade level** is a bit field which determines when and where the event should cascade depending on which bits are flipped."

```csharp
// The cascade category bits of MinEvent.
public const int CASCADE_NONE = 0x0;                  // 0b00000
public const int CASCADE_EQUIPMENT = 0x1;             // 0b00001
public const int CASCADE_INVENTORY = 0x2;             // 0b00010
public const int CASCADE_SLOTS = 0x4;                 // 0b00100
public const int CASCADE_COMPONENTS = 0x8;            // 0b01000
public const int CASCADE_EXCEPT_THROWN_WEAPON = 0x10; // 0b10000
public const int CASCADE_ALL = CASCADE_EQUIPMENT | CASCADE_INVENTORY | CASCADE_SLOTS | CASCADE_COMPONENTS;

// A list of items.
List<GameObject> Inventory = new List<GameObject>();

public override bool WantEvent(int ID, int cascade)
{
	// If the cascade variable has the inventory bit...
	if (MinEvent.CascadeTo(cascade, MinEvent.CASCADE_INVENTORY)) {
		// Check if any item in our inventory wants this event.
		foreach(GameObject item in Inventory) {
			if(item.WantEvent(ID, cascade))
				return true;
		}
	}
	
	return base.WantEvent(ID, cascade);
}

public override bool HandleEvent(MinEvent E)
{
	// If the CascadeLevel has the inventory bit...
	if (E.CascadeTo(MinEvent.CASCADE_INVENTORY)) {
		// Allow each item in the inventory to handle the event.
		// Stops processing should any of them return false.
		foreach(GameObject item in Inventory) {
			if(!item.HandleEvent(E))
				return false;
		}
	}
	
	return base.HandleEvent(E);
}
```

### 3.7 Wish commands (a separate, non-event registration system)

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

**Verbatim wiki defects in this sample** (do not copy blindly): the `Regex = @"other fancy match \d things"` line is missing its closing `)` and the string's closing `"`, and `System.Text.RegularExpression.Match` is misspelled (the .NET type is `System.Text.RegularExpressions.Match`). "The regular expression passed to the attribute is parsed using case insensitive matching." Methods may be `void` or `bool`; `bool` returning `false` lets other wishes also parse the message; a class must carry `[HasWishCommand]`.

### 3.8 Documented event names (the wiki's complete, scattered set)

String events seen in wiki code: `AwardXP`, `EnteredCell`, `Equipped`, `Unequipped`, `TakeDamage`, `CommandForceUnequipObject`, `CommandForceEquipObject`, `AIGetOffensiveMutationList`, `CommandCudgelSlam`, `CommandFlamingHands`, plus mod-defined keybind commands (e.g. `ModName_Cmd_One`).

MinEvents seen in wiki code: `ZoneActivatedEvent`, `EndTurnEvent`, `GiveDramsEvent`, `FrozeEvent`, `AfterGameLoadedEvent`, `GetShortDescriptionEvent`, `BeforeRenderEvent`, `AfterPetEvent`, `AfterConversationEvent`, `AfterLevelGainedEvent`, `BeforeDieEvent`, `AfterDieEvent`, `GetMeleeAttackChanceEvent`, `AnimateEvent`, `AfterPlayerBodyChangeEvent`.

This is a sample, not a catalogue: the wiki states the event table is still a TODO.

---

## 4. Effects, parts, and abilities

### 4.1 `Effect`

**`Modding:Effects` is a stub.** Its banner is literally `{{Stub}}`, and its entire body covers only `GetEffectType()` plus the type-bit tables. Therefore:

- **[UNDOCUMENTED ON THE WIKI]** `Effect.Apply`, `Effect.Duration`, `Effect.DisplayName`, and effect stacking. None of these members appear on the Effects page (or anywhere else searched). The only related facts the wiki gives are: `StatShifter` derives its shift description from `Effect.GetDescription()` by default ("It defaults to the `GetDescription()` of the Effect, and `""` for a part"), and MinEvent examples reference `E.Object` and `Object.DisplayName` for a frozen object.

What *is* documented:

> All effects inherit a virtual method called `GetEffectType()`. The result of `GetEffectType()` is an integer, representing a bit vector in which each bit is a flag that designates whether or not the effect is of a given type.

Mechanism bits (bit # from the right → type, decimal): 1 General 1; 2 Mental 2; 3 Metabolic 4; 4 Respiratory 8; 5 Circulatory 16; 6 Contact 32; 7 Field 64; 8 Activity 128; 9 Dimensional 256; 10 Chemical 512; 11 Structural 1024; 12 Sonic 2048; 13 Temporal 4096; 14 Neurological 8192; 15 Disease 16384.

Class bits: 25 Minor 16777216; 26 Negative 33554432; 27 Removable 67108864; 28 Voluntary 134217728.

Mask groups: Mechanism = 16777215 (bits 1–24); Class = 251658240 (bits 28–25); **Duration Indefinite = 9999** (bits 1, 2, 3, 4, 9, 10, 11, 14).

Applicability rules the game enforces in `Effect.cs` every time "Apply Event Effect" is called (objects that are considered solid always return true): cannot be applied to **liquids** if bits 6 or 11 (Contact/Structural) are set; to **gas** if bits 3, 6, or 11 (Metabolic/Contact/Structural); to **plasma** if bits 3, 6, 7, 10, 11, or 12 (Metabolic, Contact, Field, Chemical, Structural, Sonic). `Mental` cannot be applied to objects lacking a brain; `Metabolic` not to objects without stomachs, gases, or plasma; `Respiratory` not to objects without stomachs; `Circulatory` not to objects that cannot bleed; `Activity` not to objects without bodies (`Effect.CanEffectTypeBeAppliedTo*` helpers).

Two masking helpers: "`bool IsOfType()` - returns true if ANY of the specified digits are true" and "`bool IsOfTypes()` - returns true if ALL of the specified digits are true… These checks are done by running a bitwise AND(&) operation on them."

Verbatim example (the `Shamed` effect; **note the wiki's inconsistent casing** — `Effect.TYPE_MENTAL` vs `EFFECT.TYPE_MINOR`):

```csharp
// As of patch: 
public override int GetEffectType()
{
    return Effect.TYPE_MENTAL
        | EFFECT.TYPE_MINOR
        | EFFECT.TYPE_NEGATIVE
        | EFFECT.TYPE_REMOVABLE;
}
```

### 4.2 Active parts (`IActivePart`, `IPoweredPart`)

"Many object parts (the entity components specified in `ObjectBlueprints.xml` with `part` tags) are based on `IActivePart` architecture." The concepts are **subjects** (objects the part operates on) and **status** (the part's operational state): "The basic form of a part's integration with its `IActivePart` support is for it to check its status, and if it is operational, to apply its behavior to its subjects."

Statuses, evaluated in this order (first match wins): `NeedsSubject`, `SwitchedOff`, `EMP`, `Broken`, `Rusted`, `Booting`, `NotHanging`, `LimbIncompatible`, `RealityStabilized`, `LocallyDefinedFailure`, `PrimarySystemOffline`, `Unpowered`, `Operational`. "Only the status Operational represents working functionality."

Status-determining configuration fields (name → failure status → `IActivePart` default): `ChargeUse` int → Unpowered (0); `ChargeMinimum` int → Unpowered (0); `IsBootSensitive` → Booting (false); `IsBreakageSensitive` → Broken (true); `IsEMPSensitive` → EMP (false); `IsHangingSensitive` → NotHanging (false); `IsPowerSwitchSensitive` → SwitchedOff (false); `IsRealityDistortionBased` → RealityStabilized (false); `IsRustSensitive` → Rusted (true); `NeedsOtherActivePartOperational` string → PrimarySystemOffline (null); `NeedsOtherActivePartEngaged` string → PrimarySystemOffline (null); `RequiresBodyPartCategory` string → LimbIncompatible (null; categories "Animal", "Arthropod", "Plant", "Fungal", "Protoplasmic", "Cybernetic", "Mechanical", "Metal", "Wooden", "Stone", "Glass", "Leather", "Bone", "Chitin", "Plastic", "Cloth", "Psionic", "Extradimensional").

Subject-determining booleans, all defaulting to false: `MustBeUnderstood`, `WorksOnAdjacentCellContents`, `WorksOnCarrier`, `WorksOnCellContents`, `WorksOnEnclosed`, `WorksOnEquipper`, `WorksOnHolder`, `WorksOnImplantee`, `WorksOnInventory`, `WorksOnSelf`, `WorksOnWearer`. Overridable restrictions: `public virtual bool WorksFor(GameObject obj)`, `public virtual bool GetActivePartLocallyDefinedFailure()`, `public virtual string GetActivePartLocallyDefinedFailureDescription()`, `public virtual bool IsActivePartEngaged()`.

Status-display fields: `DescribeStatusForProperty` (string, null), `IsBioScannable` / `IsStructureScannable` / `IsTechScannable` (bool, false; set the property and the StatusStyle), `NameForStatus` (string; "The default is the name of the part class (so this is particularly crucial for very generic parts like `IntPropertyChanger`)"), `StatusStyle` ("angry", "bio", "leet", "ooc", "plain", "structure", "tech"; default "plain").

Other integration points: `IsPowerLoadSensitive` (bool, false), `ReadyColorString` / `ReadyDetailColor` / `DisabledColorString` / `DisabledDetailColor` (set `Render`'s `ColorString`/`DetailColor` on the parent object when status becomes Operational / non-Operational).

`IPoweredPart` "is a variant of `IActivePart` intended for more technical applications" with defaults `ChargeUse = 1`, `IsBootSensitive = true`, `IsEMPSensitive = true`, `IsPowerSwitchSensitive = true`, `IsTechScannable = true`.

Key methods (exact signatures from the wiki):

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

`IsDisabled(...)` takes the identical parameter list and returns the opposite. `GetActivePartStatus(...)` takes the identical parameter list and returns an `ActivePartStatus`. `IsReady()` "Returns true if the part's status is Operational. Triggers any appropriate render color changes. Updates the last known status."

Also documented: `WasReady()`, `WasDisabled()`, `GetLastActivePartStatus()`, `ConsumeCharge(int? ChargeUse = null)`, `ConsumeCharge(int MultipleCharge, int? ChargeUse = null)`, `ConsumeChargeIfOperational(...)`, `GetActivePartSubjects()` → `List<GameObject>`, `GetActivePartFirstSubject()`, `GetActivePartFirstSubject(Predicate<GameObject> Filter)`, `IsObjectActivePartSubject(GameObject obj)`, `ActivePartHasMultipleSubjects()`, `GetActivePartSubjectCount()`, `ForeachActivePartSubjectWhile(Predicate<GameObject> pProc, bool MayMoveAddOrDestroy = false)` ("`MayMoveAddOrDestroy` should be set to true if the code in `pProc` might result in any object being moved, created, or destroyed; otherwise, there is a risk of exceptions being thrown due to loop control disruption"), `AnyActivePartSubjectWantsEvent(int ID, int cascade)`, `ActivePartSubjectsHandleEvent(MinEvent E)`, `GetStatusSummary(ActivePartStatus Status)`, `GetStatusSummary()`, `AddStatusSummary(StringBuilder SB)`, `GetOperationalScopeDescription()`, `MyPowerLoadBonus(int Load = int.MinValue, int Baseline = 100, int Divisor = 150)`, `MyPowerLoadLevel()`.

Documented status summaries: EMP → `"{{W|EMP}}"`, Unpowered → `"{{K|unpowered}}"`, SwitchedOff → `"{{K|switched off}}"`, Booting (only if `BootSequence.IsObvious()`) → `"{{b|warming up}}"`, NeedsSubject → null, Operational → null, any other status → `"{{r|nonfunctional}}"`.

`AddStatusSummary` usage pattern:

```csharp
public override bool HandleEvent(GetShortDescriptionEvent E)
{
    E.Postfix.AppendRules(AppendRulesDescription, AddStatusSummary);
    return true;
}
```

Verbatim integration examples:

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
            return
                base.WantEvent(ID, cascade)
                || ID == EndTurnEvent.ID
            ;
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

```csharp
using System;

namespace XRL.World.Parts
{

    [Serializable]
    public class HealEveryTurn : IActivePart
    {

        public override bool WantEvent(int ID, int cascade)
        {
            return
                base.WantEvent(ID, cascade)
                || ID == EndTurnEvent.ID
            ;
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

}
```

The wiki notes `[Serializable]` on `IActivePart` subclasses, and that `[Serializable]` is what lets the game save the part. The Active Parts page also carries a ~200-entry list of the game's own active parts (`AccelerativeTeleporter` … `ZoneAdjust`), including the note "**Mod\* (IModification, which all mods inherit, is an IActivePart)**".

### 4.3 Activated abilities

"Any part can add activated abilities, including mutations, skills, or even equipment (see: Hologram Bracelet and Rocket Skates for examples). All parts add abilities through the same interface." The owning engine part is `ActivatedAbilities`, reached via `GO.GetPart("ActivatedAbilities") as ActivatedAbilities`.

Verbatim, from `Cudgel_Slam.cs`:

```csharp
    public override bool AddSkill(GameObject GO)
    {
      ActivatedAbilities part = GO.GetPart("ActivatedAbilities") as ActivatedAbilities;
      if (part != null)
      {
        this.ActivatedAbilityID = part.AddAbility("Slam [&Wattack&y]", "CommandCudgelSlam", "Skill", -1, false, false, "You make an attack with a cudgel at an adjacent opponent at +1 penetration. If you hit, you slam your opponent backwards up to 3 spaces, pushing other creatures and breaking through walls if their AVs are less than 5 times your strength modifier. Opponents who get pushed are stunned for 1 round plus an additional round for each space pushed. Opponents who are pushed through or against walls take extra weapon damage for each wall. Colossal opponents don't get pushed but are still stunned for 1 round.", "-", false, false);
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

> The important fields to note in the AddAbility function call are the first 3. This tells the game object to add a new ability called "Slam[attack]", and to fire the "CommandCudgelSlam" event when it is used. This ability will show up under the "Skill" category in the ability menu. This function returns a `Guid`, which can be used to identify the ability when you need to reference or remove it.

So the documented `ActivatedAbilities` API is `AddAbility(name, command, class/category, …) → Guid`, `AbilityByGuid[guid] → ability object`, `RemoveAbility(guid)`, and ability object members `Cooldown`, plus `Ability.Cooldown <= 0` checks. **The full parameter list of `AddAbility` is not documented** (the wiki only says "the first 3" matter and passes the rest positionally) — **[UNDOCUMENTED: remaining `AddAbility` parameters]**.

Registration of the command event and AI integration:

```csharp
    public override void Register(GameObject Object)
    {
      Object.RegisterPartEvent((IPart) this, "CommandCudgelSlam");
      Object.RegisterPartEvent((IPart) this, "AIGetOffensiveMutationList");
      base.Register(Object);
    }
```

```csharp
      if (E.ID == "AIGetOffensiveMutationList")
      {
        int intParameter = E.GetIntParameter("Distance");
        if (E.GetGameObjectParameter("Target") == null || !this.IsPrimaryCudgelEquipped() || this.ParentObject.pPhysics != null && this.ParentObject.pPhysics.IsFrozen())
          return true;
        List<AICommandList> parameter = (List<AICommandList>) E.GetParameter("List");
        if (this.Ability != null && this.Ability.Cooldown <= 0 && intParameter <= 1)
          parameter.Add(new AICommandList("CommandCudgelSlam", 1));
        return true;
      }
```

Activation body (validation + energy + cooldown):

```csharp
      if (E.ID == "CommandCudgelSlam")
      {
        if (!this.IsPrimaryCudgelEquipped())
        {
          if (this.ParentObject.IsPlayer())
            Popup.Show("You must have a cudgel equipped in order to use slam.", true);
          return true;
        }
        if (this.ParentObject.pPhysics != null && this.ParentObject.pPhysics.IsFrozen())
        {
          if (this.ParentObject.IsPlayer())
            Popup.Show("You are frozen solid!", true);
          return true;
        }
        string str = this.PickDirectionS();
        Cell cellFromDirection = this.ParentObject.GetCurrentCell().GetCellFromDirection(str, true);
        if (cellFromDirection == null)
          return true;
```

```csharp
          this.ParentObject.UseEnergy(1000, "Skill Cudgel Slam");
          if (!this.ParentObject.HasEffect("Cudgel_SmashingUp"))
            (this.ParentObject.GetPart("ActivatedAbilities") as ActivatedAbilities).AbilityByGuid[this.ActivatedAbilityID].Cooldown = 510;
```

Timing rules, verbatim: "The `UseEnergy` call sets the amount of time the ability takes to activate. **1000 energy is equivalent to 1 turn**, and should be used for most regular skills. 2000 energy would be 2 turns, and 500 would be half a turn. Free actions do not need to call this function." and "To set a cooldown, you simply set the cooldown property on the ability. **10 cooldown is equivalent to 1 turn (at 16 willpower).** Cudgel adds 10 to its cooldown counter, because the cooldown will count down by 1 in the time it takes to use the ability."

Ability lifecycle hooks: "the `AddSkill` and `RemoveSkill` functions are unique to skills. For mutations, you would add your ability in the `Mutate` and `UnMutate` functions, and on equipment, you would listen for the `OnEquipped` and `OnUnequipped` messages."

The mutation/skill helper wrappers documented elsewhere on the wiki:

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

Also documented on `BaseMutation` subclasses (from the decompiled `FlamingHands`): `AddMyActivatedAbility("Flaming Hands", "CommandFlamingHands", "Physical Mutation", -1, null, "\a", false, false, false, false, false, false, null)`, `RemoveMyActivatedAbility(ref this.FlamingHandsActivatedAbilityID, null)`, `CooldownMyActivatedAbility(mutation.FlamingHandsActivatedAbilityID, 10, null)`, `IsMyActivatedAbilityAIUsable(this.FlamingHandsActivatedAbilityID, null)`, and `Mutation`/`Unmutate`/`ChangeLevel`/`GetDescription`/`GetLevelText` overrides. `BaseMutation` "derives from Part, so the typical event registration and handling functions are available." New abilities default to *not* usable on the world map; `IsWorldMapUsable: true` opts in.

**Where abilities appear in the UI / keybinds**: the third `AddAbility` argument is the category string ("Skill", "Physical Mutation", …) that groups the ability in the ability menu; the second is the command event name that the ability fires when invoked. For player-facing keybinds of *custom* commands, see §6.2 (the `Commands.xml` + key-mapping pipeline), which is a separate mechanism from `ActivatedAbilities`.

### 4.4 `StatShifter` for temporary stat changes

"Anything that inherits from `IPart` or `Effect` has utility methods for tracking stat shifts applied to targets… The StatShifter API takes care of the book keeping of the 'current shift' for you, giving convenient `RemoveStatShifts`, or the ability to `SetStatShift` again to overwrite a previous shift with a new value." All methods are "designed to be safe to call with empty/null objects, an amount of 0, or other cases that would result in no stat shifts applied at all."

Documented API:

- `StatShifter.SetStatShift(GameObject target, string stat, int amount, bool baseValue = false)` — "You can call `SetStatShift` again with a different amount, and it will remove the previous and apply the new. The `baseValue` option is provided in case you want to stat shift the `HitPoints` stat as the 'BaseValue' is your max instead of the normal 'Value' used for everything else. This version takes a target allowing a Part on a piece of armor/worn inventory to target the wearer, instead of boosting stats of the armor object by default."
- `StatShifter.SetStatShift(string stat, int amount, bool baseValue = false)` — applies to the `Owner` of the StatShifter: "for a part or mutation would be `ParentObject` and for an effect `Object`, either way, the object that owns the part / effect."
- `StatShifter.RemoveStatShift(GameObject target, string stat)`
- `StatShifter.RemoveStatShifts(GameObject target)`
- `StatShifter.RemoveStatShifts()` — "Remove all stat shifts applied by this shifter to all objects."

Descriptions: "The StatShifter also takes care of creating a 'description' for the shift. It defaults to the `GetDescription()` of the Effect, and `""` for a part. The 'owner' of the shifter is compared with the 'target' of the shift. When they differ it will look like '{Owner}'s {DefaultDisplayName}' - When they are the same, it will just be set to DefaultDisplayName… This 'DisplayName' for the shift is used with the Debug option that shows stat shifts as well as the `showstatshifts` wish." You can set `StatShifter.DefaultDisplayName` per part.

Verbatim example (grassy yurtmat, +2 DV camouflage):

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

"This results in `showstatshifts` wish telling us `+2 from Grassy Yurtmat's camouflage`."

### 4.5 Where to find the game's API / decompiled assembly

From `Modding:Scripting`:

- "**There is not a public repository of Caves of Qud's source code.** However, there are several third-party tools available for decompiling `.dll` files into equivalent `.cs` files. Due to the nature of decompilation the decompiled file will be somewhat different from the original: Names of variables, objects, and methods may be different and there will be no code comments."
- "**ILSpy** can be used to decompile the source code. Point it at **`Assembly-CSharp.dll`**, which can be found in the **`QudLibPath`** directory." `QudLibPath` resolves to `[Steam root folder]\steamapps\common\Caves of Qud\CoQ_Data\Managed` on Windows, `~/.steam/steam/steamapps/common/Caves of Qud/CoQ_Data/Managed` on Linux (macOS path not listed on the File locations page for `QudLibPath`).
- "Note that the decompiled code will normally have errors due to being removed from its intended context."
- Visual Studio "ships with the ILSpy .NET decompilation engine included… Note that Microsoft Visual Studio is an entirely seperate program from Microsoft Visual Studio **Code**. Developers using Mac and Linux can use Visual Studio Code with the ILSpy plugin for similar functionality."
- "You can press **F12** in order to navigate to the definition of a selected object, variable, or method. If the definition is in the source code, Visual Studio will automatically decompile and open the relevant file."
- The API surface is the **`XRL`** namespace family: `XRL.World`, `XRL.World.Parts`, `XRL.World.Parts.Mutation`, `XRL.World.Parts.Skill`, `XRL.World.ZoneParts`, `XRL.World.ObjectBuilders`, `XRL.World.AI`, `XRL.World.Text.Delegates`, `XRL.World.Text.Attributes`, `XRL.Core`, `XRL.Rules`, `XRL.UI`, `XRL.Messages`, `XRL.Wish`, plus `ConsoleLib.Console` and `UnityEngine`. The wiki also documents that the game's own documentation-comment style exists in source (`/// <summary>` blocks appear in the `IComposite` quote).

---

## 5. Harmony

`Modding:Harmony` is marked `{{Stub}}`. What it documents:

- "**Harmony** is a library for dynamically patching C# code. In the context of Caves of Qud modding, it's useful for changing the behavior of things that are not explicitly exposed through the game's modding interface. **Harmony is included in the base game, and as such doesn't require any external mods to use.**"
- The page points at the upstream primer: <https://harmony.pardeike.net/articles/patching.html>.
- Fragility/compatibility warning: "While Harmony Patches are powerful and are often the easiest and most direct way to modify behaviour, they are **more prone to incompatibility with other mods and future updates**, and they can be more difficult to debug or troubleshoot if something goes wrong. A Harmony patch should be considered a **last resort** only if the desired functionality cannot otherwise be achieved by using the existing part and event infrastructure available in the game, or another similar solution."
- Patch-kind ranking: "In these cases, **Postfix patches tend to be the most compatibility friendly, followed by non-blocking Prefix patches. Prefix patches that prevent the main function from running or Transpiler Patches that modify the IL Code of a function are often more likely to conflict with other mods and should be avoided** unless they are the only option."
- Developer preference and escalation path: "In general, the Caves of Qud development team prefers data driven behavior, and would be likely to entertain adding a better hook for your mod to use than a Harmony patch. Specifically, reach out to `@gnarf37` or `@armithaig` in `#modding` on the [Official Discord Server](https://discord.gg/cavesofqud)."

Verbatim example — "Create `somefile.cs` in your `.../Mods/ModName/` directory":

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

Documented mechanics visible in that sample: `using HarmonyLib;`, class-level `[HarmonyPatch(typeof(TargetType))]`, method-level `[HarmonyPatch("MethodName")]` (string method-name overload), `[HarmonyPrefix]`, `static void Prefix(ref string Message)` with a `ref` parameter to rewrite an argument.

**[UNDOCUMENTED ON THE WIKI]:**
- **Required NuGet package / DLL references.** The page says only that Harmony ships with the game. No Harmony NuGet package, version, or `HarmonyLib.dll` reference path is given. The only documented reference source is the game-generated `Mods.csproj` (§7).
- **An explicit patching entry point.** `Harmony.PatchAll`, `new Harmony(id).PatchAll()`, `Harmony.CreateAndPatchAll`, `[HarmonyPatchAll]`, and manual `harmony.Patch(...)` are **not mentioned anywhere on the wiki**. The single example relies on attribute-driven automatic patching, and the wiki never states how/when those attributes are applied. (Practical implication: the parent project should verify `PatchAll` availability against the decompiled `Assembly-CSharp.dll`/Harmony version rather than assume it.)
- **Transpiler example code.** Transpilers are discussed only as a risk category; no `[HarmonyTranspiler]`/`CodeInstruction` sample exists on the wiki.

---

## 6. Options, keybinds, serialization, randomness

### 6.1 Mod options (`Options`)

"you can piggyback off of Qud's built-in Options menu by creating new options via XML." Any XML file whose root is `<options />` is parsed; only files with the word `Option` in the filename are considered for conditional content loading.

Verbatim example:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<options>
  <option ID="Option_MyName_MyMod_EnableFoo" DisplayText="Enable Foo" Category="Mods: MyMod"
    Type="Checkbox" Default="Yes" Restart="true">
    <helptext>
      Controls whether Foo is enabled.
    </helptext>
  </option>
  <option ID="Option_MyName_MyMod_FooSelector" DisplayText="Foo, Bar, or Baz?" Category="Mods: MyMod"
    Type="Combo" Values="Foo,Bar,Quux|Baz" Default="Foo" />
  <option ID="Option_MyName_MyMod_FooAmount" DisplayText="Amount of foo to use" Requires="Option_MyName_MyMod_EnableFoo==Yes" Category="Mods: MyMod"
    Type="Slider" Min="0" Max="100" Increment="2" Default="50" />
</options>
```

Documented `<option/>` attributes: `ID` ("A unique ID… This ID is used when retrieving the value of the configured setting. By convention, this ID starts with `Option`, and per best practices it is encouraged to have it also include the mod's ID. If this attribute is set to an already existing option, it will overwrite it."), `DisplayText`, `Category` ("In general, all of your options should go under a single custom category for your mod."), `Type`, `Default`, `Requires`, `SearchKeywords`, `Restart` (true → "Players will be prompted to restart their game when they change these options… useful in conjunction with conditional path loading"). A `<helptext />` child node supplies the hover tooltip.

Types: `Checkbox` (Yes/No), `Combo` / `BigCombo` (`Values` comma-delimited; `0|None` form uses the left value as the option value and the right for display; "In the Modern UI, there is no visual distinction between Combo and BigCombo options."), `Slider` (`Min`, `Max`, `Increment`), `Button` (`OnClick` "must be a fully-named method (e.g., `Qud.UI.KeybindsScreen.ShowKeybindsClick`), and may be an async function").

Requirement specs: "a comma-delimited list of entries of the form `OptionID==Value`… or `OptionID!=Value`… A specification is only satisfied when all entries are satisfied."

Reading options in code — modern attribute approach (verbatim):

```csharp
using System;
using XRL;

namespace MyName.MyMod
{
  [HasOptionFlagUpdate(Prefix="Option_MyName_MyMod_")]
  public static class Options
  {
    [OptionFlag] public static bool EnableFoo;
    
    public static FooTypes FooVariant;
    [OptionFlag("FooSelector")]
    private static string _FooVariantBind
    {
      set
      {
        FooVariant = value switch
        {
          "Foo" => FooTypes.Foo,
          "Bar" => FooTypes.Bar,
          "Quux" => FooTypes.Quux,  // internal value for Baz
          _ => FooTypes.Foo
        };
      }
    }
    
    [OptionFlag] public static int FooAmount;
    [OptionFlagUpdate] public static void OptionUpdate()
    {
      // you could update some UI elements, or change states when the Flags Update
    }
  }
}
```

`[OptionFlag]` "can be applied to a static field or property on a modded class. This property will be automatically set when the user changes options in the option menu. It works with string (Combo), int (Slider), and bool (Checkbox) options. The Combo option type can also map to an int or bool as long as the values of that option parse. You must include the `[HasOptionFlagUpdate]` on the class for your attributes to be found. `[OptionFlagUpdate]` can also be put on a `public static void` method and will be called after updating the `[OptionFlag]`s."

Legacy approach (verbatim):

```csharp
using System;

namespace MyName.MyMod {
    public static class Options {
        private static string GetOption(string ID, string Default = "") {
            return XRL.UI.Options.GetOption(ID, Default: Default);
        }

        public static bool EnableFoo => GetOption("Option_MyName_MyMod_EnableFoo").EqualsNoCase("Yes");
        public static int FooAmount => Convert.ToInt32(GetOption("Option_MyName_MyMod_FooAmount"));
    }
}
```

Options can gate XML loading through the `Options` field of `Directories` entries in `manifest.json` (with the documented quirk that the referenced options file must have `Option` in its filename, "caused by populating the option defaults prior to directory initialization").

Documented failure mode: "If an options file contains a syntax error, it will be listed in the `Player.log` as normal. However, **type errors are only evaluated on opening the in-game options menu. The menu will fail to open** and you'll see an error in the `Player.log`: `---> (Inner Exception #0) System.FormatException: Input string was not in a correct format.` If you save the game without opening the options menu, this erroneous value will then be stored in `PlayerOptions.json` and, because those values are only regenerated on close of the options menu, you will need to manually edit and correct the file."

### 6.2 Key mapping / commands

"Mods can add custom key mapping entries to the Key Mapping menu… When the player presses that key, **the mod's custom command is received as an event on the player character**. This means that at least a small amount of C# scripting is required to take advantage of the key mapping infrastructure."

`Commands.xml` example (verbatim):

```xml
<?xml version="1.0" encoding="utf-8"?>
<commands>
  <command ID="ModName_Cmd_One" DisplayText="My Custom Debug Command" Category="Debug"></command>
  <command ID="ModName_Cmd_Two" DisplayText="My Custom Cool Command" Category="Cool"></command>
</commands>
```

"In this case, we've added one command to the existing 'Debug' category in the Key Mapping menu. We've added another command to a new category - the 'Cool' category."

The listener part (verbatim):

```csharp
using System;

namespace XRL.World.Parts
{
    [Serializable]
    public class ModName_CommandListener : IPart
    {
        public static readonly string CmdOne = "ModName_Cmd_One";
        public static readonly string CmdTwo = "ModName_Cmd_Two";

        public override void Register(GameObject Object)
        {
            Object.RegisterPartEvent(this, CmdOne);
            Object.RegisterPartEvent(this, CmdTwo);
            base.Register(Object);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == CmdOne)
            {
                //Do something when the keybind for ModName_Cmd_One is pressed!
            }
            if (E.ID == CmdTwo)
            {
                //Do something when the keybind for ModName_Cmd_Two is pressed!
            }
            return base.FireEvent(E);
        }
    }
}
```

Attachment to the player and shipping layout (verbatim):

```csharp
  using XRL;
  using XRL.World;
  
  [PlayerMutator]
  public class ModName_PlayerMutator : IPlayerMutator
  {
      public void mutate(GameObject player)
      {
          // add your command listener to the player when a New Game begins
          player.AddPart<ModName_CommandListener>();
      }
  }
```

```yaml
<Caves of Qud App Directory>
    Mods
        MyModFolder
            Commands.xml
            ModName_CommandListener.cs
            ModName_PlayerMutator.cs
```

Note the version-dependent `Register` signature: this page (and the Key Mapping, Activated Abilities, and Mutations pages) uses `public override void Register(GameObject Object)` with explicit `Object.RegisterPartEvent(this, …)` calls, while `Modding:Events` and the Spring Molting page use `public override void Register(GameObject Object, IEventRegistrar Registrar)`. **The wiki is internally inconsistent across game versions**; the `IEventRegistrar` form is described as "new with this update" (Spring Molting), so the one-argument form in older articles is the pre-Spring-Molting API. Corresponding unregister call seen in wiki code: `GO.UnregisterPartEvent(this, "EnteredCell")`.

`[Command]`-style C# attribute registration **[UNDOCUMENTED ON THE WIKI]** — commands are declared in `Commands.xml`, not via a C# attribute. There is no documented keybind JSON (the wiki documents `Commands.xml` only; `PlayerOptions.json` is the game's own option store, not a mod-authoring surface).

### 6.3 Serialization

Documented basics:

- Required usings, verbatim: `using System;` and `using SerializeField = UnityEngine.SerializeField;`.
- "In many cases, you can simply include the `[Serializable]` attribute at the top of your class definition, and Qud's game engine will take care of serializing all of your `public` class fields for you. Specifically, Qud is capable of correctly serializing all intrinsic types (such as `string`, `int`, etc.) as well as containers of those types (such as `List<string>`)."
- "Note that `private`, `protected`, and `static` class fields are **not** serialized by default… you need to mark each individual private or protected field with a `[SerializeField]` attribute. **Static fields cannot be serialized in this manner.**"

```csharp
using System;
using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [Serializable] //this attribute causes all public fields to be serialized automatically
    public class MyCoolModPart : IPart
    {
        public float CoolnessRatio;  //this field gets serialized!
        public int AbilityLevel;     //so does this field!
        private string CurrentValue; //because it's marked private, this field does NOT get serialized - the value is cleared out after a saved game is reloaded! Be careful with how you use this value.
        [SerializeField]
        private string SecretID;     //this field DOES get serialized even though it's private, because it's marked with the SerializeField attribute!

        public void MyMethod()
        {
            //...
        }
    }
}
```

- "**Object fields are not serialized automatically.** If your class introduces a new field for an object type or a container of objects, such as a `GameObject` field… you must implement custom serialization for those object fields, or your object references will no longer be valid after the game is reloaded."
- Inherited object fields are already handled: "`IPart` contains the field `public GameObject _ParentObject`… `IPart.Write()` contains the special logic necessary to serialize that GameObject when the game is saved, and `IPart.Read()` contains the logic required to deserialize that object… This means that if your class extends IPart, you can safely rely on the fact that references to the parent object, such as `this.ParentObject`, will always be valid in your code."
- Advice: "If you can find a way to reference objects in your code without explicitly saving a reference to those objects in a new field, it's generally a good idea to avoid creating that field."
- Custom serialization: mark the field `[NonSerialized]` and override `Read`/`Write`. "If your class extends `IComponent` (which includes descendants like `IPart` and `Effect`), you should override the `Read` and `Write` methods… **These are the only virtual methods the game currently makes available for serialization.** If you need to serialize data outside of a class that extends `IComponent`, you will need to look at the `SerializationReader` and `SerializationWriter` classes and implement your own serialization logic." Helpers exist: "serializing GameObject fields or lists is particularly easy if you use the `WriteGameObject` or `WriteGameObjectList` functions."

Verbatim game example (`Inventory`):

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

		//...

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

		//...

		[NonSerialized]
		public List<GameObject> Objects = new List<GameObject>();

		//...
	}
}
```

Verbatim custom-list example (**the wiki flags this as incomplete**: "The code below assumes that `StandAbility` has implemented a meaningful Write method when it calls `ability.Write(Basis, Writer)`. It's not really a complete example." — and note the comment/code mismatch: the comment says "call base.SaveData" while the code calls `base.Write`):

```csharp
...
        // The non-serialized attribute signals not to save this list when Write is called.
        [NonSerialized]
        public List<StandAbility> abilities = new List<StandAbility>();

        // Write is called when the game is ready to save this object, so we override it here.
        public override void Write(GameObject Basis, SerializationWriter Writer)
        {
            // We have to call base.SaveData to save all normally serialized fields on our class
            base.Write(Basis, Writer);
            // Writing out the number of items in this list lets us know how many items we need to read back in on Load
            Writer.Write(abilities.Count);
            foreach (StandAbility ability in abilities)
            {
                // Here, we call the save function on each ability because they are game objects
                ability.Write(Basis, Writer);
                // If our list was full of basic types, such as integers, instead, we would call Writer.Write, for example:
                // Writer.Write(someNumber)
            }
        }

        // Read is called when loading the save game, we also need to override this
        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            // Load our normal data
            base.Read(Basis, Reader);
            // Read the number we wrote earlier telling us how many items there were
            int arraySize = Reader.ReadInt32();
            for (int i = 0; i < arraySize; i++)
            {
                // Load returns a generic object, so we have to cast it to our object type before we add it to our list.
                abilities.Add((StandAbility) Reader.ReadObject());
                // If we had a basic type in our list, we would instead use the Reader.Read function specific to our object type.
            }
        }
...
```

Documented reader/writer members: `Writer.WriteGameObjectList(...)`, `Writer.Write(value)`, `Writer.WriteNamedFields(this, GetType())`, `Reader.ReadGameObjectList(...)`, `Reader.ReadInt32()`, `Reader.ReadObject()`, `Reader.ReadString()`, `Reader.ReadNamedFields(this, GetType())`, `Reader.ModVersions["ExampleMod"]` (returns a `System.Version`).

**What breaks saves** (documented):

- "Qud uses **binary blobs** for save data… to some extent it also relies on certain things not changing across game and mod versions, so it can be **easily invalidated by certain types of changes**."
- Adding/removing fields on a plain `[Serializable]` `IPart`: "Historically this has been a major issue, as **replacing fields in a part could permanently corrupt its parent object.**" Post-Spring-Molting the game removes unknown parts gracefully, at the cost of surfacing errors to the user.
- Renaming a class works (the old part is dropped as unknown) but "will surface errors to the user", and "in some situations removing a part altogether may be undesirable if its functionality is critical to the operation of an object."
- You "cannot change the type of an existing field" in an `IScribed` class.
- `Read` limitations: "Note that **you cannot remove a part when calling `Read`**. Moreover, certain fields may not be initialized and other objects/effects may not have been deserialized at the point where `Read` is called. Thus, you will typically want to use `Read` only for the purpose of deserializing fields, and **defer the actual migration to a later point**."

**Recommended modern approach:** "In version 207.69, three new interfaces were added… `IScribedEffect`, `IScribedPart`, and `IScribedSystem`. These classes work like their non-'scribed' counterparts, except that you can add and remove fields from them at will. **It is strongly recommended that you use these new interfaces in place of `Effect` or `IPart` for new code.**" Cost: "the former writes fields alongside their names during serialization, while the latter does not. As a result, IScribed classes increase save size, and reduce serialization/deserialization speed… You should in general only consider avoiding the IScribed interfaces for `IComponent`s that have *many* fields… and are added to *many* objects." To scribe an existing class you cannot re-base (e.g. an `IActivePart`), implement:

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

**Migration pattern** (verbatim, abridged to the mechanics): read `Reader.ModVersions["ExampleMod"]`, branch on `System.Version` comparisons, stash a `Version? MigrateFrom`, read only the fields that existed in that version, and finish the migration in an `AfterGameLoadedEvent` handler — which is also where you are allowed to `ParentObject.RemovePart(this)`:

```csharp
        public override void Read(GameObject Basis, SerializationReader Reader) {
            var modVersion = Reader.ModVersions["ExampleMod"];

            if (modVersion >= (new Version("0.3.0"))) {
                base.Read(Basis, Reader);
                return;
            }

            // Set MigrateFrom so that we know we'll need to migrate from an
            // older version of the part.
            MigrateFrom = modVersion;

            // S1 has existed in all versions of the part
            S1 = Reader.ReadString();

            // S2 was only added in v0.2.0
            if (modVersion >= (new Version("0.2.0")))
                S2 = Reader.ReadString();
        }
```

For classes that inherit serialized state (e.g. `IActivePart` with "dozens" of fields), the documented strategy is to mark **all** of your own fields `[NonSerialized]`, deserialize them manually, and call `base.Read`/`base.Write` last so the parent's fields are handled by the base implementation rather than by reflection over your fields.

Other documented deserialization hooks: "**`ReadError`** can be overridden on children of `IComponent` to handle exceptions raised during deserialization. This function can be used as a fallback if version checking is insufficient." and "**`FinalizeRead`** can be used as an alternative to having an `AfterGameLoadedEvent` handler to hook the end of object deserialization. This method gets called on all parts and effects attached to an object once the object has been fully deserialized." A composite/custom data container can implement `IComposite` (`WantFieldReflection` + `Write(SerializationWriter)` / `Read(SerializationReader)`).

Note for the requested `ISerializable` interface / `Serialize`/`Deserialize` methods: **[UNDOCUMENTED ON THE WIKI]**. The wiki documents `[Serializable]`, `[SerializeField]`, `[NonSerialized]`, and the `Read`/`Write` overrides — not a Qud `ISerializable` interface.

### 6.4 Randomness

- "calling the built in `Next()` function in the Random Class built into C# will net you some compilation errors. **The best practice is to use the random functions inside `XRL/Rules/Stat.cs`.**"

The documented seed derivation (verbatim):

```csharp
      Stat.Rand = new Random(Hash.String("Seed0" + (object) Seed));
      Stat.Rnd = new Random(Hash.String("Seed1" + (object) Seed));
      Stat.Rnd2 = new Random(Hash.String("Seed2" + (object) Seed));
      if (includeLifetimeSeeds)
        Stat.LevelUpRandom = new Random(Hash.String("Seed3" + (object) Seed));
      Stat.Rnd4 = new Random(Hash.String("Seed4" + (object) Seed));
      Stat.Rnd5 = new Random(Hash.String("Seed5" + (object) Seed));
```

Generators and determinism consequences:

- `Rnd()` — "the main function that randomizes and determines things such as sultan artifacts, villages, and many other things based on world seed. **Using this generator or `Stat.Random` in a mod may cause a world seed to give different results than in vanilla.**"
- `Rnd2()`, `Rnd4()`, `Rnd5()` — "deals with the smaller events you find in Qud."
- `LevelUpRandom()` — "The Random used to determine randomized things at level up."
- `GetSeededRandomGenerator(string Seed)` — "If you don't want to use the game's seeds in fear of altering worldseed too much, there is a helper function that returns a new random that hashes a new random. **This allows your mod to be affected by world seed but does not alter the base game's random number calls.**"

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

"The Seed here should follow best practice conventions, so `YourName_YourMod` to avoid overlapping conflicts as much as possible."

Returners: `Random(int low, int high)` → calls `Stat.Rnd()`; `TinkerRandom(int Low, int High)` → calls `Stat.Rnd4()`; `RandomCosmetic(int low, int high)` → calls `Stat.Rnd2()` ("Used most often when getting random angles for particles and other cosmetic effects"); `SeededRandom(string Seed, int Low, int High)` → "Returns an int based on `Seed`, in the range of `[Low, High]`"; and:

```csharp
double num = Math.Sqrt(-2.0 * Math.Log(Stat.Rnd.NextDouble())) * Math.Sin(2.0 * Math.PI * Stat.Rnd.NextDouble());
      return (double) Mean + (double) StandardDeviation * num;
```

(`GaussianRandom(float Mean, float StandardDeviation)` — "Returns a random float around the mean with a probability density of a normal distribution.")

The recommended mod-local provider (verbatim) — note it combines `[HasGameBasedStaticCache]` with `[GameBasedCacheInit]` and stores its seed in the game state:

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

Usage:

```csharp
MODNAME_Random.Next(1, 10);
```

```csharp
someList.ShuffleInPlace(MODNAME_Random.Rand);
```

Hard rule from `Modding:Compatibility`: "**To avoid conflicts and to keep consistency between seeds, `Stat.Random()` and `Stat.Rnd()` should not be called. The ideal is to use `GetSeededRandomGenerator()` for your mod's randomness. The next best is calling `RandomCosmetic()` or `Rnd2()`.**" Note the deterministic-order implication: any call into `Rnd()`/`Stat.Random` shifts the global stream, so other systems (including vanilla worldgen) can diverge from the seed's vanilla results; `GetSeededRandomGenerator` hashes the world seed with your own string, so it is seed-dependent but does not consume the shared stream.

---

## 7. Practical gotchas

### 7.1 Build requirements and project setup

Documented workflow (`Modding:Scripting`, "Visual Studio Setup Guide"):

1. Create a working directory for the mod **outside** the game's `Mods` folder — "It is not recommended that this be inside the Mods folder of the game, as this folder will also contain some auto-generated files from Visual Studio that are not necessary to include in your finished mod."
2. Enable "Enable Mods (restart required.)", "Select enabled mods on new game.", "Allow scripting mods. Scripting mods may contain malicious code!" in the game's Modding settings; close and reopen the game.
3. Title screen → bottom-right → **"Modding Utilities"** → **"Write Mods.csproj file"**. "This will create a `Mods.csproj` file in your configuration files directory."
4. Copy `Mods.csproj` into the working directory and open it in Visual Studio; VS creates `obj`, `bin`, `.vs` "which are metadata used by Visual Studio and can be ignored."
5. Optionally rename the project and edit "Author title, AssemblyName, or other fields".
6. **Known noise:** "Due to Issue #9116, you may see approximately 50 warnings in the error list. This is because `Mods.csproj` erroneously contains references to files which do not actually exist in the public release of Caves of Qud. These warnings may safely be ignored, or the erroneous references can be deleted from the `Mods.csproj` file."

Language/runtime: "Caves of Qud is programmed in the **C Sharp v9.0** language". The wiki does **not** name a specific .NET/Mono target framework, SDK version, or `LangVersion` setting **[UNDOCUMENTED: target framework / SDK version]** — the generated `Mods.csproj` is the authoritative source and should be inspected rather than assumed.

**Is a DLL shipped, or is source compiled by the game?** The documented answer is that **the game compiles the `.cs` sources itself**: "These files are compiled at runtime into an assembly, meaning that adding new C# code is technically as simple as including `.cs` files somewhere in your mod directory," and `Modding:Overview` lists `*.cs` files as content that "can be nested anywhere within the mod folder." The wiki never instructs you to ship a compiled DLL from your working directory **[UNDOCUMENTED: whether prebuilt DLLs are also loaded/supported]**. The `Mods.csproj` generated by the game exists to give the IDE the game's assembly references and IntelliSense, not to produce a shipped artifact.

Unity references available to mods (observed in wiki code): `UnityEngine.Debug`, `UnityEngine.SerializeField`, plus the game's own `ConsoleLib.Console` (`ScreenBuffer`, `DialogResult`) and `HarmonyLib`.

### 7.2 Errors, logs, and debugging

- **Build/load time errors** → the build log. `Modding:Overview`: "At **load time**, when the game loads all content from all enabled mods. You can find load errors and warnings in the **build log**. The mod manager, available from the main menu, will also note errors on each mod by clicking on it." `Modding:Scripting`: "Errors that occur during the initial building-in of your mod files are logged in `build-log.txt`. These errors will usually cause a mod to be unable to build, which prevents it from being enabled by the player."
  - **Filename discrepancy in the wiki**: `Modding:Scripting` says `build-log.txt`; `File locations` says `build_log.txt`. On Windows the path is `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\build_log.txt`; macOS `~/Library/Logs/Freehold Games/CavesOfQud/build_log.txt`; Linux `~/.config/unity3d/Freehold Games/CavesOfQud/build_log.txt`.
- **Run time errors** → the player log. Windows `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log`; macOS `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`; Linux `~/.config/unity3d/Freehold Games/CavesOfQud/Player.log`. "To see them immediately as they happen, enable 'Show error popups' under the debug options. **Note that there are some situations where this option can softlock your game.**"
- In-game debug output: `XRL.Messages.MessageQueue.AddPlayerMessage("Hello world!")` (message log) and `UnityEngine.Debug.LogError("Hello world!")` / other `UnityEngine.Debug` methods (Player.log).
- File locations for everything else: mods (offline) `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods`; Steam Workshop mods `[Steam root]\steamapps\workshop\content\333640\[id]`; configuration files `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud`; game data `[Steam root]\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base`; `QudLibPath` `[Steam root]\steamapps\common\Caves of Qud\CoQ_Data\Managed`.
- "If you later upload it to the Steam Workshop and subscribe to it, make sure to move your working copy elsewhere to avoid conflicts."

### 7.3 Version breakage and interface drift

- **Concrete example documented by the wiki itself:** the event-registration override changed from `public override void Register(GameObject Object)` (older wiki articles: Key Mapping, Activated Abilities, Mutations) to `public override void Register(GameObject Object, IEventRegistrar Registrar)` ("IEventRegistrar is also new with this update"). Likewise `IEventHandler`, `IModEventHandler<T>`, `ModPooledEvent<T>`, `ModSingletonEvent<T>`, `EventOrder`, `Registrar.Register(The.Player, …)` cross-object registration, and `IPart.Priority` all arrived with the 2024 Spring Molting patch. `ModPooledEvent`-based custom events also replaced a reflection-driven custom-event mechanism ("had significant performance drawbacks because the game had to invoke any handlers of the event using reflection").
- `IScribedPart` / `IScribedEffect` / `IScribedSystem` arrived in **version 207.69** specifically to make interface/field drift survivable; `IComposite` and the ability to "gracefully remove missing mod data from an ongoing save without corrupting it" arrived with Spring Molting.
- Mitigations documented by the wiki: prefer `IScribed*` for new code; use `Read`/`Write` + `Reader.ModVersions` migrations; use **named arguments** for optional parameters — "By specifying only the arguments you want to set, your code will adapt to changes in default values and many common parameter list changes (e.g. the addition of a new optional parameter or the reorganization of optional parameters)… If the parameter list changes in a way that cannot be automatically resolved (e.g. the removal of a parameter you set), the error message will generally include the name of your argument instead of the index (or worse, continuing to compile with the argument going to the wrong parameter)." Followed by: "**Avoid including positional arguments after named ones!** Calling `void Foo(int A, string B, bool C)` like `Foo(A: 0, "blah", C: false)` will cause cryptic errors if the parameter list gets reorganized."
- Gate version-specific code/XML with `manifest.json` `Directories` entries using the `Version` (game `XRLGame.MarketingVersion`, "the bright number in the bottom right of the main menu") and `Build` (game `XRLGame.CoreVersion`, "the dark number prefixed with 'build'") version ranges; the syntax is documented (`*`, `1.0.*`, `2.0.208 - 3.0.0`, `>=2.0.209.52`, `>3.5`, `>=2 <5`, `^0.5.2 || 7.2.1`).
- Prefixing everything unique (part class names, blueprint names, event names, wish commands, seeded-random seeds, option IDs) is the documented defence against both mod conflicts and future base-game additions.
- Steam Workshop "marked incompatible" flow: "1. Fix any errors it causes. 2. Upload a new version to the Steam Workshop. 3. Request that your mod be unmarked as incompatible through either the support email or the Caves of Qud Discord's `#modding` channel."

### 7.4 Other documented traps

- **`Read` cannot remove parts**; defer migration to `AfterGameLoadedEvent`, `FinalizeRead`, or `ReadError`.
- **`ForeachActivePartSubjectWhile`**: set `MayMoveAddOrDestroy: true` if your callback can move/create/destroy objects, "otherwise, there is a risk of exceptions being thrown due to loop control disruption."
- **`Stat.Random()` / `Stat.Rnd()` are off-limits** for mods (seed divergence).
- **Options type errors are only caught when the options menu is opened**, and a bad value can already have been persisted into `PlayerOptions.json`.
- **Blueprint preload runs for every blueprint using the part, potentially multiple times**; constructors run on every object creation.
- **`[PlayerMutator]` silently does nothing on existing saves.** Pair it with `[HasCallAfterGameLoadedAttribute]` + `RequirePart<T>()`.
- **Harmony** is a last resort; blocking prefixes and transpilers are the most conflict-prone patch kinds.
- **Existing saves / approvals:** a changed scripting mod requires re-approval, and if the user does not approve, none of the mod's files load — including its XML.

### 7.5 Pages that errored / are stubs / are incomplete

All 13 requested URLs returned HTTP 200 (the first, `Modding:C_Sharp_Scripting`, is a redirect to `Modding:Scripting`). Status of each:

| Requested page | Status | Notes |
|---|---|---|
| `Modding:C_Sharp_Scripting` | redirect → `Modding:Scripting` | Full article: source code acquisition (ILSpy/`Assembly-CSharp.dll`/`QudLibPath`), VS setup, `Mods.csproj`, debugging. |
| `Modding:Scripting` | full | Same article. |
| `Modding:Adding_Code_at_Startup` | full | Cache/preload/PlayerMutator/Harmony overview + code. |
| `Modding:Adding_Code_to_the_Player` | full | PlayerMutator + CallAfterGameLoaded. |
| `Modding:Events` | **partially incomplete** | Self-flagged `{{Missing info}}` (no event priorities, no `IEventRegistrar`, no cross-object registration) and `<!-- TODO: Insert event table -->` — **no event catalogue exists**. Coverage of string vs. MinEvents, cascading, and custom events is otherwise substantial. |
| `Modding:Effects` | **stub** | `{{Stub}}`; covers `GetEffectType()` and type masks only. `Apply`, `Duration`, `DisplayName`, stacking undocumented. |
| `Modding:Active_Parts` | full (large) | Statuses, configuration points, full method list, big list of vanilla active parts. |
| `Modding:Harmony` | **stub** | `{{Stub}}`; one example patch, policy guidance, no references/NuGet/`PatchAll` documentation. |
| `Modding:Options` | full | XML schema, option types, `[OptionFlag]` binding, `Options.GetOption`, error behaviour. |
| `Modding:Key_Mapping_(Commands)` | full | `Commands.xml`, listener part, PlayerMutator attach, folder layout. |
| `Modding:Serialization_(Saving/Loading)` | full | `[Serializable]`/`[SerializeField]`/`[NonSerialized]`, `Read`/`Write`, `IScribed*`, migration, `ReadError`/`FinalizeRead`. One code block is self-flagged `{{Missing info}}` (incomplete custom-list example). |
| `Modding:Randomness` | full | Seed derivation, generators, `GetSeededRandomGenerator`, sample provider. Cross-referenced elsewhere as `Modding:Random Functions`. |
| `Modding:StatShifter` | full | Complete API + worked example. |

Additional pages fetched to fill gaps (beyond the requested list): `Modding:Activated_Abilities` (ability registration/activation/cooldowns/AI), `Modding:Parts` (part types, `[Serializable]`, XML `<part>`), `Modding:Mod_Configuration` (`manifest.json`/`Directories`/version ranges), `Modding:Overview` (file structure, `.cs` filename rules, mod tiers, debugging), `Modding:Wishes` (`WishCommand`), `Modding:Mutations` (`BaseMutation`, `Class` binding, decompiled `FlamingHands`), `Modding:Compatibility` (prefixing table, random-function policy, `IScribed*`, named arguments), `File_locations` (log/mod/DLL paths), and the user page `User:Armithaig/Spring_Molting_Moddability` (the source for `IEventRegistrar`, `EventOrder`, `IEventHandler`, `IModEventHandler<T>`, `IPart.Priority`, `IComposite`, `IObjectBuilder`, variable replacers).

**Explicitly undocumented (searched, zero hits):** `IMod`, `IModPart`, `Mod`-as-entry-point, `IEventRegistration`, `[OnGameInit]`, `CommandEvent`, `MinEvent<C>`, `Harmony.PatchAll`, Harmony NuGet/reference requirements, `Effect.Apply`/`Duration`/`DisplayName`/stacking, `ISerializable`/`Serialize`/`Deserialize` methods, the full `ActivatedAbilities.AddAbility` parameter list, and the target .NET/Mono framework version.

---

## Sources

- [Modding:Scripting](https://wiki.cavesofqud.com/wiki/Modding:Scripting) (also served at [Modding:C Sharp Scripting](https://wiki.cavesofqud.com/wiki/Modding:C_Sharp_Scripting), a redirect)
- [Modding:Adding Code at Startup](https://wiki.cavesofqud.com/wiki/Modding:Adding_Code_at_Startup)
- [Modding:Adding Code to the Player](https://wiki.cavesofqud.com/wiki/Modding:Adding_Code_to_the_Player)
- [Modding:Events](https://wiki.cavesofqud.com/wiki/Modding:Events)
- [Modding:Effects](https://wiki.cavesofqud.com/wiki/Modding:Effects)
- [Modding:Active Parts](https://wiki.cavesofqud.com/wiki/Modding:Active_Parts)
- [Modding:Harmony](https://wiki.cavesofqud.com/wiki/Modding:Harmony)
- [Modding:Options](https://wiki.cavesofqud.com/wiki/Modding:Options)
- [Modding:Key Mapping (Commands)](https://wiki.cavesofqud.com/wiki/Modding:Key_Mapping_(Commands))
- [Modding:Serialization (Saving/Loading)](https://wiki.cavesofqud.com/wiki/Modding:Serialization_(Saving/Loading))
- [Modding:Randomness](https://wiki.cavesofqud.com/wiki/Modding:Randomness)
- [Modding:StatShifter](https://wiki.cavesofqud.com/wiki/Modding:StatShifter)
- [Modding:Activated Abilities](https://wiki.cavesofqud.com/wiki/Modding:Activated_Abilities)
- [Modding:Parts](https://wiki.cavesofqud.com/wiki/Modding:Parts)
- [Modding:Mod Configuration](https://wiki.cavesofqud.com/wiki/Modding:Mod_Configuration)
- [Modding:Overview](https://wiki.cavesofqud.com/wiki/Modding:Overview)
- [Modding:Wishes](https://wiki.cavesofqud.com/wiki/Modding:Wishes)
- [Modding:Mutations](https://wiki.cavesofqud.com/wiki/Modding:Mutations)
- [Modding:Compatibility](https://wiki.cavesofqud.com/wiki/Modding:Compatibility)
- [File locations](https://wiki.cavesofqud.com/wiki/File_locations)
- [User:Armithaig/Spring Molting Moddability](https://wiki.cavesofqud.com/wiki/User:Armithaig/Spring_Molting_Moddability)
- [Harmony patching primer](https://harmony.pardeike.net/articles/patching.html) (linked from the wiki)
