using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio; // <--- FIX: Menambahkan namespace ini agar SoundEngine terdeteksi!
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Projectiles
{
    public class CrystalShardGrenade : ModProjectile
    {
        // Meminjam aset gambar Item Crystal Vile Shard asli dari vanilla Terraria
        public override string Texture => $"Terraria/Images/Item_{ItemID.CrystalVileShard}";

        public override void SetDefaults()
        {
            Projectile.width = 18;               
            Projectile.height = 18;
            Projectile.aiStyle = -1;             
            Projectile.friendly = false;         
            Projectile.hostile = true;           
            Projectile.penetrate = 1;            
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;           
            Projectile.tileCollide = true;       

            // --- LOKASI BALANCING: UKURAN SPRITE ---
            Projectile.scale = 1.2f;             
        }

        public override void AI()
        {
            // =========================================================================
            // 1. LOGIKA FISIK: GRAVITASI & EFEK BERPUTAR (ROTASI)
            // =========================================================================
            // --- LOKASI BALANCING: KEKUATAN GRAVITASI ---
            float gravity = 0.25f; 
            Projectile.velocity.Y += gravity;

            if (Projectile.velocity.Y > 14f)
            {
                Projectile.velocity.Y = 14f;
            }

            // --- LOKASI BALANCING: KECEPATAN PUTARAN ---
            Projectile.rotation += 0.15f * (float)Projectile.direction;

            // --- BEAUTIFIER: EFFEK PARTIKEL UDARA ---
            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkCrystalShard, Vector2.Zero, 150, default, 1.0f);
                d.noGravity = true;
            }
        }

        // =========================================================================
        // 2. LOGIKA KLASIFIKASI TABRAKAN (BLOCK VS PLATFORM CERDAS)
        // =========================================================================
        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxModifier)
        {
            Player targetPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];

            int tileX = (int)(Projectile.Center.X / 16f);
            int tileY = (int)((Projectile.position.Y + Projectile.height) / 16f);

            if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY)
            {
                Tile tile = Main.tile[tileX, tileY];

                if (tile.HasTile && TileID.Sets.Platforms[tile.TileType])
                {
                    // KONDISI A: Dilempar dari bawah ke atas
                    if (Projectile.velocity.Y < 0)
                    {
                        fallThrough = true; 
                        return true;
                    }

                    // KONDISI B: Dilempar dari atas ke bawah
                    if (Projectile.Center.Y < targetPlayer.Bottom.Y)
                    {
                        fallThrough = true; 
                    }
                    else
                    {
                        fallThrough = false; 
                    }
                }
            }

            return true;
        }

        // =========================================================================
        // 3. LOGIKA PEMICU KETIKA MENYENTUH LANTAI YANG VALID (TRIGGER CRYSTAL SHARD)
        // =========================================================================
        public override void OnKill(int timeLeft)
        {
            // Sekarang SoundEngine aman digunakan tanpa error compiler
            SoundEngine.PlaySound(SoundID.Item27, Projectile.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // --- LOKASI BALANCING: KECEPATAN & DAMAGE DURI KRISTAL ---
                Vector2 upwardVelocity = new Vector2(0f, -6f); 
                int shardDamage = Projectile.damage; 

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    upwardVelocity,
                    ModContent.ProjectileType<CrystalShardShaft>(),
                    shardDamage,
                    4f,
                    Main.myPlayer,
                    1f, 
                    0f
                );
            }

            // --- BEAUTIFIER: LEDAKAN PECAHAN KRISTAL ---
            for (int i = 0; i < 15; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkCrystalShard, dustVel, 50, default, 1.4f);
                d.noGravity = Main.rand.NextBool(2);
            }
        }

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