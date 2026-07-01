using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RainbowCustomBolt : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/RainbowSpike";

        public override void SetStaticDefaults()
        {
            // --- PANDUAN VISUAL: PANJANG EKOR PELANGI ---
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;       
            Projectile.height = 16;      
            
            // --- REKUES FIX: GRAVITASI KUSTOM ---
            // Kita matikan aiStyle peluru bawaan (-1 artinya AI murni buatan sendiri)
            // Dan hapus baris AIType = ProjectileID.Bullet;
            Projectile.aiStyle = -1;      
            
            Projectile.hostile = true;   
            Projectile.friendly = false;
            Projectile.tileCollide = true; 
            Projectile.timeLeft = 360;   
            Projectile.scale = 1.0f;     
        }

        // =========================================================================
        // LOGIKA PERGERAKAN: RUMUS GRAVITASI KUSTOM & ROTASI PARABOLA HALUS
        // =========================================================================
        public override void AI()
        {
            // --- LOKASI BALANCING: KEKUATAN GRAVITASI ---
            // Nilai Y ditambahkan terus-menerus di setiap frame agar proyektil melengkung jatuh ke bawah.
            // Naikkan angkanya (misal ke 0.25f atau 0.3f) jika ingin objek terasa lebih berat dan cepat jatuh!
            float gravityForce = 0.18f; 
            
            // Batasi kecepatan jatuh maksimal agar tidak melesat menembus batas kewajaran (Terminal Velocity)
            float maxFallSpeed = 14f; 

            Projectile.velocity.Y += gravityForce;
            if (Projectile.velocity.Y > maxFallSpeed)
            {
                Projectile.velocity.Y = maxFallSpeed;
            }

            // --- REKUES FIX: ROTASI MENUKIK DINAMIS MENGHADAP KE ATAS ---
            // Karena sprite dasar menghadap ke atas (+90 derajat / PiOver2), kalkulasi rotasi dihitung 
            // secara real-time berdasarkan pergeseran Velocity saat ini. 
            // Ketika melengkung jatuh, kepala spike otomatis ikut menukik ke bawah dengan sangat halus!
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            // Menambahkan efek pendaran cahaya (Light) RGB di sekitar peluru
            float colorOffset = Main.GlobalTimeWrappedHourly * 3f;
            Color rgbLight = Main.hslToRgb(colorOffset % 1f, 1f, 0.6f);
            Lighting.AddLight(Projectile.Center, rgbLight.ToVector3() * 0.5f);
        }

        // =========================================================================
        // LOGIKA DEBUFF: MEMBERIKAN 3 DEBUFF SEKALIGUS SELAMA 10 DETIK (600 FRAME)
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            int debuffDuration = 600; // 10 detik = 600 frame

            target.AddBuff(BuffID.WitheredArmor, debuffDuration);
            target.AddBuff(BuffID.WitheredWeapon, debuffDuration);
            target.AddBuff(BuffID.NoBuilding, debuffDuration);
        }

        // =========================================================================
        // LOGIKA GAMBAR: MANIPULASI WARNA RGB PELANGI & TRAIL KOSMETIK
        // =========================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // 1. GAMBAR EFEK TRAIL PELANGI (MURNI VISUAL)
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
            {
                if (Projectile.oldPos[k] == Vector2.Zero) continue;

                Vector2 trailDrawPos = Projectile.oldPos[k] + (Projectile.Size * 0.5f) - Main.screenPosition;

                float trailOpacity = 1f - ((float)k / Projectile.oldPos.Length);

                float trailColorOffset = (Main.GlobalTimeWrappedHourly * 3f) - (k * 0.05f);
                Color trailRainbow = Main.hslToRgb(trailColorOffset % 1f, 1f, 0.6f);
                
                Color finalTrailColor = trailRainbow * trailOpacity * 0.5f; 
                finalTrailColor.A = 0; // Efek Neon Glow

                float trailScale = Projectile.scale * trailOpacity * 0.9f;

                Main.EntitySpriteDraw(
                    texture,
                    trailDrawPos,
                    null,
                    finalTrailColor,
                    Projectile.rotation, 
                    drawOrigin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. GAMBAR UTAMA: PROYEKTIL ASLI DI BAGIAN DEPAN
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            
            float mainColorOffset = Main.GlobalTimeWrappedHourly * 3f;
            Color mainRainbow = Main.hslToRgb(mainColorOffset % 1f, 1f, 0.6f);
            
            Color finalMainColor = mainRainbow * 1.0f;
            finalMainColor.A = 0; 

            Main.EntitySpriteDraw(
                texture,
                mainDrawPos,
                null,
                finalMainColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false; 
        }
    }
}