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

                    if (heldItem != null && heldItem.damage > 0 &&
                        !heldItem.DamageType.CountsAsClass(DamageClass.Summon) &&
                        heldItem.DamageType != DamageClass.Default)
                    {
                        // 🔥 Penalty 95% → damage minion hanya 5%
                        modifiers.FinalDamage *= 0.05f;
                    }
                }
            }
        }
    }
}