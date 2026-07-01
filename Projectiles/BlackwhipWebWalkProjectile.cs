using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class BlackwhipWebWalkProjectile : ModProjectile
    {
        // Berbagi file gambar 9 frame yang sama agar teksturnya seragam dengan senjata
        public override string Texture => "TheSanity/Projectiles/BlackwhipProjectile"; 

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2; 
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            BlackwhipPlayer modPlayer = player.GetModPlayer<BlackwhipPlayer>();

            // Hancurkan visual jika mode Web Walk dimatikan
            if (!modPlayer.isWebWalking) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;
        }

        public override bool PreDraw(ref Color lightColor) {
            Player player = Main.player[Projectile.owner];
            BlackwhipPlayer modPlayer = player.GetModPlayer<BlackwhipPlayer>();
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            int frameWidth = texture.Width; 
            int frameHeight = texture.Height / 9; 

            // Loop untuk menggambar 4 buah kaki secara bersamaan
            for (int k = 0; k < 4; k++) {
                Vector2 start = player.Center;
                Vector2 end = modPlayer.legAnchors[k];
                Vector2 path = end - start;
                float distance = path.Length();
                
                if (distance <= 0) continue;

                Vector2 direction = Vector2.Normalize(path);
                float rotation = direction.ToRotation() - MathHelper.PiOver2;

                float baseSegmentLength = frameHeight - 2; 
                int numSegments = (int)Math.Ceiling(distance / baseSegmentLength);
                if (numSegments < 2) numSegments = 2;

                float actualSegmentLength = distance / (numSegments - 1);
                Vector2 dynamicScale = new Vector2(1f, (actualSegmentLength + 0.5f) / frameHeight);

                for (int i = 0; i < numSegments; i++) {
                    float progress = (float)i / (numSegments - 1);
                    Vector2 currentPos = Vector2.Lerp(start, end, progress);

                    // Pengaturan bagian frame jaring: 
                    // Frame 0 = Pangkal nempel di punggung, Frame 1 = Rantai tengah, Frame 2 = Ujung cakar jaring
                    int actualFrame = 1; 
                    if (i == 0) actualFrame = 0; 
                    if (i == numSegments - 1) actualFrame = 2; 

                    Rectangle frameRect = new Rectangle(0, actualFrame * frameHeight, frameWidth, frameHeight);
                    Vector2 origin = new Vector2(frameWidth * 0.5f, 0);

                    Main.EntitySpriteDraw(
                        texture,
                        currentPos - Main.screenPosition,
                        frameRect,
                        Lighting.GetColor((int)(currentPos.X / 16f), (int)(currentPos.Y / 16f)),
                        rotation,
                        origin,
                        dynamicScale, // Menggunakan anti-celah dinamis agar jaring tidak putus-putus
                        SpriteEffects.None,
                        0
                    );
                }
            }

            return false;
        }
    }
}