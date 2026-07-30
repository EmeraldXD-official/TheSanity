using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    public class PlutoBomb : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PlutoBomb";

        // 🛑 [INTEGRASI PATTERN 5 - ARENA BOMB] Spin acak per bomb (bukan rate tetap lagi),
        // biar tiap bomb yang dimuntahin Pluto pas muter di border keliatan variatif.
        private float spinSpeed = 0.02f;

        // 🛑 [INTEGRASI PATTERN 5 - ARENA BOMB] Bomb yang dilempar dengan velocity awal (misal
        // dari ArenaBombDash.LaunchArenaBomb) bakal melambat sendiri tiap tick pakai friction ini,
        // sampai akhirnya berhenti total & "menetap" di titik acak. Kalau bomb di-spawn diam
        // (velocity nol, kayak dipakai pattern-pattern lama), baris frictionnya ga ngefek apa-apa
        // karena emang udah nol dari awal.
        private const float ArenaFlightFriction = 0.985f;

        // 🛑 [LOKASI BALANCING DAMAGE LEDAKAN RED SPIKE] Sebelumnya damage 8 duri ini setengah
        // dari damage kontak si bomb (Projectile.damage / 2), jadi ikut naik/turun kalau damage
        // bomb di-balancing ulang. Sekarang independen, angka tetap sendiri.
        //
        // 🛑 [NERF ROUND 2] Diturunin lagi dari 35 -> 18. Round 1 (100 -> 35) ternyata masih
        // ke-observed ~200 di Master Mode (rasio aktual ~5.7x, lebih tinggi dari perkiraan awal).
        // 18 x ~5.7 ≈ 103, deket ke target ~100. Kalau masih meleset pas dites, tinggal geser
        // angka ini lagi (rasio boleh dihitung: observed_baru / 18, terus base_baru = 100 / rasio).
        private const int RedSpikeExplosionDamage = 18;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400; 
        }

        public override void SetDefaults() {
            Projectile.width = 48;       
            Projectile.height = 48;      
            
            Projectile.hostile = true;   
            Projectile.friendly = false; 
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;   
            Projectile.timeLeft = 300; 
        }

        public override void AI() {
            // PLAY SUARA ITEM61 SAAT PERTAMA KALI SPAWN
            if (Projectile.localAI[0] == 0f) {
                SoundEngine.PlaySound(SoundID.Item61, Projectile.Center);
                
                // 🛑 [SOLUSI ANOMALI SUDUT TETAP]
                // Mengacak total sudut rotasi awal ranjau saat pertama kali lahir di dunia.
                // Ini menjamin posisi akhir putaran bomb saat meledak selalu bervariasi secara dinamis!
                Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);

                // 🛑 [INTEGRASI PATTERN 5] Kecepatan & arah putar acak per bomb (dulu tetap 0.02f)
                spinSpeed = Main.rand.NextFloat(0.015f, 0.05f) * (Main.rand.NextBool() ? 1f : -1f);
                
                Projectile.localAI[0] = 1f; 
            }

            // Logika rotasi berputar visual tetap aktif bergerak maju
            Projectile.rotation += spinSpeed;

            // 🛑 [INTEGRASI PATTERN 5 - ARENA BOMB] Melambatkan bomb tiap tick sampai akhirnya
            // berhenti total di titik acak (dampak: posisi meledaknya jadi random & nyebar).
            if (Projectile.velocity != Vector2.Zero) {
                Projectile.velocity *= ArenaFlightFriction;
                if (Projectile.velocity.LengthSquared() < 0.04f) {
                    Projectile.velocity = Vector2.Zero;
                }
            }

            // LOGIKA AKSELERASI KEDIPAN INDEPENDEN PER BOMB
            float progress = (300f - Projectile.timeLeft) / 300f;
            float speedMultiplier = MathHelper.Lerp(1f, 7f, progress); 
            Projectile.localAI[1] += speedMultiplier * 0.15f; 
        }

        public override void OnKill(int timeLeft) {
            string randomSoundPath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PlutoBombExplode" + Main.rand.Next(1, 3);
            SoundEngine.PlaySound(new SoundStyle(randomSoundPath), Projectile.Center);

            // 🛑 [SHOCKWAVE DIHAPUS] Efek shockwave saat bomb meledak udah dicabut sesuai request --
            // bomb sekarang meledak tanpa distorsi layar sama sekali, cuma sisa suara + partikel +
            // red spike di bawah ini.

            // =========================================================================
            // LOKASI BALANCING & KALIBRASI PENEMBAKAN RED SPIKE SEJAJAR SPRITE
            // =========================================================================
            // 🛑 [LOKASI ADJUSTMENT KALIBRASI SUDUT SPIKE]
            // Jika arah jepretan 8 duri masih sedikit melenceng dari letak spike di spritemu,
            // silakan ganti angka di dalam MathHelper.ToRadians(0f) di bawah ini.
            // Contoh: ganti ke 45f jika ingin digeser miring, atau gunakan angka minus (-) untuk arah sebaliknya.
            float spikeCalibrationOffset = MathHelper.ToRadians(0f); 

            int amountOfSpikes = 8; 
            float spikeSpeed = 6f; 
            int spikeDamage = RedSpikeExplosionDamage;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < amountOfSpikes; i++) {
                    // Menggabungkan rotasi real-time bomb + offset kalibrasi + pembagian arah sudut 8 sisi melingkar
                    float angle = Projectile.rotation + spikeCalibrationOffset + i * (MathHelper.TwoPi / amountOfSpikes);
                    Vector2 launchVelocity = angle.ToRotationVector2() * spikeSpeed;

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(), 
                        Projectile.Center, 
                        launchVelocity, 
                        ModContent.ProjectileType<RedSpike>(), 
                        spikeDamage, 
                        0f, 
                        Main.myPlayer
                    );
                }
            }

            // Partikel Ledakan
            for (int k = 0; k < 20; k++) {
                int d1 = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0f, 0f, 100, default, 1f);
                Main.dust[d1].noGravity = true;
                int d2 = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PinkTorch, 0f, 0f, 100, default, 1.2f);
                Main.dust[d2].noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 10 * 60); 
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainTexture = ModContent.Request<Texture2D>(Texture).Value;
            Texture2D glowTexture = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PlutoBombGlow").Value;
            
            Vector2 drawOrigin = new Vector2(mainTexture.Width * 0.5f, mainTexture.Height * 0.5f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);

            Color finalBombColor = lightColor;
            Color finalGlowColor = Color.White;

            if (Projectile.timeLeft > 90) { 
                float alphaIntensity = (float)Math.Sin(Projectile.localAI[1]) * 0.5f + 0.5f;
                finalGlowColor = Color.White * alphaIntensity;
            }
            else if (Projectile.timeLeft > 30) {
                finalGlowColor = Color.White; 
            }
            else {
                float redProgress = (30f - Projectile.timeLeft) / 30f; 
                finalBombColor = Color.Lerp(lightColor, Color.Red, redProgress);
                finalGlowColor = Color.Red;
            }

            Main.EntitySpriteDraw(
                mainTexture, 
                drawPos, 
                null, 
                finalBombColor, 
                Projectile.rotation, 
                drawOrigin, 
                1f, 
                SpriteEffects.None, 
                0
            );

            Main.EntitySpriteDraw(
                glowTexture, 
                drawPos, 
                null, 
                finalGlowColor, 
                Projectile.rotation, 
                drawOrigin, 
                1f, 
                SpriteEffects.None, 
                0
            );

            return false; 
        }
    }
}