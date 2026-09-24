using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.DukeFishron.Content.Projectiles
{
    // One of the 15 protective bubbles summoned by the DukeFishron Armor's second
    // set bonus. Follows the player in a loose, slowly rotating ring for 15 seconds.
    // Any hostile projectile that touches a bubble is destroyed and the bubble pops.
    public class DukeFishronBubble : ModProjectile
    {
        public override string Texture => "TheSanity/Items/ArmorBoss/DukeFishron/Assets/Projectiles/Bubble";

        private const int Lifetime = 900; // 15 seconds

        // ai[0]: this bubble's slot angle around the player (radians), assigned once on spawn
        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.alpha = 40;
        }

        public override void OnSpawn(IEntitySource source)
        {
            // ai[0] arrives pre-set to this bubble's evenly-spaced ring slot angle, assigned by
            // DukeFishronCompanion so the 15 bubbles spread out around the player instead of
            // clumping. A small jitter keeps the ring from looking too perfectly regimented.
            Projectile.ai[0] += Main.rand.NextFloat(-0.08f, 0.08f);
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.ai[0] += 0.015f;
            float radius = 90f + 10f * (float)Math.Sin(Main.GameUpdateCount * 0.05f + Projectile.whoAmI);

            // Lead the target by the player's current velocity so the ring keeps pace with
            // movement instead of visibly trailing behind before catching up.
            Vector2 anchor = owner.Center + owner.velocity * 6f;
            Vector2 target = anchor + radius * new Vector2((float)Math.Cos(Projectile.ai[0]), (float)Math.Sin(Projectile.ai[0]));
            Projectile.Center = Vector2.Lerp(Projectile.Center, target, 0.12f);
            Projectile.rotation += 0.02f;

            // Pop on contact with any hostile projectile, destroying it too.
            // NOTE: mutating Main.projectile while iterating it is fine here because
            // Projectile.Kill() only flips a flag rather than resizing the array, but
            // if you extend this, prefer collecting hits first and killing after the loop.
            foreach (Projectile other in Main.projectile)
            {
                if (other == null || !other.active || !other.hostile || other.friendly) continue;
                if (other.Hitbox.Intersects(Projectile.Hitbox))
                {
                    other.Kill();
                    Projectile.Kill();
                    break;
                }
            }
        }
    }
}