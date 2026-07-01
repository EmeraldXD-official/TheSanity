using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class IchorDartVisualRework : ModProjectile
    {
        // --- TRIK MEMINJAM SPRITE INTERNAL TERRARIA ---
        // Meminjam aset gambar peluru Ichor Dart asli milik vanilla Terraria
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.IchorDart}";

        public override void SetDefaults()
        {
            Projectile.width = 14;               // Ukuran hitbox disesuaikan dengan fisik Dart yang ramping
            Projectile.height = 14;
            Projectile.aiStyle = -1;             // AI kustom penuh agar jalurnya stabil dan lurus
            Projectile.friendly = false;         // Set false agar tidak melukai musuh/NPC
            Projectile.hostile = true;           // Set true agar bisa memberikan damage langsung ke player
            Projectile.penetrate = 1;            // Set 1 agar peluru langsung hancur/meletup saat mengenai target atau dinding
            Projectile.tileCollide = true;       // Set true agar peluru pecah saat menabrak block/tanah
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;           // Umur peluru di arena (5 detik sebelum hilang otomatis)

            // --- LOKASI BALANCING: UKURAN SPRITE PELURU ---
            // Mengubah nilai ini jika kamu ingin peluru Ichor ini terlihat lebih besar dan jelas di layar
            Projectile.scale = 1.2f;             
        }

        public override void AI()
        {
            // --- LOGIKA ROTASI PELURU MELUNCUR ---
            // Mengatur arah rotasi gambar agar ujung tajam dart selalu menghadap ke arah jalurnya melesat
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2; // Offset 90 derajat agar sesuai orientasi sprite dart
            }

            // --- BEAUTIFIER: EFEK TRACER PARTIKEL ICHOR ---
            // Mengeluarkan partikel kuning menyala khas cairan Ichor di sepanjang jalur terbangnya
            if (Main.rand.NextBool(2)) // 50% peluang keluar di setiap frame agar efek ekor peluru terlihat padat
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center, 
                    DustID.IchorTorch, 
                    Projectile.velocity * 0.2f, // Partikel tertinggal sedikit di belakang peluru
                    100, 
                    default, 
                    1.2f
                );
                d.noGravity = true; // Biarkan partikel melayang stabil tanpa jatuh bebas
            }
        }

        // =========================================================================
        // LOKASI BALANCING: EFEK TAMBAHAN SAAT MENGENAI PLAYER
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // --- LOKASI BALANCING: DURASI DEBUFF ICHOR ---
            // Saat player tertembak peluru ini, mereka otomatis terkena debuff Ichor (Defense berkurang 15)
            // 300 frame = 5 Detik durasi debuff
            target.AddBuff(BuffID.Ichor, 300);
        }

        public override void OnKill(int timeLeft)
        {
            // Efek suara letupan kecil saat peluru hancur menabrak dinding atau player
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);

            // Percikan cairan Ichor saat peluru pecah/mati
            for (int i = 0; i < 12; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IchorTorch, dustVel, 80, default, 1.4f);
                d.noGravity = Main.rand.NextBool(2); // Sebagian partikel jatuh secara alami seperti cipratan air
            }
        }

        // --- CUSTOM DRAW RENDER ---
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

            return false; // Mematikan metode render standar bawaan game
        }
    }
}