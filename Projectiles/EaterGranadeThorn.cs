using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio; 
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Projectiles
{
    public class VileSpikeSeed : ModProjectile
    {
        // REKUES: Meminjam aset gambar Item Vilethorn asli dari vanilla Terraria
        public override string Texture => $"Terraria/Images/Item_{ItemID.Vilethorn}";

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

            // --- 📐 LOKASI BALANCING: UKURAN SPRITE BIJI ---
            Projectile.scale = 1.2f;             
        }

        public override void AI()
        {
            // =========================================================================
            // 1. LOGIKA FISIK: GRAVITASI & EFEK BERPUTAR (ROTASI)
            // =========================================================================
            // --- 🚀 LOKASI BALANCING: KEKUATAN GRAVITASI ---
            // Semakin besar nilainya, semakin cepat biji ini jatuh menukik ke tanah
            float gravity = 0.25f; 
            Projectile.velocity.Y += gravity;

            // Batas maksimum kecepatan jatuh bebas (Terminal Velocity)
            if (Projectile.velocity.Y > 14f)
            {
                Projectile.velocity.Y = 14f;
            }

            // --- 🔄 LOKASI BALANCING: KECEPATAN PUTARAN SAAT MELAYANG ---
            Projectile.rotation += 0.15f * (float)Projectile.direction;

            // --- ✨ BEAUTIFIER: EFEK PARTIKEL UDARA (CORRUPTION THEME) ---
            if (Main.rand.NextBool(4))
            {
                // Mengganti duri pink menjadi Dust Demonite ungu
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Demonite, Vector2.Zero, 150, default, 1.0f);
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
                    // KONDISI A: Dilempar dari bawah ke atas menembus platform
                    if (Projectile.velocity.Y < 0)
                    {
                        fallThrough = true; 
                        return true;
                    }

                    // KONDISI B: Dilempar dari atas ke bawah mengikuti posisi player
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
        // 3. LOGIKA PEMICU KETIKA MENYENTUH LANTAI (TRIGGER VILETHORN SPIKE)
        // =========================================================================
        public override void OnKill(int timeLeft)
        {
            // --- 🔊 LOKASI BALANCING: SOUND EFFECT ---
            // Mengubah Item27 (kristal pecah) menjadi Item8 (Suara desingan sihir Vilethorn vanilla)
            SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // --- ⚔️ LOKASI BALANCING: KECEPATAN & DAMAGE DURI ---
                // upwardVelocity.Y mengatur arah dorongan awal pilar duri (default -6f berarti lurus ke atas)
                Vector2 upwardVelocity = new Vector2(0f, -6f); 
                
                // Meneruskan damage dari senjata/NPC pelempar ke proyektil pilar duri baru
                int shardDamage = Projectile.damage; 

                // REKUES: Memanggil VilethornSpikeShaft yang pre-Hardmode sebagai pengganti Crystal Shard
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    upwardVelocity,
                    ModContent.ProjectileType<VilethornSpikeShaft>(),
                    shardDamage, // [DAMAGE LOCATION]
                    4f,
                    Main.myPlayer,
                    1f, 
                    0f
                );
            }

            // --- ✨ BEAUTIFIER: LEDAKAN SERPIHAN SEED (CORRUPTION THEME) ---
            for (int i = 0; i < 15; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(5f, 5f);
                // Mengubah partikel ledakan menjadi Demonite berwana ungu gelap
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Demonite, dustVel, 50, default, 1.4f);
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