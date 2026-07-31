using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Empress.Buffs;

namespace TheSanity.Items.ArmorBoss.Empress.Projectiles
{
    // Replaces the old PrismaticBolt. Instead of a hand-rolled homing script wearing the
    // Prismatic Bolt sprite, this borrows the REAL vanilla Nightglow projectile (both its
    // texture and its AI) via CloneDefaults + AIType, then overrides it to be a friendly
    // Summon-damage bolt fired by the armor's set bonus instead of the actual Nightglow weapon.
    //
    // ai[0] is still set to the target NPC's whoAmI by EmpressPlayer when spawned aimed at
    // something specific -- Nightglow's own vanilla AI already reads/writes that slot to
    // acquire and track a target, so nothing needs to change on the spawner's side.
    public class NightglowBolt : ModProjectile
    {
        // Point at the real Nightglow projectile's texture instead of drawing our own sprite.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FairyQueenMagicItemShot;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            // Copies width/height/scale/aiStyle/etc. straight from vanilla Nightglow...
            Projectile.CloneDefaults(ProjectileID.FairyQueenMagicItemShot);
            // ...and tells the game to actually run Nightglow's vanilla AI code each tick,
            // which is what gives us its real homing behavior for free.
            AIType = ProjectileID.FairyQueenMagicItemShot;

            // Overrides on top of the cloned vanilla defaults: this version belongs to the
            // player's armor, so it needs to be friendly and use Summon damage rather than
            // the hostile Magic damage the real weapon's bolt uses.
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.timeLeft = 90; // 1.5 seconds, same lifetime PrismaticBolt used
            Projectile.light = 1f; // brighter glow than the cloned vanilla default
        }

        public override void PostAI()
        {
            // AIType runs vanilla Nightglow's own movement code for us, but the light it casts
            // by default is plain white -- push out our own colored light every frame instead,
            // matching whatever hue the sprite is tinted this frame, so the glow actually reads
            // as "prismatic" rather than a flat white bolt.
            float hue = (Main.GlobalTimeWrappedHourly * 0.8f + Projectile.whoAmI * 0.1f) % 1f;
            Color glowColor = Main.hslToRgb(hue, 1f, 0.65f);
            Lighting.AddLight(Projectile.Center, glowColor.ToVector3() * Projectile.light);

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkTorch, Vector2.Zero, 0, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.1f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // The real Nightglow weapon's bolt gets its rainbow look from a dye-style shader
            // that doesn't carry over automatically onto a modded projectile ID (the same
            // limitation that applies to reused vanilla textures elsewhere in this mod), so we
            // approximate it here with the same cycling-hue tint the rest of the set uses.
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            float hue = (Main.GlobalTimeWrappedHourly * 0.8f + Projectile.whoAmI * 0.1f) % 1f;
            Color glowColor = Main.hslToRgb(hue, 1f, 0.65f) * Projectile.Opacity;

            // Afterimage trail from the bolt's own recorded position history -- it didn't have
            // one before since AIType only carries over vanilla's movement, not its drawing.
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                Vector2 trailDrawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - (i + 1f) / (Projectile.oldPos.Length + 1f);
                Color trailColor = glowColor * fade * 0.6f;

                Main.EntitySpriteDraw(
                    texture,
                    trailDrawPos,
                    null,
                    trailColor,
                    Projectile.rotation,
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glowColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // New behavior: enemies hit by this armor's Nightglow bolts now also pick up
            // the same debuff as the Ethereal Lance, so the whole set's projectiles feel connected.
            target.AddBuff(ModContent.BuffType<LightInYourSoulBuff>(), 180); // 3 seconds
        }
    }
}