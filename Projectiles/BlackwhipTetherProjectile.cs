using System; // FIX: Ditambahkan agar tModLoader mengenali fungsi 'Math'
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class BlackwhipTetherProjectile : ModProjectile
    {
        // Mengarahkan tekstur langsung ke file asset cambuk utama
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

            if (!modPlayer.isTethered || modPlayer.tetheredNPCIndex == -1) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;
        }

        public override bool PreDraw(ref Color lightColor) {
            Player player = Main.player[Projectile.owner];
            BlackwhipPlayer modPlayer = player.GetModPlayer<BlackwhipPlayer>();
            if (!modPlayer.isTethered || modPlayer.tetheredNPCIndex == -1) return false;

            NPC npc = Main.npc[modPlayer.tetheredNPCIndex];
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            int frameWidth = texture.Width; 
            int frameHeight = texture.Height / 9; 

            Vector2 start = player.Center;
            Vector2 end = npc.Center;
            Vector2 path = end - start;
            float distance = path.Length();
            
            if (distance <= 0) return false;

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

                int segmentType = 1; 
                if (i == 0) segmentType = 0; 
                else if (i == numSegments - 1) segmentType = 2;

                int actualFrame = segmentType; 

                Rectangle frameRect = new Rectangle(0, actualFrame * frameHeight, frameWidth, frameHeight);
                Vector2 origin = new Vector2(frameWidth * 0.5f, 0);

                Main.EntitySpriteDraw(
                    texture,
                    currentPos - Main.screenPosition,
                    frameRect,
                    Lighting.GetColor((int)(currentPos.X / 16f), (int)(currentPos.Y / 16f)),
                    rotation,
                    origin,
                    dynamicScale, 
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }
    }
}