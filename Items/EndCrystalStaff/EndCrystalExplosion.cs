using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

// TODO: change "YourModName" to your actual mod's root namespace / folder structure.
namespace TheSanity.Items.EndCrystalStaff
{
    // The .png for this MUST be named "EndCrystalExplosion.png" and sit right next to this .cs file.
    // It's a 16-frame vertical strip (each frame 185x185) built from the ExplodeProj.png sheet you sent.
    //
    // This is a purely visual effect - it deals no damage itself (the damage already happened via
    // EndCrystalProjectile.OnHitNPC). It just plays the ring/shockwave animation once, then removes itself.
    public class EndCrystalExplosion : ModProjectile
    {
        private const int TotalFrames = 16;
        private const int TicksPerFrame = 1; // higher = slower playback (sped up per request)

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = TotalFrames;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames * TicksPerFrame + 5;

            // The source frames are 185x185, which is huge for a small crystal blast -
            // scale this down/up until it matches the size explosion you want.
            Projectile.scale = 0.5f;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= TicksPerFrame)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= TotalFrames)
                {
                    Projectile.Kill();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / TotalFrames;
            var sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);

            // The source art is a plain grey/white ring - tint it pink/purple to match the crystal theme.
            // Adjust this color freely to match your crystal's palette.
            Color drawColor = new Color(255, 140, 255, 180);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                sourceRect,
                drawColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0);

            return false;
        }
    }
}
