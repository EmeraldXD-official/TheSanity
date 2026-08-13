using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    /// <summary>
    /// Projectile ini hanya menyalakan CosmicSkyBackground lewat SkyManager.
    /// </summary>
    public class CosmicSpaceBackground : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_197";

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.hide = true; // tidak perlu digambar sendiri lagi
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }

        public override void AI()
        {
            // Mencegah background mati selama boss aktif
            Projectile.timeLeft = 2;

            // --- [TAMBAHAN]: Cegah background nempel selamanya saat Boss menghilang (Despawn) ---
            bool bossAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type == ModContent.NPCType<UnknownEntity>())
                {
                    bossAlive = true;
                    break;
                }
            }

            // Jika Boss sudah tidak ada di dunia (mati/despawn), bunuh diri proyektil ini
            if (!bossAlive)
            {
                Projectile.Kill();
                return;
            }

            bool isPhase2 = Projectile.ai[0] == 1f;
            SkyManager.Instance.Activate(CosmicSkySystem.SkyKey, Projectile.Center, isPhase2);
        }

        public override void Kill(int timeLeftWhenKilled)
        {
            // Matikan langit saat proyektil mati
            SkyManager.Instance.Deactivate(CosmicSkySystem.SkyKey);
        }
    }
}