using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.Audio; 

namespace TheSanity.Projectiles
{
    public class TomeOfEclipsaHeld : ModProjectile
    {
        public override string Texture => "TheSanity/Items/TomeOfEclipsa_Arms";
        private int maxOrbitProjectiles = 12;
        private int stopTimer = 0;

        public override void SetStaticDefaults() => Main.projFrames[Type] = 8;

        public override void SetDefaults()
        {
            Projectile.width = 28; Projectile.height = 20;
            Projectile.friendly = false; Projectile.tileCollide = false;
            Projectile.penetrate = -1; Projectile.timeLeft = 2;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player.dead || !player.active) { Projectile.Kill(); return; }

            Vector2 dir = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.Zero);
            Projectile.spriteDirection = player.direction;
            Projectile.rotation = dir.ToRotation();
            if (Projectile.spriteDirection == -1) Projectile.rotation += MathHelper.Pi;

            if (!player.channel) 
            {
                stopTimer++;
                UpdateAnimation(true); 
                if (stopTimer >= 5) { ReleaseOrbitProjectiles(player); Projectile.Kill(); return; }
            }
            else
            {
                stopTimer = 0;
                player.itemAnimation = player.itemAnimationMax;
                int orbitCount = CountOrbits(player);
                UpdateAnimation(orbitCount < maxOrbitProjectiles);

                if (player.whoAmI == Main.myPlayer && Main.GameUpdateCount % 20 == 0 && orbitCount < maxOrbitProjectiles)
                {
                    SoundEngine.PlaySound(new SoundStyle("TheSanity/Sounds/Custom/Orbit_Spawn"), player.Center);
                    
                    float startRotation = (MathHelper.TwoPi / maxOrbitProjectiles) * orbitCount;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), player.Center, Vector2.Zero, 
                        ModContent.ProjectileType<EclipsaOrbitProjectile>(), Projectile.damage / 2, 2f, player.whoAmI, 
                        startRotation, 1f);
                }
            }

            Projectile.timeLeft = 2;
            player.heldProj = Projectile.whoAmI;
            Projectile.Center = player.MountedCenter + dir * 12f;
            player.itemRotation = Projectile.rotation;
            player.ChangeDir(dir.X > 0 ? 1 : -1);
        }

        private void UpdateAnimation(bool shouldAnimate)
        {
            if (shouldAnimate) { if (++Projectile.frameCounter >= 5) { Projectile.frameCounter = 0; Projectile.frame = (Projectile.frame + 1) % 8; } }
            else { Projectile.frame = 0; Projectile.frameCounter = 0; }
        }

        private int CountOrbits(Player player)
        {
            int count = 0;
            foreach (Projectile p in Main.projectile) 
                if (p.active && p.owner == player.whoAmI && p.type == ModContent.ProjectileType<EclipsaOrbitProjectile>()) count++;
            return count;
        }

        private void ReleaseOrbitProjectiles(Player player)
        {
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.type == ModContent.ProjectileType<EclipsaOrbitProjectile>())
                {
                    if (proj.ModProjectile is EclipsaOrbitProjectile orbit) orbit.FireAsMainProjectile();
                }
            }
        }

        public override void PostDraw(Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(14f, 10f);
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * 20, 28, 20);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            
            for (float i = 0; i < 1f; i += 0.3f)
            {
                Main.EntitySpriteDraw(texture, drawPos, sourceRect, Color.Purple * 0.4f, Projectile.rotation, origin, Projectile.scale + (i * 0.1f), effects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}