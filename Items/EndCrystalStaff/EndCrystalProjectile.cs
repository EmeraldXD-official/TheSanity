using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

// TODO: change "YourModName" to your actual mod's root namespace / folder structure.
namespace TheSanity.Items.EndCrystalStaff
{
    // The .png for this MUST be named "EndCrystalProjectile.png" and sit right next to this .cs file.
    // Now using the pink/white crystal cube sprite. It's a single static image (no frames) -
    // the spin is done entirely via Projectile.rotation at a fixed speed (no randomness = no jiggle),
    // plus a fading pink/white afterimage trail drawn behind it.
    public class EndCrystalProjectile : ModProjectile
    {
        // How many after-image copies trail behind the crystal. Higher = longer trail.
        private const int TrailLength = 8;

        public override void SetStaticDefaults()
        {
            // This is the correct API for sizing Projectile.oldPos[] - "oldPosLength" isn't a
            // real member on Projectile, which is what CS1061 was complaining about.
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 36;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;      // dies after hitting 1 enemy
            Projectile.timeLeft = 300;     // despawns after 5s if it hits nothing
            Projectile.tileCollide = true; // dies on hitting tiles
            Projectile.ignoreWater = true;
            Projectile.light = 0.4f;       // faint glow while flying
            Projectile.extraUpdates = 0;
            Projectile.scale = 0.55f;      // sprite is fairly large (116x138) - scaled down to a bolt size
        }

        public override void AI()
        {
            // Fixed, constant spin speed - deliberately NOT randomized so it reads as a
            // smooth clean rotation instead of a jittery/jiggly one.
            Projectile.rotation += 0.25f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            // Pink/white after-image trail, fading out toward the tail.
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length; // 0 = oldest, ~1 = newest
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(Color.White, new Color(255, 110, 225), 0.5f)
                    * (progress * 0.6f);

                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.rotation,
                    origin, Projectile.scale, SpriteEffects.None, 0);
            }

            // The crystal itself, drawn last (on top of the trail).
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Explosion visual only happens on an actual enemy hit, per your spec.
            Projectile.NewProjectile(
                Projectile.GetSource_OnHit(target),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<EndCrystalExplosion>(),
                0, 0f, Projectile.owner);
        }
    }
}