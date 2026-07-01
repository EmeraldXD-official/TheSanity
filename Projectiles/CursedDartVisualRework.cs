using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class CursedDartVisualRework : ModProjectile
    {
        // --- TRIK MEMINJAM SPRITE INTERNAL TERRARIA ---
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.CursedDart}";

        public override void SetDefaults()
        {
            Projectile.width = 14;               // Ukuran hitbox disesuaikan dengan Dart
            Projectile.height = 14;
            Projectile.aiStyle = -1;             // Menggunakan AI kustom sepenuhnya
            Projectile.friendly = false;         // Set false karena ini serangan musuh
            Projectile.hostile = true;           // UPDATE: Set true agar AKTIF bisa melukai player!
            Projectile.penetrate = 1;            // Hancur setelah 1x menabrak player/dinding
            Projectile.tileCollide = true;       // Hancur jika menabrak block/dinding
            Projectile.ignoreWater = false;
            Projectile.timeLeft = 360;           // Waktu aktif proyektil (6 detik)

            // --- LOKASI BALANCING: BASE STATS ---
            // Catatan: Damage musuh di Expert/Master Mode otomatis dikalikan oleh engine Terraria.
            // Angka 15 di sini adalah nilai dasar (Base Damage) saat di Classic Mode.
            Projectile.damage = 15; 
        }

        public override void AI()
        {
            // LOGIKA ROTASI MENGIKUTI ARAH TERBANG
            // Menambahkan MathHelper.PiOver2 (90 derajat) agar ujung dart yang aslinya menghadap atas
            // berputar mengikuti arah vektor kecepatannya (velocity)
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            // BEAUTIFIER (EFEK PARTIKEL JEJAK API)
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center, 
                    DustID.CursedTorch, 
                    -Projectile.velocity * 0.2f, // Partikel terlempar sedikit ke belakang proyektil
                    100, 
                    default, 
                    1.2f
                );
                d.noGravity = true;
            }
        }

        // =========================================================================
        // LOKASI BALANCING: EFEK SAAT MENGENAI PLAYER
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // --- LOKASI BALANCING: DURASI DEBUFF ---
            // Menambahkan debuff Cursed Inferno ke player yang terkena dart
            // 3 detik = 180 frame (3 * 60 frame per detik)
            int debuffDuration = 180; 

            target.AddBuff(BuffID.CursedInferno, debuffDuration);
        }

        public override void OnKill(int timeLeft)
        {
            // Efek ledakan partikel kecil saat dart menabrak sesuatu dan hancur
            for (int i = 0; i < 10; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch, dustVel, 80, default, 1f);
                d.noGravity = true;
            }
        }

        // CUSTOM DRAW RENDER
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
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