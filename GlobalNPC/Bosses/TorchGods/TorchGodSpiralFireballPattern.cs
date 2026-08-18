using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Patterns
{
    /// <summary>
    /// Pattern "spiral fireball": nembak fireball muter terus-terusan (bukan
    /// cuma 1 putaran 360 derajat lagi) SELAMA 3-5 DETIK (di-random tiap
    /// Activate()), 1 shot tiap beberapa tick, sudutnya nambah dikit-dikit
    /// tiap shot dan otomatis wrap balik ke 0 tiap genap 360 derajat - jadi
    /// selama durasi itu dia bakal muter beberapa putaran penuh berturut-turut.
    ///
    /// Fireball-nya pakai TorchGodFireballProjectile (custom AI: trail
    /// mengecil, tembus block, ignore gravity, sprite utama transparan -
    /// lihat file itu), BUKAN ProjectileID.Fireball vanilla.
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
        private const int TicksPerShot = 2;           // 1 shot tiap 2 tick
        private const float AngleStepDegrees = 10f;
        private const float ProjectileSpeed = 6f;
        private const int ProjectileDamage = 30;

        // Total durasi pattern ini di-random ULANG tiap Activate() dipanggil,
        // antara 3-5 detik (180-300 tick, 60 tick/detik) - BUKAN cuma 1
        // putaran 360 derajat kayak sebelumnya.
        private const int MinDurationTicks = 180; // 3 detik
        private const int MaxDurationTicksInclusive = 300; // 5 detik

        private int timer;

        // SENGAJA gak di-reset ke 0 tiap 360 derajat - biar hitungan sudutnya
        // terus jalan (di-modulo pas dipakai di FireOneProjectile), jadi kalau
        // pattern ini kebagian durasi panjang dia beneran muter berkali-kali.
        private int shotIndex;

        private int totalDurationTicks;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Mulai attack ini dari awal (shot pertama di sudut 0 derajat),
        /// sekaligus nge-random durasi total pattern ini (3-5 detik).
        /// </summary>
        public void Activate()
        {
            IsActive = true;
            timer = 0;
            shotIndex = 0;
            totalDurationTicks = Main.rand.Next(MinDurationTicks, MaxDurationTicksInclusive + 1);
        }

        /// <summary>
        /// Panggil tiap tick (biasanya dari PostAI). Return TRUE persis di
        /// tick pas durasi total pattern ini abis - dipakai caller buat
        /// trigger pattern berikutnya.
        /// </summary>
        public bool Update(NPC npc)
        {
            if (!IsActive)
                return false;

            timer++;

            if (timer % TicksPerShot == 0)
            {
                FireOneProjectile(npc);
                shotIndex++;
            }

            if (timer >= totalDurationTicks)
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

            // Modulo 360 derajat - shotIndex terus nambah tanpa batas selama
            // pattern ini aktif, tapi sudut tembaknya selalu wrap rapi.
            float angle = MathHelper.ToRadians((shotIndex * AngleStepDegrees) % 360f);
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
