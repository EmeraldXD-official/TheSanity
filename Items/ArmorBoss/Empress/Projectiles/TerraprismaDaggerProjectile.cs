using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Empress.Buffs;

namespace TheSanity.Items.ArmorBoss.Empress.Projectiles
{
    // Thrown-dagger version of the Terraprisma blade. Flies straight, spins in the air, and
    // pierces a few enemies before fading out. No Texture override here on purpose -- tModLoader
    // auto-resolves the sprite from this class's namespace + name, so just drop
    // TerraprismaDaggerProjectile.png next to this file (Content/Projectiles/) and it's picked up.
    public class TerraprismaDaggerProjectile : ModProjectile
    {
        private const float SpinSpeed = 0.3f;

        public override void SetDefaults()
        {
            Projectile.width = 18; // was 28, matches TerraprismaDaggerStrike's new smaller size
            Projectile.height = 18;
            Projectile.scale = 0.7f;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 40;
            Projectile.light = 0.5f;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            Projectile.rotation += SpinSpeed * Projectile.direction;

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkFairy, Vector2.Zero, 0, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // Same cycling-hue parameters as TerraprismaDaggerStrike, so the thrown dagger and
            // the set bonus's dash-daggers look identical, both reading as "Terraprisma".
            float hue = (Main.GlobalTimeWrappedHourly * 0.4f + Projectile.whoAmI * 0.15f) % 1f;
            Color prismaticColor = Main.hslToRgb(hue, 1f, 0.65f) * Projectile.Opacity;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                prismaticColor,
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
            target.AddBuff(ModContent.BuffType<LightInYourSoulBuff>(), 180); // 3 seconds
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkFairy, Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}