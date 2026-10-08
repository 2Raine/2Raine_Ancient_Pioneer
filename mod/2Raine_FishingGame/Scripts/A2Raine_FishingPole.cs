using System;
using System.Collections.Generic;
using System.Threading;
using ConsoleLib.Console;
using XRL.Liquids;
using XRL.Rules;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// The fishing pole. Its only job is to turn "cast this item" into "start a fishing game at a
    /// body of liquid", and to make that reachable without opening the inventory every time.
    ///
    /// TWO WAYS IN, on purpose:
    ///   1. Equipped -> you get a "Cast Line" activated ability on your ability bar (default key f
    ///      until you rebind it), so fishing is one keypress while the pole is in hand.
    ///   2. In the pack -> the old inventory verb "cast your line" still works.
    ///
    /// The ability plumbing is copied wholesale from AjiConch.cs:64-107, which is the base game's
    /// own example of "a worn item that hands its wearer a command": AddDynamicCommand on Equipped,
    /// RemoveActivatedAbility + UnregisterPartEvent on Unequipped, and a CommandEvent handler in
    /// between. Nothing about it is invented here.
    ///
    /// LIQUID GATING. The intent is that every liquid in the game is eventually fishable -- not just
    /// water -- so this does NOT test for water. It tests for liquid, and then asks whether this
    /// particular pole can stand it. Three of the 28 liquids are flagged ConsiderDangerousToContact
    /// (acid, lava, neutronflux -- BaseLiquid.cs:78, LiquidAcid.cs:33, LiquidLava.cs:32,
    /// LiquidNeutronFlux.cs:34); those need a pole with CanFishDangerous, i.e. a future lava pole.
    /// The other 25 (water, salt, blood, oil, wax, wine, honey, ooze, slime ...) take an ordinary
    /// pole. More bands can be added later by giving the part more fields; one bool is enough for
    /// the dangerous/not split the plan actually asks for today.
    ///
    /// PART NAMESPACE: XRL.World.Parts, and the XML refers to it by bare name
    /// (&lt;part Name="A2Raine_FishingPole" /&gt;) because the prefix is prepended unconditionally.
    ///
    /// The leading "A" is not decoration: C# identifiers may not begin with a digit, so the
    /// 2Raine_ mod prefix has to be spelled A2Raine_ on anything that is a class.
    ///
    /// LANGUAGE LEVEL: /langversion:5 (see _tools/check_csharp.ps1) -- no ?., no expression bodies,
    /// no out var, no string interpolation.
    /// </summary>
    [Serializable]
    public class A2Raine_FishingPole : IPart
    {
        /// <summary>Shown on the ability bar.</summary>
        public const string ABILITY_NAME = "Cast Line";

        /// <summary>Internal command id. Prefixed so it cannot collide with a vanilla command.</summary>
        public const string COMMAND_NAME = "A2Raine_CastLine";

        /// <summary>Action point cost of one cast, on the same scale as a single attack (1000).</summary>
        public const int ENERGY_PER_CAST = 1000;

        /// <summary>
        /// How long the line soaks with nothing rigged, in milliseconds. The bait's SpeedBonus
        /// subtracts from this, which is what makes a bait worth carrying.
        /// </summary>
        public const int BASE_WAIT_MS = 6000;

        /// <summary>No combination of tackle can cut the wait below this.</summary>
        public const int MIN_WAIT_MS = 1500;

        /// <summary>Milliseconds per animation frame of the wait.</summary>
        public const int TICK_MS = 120;

        /// <summary>
        /// Chance in 100 that something takes the hook at all, with nothing rigged. The float's
        /// CatchBonus adds to this -- that is the "bright flasher raises the bite rate" half.
        /// </summary>
        public const int BASE_CATCH_PERCENT = 55;

        /// <summary>Nothing ever becomes a sure thing; there is always a chance of a bare line.</summary>
        public const int MAX_CATCH_PERCENT = 95;

        /// <summary>How many markers the wait-progress row is made of.</summary>
        public const int DOTS = 8;

        /// <summary>
        /// Art for one marker of the wait bar. A luminous sphere reads as something floating on the
        /// line; it is vanilla (Items.xml, the object that uses Items/sw_glowsphere.bmp). Swap this
        /// string for the mod's own tile when one exists -- nothing else depends on the choice.
        /// </summary>
        public const string BAIT_MARKER_TILE = "Items/sw_glowsphere.bmp";

        /// <summary>
        /// Chance in 100 that a fish which was tempted but not landed turns on the fisher instead of
        /// simply leaving. Set to 0 to make break-offs harmless.
        /// </summary>
        public const int ANGER_PERCENT = 35;

        /// <summary>Chance in 100 that a cast spends the bait it used.</summary>
        public const int BAIT_CONSUME_PERCENT = 40;

        /// <summary>
        /// How much the wait costs the fisher, expressed in game TURNS per real second.
        ///
        /// The conversion is worth spelling out because both sides are already in the same unit.
        /// Stomach.CookingCounter rises by exactly 1 per game turn (Stomach.cs:258) and the hungry
        /// threshold is 1200 (CalculateCookingIncrement, Stomach.cs:73) -- and Calendar.TurnsPerDay
        /// is also 1200 (Calendar.cs:13). So "full to hungry" IS one in-game day, and one turn of
        /// hunger is also one turn of world time.
        ///
        /// At 10 turns per real second, a bare 6-second soak costs 60 turns: 5% of a day's hunger,
        /// and about an hour of world time. Twenty casts to work up an appetite, which is the
        /// "don't make it punishing" note the plan called for. Raise this to make fishing costlier.
        /// </summary>
        public const int TURNS_PER_REAL_SECOND = 10;

        /// <summary>
        /// Whether a cast advances world time by the same amount. TimeTicks drives food rot, effect
        /// timers and merchant restocking, so keeping this small matters: GenericInventoryRestocker
        /// only fires past a 6000-turn gap (XRL/World/Parts/GenericInventoryRestocker.cs:125-142).
        /// At 10/second a long cast is 60 turns -- nowhere near any of those thresholds.
        /// </summary>
        public const int WORLD_TURNS_PER_REAL_SECOND = 10;

        /// <summary>
        /// How much the bite time varies, as a percentage either way. Without this every identical
        /// rig on identical water bites at exactly the same instant, which reads as a stopwatch
        /// rather than as fish.
        /// </summary>
        public const int WAIT_JITTER_PERCENT = 20;

        /// <summary>
        /// Corpse blueprint -> the creature whose art to throw up in the splash.
        ///
        /// A corpse carries the CORPSE tile (Corpse renders as items/sw_splat1.bmp), so reading the
        /// tile off the item that was just handed over would show a smear of meat instead of a fish.
        /// These five are the fish with corpses of their own; see the catch tables in
        /// A2Raine_FishingCatch for why the list is not longer.
        /// </summary>
        private static readonly Dictionary<string, string> CorpseArt = new Dictionary<string, string>
        {
            { "Glowfish Corpse",       "Glowfish" },
            { "Madpole Corpse",        "Madpole" },
            { "Ghost Perch Corpse",    "Ghost Perch" },
            { "Memory Eater Corpse",   "Memory Eater" },
            { "Urchin Belcher Corpse", "Urchin Belcher" }
        };

        /// <summary>
        /// Can this pole be used on a liquid that is dangerous to touch?
        ///
        /// False (the default, and what the ordinary 2Raine_FishingPole blueprint leaves it at) means
        /// acid, lava and neutron flux refuse the cast. A future "lava pole" blueprint sets this true.
        /// Read from XML as CanFishDangerous="true".
        /// </summary>
        public bool CanFishDangerous;

        /// <summary>
        /// The command id the engine actually hands back for this ability. AddDynamicCommand
        /// *generates* it (it is an out parameter) rather than taking ours, so the handler has to
        /// compare against this rather than against COMMAND_NAME -- same shape as AjiConch.CommandID.
        /// </summary>
        public string CommandID;

        public Guid ActivatedAbilityID = Guid.Empty;

        /// <summary>
        /// Never merge two poles that were built at runtime: this part carries a live ability id,
        /// and AjiConch returns false here for the same reason.
        /// </summary>
        public override bool SameAs(IPart p)
        {
            return false;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            if (!base.WantEvent(ID, cascade)
                && ID != GetInventoryActionsEvent.ID
                && ID != EquippedEvent.ID
                && ID != UnequippedEvent.ID
                && ID != PooledEvent<CommandEvent>.ID)
            {
                return ID == InventoryActionEvent.ID;
            }
            return true;
        }

        // ------------------------------------------------------------------ the ability

        public override bool HandleEvent(EquippedEvent E)
        {
            if (ParentObject.IsEquippedProperly(E.Part))
            {
                ActivatedAbilityID = E.Actor.AddDynamicCommand(out CommandID, COMMAND_NAME, ABILITY_NAME, "Items", "Cast your line into a body of liquid and wait on what lives under the surface.");
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(UnequippedEvent E)
        {
            E.Actor.RemoveActivatedAbility(ref ActivatedAbilityID);
            E.Actor.UnregisterPartEvent(this, CommandID);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == CommandID)
            {
                if (AttemptCast(ParentObject.Equipped))
                {
                    E.RequestInterfaceExit();
                }
            }
            return base.HandleEvent(E);
        }

        // ------------------------------------------------------------------ the inventory verb

        public override bool HandleEvent(GetInventoryActionsEvent E)
        {
            E.AddAction("CastLine", "cast your line", "CastLine", null, 'f', FireOnActor: false, Default: 0);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == "CastLine" && AttemptCast(E.Actor))
            {
                E.Actor.UseEnergy(ENERGY_PER_CAST, "Fishing");
                E.RequestInterfaceExit();
            }
            return base.HandleEvent(E);
        }

        // ------------------------------------------------------------------ the cast

        /// <summary>
        /// Ask for a direction, check that this pole can work there, then hand off to the minigame.
        /// </summary>
        public bool AttemptCast(GameObject Actor)
        {
            if (Actor == null)
            {
                return false;
            }

            if (!Actor.CanMoveExtremities(null, ShowMessage: true, Involuntary: false, AllowTelekinetic: true))
            {
                return false;
            }

            if (Actor.AreHostilesNearby())
            {
                return Actor.Fail("You are not going to land anything with hostiles about.");
            }

            Cell cell = Actor.Physics.PickDirection("Cast your line");
            if (cell == null)
            {
                UnityEngine.Debug.Log("[Fishing] PickDirection returned null -- cast cancelled at the direction prompt");
                return false;
            }
            UnityEngine.Debug.Log("[Fishing] cast attempt at cell (" + cell.X + "," + cell.Y + ") by " + Actor.Blueprint);

            string refusal = RefusalFor(cell);
            if (refusal != null)
            {
                return Actor.Fail(refusal);
            }

            LiquidVolume liquid = FindLiquid(cell);
            string liquidID = null;
            if (liquid != null)
            {
                BaseLiquid primary = liquid.GetPrimaryLiquid();
                if (primary != null)
                {
                    liquidID = primary.ID;
                }
            }
            UnityEngine.Debug.Log("[Fishing] accepted: liquid=" + (liquidID ?? "(terrain)"));

            // ---- what is rigged on the pole ------------------------------------------------
            //
            // The bait and the float feed two numbers each: how long the wait is, and how likely
            // something takes the hook. Nothing here reads them by name -- they are fields on the
            // shared A2Raine_FishingTackle base, so a third kind of tackle would be summed in by
            // adding one line, not by adding a branch per item.
            A2Raine_FishingRig rig = ParentObject.GetPart<A2Raine_FishingRig>();
            A2Raine_FishingBait bait = (rig == null) ? null : rig.GetBait();
            A2Raine_FishingFloat flt = (rig == null) ? null : rig.GetFloat();

            int speedBonus = 0;
            int catchBonus = 0;
            if (bait != null)
            {
                speedBonus += bait.SpeedBonus;
                catchBonus += bait.CatchBonus;
            }
            if (flt != null)
            {
                speedBonus += flt.SpeedBonus;
                catchBonus += flt.CatchBonus;
            }

            // ---- wait for a bite -----------------------------------------------------------
            int waitMS = BASE_WAIT_MS - speedBonus;
            if (waitMS < MIN_WAIT_MS)
            {
                waitMS = MIN_WAIT_MS;
            }
            // Spread it by +/- WAIT_JITTER_PERCENT so the same rig never bites on the same beat twice.
            int jitter = waitMS * WAIT_JITTER_PERCENT / 100;
            if (jitter > 0)
            {
                waitMS += Stat.Random(-jitter, jitter);
                if (waitMS < MIN_WAIT_MS)
                {
                    waitMS = MIN_WAIT_MS;
                }
            }
            UnityEngine.Debug.Log("[Fishing] waited " + waitMS + "ms (tackle speed bonus " + speedBonus + ", jitter +/-" + jitter + ")");
            WaitForBite(Actor, cell, waitMS);

            // The wait costs time. Both sides are in game turns (see TURNS_PER_REAL_SECOND):
            // the fisher gets hungrier, and the world moves on by the same amount.
            int elapsed = waitMS * TURNS_PER_REAL_SECOND / 1000;
            if (elapsed > 0)
            {
                SpendTime(Actor, elapsed);
            }

            // ---- what is down there comes first, then whether it takes -----------------------
            //
            // The order matters and was changed on purpose. Rolling the table BEFORE the bite means a
            // failed bite can still know what was tempted -- so a fish that got away can be provoked
            // into coming at you, which junk or a box never would.
            int tier = ZoneTierOf(cell);
            A2Raine_FishingCatch.Entry entry = A2Raine_FishingCatch.RollEntry(liquidID, tier);
            UnityEngine.Debug.Log("[Fishing] table roll: tier=" + tier + " -> "
                + ((entry == null) ? "nothing" : (entry.Blueprint + (entry.IsFish ? " (fish)" : ""))));

            int chance = BASE_CATCH_PERCENT + catchBonus;
            if (chance > MAX_CATCH_PERCENT)
            {
                chance = MAX_CATCH_PERCENT;
            }
            if (chance < 0)
            {
                chance = 0;
            }
            int roll = Stat.Random(1, 100);
            UnityEngine.Debug.Log("[Fishing] bite roll " + roll + " vs " + chance + "%");

            if (entry == null)
            {
                return true;
            }

            if (roll > chance)
            {
                // Nothing was landed. If what was down there is a fish, it may take offence at having
                // been tempted: that is the "a break-off spawns something you have to fight" rule.
                if (entry.IsFish && Stat.Random(1, 100) <= ANGER_PERCENT)
                {
                    Provoke(Actor, cell, entry.Blueprint);
                }
                else
                {
                    IComponent<GameObject>.AddPlayerMessage("The line comes back bare. Whatever was down there has moved on.");
                }
                return true;
            }

            // ---- it took the hook ----------------------------------------------------------
            string blueprint = A2Raine_FishingCatch.Resolve(entry.Blueprint, tier);
            UnityEngine.Debug.Log("[Fishing] landed: " + blueprint);

            // Bait is spent only on a fish, and only after the catch is settled -- junk does not eat it.
            if (entry.IsFish)
            {
                ConsumeBait(Actor, rig);
            }

            Payout(Actor, blueprint);

            // The splash and the cache both happen AFTER the wait, in this order: the fish breaks
            // the surface, then (if it was a box) the box is opened.
            Surface(Actor, cell, blueprint);
            if (A2Raine_FishingCatch.IsCache(blueprint))
            {
                OpenCache(Actor, blueprint);
            }
            return true;
        }

        /// <summary>
        /// Charges the wait against the fisher's hunger and against the world clock.
        ///
        /// HUNGER: CookingCounter is the hunger counter -- it rises by 1 per turn (Stomach.cs:258) and
        /// 1200 of it is the Hungry threshold (Stomach.cs:73), which is also exactly one in-game day
        /// (Calendar.cs:13). Assigning to CookingCounter runs UpdateHunger by itself (it is the
        /// property setter, Stomach.cs:41-50), which re-derives HungerLevel and fires BecameHungry if
        /// the line was crossed; the explicit call below is belt and braces, not a requirement.
        /// Calling it by hand is nothing unusual -- the base game does it in JoppaTutorial/MakeCamp.cs:91.
        ///
        /// WORLD TIME: TimeTicks is advanced directly, which is exactly what the base game's own
        /// "advanceticks" wish does (Calendar.cs:356-363), so this is not a back door.
        ///
        /// KEEPING THE ZONE'S CLOCK IN STEP: ActionManager advances TimeTicks and LastPlayerPresence
        /// together (ActionManager.cs:1665-1669). LastPlayerPresence is what Lost (Lost.cs:160),
        /// Amnesia (Amnesia.cs:107) and TeleportGate (TeleportGate.cs:162) compare against, so leaving
        /// it behind while the world clock moves would make the zone believe the fisher had been away
        /// -- hence the second assignment.
        ///
        /// SCALE: a cast is around 60 turns. Clocks that react to gaps are all far coarser than that --
        /// GenericInventoryRestocker waits for 6000 (GenericInventoryRestocker.cs:125-142) -- so no
        /// catch-up burst is triggered here. This is why the amount matters more than the mechanism:
        /// pushing thousands of turns would fire every one of those at once.
        /// </summary>
        private static void SpendTime(GameObject Actor, int Turns)
        {
            if (Actor != null)
            {
                Stomach stomach = Actor.GetPart<Stomach>();
                if (stomach != null)
                {
                    stomach.CookingCounter += Turns;
                    stomach.UpdateHunger();
                }
            }
            if (The.Game != null)
            {
                The.Game.TimeTicks += Turns;
            }
            // Follow the world clock, the way ActionManager does when it advances time itself.
            Cell here = (Actor == null) ? null : Actor.CurrentCell;
            if (here != null && here.ParentZone != null && The.Game != null)
            {
                here.ParentZone.LastPlayerPresence = The.Game.TimeTicks;
            }
            UnityEngine.Debug.Log("[Fishing] spent " + Turns + " turns (hunger + world time, presence synced)");
        }

        /// <summary>
        /// A fish that was tempted and got away turns on the fisher. Uses the same corpse->creature
        /// map as the surfacing art, so the thing that lunges is the thing that was on the line.
        /// </summary>
        private static void Provoke(GameObject Actor, Cell Water, string CorpseBlueprint)
        {
            string creature;
            if (!CorpseArt.TryGetValue(CorpseBlueprint, out creature))
            {
                return;
            }
            Cell where = (Water != null) ? Water : ((Actor == null) ? null : Actor.CurrentCell);
            if (where == null)
            {
                return;
            }
            GameObject angry = GameObject.Create(creature);
            where.AddObject(angry);
            UnityEngine.Debug.Log("[Fishing] provoked " + creature + " at (" + where.X + "," + where.Y + ")");
            IComponent<GameObject>.AddPlayerMessage("The line goes taut and something comes up it at you -- " + creature + "!");
        }

        /// <summary>
        /// Spends one bait, with a chance of leaving it usable. The pole's rig holds them; removing
        /// one is enough, the rest stay in the socket for the next cast.
        /// </summary>
        private static void ConsumeBait(GameObject Actor, A2Raine_FishingRig Rig)
        {
            if (Rig == null)
            {
                return;
            }
            A2Raine_FishingBait bait = Rig.GetBait();
            if (bait == null)
            {
                return;
            }
            if (Stat.Random(1, 100) > BAIT_CONSUME_PERCENT)
            {
                UnityEngine.Debug.Log("[Fishing] bait survived this cast");
                return;
            }
            GameObject spent = bait.ParentObject;
            spent.Destroy();
            UnityEngine.Debug.Log("[Fishing] bait consumed: " + spent.Blueprint);
            if (Actor != null)
            {
                IComponent<GameObject>.AddPlayerMessage("The bait is gone off the hook.");
            }
        }

        /// <summary>
        /// The wait itself, animated.
        ///
        /// HOW A BLOCKING LOOP STILL ANIMATES: ScreenBuffer.Draw (ScreenBuffer.cs:180) hands the
        /// buffer to XRLCore._Console.DrawBuffer immediately rather than marking it dirty for the next
        /// Unity frame, so a loop that redraws and sleeps really does put new frames on screen. The
        /// shape is CyberneticsTerminal's (CyberneticsTerminal.cs:314 + :475): draw, then
        /// Thread.Sleep, forever. The slight difference is that this draws into a scratch buffer
        /// seeded from the current screen, so the world stays visible underneath the ripples instead
        /// of being blanked.
        ///
        /// The world is frozen for the duration, exactly as it is while a modal screen is open --
        /// that is normal for Qud and not something this loop introduces.
        /// </summary>
        private static void WaitForBite(GameObject Actor, Cell Water, int WaitMS)
        {
            if (Water == null)
            {
                Thread.Sleep(WaitMS);
                return;
            }

            ScreenBuffer backdrop = ScreenBuffer.create(80, 25);
            backdrop.Copy(TextConsole.CurrentBuffer);

            int ticks = WaitMS / TICK_MS;
            if (ticks < 1)
            {
                ticks = 1;
            }

            // Swallow anything already queued before we start holding the thread.
            Keyboard.ClearInput();

            ScreenBuffer buffer = ScreenBuffer.GetScrapBuffer1();
            for (int tick = 0; tick < ticks; tick++)
            {
                buffer.Copy(backdrop);
                DrawBiteProgress(buffer, Actor, tick, ticks);
                buffer.Draw();
                Thread.Sleep(TICK_MS);

                // WHY THIS LOOP HAS TO DRAIN THE QUEUE:
                //
                // The wait occupies the game thread, so nothing is reading KeyQueue while it runs --
                // but the key handler still puts every press in there. Those presses do not vanish;
                // they pile up and are consumed one by one the moment the cast ends, so an ability
                // keyed during the wait fires half a second later, after the fishing is over. That is
                // the "accidental keystrokes execute afterwards" report.
                //
                // Clearing each frame means a press has at most TICK_MS (120ms) to be discarded, and
                // keystrokes during a wait are meaningless anyway. ClearInput is the ordinary call for
                // this -- SifrahGame.Play brackets its own input loop with it (SifrahGame.cs:666, :757).
                Keyboard.ClearInput();
            }

            backdrop.Draw();
        }

        /// <summary>
        /// One frame of the wait: a row of dots that fills as the wait runs out, so the player can
        /// see time passing rather than staring at a frozen screen.
        ///
        /// WRITTEN AS TILES, NOT CHARACTERS. A character cell has no transparency -- writing "." over
        /// a map square replaces whatever was there, which is what made the first version look like it
        /// was punching holes in the world. ScreenBuffer.Write has an overload that takes a Tile
        /// (ScreenBuffer.cs:392), and tile art carries an alpha channel, so the marker draws on top of
        /// the map instead of erasing it. The RenderString passed alongside is only a fallback for
        /// when the tile cannot be resolved, so a missing image degrades to a dot rather than to
        /// nothing.
        ///
        /// THE ROW IS PINNED TO THE FISHER, one line above the head, not to the water: standing at
        /// the bank you look at yourself, and a marker bobbing over the far cell is easy to lose.
        /// </summary>
        private static void DrawBiteProgress(ScreenBuffer B, GameObject Actor, int Tick, int Total)
        {
            Cell where = (Actor == null) ? null : Actor.CurrentCell;
            if (where == null)
            {
                return;
            }

            int y = where.Y - 1;
            if (y < 0)
            {
                return;
            }
            // Centre the row of DOTS markers on the fisher.
            int x = where.X - (DOTS / 2);
            if (x < 0)
            {
                x = 0;
            }

            int shown = (Tick + 1) * DOTS / Total;
            for (int i = 0; i < shown; i++)
            {
                B.Goto(x + i, y);
                B.Write(BAIT_MARKER_TILE, ".", "&W", "&W", 'W');
            }
        }

        /// <summary>Hands the catch over, or announces a cache instead of trying to carry it.</summary>
        private static void Payout(GameObject Actor, string Blueprint)
        {
            if (Actor == null || Blueprint == null)
            {
                return;
            }
            if (A2Raine_FishingCatch.IsCache(Blueprint))
            {
                IComponent<GameObject>.AddPlayerMessage("Something heavy and wooden comes up with the line -- not a fish at all.");
                return;
            }
            Actor.ReceiveObject(Blueprint);
            IComponent<GameObject>.AddPlayerMessage("You bring the line up with " + Blueprint + " on the hook.");
        }

        /// <summary>
        /// Region tier for the water being fished. Zone.Tier (Zone.cs:231) is filled by Qud from the
        /// blueprint's Tier or else the terrain's RegionTier tag (Zone.cs:405-412).
        /// </summary>
        private static int ZoneTierOf(Cell C)
        {
            if (C == null || C.ParentZone == null)
            {
                return 1;
            }
            return C.ParentZone.Tier;
        }

        /// <summary>
        /// Opens a sunken cache on the spot and then destroys it, which is the behaviour asked for:
        /// the interface comes up like a chest on the ground, you take what you want, and when you
        /// close it the box is gone for good.
        ///
        /// Container.AttemptOpen (Container.cs:77) is the vanilla entry point, and it BLOCKS -- it
        /// calls TradeUI.ShowTradeScreen in TradeScreenMode.Container (TradeUI.cs:344), which does not
        /// return until the player closes the screen. That is what makes "gone after closing" exact:
        /// the Destroy below runs on the frame the interface disappears, with no polling or callbacks.
        ///
        /// The chest is parented to the fisher first. An object that belongs to nothing at all is not
        /// something this mod has ever exercised, and opening is a heavy path (it fires BeforeOpen,
        /// Opening, ownership checks and a trade screen); giving the cache a real home avoids betting
        /// on it. Chest1..Chest7 are vanilla furniture chests, so they cannot be worn or carried off,
        /// and each already carries its own premade loot (Builder="InventoryChestJunk1".."7").
        /// </summary>
        private static void OpenCache(GameObject Actor, string Blueprint)
        {
            if (Actor == null || Blueprint == null)
            {
                return;
            }
            GameObject cache = null;
            try
            {
                cache = GameObject.Create(Blueprint);
                if (cache == null)
                {
                    UnityEngine.Debug.Log("[Fishing] could not create cache blueprint " + Blueprint);
                    return;
                }
                Actor.ReceiveObject(cache);

                Container container = cache.GetPart<Container>();
                if (container == null)
                {
                    UnityEngine.Debug.Log("[Fishing] " + Blueprint + " has no Container part; nothing to open");
                    return;
                }
                container.AttemptOpen(Actor);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("[Fishing] opening the cache failed: " + e);
            }
            finally
            {
                // Runs whether or not the open worked, and whether or not the player took anything.
                if (cache != null && cache.IsValid())
                {
                    cache.Destroy();
                    UnityEngine.Debug.Log("[Fishing] cache " + Blueprint + " destroyed after opening");
                }
            }
        }

        /// <summary>
        /// The fish comes out of the water AFTER the interface has closed, using the base game's own
        /// surfacing演出 rather than anything invented here:
        ///
        ///   Cell.LiquidSplash(BaseLiquid)          Cell.cs:9707 -> :9671 -> :9634
        ///     throws three droplets on random bearings, each separated by Thread.Sleep(5..15), so the
        ///     splashes are animated rather than appearing all at once;
        ///   "Sounds/Abilities/sfx_ability_water_emerge"   the clip Submerged plays when a creature
        ///     surfaces (Submerged.cs:72) -- the same sound a frog makes climbing out.
        ///
        /// This is deliberately called from the pole and not from Finish(): Finish() runs while the
        /// Sifrah screen is still up (SifrahGame.cs:756 vs the PopGameView at :759), so a splash fired
        /// there would be drawn underneath the interface and never seen.
        /// </summary>
        private static void Surface(GameObject Actor, Cell WaterCell, string Blueprint)
        {
            try
            {
                if (WaterCell != null)
                {
                    GameObject pool = WaterCell.GetAquaticSupportFor(Actor);
                    LiquidVolume liquid = (pool == null) ? null : pool.LiquidVolume;
                    BaseLiquid primary = (liquid == null) ? null : liquid.GetPrimaryLiquid();
                    if (primary != null)
                    {
                        WaterCell.LiquidSplash(primary);
                    }
                }

                ThrowTheFishUp(WaterCell, Blueprint);

                if (Actor != null)
                {
                    Actor.PlayWorldSound("Sounds/Abilities/sfx_ability_water_emerge");
                }
            }
            catch (Exception e)
            {
                // A cosmetic flourish must never take the catch down with it.
                UnityEngine.Debug.Log("[Fishing] surfacing splash failed: " + e);
            }
        }

        /// <summary>
        /// Hurls the fish's own tile up out of the water, in step with the droplets.
        ///
        /// Cell.TileParticleBlip (Cell.cs:9084) is the tidier call, but it passes xDel/yDel as 0, so
        /// the art simply appears in place -- no leap. ParticleManager.AddTile (ParticleManager.cs:119)
        /// takes the deltas, so this calls it directly.
        ///
        /// THE NUMBERS ARE A PARABOLA, not guesses. Frame() (ParticleManager.cs:659-670) runs one
        /// simulation step per 16ms and applies:
        ///     y   += yDel      * steps
        ///     yDel+= yDelDel   * steps
        /// so yDel is GRID CELLS PER FRAME. The first attempt used -0.9, which over 24 frames travels
        /// 21.6 cells -- the whole screen is 25 rows, so the fish left the visible area on its first
        /// step and the flourish was invisible. For an arc that peaks at half the lifetime and lands
        /// back where it started:
        ///     peak at t = Life/2   =>   yDel = -yDelDel * (Life/2)
        ///     peak height h cells  =>   h = -yDel * (Life/2) / 2
        /// With Life = 30 and h = 2 that gives yDel = -0.267, yDelDel = +0.0178 -- a two-cell hop that
        /// is back on the surface by the time the particle expires. Tune those two to taste; nothing
        /// else depends on them.
        /// </summary>
        private static void ThrowTheFishUp(Cell WaterCell, string Blueprint)
        {
            if (WaterCell == null || Blueprint == null)
            {
                return;
            }

            string creature;
            if (!CorpseArt.TryGetValue(Blueprint, out creature))
            {
                return;
            }

            GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprint(creature);
            if (blueprint == null)
            {
                return;
            }

            string tile = blueprint.GetPartParameter<string>("Render", "Tile");
            if (tile.IsNullOrEmpty())
            {
                return;
            }

            string color = blueprint.GetPartParameter<string>("Render", "ColorString");
            string detail = blueprint.GetPartParameter<string>("Render", "DetailColor");

            The.ParticleManager.AddTile(
                tile, color, detail,
                WaterCell.X, WaterCell.Y,
                0f, -0.267f,        // initial velocity: up, ~2 cells at the apex
                30,                 // frames of life (~480ms at 16ms per step)
                0f, 0.0178f,        // gravity: brings it back to the surface by the end
                false, false, 0L);

            UnityEngine.Debug.Log("[Fishing] threw " + creature + " art up out of the water");
        }

        // ------------------------------------------------------------------ what is fishable here

        /// <summary>
        /// Finds the liquid pool in this cell, if there is one.
        ///
        /// EVERY in-zone body of liquid is an object carrying a LiquidVolume, not a terrain: the zone
        /// builders put them there --
        ///   OverlandWater.cs:206      AddObject("SaltyWaterDeepPool");       // rivers
        ///   OverlandAlgaeLake.cs:205  AddObject("AlgalWaterDeepPool");       // Lake Hinnom
        ///   Redrock.cs:102, Waterway.cs:69, Rustwells.cs:103, Reef.cs:1139 ...  same family
        /// The TerrainWater object and its Terrain tag live out on the JoppaWorld world-map grid
        /// (WorldTerrain.xml:319, read at OverlandWater.cs:43); that is not what the player is
        /// standing next to, which is why a terrain-tag test finds nothing in play.
        ///
        /// Deliberately NOT Cell.HasAquaticSupportFor, which asks "could this creature swim here"
        /// (LiquidVolume.cs:4565 -> IsSwimmingDepth needs Volume &gt;= 2000 at :4560, and
        /// Swimming.GetTargetMoveSpeedPenalty is 50 by default at Swimming.cs:61). That answers false
        /// for a river the fisher is simply not a strong enough swimmer for.
        ///
        /// Containers are skipped: MaxVolume &gt;= 0 means a flask or canteen, not a pool. Same test
        /// GameObject.IsWaterPuddle uses (GameObject.cs:8915).
        /// </summary>
        public static LiquidVolume FindLiquid(Cell C)
        {
            if (C == null)
            {
                return null;
            }
            foreach (GameObject obj in C.Objects)
            {
                LiquidVolume liquid = obj.LiquidVolume;
                if (liquid != null && liquid.MaxVolume < 0)
                {
                    return liquid;
                }
            }
            return null;
        }

        /// <summary>
        /// Why the cast cannot happen here, or null if it can.
        ///
        /// Any liquid is fine -- the plan is to fish all of them eventually, which is why this is not
        /// a water test. The only refusals are: nothing liquid in the cell at all, or a liquid that
        /// is dangerous to touch when this pole is not built for it.
        /// </summary>
        private string RefusalFor(Cell C)
        {
            if (C == null)
            {
                return "There is nothing there to cast into.";
            }

            LiquidVolume liquid = FindLiquid(C);
            if (liquid != null)
            {
                if (liquid.ConsiderLiquidDangerousToContact() && !CanFishDangerous)
                {
                    return "That would eat straight through your line. You would need a pole built to stand it.";
                }
                return null;
            }

            // Falls back to the world-map terrain tag, for cells that carry water as terrain rather
            // than as a pool. Checked by walking every object: HasObjectWithPropertyOrTagEqualToValue
            // returns on the FIRST object carrying the name (Cell.cs:1672-1682) instead of continuing,
            // so one non-water Terrain object in the cell would mask the real one.
            if (HasWaterTerrain(C))
            {
                return null;
            }

            // DIAGNOSTIC. Every branch above can fail silently, and guessing which one was wrong cost
            // several rounds already. Prints the cell's actual contents so a report is evidence
            // instead of another hypothesis. Remove once fishing is confirmed working in play.
            UnityEngine.Debug.Log("[Fishing] REFUSED at (" + C.X + "," + C.Y + "), cell contents:");
            foreach (GameObject obj in C.Objects)
            {
                string liquidInfo = "none";
                if (obj.LiquidVolume != null)
                {
                    BaseLiquid primary = obj.LiquidVolume.GetPrimaryLiquid();
                    liquidInfo = "vol=" + obj.LiquidVolume.Volume
                        + " max=" + obj.LiquidVolume.MaxVolume
                        + " primary=" + ((primary == null) ? "?" : primary.ID);
                }
                UnityEngine.Debug.Log("[Fishing]   " + obj.Blueprint
                    + "  Terrain=[" + (obj.GetPropertyOrTag("Terrain") ?? "-") + "]"
                    + "  liquid=" + liquidInfo);
            }
            return "There is nothing there to cast into.";
        }

        private static bool HasWaterTerrain(Cell C)
        {
            foreach (GameObject obj in C.Objects)
            {
                GameObjectBlueprint blueprint = obj.GetBlueprint();
                if (blueprint != null && blueprint.HasTag("Terrain"))
                {
                    string kind = blueprint.GetTag("Terrain");
                    if ("Water".Equals(kind) || "LakeHinnom".Equals(kind))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
