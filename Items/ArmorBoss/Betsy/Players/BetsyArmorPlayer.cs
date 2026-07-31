using Terraria;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Betsy.Projectiles;

namespace TheSanity.Items.ArmorBoss.Betsy.Players
{
    public class BetsyArmorPlayer : ModPlayer
    {
        public bool setBonusActive;

        // Ditandai true dari BetsyMask.UpdateArmorSet() -- ini jalur RESMI tModLoader buat ngecek
        // "full set lagi valid dipakai" (dipanggil tiap tick selama IsArmorSet() true), jadi lebih
        // reliable daripada BetsyPortalLayer ngecek ulang head/body/legs sendiri secara manual.
        public bool fullSetActive;

        private const float RadiusInPixels = 80 * 16f; // 80 tile = 1280 pixel

        // Nembak beberapa partikel api tiap sekian tick (bukan tiap tick banget) biar kepadatan semburan
        // mirip Flamethrower vanilla tapi tidak terlalu berat buat performa. Kecilkan angka ini kalau mau
        // semburan makin padat, besarkan kalau mau lebih hemat performa.
        // Dinaikkan drastis dari sebelumnya (3 tick / 2 partikel) karena hasilnya kelihatan jauh lebih
        // tipis/jarang dibanding referensi Flamethrower vanilla.
        private const int TicksBetweenBursts = 1;
        private const int ParticlesPerBurst = 4;

        private int burstTimer;

        public override void ResetEffects()
        {
            setBonusActive = false;
            fullSetActive = false;
        }

        public override void PostUpdateEquips()
        {
            if (!setBonusActive)
            {
                burstTimer = 0;
                return;
            }

            if (Main.myPlayer != Player.whoAmI)
                return; // hanya player lokal yang spawn proyektil (multiplayer-safe)

            NPC target = FindNearestEnemy(Player.Center, RadiusInPixels);
            if (target == null)
            {
                burstTimer = 0;
                return; // tidak ada musuh dalam radius 80 tile, jangan nyembur
            }

            burstTimer++;
            if (burstTimer < TicksBetweenBursts)
                return;

            burstTimer = 0;
            SpawnFlameBurst(target);
        }

        private void SpawnFlameBurst(NPC target)
        {
            // Damage per partikel yang kena -- karena sekarang nembak banyak partikel kecil terus-menerus
            // (bukan satu proyektil besar sekali kena), damage per partikelnya sengaja kecil. Sesuaikan
            // angka 4f ini kalau DPS keseluruhan kurang/kelebihan.
            int damage = (int)Player.GetDamage(DamageClass.Melee).ApplyTo(4f);
            float knockback = 8f;

            Microsoft.Xna.Framework.Vector2 baseDirection =
                (target.Center - Player.Center).SafeNormalize(Microsoft.Xna.Framework.Vector2.UnitX * Player.direction);
            Microsoft.Xna.Framework.Vector2 spawnPos = Player.Center + baseDirection * 24f;

            for (int i = 0; i < ParticlesPerBurst; i++)
            {
                // Sedikit sebaran arah acak biar keliatan kayak semburan, bukan garis lurus sempurna --
                // ini yang bikin efeknya mirip gambar referensi Flamethrower vanilla.
                float spreadAngle = Microsoft.Xna.Framework.MathHelper.ToRadians(Main.rand.NextFloat(-12f, 12f));
                Microsoft.Xna.Framework.Vector2 shotDirection = baseDirection.RotatedBy(spreadAngle);
                float speedVariance = Main.rand.NextFloat(0.85f, 1.15f);

                int index = Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    spawnPos,
                    shotDirection * BetsyFlameBreath.Speed * speedVariance,
                    ModContent.ProjectileType<BetsyFlameBreath>(),
                    damage, knockback, Player.whoAmI);

                // Ukuran tiap partikel dibesarin & divariasiin (1.3x - 2x) -- sprite vanilla Flames aslinya
                // kecil, jadi kalau digambar di scale normal (1x) hasilnya keliatan tipis/jarang dibanding
                // Flamethrower asli yang partikelnya digambar lebih besar & padat.
                if (index >= 0 && index < Main.maxProjectiles)
                {
                    var proj = Main.projectile[index];
                    proj.scale = Main.rand.NextFloat(1.3f, 2f);

                    // Kirim jarak SEBENARNYA ke musuh lewat ai[0] -- proyektilnya pakai ini buat nentuin
                    // seberapa jauh dia harus terbang & seberapa cepat dia "membesar" sepanjang jalan.
                    // Ini yang bikin semburan otomatis menyesuaikan jarak musuh: musuh jauh -> semburan
                    // ikut jauh, musuh deket -> semburan berhenti/mengecil deket juga (persis Flamethrower).
                    float distanceToTarget = Microsoft.Xna.Framework.Vector2.Distance(spawnPos, target.Center);
                    proj.ai[0] = distanceToTarget;

                    // timeLeft disesuaikan biar partikel punya cukup waktu buat beneran nyampe ke jarak itu,
                    // dengan sedikit buffer. Dulu timeLeft fix 20 tick, makanya musuh yang jauh gak kekejar.
                    float speedNow = proj.velocity.Length();
                    proj.timeLeft = speedNow > 0.01f
                        ? (int)(distanceToTarget / speedNow) + 10
                        : 20;
                }
            }
        }

        private NPC FindNearestEnemy(Microsoft.Xna.Framework.Vector2 center, float maxDistance)
        {
            NPC closest = null;
            float closestDist = maxDistance;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.CanBeChasedBy() && !npc.dontTakeDamage)
                {
                    float dist = Microsoft.Xna.Framework.Vector2.Distance(center, npc.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }
    }
}