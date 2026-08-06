using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class ValkyrieAura : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NebulaArcanum;

        private float auraRotation = 0f;
        private const int TargetHitboxSize = 100;

        public override void SetDefaults()
        {
            Projectile.width = TargetHitboxSize;
            Projectile.height = TargetHitboxSize;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Memberikan Debuff SHADOWFLAME
            target.AddBuff(BuffID.ShadowFlame, 180); // 3 Detik
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[0];

            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];
            if (!parent.active || parent.type != ProjectileID.ValkyrieYoyo)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = parent.Center;
            Projectile.timeLeft = 30;

            auraRotation += 0.04f;

            if (Main.rand.NextBool(2))
            {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f);
                Dust d = Dust.NewDustPerfect(dustPos, DustID.PurpleTorch, Vector2.Zero, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.NebulaArcanum].Value;
            Vector2 origin = texture.Size() / 2f;

            Vector2 drawCenter = Projectile.Center - Main.screenPosition;
            float scale = (float)TargetHitboxSize / texture.Width;

            Color purpleAuraColor = new Color(160, 40, 255, 0) * 0.85f;

            Main.EntitySpriteDraw(
                texture, drawCenter, null, purpleAuraColor,
                auraRotation, origin, scale * 1.1f,
                SpriteEffects.None, 0
            );

            Color cyanCoreColor = new Color(50, 220, 255, 0) * 0.90f;
            Main.EntitySpriteDraw(
                texture, drawCenter, null, cyanCoreColor,
                -auraRotation * 1.5f, origin, scale * 0.6f,
                SpriteEffects.FlipHorizontally, 0
            );

            return false;
        }
    }
}