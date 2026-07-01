using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.TownNPCs
{
    public class SunCultistShot : ModProjectile
    {
        // Membajak aset visual asli dari proyektil Flame Burst Tower Tier 3 vanilla
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2FlameBurstTowerT3Shot;

        public override void SetDefaults() {
            // Mengkloning seluruh basis data pergerakan dan partikel api dari vanilla
            Projectile.CloneDefaults(ProjectileID.DD2FlameBurstTowerT3Shot);
            AIType = ProjectileID.DD2FlameBurstTowerT3Shot;
            
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // Memberikan efek debuff OnFire3 (Hellfire) selama 240 ticks (4 Detik)
            target.AddBuff(BuffID.OnFire3, 240);
        }
    }
}