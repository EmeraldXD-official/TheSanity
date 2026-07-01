using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class DartPistolTurretVisual : ModProjectile
    {
        // --- TRIK MEMINJAM SPRITE INTERNAL TERRARIA ---
        // Meminjam aset gambar Dart Pistol asli dari item vanilla Terraria
        public override string Texture => $"Terraria/Images/Item_{ItemID.DartPistol}";

        public override void SetDefaults()
        {
            Projectile.width = 28;               // Ukuran hitbox disesuaikan dengan proporsi pistol
            Projectile.height = 20;
            Projectile.aiStyle = -1;             // AI kustom sepenuhnya untuk menempel dan membidik
            Projectile.friendly = false;         
            Projectile.hostile = false;          // Set false karena ini hanya unit visual senjata, pelurunya yang berbahaya
            Projectile.penetrate = -1;           
            Projectile.tileCollide = false;      // Tembus block agar tidak menyangkut saat menempel pada boss
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;            // Durasi hidup dasar (bisa dioverride saat di-spawn oleh NPC)

            // --- LOKASI BALANCING: UKURAN SPRITE SENJATA ---
            // Mengubah nilai ini untuk mengatur skala besar/kecilnya pistol di arena (1.5f = 150% lebih besar)
            Projectile.scale = 1.5f; 
        }

        public override void AI()
        {
            // =========================================================================
            // LOGIKA MENEMPEL PADA ENEMY / BOSS
            // =========================================================================
            // Mengambil index WHOAMI dari NPC pemanggil yang disimpan di Projectile.ai[1]
            int ownerNPCIndex = (int)Projectile.ai[1];

            // Validasi apakah NPC tersebut masih aktif di dalam game
            if (ownerNPCIndex >= 0 && ownerNPCIndex < Main.maxNPCs && Main.npc[ownerNPCIndex].active)
            {
                NPC ownerNPC = Main.npc[ownerNPCIndex];
                
                // Mengunci posisi koordinat pistol agar selalu menempel persis di tengah tubuh Boss/Mimic
                Projectile.Center = ownerNPC.Center;
            }
            else
            {
                // Jika NPC mati atau menghilang, langsung hancurkan senjata visual ini
                Projectile.Kill();
                return;
            }

            // =========================================================================
            // LOGIKA AIMING & ROTASI MENGIKUTI PLAYER
            // =========================================================================
            // Mencari target Player terdekat yang aktif
            Player targetPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            
            if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead)
            {
                Vector2 directionToPlayer = targetPlayer.Center - Projectile.Center;

                // Mengatur rotasi agar moncong pistol lurus mengarah ke Player
                Projectile.rotation = directionToPlayer.ToRotation();

                // Mengatur arah hadap sprite (Flip vertikal jika membidik ke arah kiri)
                Projectile.spriteDirection = (directionToPlayer.X < 0) ? -1 : 1;
            }

            // --- BEAUTIFIER: EFEK VISUAL PARTIKEL ---
            // Mengeluarkan percikan dust tipis di sekitar pistol agar terlihat memiliki energi/aura kustom
            if (Main.rand.NextBool(5))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.IchorTorch, Vector2.Zero, 120, default, 1.1f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Efek partikel letupan kecil saat pistol selesai menembak dan menghilang
            for (int i = 0; i < 10; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IchorTorch, dustVel, 100, default, 1.3f);
                d.noGravity = true;
            }
        }

        // --- CUSTOM DRAW RENDER (MENGATASI FLIP VISUAL SAAT HADAP KIRI) ---
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            SpriteEffects spriteEffects = SpriteEffects.None;
            float rotationOffset = Projectile.rotation;

            // Jika pistol menghadap ke kiri, balik secara vertikal agar aset gambar tidak terbalik ke bawah
            if (Projectile.spriteDirection == -1)
            {
                spriteEffects = SpriteEffects.FlipVertically;
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                rotationOffset,
                drawOrigin,
                Projectile.scale,
                spriteEffects,
                0
            );

            return false; // Mematikan render otomatis bawaan tModLoader
        }
    }
}