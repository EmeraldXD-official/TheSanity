using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Patterns
{
    /// <summary>
    /// Pattern "spiral fireball": nembak fireball muter 360 derajat (satu
    /// putaran penuh), 1 shot tiap beberapa tick, sudutnya nambah dikit-dikit
    /// tiap shot sampe balik ke titik awal.
    ///
    /// Fireball-nya sekarang pakai TorchGodFireballProjectile (custom AI:
    /// trail mengecil, tembus block, ignore gravity - lihat file itu),
    /// BUKAN ProjectileID.Fireball vanilla lagi.
    ///
    /// Cara pakai (dari ModNPC pemilik boss):
    ///   private readonly TorchGodSpiralFireballPattern spiralFireball = new();
    ///
    ///   // trigger sekali (misal abis life-reveal kelar):
    ///   spiralFireball.Activate();
    ///
    ///   PostAI() => bool justFinished = spiralFireball.Update(NPC);
    /// </summary>
    public class TorchGodSpiralFireballPattern
    {
        private const int TotalShots = 36;           // 36 shot x 10 derajat = 1 muteran penuh
        private const int TicksPerShot = 2;           // 1 shot tiap 2 tick
        private const float AngleStepDegrees = 10f;
        private const float ProjectileSpeed = 6f;
        private const int ProjectileDamage = 30;

        private int timer;
        private int shotIndex;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Mulai attack ini dari awal (shot pertama di sudut 0 derajat).
        /// </summary>
        public void Activate()
        {
            IsActive = true;
            timer = 0;
            shotIndex = 0;
        }

        /// <summary>
        /// Panggil tiap tick (biasanya dari PostAI). Return TRUE persis di
        /// tick pas attack ini BARU AJA kelar (muteran 360 derajat abis) -
        /// dipakai caller buat trigger pattern berikutnya.
        /// </summary>
        public bool Update(NPC npc)
        {
            if (!IsActive)
                return false;

            timer++;

            if (timer % TicksPerShot != 0)
                return false;

            FireOneProjectile(npc);
            shotIndex++;

            if (shotIndex >= TotalShots)
            {
                IsActive = false;
                return true;
            }

            return false;
        }

        private void FireOneProjectile(NPC npc)
        {
            // Cuma server/singleplayer yang boleh spawn projectile baru (biar
            // gak dobel-dobel kalau ini kepanggil di tiap client).
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            float angle = MathHelper.ToRadians(shotIndex * AngleStepDegrees);
            Vector2 velocity = angle.ToRotationVector2() * ProjectileSpeed;

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                velocity,
                ModContent.ProjectileType<TorchGodFireballProjectile>(),
                ProjectileDamage,
                2f,
                Main.myPlayer
            );
        }
    }
}
