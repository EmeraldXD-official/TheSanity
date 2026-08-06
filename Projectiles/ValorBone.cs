using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class ValorBone : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        private const int TrailLength = 5;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI()
        {
            // Berputar kencang
            Projectile.rotation += 0.35f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.BrokenArmor, 300); // Debuff Broken Armor 5 Detik
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            // After Image
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = lightColor * (progress * 0.4f);

                Main.EntitySpriteDraw(
                    texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale,
                    SpriteEffects.None, 0
                );
            }

            // Sprite Utama
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}