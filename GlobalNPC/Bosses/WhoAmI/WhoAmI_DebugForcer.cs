using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // DEBUG: PATTERN FORCER — testing-only API for jumping the boss straight into any attack pattern.
    // ================================================================================================
    // This file is a partial-class add-on to WhoAmI, NOT a new pattern. It exists purely so a debug
    // ModItem/ModCommand (see WhoAmIPatternForcerItem.cs / WhoAmIForceCommand.cs) can reach into the
    // boss's private aiState machinery safely instead of poking public fields from the outside and
    // leaving stale sub-state behind (the exact class of bug several of the Handle* comments in this
    // mod already call out - e.g. blinkEchoLandingPoint/gridLockMarkers never resetting because the
    // `aiTimer == 1` init check silently never fired).
    //
    // WHY aiTimer = 0 IS ENOUGH: every Handle* method in this mod already self-initializes its own
    // scratch fields inside an `if (aiTimer == 1)` block the first tick it runs (aiTimer increments
    // once, unconditionally, in AI() right before the switch that dispatches here). So the forcer does
    // NOT need to know about every pattern's private fields (there are dozens across a dozen files) -
    // it just needs to guarantee aiTimer starts at 0 so the NEXT tick is a clean "tick 1" for whichever
    // handler is about to run, plus clear the handful of GENERIC flags several patterns share
    // (isParrying, isCurrentlyChanneling, combo counters) so nothing bleeds over from whatever state
    // the boss was in a moment ago.
    //
    // KNOWN ISSUE THIS TOOL WILL SURFACE: STATE_SUMMON_SPECTRAL_CAROUSEL (=40) and STATE_WHIP_SERPENTS_
    // COIL (=41), defined in WhoAmI.cs, numerically collide with STATE_PHASE3_TRANSITION (=40) and
    // STATE_PHASE3_ARENA (=41), defined in WhoAmI_Phase3Cartesian.cs. AI() checks
    // `if (aiState == STATE_PHASE3_TRANSITION || aiState == STATE_PHASE3_ARENA) { HandlePhase3(...); return; }`
    // BEFORE it ever reaches the switch statement that has `case STATE_SUMMON_SPECTRAL_CAROUSEL` /
    // `case STATE_WHIP_SERPENTS_COIL` - and since C# switches/ifs only see the numeric value, not the
    // constant's name, that early check silently hijacks both patterns into HandlePhase3 every single
    // time they'd normally fire. Forcing either one below will reproduce that immediately. The real
    // fix is renumbering one pair of constants (e.g. move the two Phase3 states to 50/51) - this
    // forcer does not attempt to patch that for you, it only flags it (see WarnDuplicateStateIds).
    // ================================================================================================
    public partial class WhoAmI
    {
        // Every entry here uses the REAL constant, so if state IDs ever get renumbered this list (and
        // its duplicate-detection) stays correct automatically - nothing here is a hardcoded literal.
        private static readonly List<(int State, string Label)> DebugPatternCatalog = new List<(int, string)>
        {
            (STATE_DODGE,                       "Dodge"),
            (STATE_DASH_ATTACK,                 "Dash Attack"),
            (STATE_MELEE_COMBO,                 "Melee Combo"),
            (STATE_RANGED_BARRAGE,              "Ranged Barrage"),
            (STATE_PARRY_STANCE,                "Parry Stance"),
            (STATE_COUNTER_ATTACK,              "Counter Attack"),
            (STATE_PREDICTIVE_DODGE,            "Predictive Dodge"),
            (STATE_MAGIC_SPIRAL_RIFT,           "Phantom Mirage Cascade"),
            (STATE_BLINK_ECHO_COMBO,            "Blink & Echo Combo"),
            (STATE_ORBIT_GRID_LOCK,             "Orbiting Grid Lock"),
            (STATE_GRAVITY_WELL_TORRENT,        "Gravity Well & Arcane Torrent"),
            (STATE_MIRROR_MIRAGE,               "Mirror Mirage (3-way decoy)"),
            (STATE_SUMMON_RIFT_SWARM,           "Spectral Rift Swarm"),
            (STATE_WHIP_LASH_CAGE,              "Lash Cage"),
            (STATE_YOYO_TETHER_STORM,           "Tether Storm"),
            (STATE_BOOMERANG_CROSSFIRE,         "Boomerang Crossfire"),
            (STATE_ABYSSAL_CLEAVE,              "Abyssal Cleave & Fractured Space"),
            (STATE_ORBITING_BLADE_RING,         "Orbiting Blade Ring (Sovereign Guard)"),
            (STATE_DIMENSIONAL_PIERCE,          "Dimensional Pierce / Flash Strike"),
            (STATE_VECTOR_LASER_GRID,           "Vector Laser Grid System"),
            (STATE_HOMING_CLUSTER_COMET,        "Homing Cluster Comet"),
            (STATE_SINGULARITY_OVERDRIVE,       "Singularity Overdrive"),
            (STATE_AUREOLA_SIGNET_RAIN,         "Aureola Signet Rain"),
            (STATE_DOUBLE_HELIX_SWEEP,          "Double Helix Sweep"),
            (STATE_QUANTUM_GLITCH_PHASING,      "Quantum Glitch Phasing"),
            (STATE_MIRROR_LANCE_RUPTURE,        "Mirror Lance Rupture"),
            (STATE_MELEE_MIRROR_WALTZ,          "Warped Mirror Waltz"),
            (STATE_MELEE_FRACTURED_ONSLAUGHT,   "Fractured Persona Onslaught"),
            (STATE_MELEE_RIPOSTE_CASCADE,       "Riposte Cascade"),
            (STATE_RANGED_PARALLAX_VOLLEY,      "Parallax Volley"),
            (STATE_RANGED_MIRROR_RICOCHET,      "Mirror Ricochet"),
            (STATE_RANGED_STARFALL_CONVERGENCE, "Starfall Convergence"),
            (STATE_MAGIC_FRACTURE_BLOOM,        "Fracture Bloom"),
            (STATE_MAGIC_UMBRAL_DUALITY,        "Umbral Duality"),
            (STATE_MAGIC_PARADOX_MIRROR,        "Paradox Mirror Volley"),
            (STATE_SUMMON_WRAITH_CONVERGENCE,   "Wraith Convergence"),
            (STATE_SUMMON_SOUL_TETHER,          "Soul Tether Bind"),
            (STATE_SUMMON_SPECTRAL_CAROUSEL,    "Spectral Carousel"),      // see collision warning above
            (STATE_WHIP_SERPENTS_COIL,          "Serpent's Coil"),         // see collision warning above
            (STATE_WHIP_FAN_LASH,               "Cracked Fan Lash"),
            (STATE_WHIP_PUPPETEER_SNAP,         "Puppeteer's Snap"),
            (STATE_YOYO_PENDULUM_RECKONING,     "Pendulum Reckoning"),
            (STATE_YOYO_BINARY_SNARE,           "Binary Orbit Snare"),
            (STATE_YOYO_CASCADE_UNRAVEL,        "Cascade Unravel"),
            (STATE_BOOMERANG_WINDMILL_BARRAGE,  "Windmill Barrage"),
            (STATE_BOOMERANG_RICOCHET_TRIANGLE, "Ricochet Triangle"),
            (STATE_BOOMERANG_CURVING_RETURN,    "Curving Return Barrage"),
        };

        // States the forcer refuses to enter directly - either internal-only (decoys read this off
        // their owner, they don't drive themselves) or scripted sequences whose setup (camera state,
        // arena teleport, PendingMirrorSpawnPoint, etc.) this tool doesn't replicate. Trying to force
        // these tends to softlock or visually break the boss rather than reveal an attack-pattern bug.
        private static readonly HashSet<int> DebugBlockedStates = new HashSet<int>
        {
            STATE_MIRAGE_DECOY_HOLD, STATE_PHASE2_CUTSCENE, STATE_DESPERATION_CUTSCENE,
            STATE_PHASE3_TRANSITION, STATE_PHASE3_ARENA, 100, 101, 102,
        };

        public static int GetCatalogCount() => DebugPatternCatalog.Count;

        public static (int State, string Label) GetCatalogEntry(int index)
        {
            index = ((index % DebugPatternCatalog.Count) + DebugPatternCatalog.Count) % DebugPatternCatalog.Count;
            return DebugPatternCatalog[index];
        }

        // Finds an entry by exact index string, or by case-insensitive substring match against its
        // label. Returns -1 (catalog index, not state id) if nothing matched, -2 if more than one
        // label matched (ambiguous - caller should ask for a more specific name).
        public static int FindCatalogIndex(string query)
        {
            if (int.TryParse(query, out int asIndex) && asIndex >= 0 && asIndex < DebugPatternCatalog.Count)
                return asIndex;

            int found = -1;
            for (int i = 0; i < DebugPatternCatalog.Count; i++)
            {
                if (DebugPatternCatalog[i].Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (found != -1)
                    return -2; // ambiguous
                found = i;
            }
            return found;
        }

        // Prints the full catalog to chat, index -> label -> raw state id, and flags any state id that
        // is shared by more than one label (see the collision comment at the top of this file).
        public static void DebugPrintCatalog()
        {
            Main.NewText("=== WhoAmI Pattern Forcer catalog ===", Color.SkyBlue);
            var seenBy = new Dictionary<int, List<string>>();
            for (int i = 0; i < DebugPatternCatalog.Count; i++)
            {
                var (state, label) = DebugPatternCatalog[i];
                Main.NewText($"[{i}] {label}  (state={state})", Color.LightGray);
                if (!seenBy.TryGetValue(state, out var labels))
                    seenBy[state] = labels = new List<string>();
                labels.Add(label);
            }
            DebugWarnDuplicateStateIds(seenBy);
        }

        private static void DebugWarnDuplicateStateIds(Dictionary<int, List<string>> seenBy = null)
        {
            if (seenBy == null)
            {
                seenBy = new Dictionary<int, List<string>>();
                foreach (var (state, label) in DebugPatternCatalog)
                {
                    if (!seenBy.TryGetValue(state, out var labels))
                        seenBy[state] = labels = new List<string>();
                    labels.Add(label);
                }
            }

            foreach (var kvp in seenBy)
            {
                if (kvp.Value.Count <= 1)
                    continue;
                Main.NewText($"[WhoAmI Forcer] WARNING: state id {kvp.Key} is shared by {string.Join(" / ", kvp.Value)} " +
                             "- they are indistinguishable at runtime and one will silently hijack the other.", Color.OrangeRed);
            }
        }

        // ---------- The actual forcing entry point ----------
        // Returns a short status string (for chat feedback) rather than throwing, since this only ever
        // runs from debug tooling and a failed force shouldn't take the tool down with it.
        public string DebugForceState(int targetState)
        {
            if (isMirageDecoy)
                return "Refused: this instance is a mirage decoy, not the real boss - forcing it would desync from its owner.";

            if (DebugBlockedStates.Contains(targetState))
                return $"Refused: state {targetState} is a scripted/internal state this tool won't force directly (see DebugBlockedStates).";

            // Generic cleanup so nothing from the PREVIOUS state bleeds into the new one. Every actual
            // pattern re-initializes its own scratch fields on its own `aiTimer == 1` tick, so we only
            // need to reset flags that are read by multiple patterns / by systems outside the switch.
            isParrying = false;
            isCurrentlyChanneling = false;
            meleeComboStep = 0;
            meleeComboTimer = 0;
            bossWeaponSwingTimer = 0;
            burstShotCounter = 0;
            burstShotDelay = 0;
            archetypePatternTimer = 0;
            patternCooldown = 0;
            NPC.damage = 0;
            NPC.velocity = Vector2.Zero;

            // If we're yanking the boss out of a cutscene mid-sequence, clear the flags that would
            // otherwise leave the camera/damage-immunity state stuck from that sequence.
            if (aiState == 100 || aiState == 101 || aiState == 102 || aiState == STATE_PHASE2_CUTSCENE || aiState == STATE_DESPERATION_CUTSCENE)
            {
                IsCutsceneActive = false;
                NPC.dontTakeDamage = false;
            }

            aiState = targetState;
            aiTimer = 0; // becomes 1 on the NPC's next AI tick - matches every handler's own init check
            NPC.netUpdate = true;

            var label = DebugPatternCatalog.Find(e => e.State == targetState).Label ?? "(unlisted state)";
            return $"Forced state {targetState} - {label}";
        }

        public string DebugTogglePhase2(bool? explicitValue = null)
        {
            isPhase2 = explicitValue ?? !isPhase2;
            NPC.netUpdate = true;
            return $"isPhase2 = {isPhase2}";
        }

        public string DebugCurrentStateDescription()
        {
            var matches = DebugPatternCatalog.FindAll(e => e.State == aiState);
            string label = matches.Count == 0 ? "(no catalog entry)" : string.Join(" / ", matches.ConvertAll(e => e.Label));
            return $"aiState={aiState} [{label}], aiTimer={aiTimer}, isPhase2={isPhase2}";
        }
    }
}
