using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace TheSanity
{
    public class CrimsonBlade : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.Bladetongue}";

        public override void SetDefaults()
        {
            Projectile.width = 24; 
            Projectile.height = 24;
            
            Projectile.hostile = true;    
            Projectile.friendly = false;  
            
            // FIX PHYSICAL: Diubah menjadi true agar pedang HANCUR saat menabrak dinding/block solid
            Projectile.tileCollide = true; 
            
            Projectile.aiStyle = -1;      
            Projectile.timeLeft = 300; // Batas hidup maksimal di udara (5 detik)
        }

        public override void AI()
        {
            // Pergerakan murni mengikuti velocity
            Projectile.position += Projectile.velocity;

            // --- TRIK ROTASI BERPUTAR ATAU LOCK ARAH ---
            if (Projectile.ai[1] == 1f)
            {
                // [ROTATION SPEED BALANCING LOCATION]
                Projectile.rotation += 0.25f; 
            }
            else
            {
                if (Projectile.velocity.Y > 0)
                {
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.ToRadians(45f);
                }
            }

            // Efek partikel darah selama melayang
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity *= 0.15f; 
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.05f, 0.05f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // --- BALANCING LOCATION: INFLICT ICHOR 5 DETIK ---
            // 300 Frames = Tepat 5 Detik murni di dalam engine Terraria (60 frame/detik)
            target.AddBuff(BuffID.Ichor, 300);

            // Efek muncratan darah masif saat sabetan pedang mendarat di tubuh player
            for (int i = 0; i < 12; i++)
            {
                Dust.NewDust(target.position, target.width, target.height, DustID.Blood);
            }
        }

        // --- MEKANIK BARU: EFEK SAAT MENABRAK WALL / BLOCK ---
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Pemicu suara dentingan pedang pecah/membentur batu gua
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);

            // Munculkan cipratan darah Crimson yang menempel di dinding block saat pedangnya hancur
            for (int i = 0; i < 8; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 1f);
                d.velocity = Main.rand.NextVector2Circular(3f, 3f);
            }

            return true; // Mengembalikan nilai true akan otomatis menghancurkan proyektil ini seketika
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            SpriteEffects effects = SpriteEffects.None;
            float rotationOffset = MathHelper.ToRadians(45f); 

            if (Projectile.spriteDirection == -1 && Projectile.ai[1] != 1f)
            {
                effects = SpriteEffects.FlipHorizontally; 
                rotationOffset = MathHelper.ToRadians(-45f); 
            }

            float finalRotation = (Projectile.ai[1] == 1f) ? Projectile.rotation : rotationOffset;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                finalRotation, 
                drawOrigin,
                Projectile.scale,
                effects, 
                0
            );

            return false; 
        }
    }
}