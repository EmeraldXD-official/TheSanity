using System;
using System.Collections.Generic;
using Terraria;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public partial class WhoAmI
    {
        // Key: Item.type ; Value: (bossInstance, targetPlayer) => { spawn projectiles / mimic behaviour }
        private static readonly Dictionary<int, Action<WhoAmI, Player>> CustomWeaponFireOverrides = new Dictionary<int, Action<WhoAmI, Player>>();

        // Called from Mod.PostSetupContent to allow any hand-written overrides to register themselves.
        public static void RegisterCustomWeaponOverrides()
        {
            // Intentionally empty by default. Example stub for manual edit:
            // TODO (needs manual review — see ModdedWeaponManifest.json): ExampleMod / FrostReaverGun (5231)
            // CustomWeaponFireOverrides[5231] = (boss, target) => { /* spawn ExampleMod's real projectile here */ };
        }
    }
}
