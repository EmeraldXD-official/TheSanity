using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class AnchorVisual : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.Anchor}";

        public override void SetDefaults()
        {
            Projectile.width = 32; 
            Projectile.height = 32;
            
            Projectile.friendly = false;  
            Projectile.hostile = true;     
            Projectile.tileCollide = true; 
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;      
            Projectile.timeLeft = 240;     
        }

        public override void AI()
        {
            if (Projectile.velocity != Vector2.Zero)
            {
                // Jika sudah dilempar, putar seperti biasa
                Projectile.rotation += 0.15f * (float)Projectile.direction;
                
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 1.2f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // BALANCING DEBUFF LOCATION
            target.AddBuff(BuffID.BrokenArmor, 420);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin;

            // --- REVISI TITIK POROS (ORIGIN) ---
            // ai[0] == 1f menandakan jangkar sedang diputar di tubuh Merman
            if (Projectile.ai[0] == 1f)
            {
                // X = 0 (paling kiri/cincin belakang), Y = tengah-tengah tinggi sprite
                drawOrigin = new Vector2(0f, texture.Height * 0.5f);
            }
            else
            {
                // Jika sudah dilempar terbang, kembali ke poros tengah-tengah bodi jangkar
                drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation, 
                drawOrigin, // Menggunakan poros dinamis
                Projectile.scale,
                SpriteEffects.None, 
                0
            );

            return false; 
        }
    }
}