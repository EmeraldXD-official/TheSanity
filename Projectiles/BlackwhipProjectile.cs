using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class BlackwhipProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.DefaultToWhip();
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            // Total sprite sheet dibagi menjadi 9 frame secara vertikal
            int frameWidth = texture.Width; // 18
            int frameHeight = texture.Height / 9; // 89 / 3 per projectile, total/9

            // Mengambil ID Projectile (0, 1, atau 2) yang dikirim dari ModItem
            int projectileID = (int)Projectile.ai[1];
            if (projectileID < 0 || projectileID > 2) projectileID = 0;

            // Menggambar segmen cambuk dari pangkal ke ujung
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 point = points[i];
                Vector2 next = points[i + 1];
                Vector2 rotationVector = next - point;
                float rotation = rotationVector.ToRotation() - MathHelper.PiOver2;
                
                // Tentukan tipe segmen (0 = Gagang, 1 = Badan, 2 = Ujung)
                int segmentType = 1; 
                if (i == 0)
                {
                    segmentType = 0; // Gagang
                }
                else if (i == points.Count - 2)
                {
                    segmentType = 2; // Ujung
                }

                // RUMUS 9 FRAME:
                // Projectile 0 -> Frame 0, 1, 2
                // Projectile 1 -> Frame 3, 4, 5
                // Projectile 2 -> Frame 6, 7, 8
                int actualFrame = (projectileID * 3) + segmentType;

                Rectangle frameRect = new Rectangle(0, actualFrame * frameHeight, frameWidth, frameHeight);
                Vector2 origin = new Vector2(frameWidth * 0.5f, 0);
                Vector2 scale = new Vector2(1f, rotationVector.Length() / frameHeight);

                Main.EntitySpriteDraw(
                    texture, 
                    point - Main.screenPosition, 
                    frameRect, 
                    lightColor, 
                    rotation, 
                    origin, 
                    scale, 
                    SpriteEffects.None, 
                    0
                );
            }

            return false;
        }
    }
}