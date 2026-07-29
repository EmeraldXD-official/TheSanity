using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using System;

namespace TheSanity
{
    public class BlackHarpyFeather : ModProjectile
    {
        // Peminjaman jalur tekstur wajib tModLoader agar tidak eror
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = -1; // Matikan AI vanilla, kita tulis manual di bawah
            Projectile.friendly = false; 
            Projectile.hostile = true;  // Bisa melukai player
            Projectile.penetrate = 1;    
            Projectile.timeLeft = 300;   // Bertahan 5 detik di udara
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
        }

        // =========================================================================
        // [HOMING AI LOCATION]: 1 DETIK MEMBELOK, LALU TERBANG LURUS
        // =========================================================================
        public override void AI()
        {
            // Membuat visual bulu berputar mengikuti arah kecepatannya
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Projectile.ai[0]++; // Timer internal proyektil (berjalan per frame)

            // 60 Frame = 1 Detik. Dia hanya akan membelok/homing di detik pertama saja
            if (Projectile.ai[0] < 60f)
            {
                if (Main.rand.NextBool(3))
                {
                    // Efek partikel ungu tipis saat bulu sedang mendeteksi player
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, Vector2.Zero, 100, default, 0.8f);
                    d.noGravity = true;
                }

                // Cari lokasi player terdekat
                Player target = null;
                float maxDistance = 1000f; 

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (p.active && !p.dead)
                    {
                        float dist = Vector2.Distance(p.Center, Projectile.Center);
                        if (dist < maxDistance)
                        {
                            maxDistance = dist;
                            target = p;
                        }
                    }
                }

                // Logika Belok: Menyeret arah terbang agar mendekati posisi player
                if (target != null)
                {
                    // Angka 10f di bawah adalah kecepatan gerak laju proyektilnya
                    Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 10f; 
                    
                    // Semakin kecil pembaginya (misal 30f), beloknya makin tajam. 
                    // Angka 40f membuat belokan halus (biar chance miss tetap ada kalau player dash cepat)
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 1f / 40f); 
                }
            }
            // Setelah lewat 60 frame (1 detik), interpolasi Lerp berhenti, dan bulu melesat lurus statis!
        }

        // =========================================================================
        // [DEBUFF CHANCE LOCATION]: CHANCE DEBUFF BADAI KEGELAPAN
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            int twoSeconds = 2 * 60; // 2 Detik = 120 Frame bersih

            // 1) 100% Kena Darkness (ID 22)
            target.AddBuff(BuffID.Darkness, twoSeconds);

            // 2) 26% Chance Kena Blackout (ID 156)
            if (Main.rand.NextFloat() <= 0.26f)
            {
                target.AddBuff(BuffID.Blackout, twoSeconds);
            }

            // 3) 5% Chance Kena Obstructed / Buta Total (ID 164)
            if (Main.rand.NextFloat() <= 0.05f)
            {
                target.AddBuff(BuffID.Obstructed, twoSeconds);
            }
        }

        // =========================================================================
        // [VISUAL RECOLOR LOCATION]: RECOLOR BULU HARPY MENJADI HITAM
        // =========================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.HarpyFeather].Value;

            Vector2 drawOrigin = new Vector2(texture.Width / 2f, Projectile.height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Mengubah total warna bawaan bulu harpy menjadi Hitam Pekat bercahaya (Alpha 200)
            Color recolor = new Color(0, 0, 0, 200); 

            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                recolor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false; // Sembunyikan gambar asli panah tiruannya
        }
    }
}