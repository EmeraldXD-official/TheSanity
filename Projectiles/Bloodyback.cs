using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [CUSTOM PROJECTILE SYSTEM]: BLOODYBACK (PURE VANILLA ASSET & DUST LINGKARAN)
    // =========================================================================
    public class Bloodyback : ModProjectile
    {
        // Menggunakan aset tekstur kosong bawaan Terraria agar tidak perlu file .png kustom
        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetDefaults()
        {
            // FIXED: Menggunakan huruf kapital 'Projectile' sesuai standar tML 1.4.4+
            Projectile.width = 16;
            Projectile.height = 16;
            
            Projectile.hostile = true;    // Menyerang Player
            Projectile.friendly = false;  // Tidak menyerang monster
            Projectile.tileCollide = false; // BISA TEMBUS BLOCK / DINDING
            
            // KUNCI COOLDOWN HIT: Hanya bisa hit player 1x lalu hancur murni
            Projectile.penetrate = 1; 

            // TIME LIMIT LOCATION: 300 Frame = Tepat 5 Detik akan lenyap jika tidak kena target
            Projectile.timeLeft = 300; 

            // Nilai AI bawaan untuk tipe peluru sihir/efek kustom
            Projectile.aiStyle = -1; 
        }

        // =========================================================================
        // [HOMING & VISUAL PARTICLES LOCATION]: LOGIKA MENGEJAR PLAYER & BENTUK BOLA DETIK
        // =========================================================================
        public override void AI()
        {
            // -------------------------------------------------------------------------
            // 1. VISUAL: MEMBENTUK LINGKARAN SIMPEL DARI PARTIKEL DARAH (DUST)
            // -------------------------------------------------------------------------
            if (Main.netMode != NetmodeID.Server)
            {
                // Buat 3 partikel melingkar setiap framenya untuk mempertegas bentuk bulatnya
                for (int i = 0; i < 3; i++)
                {
                    // Gunakan rumus matematika sin/cos acak melingkar untuk memposisikan partikel darah
                    Vector2 dustOffset = Main.rand.NextVector2CircularEdge(14f, 14f);
                    
                    Dust d = Dust.NewDustDirect(
                        Projectile.Center + dustOffset, 
                        0, 0, 
                        DustID.Blood, // Efek partikel darah pekat vanilla
                        0f, 0f, 
                        100, 
                        default, 
                        1.3f
                    );
                    d.velocity *= 0.1f; // Biarkan partikel diam membentuk bola
                    d.noGravity = true;  // Efek melayang mistis
                }
            }

            // -------------------------------------------------------------------------
            // 2. LOGIKA HOMING (MENGEJAR PLAYER TERDEKAT SECARA AGRESIF)
            // -------------------------------------------------------------------------
            Player target = null;
            float maxDistance = 1000f; // Jarak deteksi maksimal peluru mengejar (62 block)

            // Cari player yang aktif di area jangkauan peluru
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player pl = Main.player[p];
                if (pl.active && !pl.dead)
                {
                    float dist = Vector2.Distance(Projectile.Center, pl.Center);
                    if (dist < maxDistance)
                    {
                        maxDistance = dist;
                        target = pl;
                    }
                }
            }

            // Jika target player valid ditemukan, kunci dan belokkan arah kecepatan
            if (target != null)
            {
                Vector2 desiredVelocity = target.Center - Projectile.Center;
                desiredVelocity.Normalize();

                // HOMING BALANCING LOCATION: Kecepatan laju peluru (6f)
                float homingSpeed = 6f; 
                desiredVelocity *= homingSpeed;

                // INTERPOLASI BELOKAN (0.07f = Belok secara halus dan meliuk estetik)
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.07f);
            }
        }

        // =========================================================================
        // [DEBUFF DAMAGE LOCATION]: MEMBERI DEBUFF BLEEDING SELAMA 2 DETIK
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // LOKASI DURASI DEBUFF: BuffID.Bleeding (ID 30), Durasi 120 Frame = Tepat 2 Detik!
            target.AddBuff(BuffID.Bleeding, 120);

            // Efek suara daging robek/muncrat saat menabrak tubuh player
            Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath11, Projectile.Center);
        }

        // Efek kosmetik hancur saat meletus / habis durasi waktu luangnya
        public override void Kill(int timeLeft)
        {
            if (Main.netMode != NetmodeID.Server)
            {
                // Ledakan partikel darah melingkar tipis saat pecah
                for (int i = 0; i < 15; i++)
                {
                    Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Crimson, 0f, 0f, 100, default, 1.2f);
                    d.velocity = Main.rand.NextVector2Circular(4f, 4f);
                    d.noGravity = true;
                }
            }
        }
    }
}