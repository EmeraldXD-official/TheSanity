using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Global
{
    public class SummonPenaltyGlobalProjectile : GlobalProjectile
    {
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (projectile.minion && projectile.owner != 255)
            {
                Player player = Main.player[projectile.owner];
                if (player != null && player.active && !player.dead)
                {
                    Item heldItem = player.HeldItem;

                    // Whitelist: hanya senjata dengan DamageType yang masuk kelas Summon
                    // (ini otomatis mencakup whip, karena SummonMeleeSpeed adalah turunan dari Summon)
                    // yang boleh membuat minion damage full.
                    bool isHoldingSummonWeapon = heldItem != null &&
                        heldItem.DamageType.CountsAsClass(DamageClass.Summon);

                    if (!isHoldingSummonWeapon)
                    {
                        // 🔥 Selain senjata Summon (Melee, Ranged, Magic, Rogue, Healer,
                        // Thrower, Bard, class modded lain, atau bahkan tangan kosong)
                        // → damage minion dibuat nyaris nol (efektif minimal 1).
                        modifiers.FinalDamage *= 0.001f;
                    }
                }
            }
        }
    }
}