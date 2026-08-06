using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Globals;

namespace TheSanity.Projectiles
{
    public class EyeOfCthulhuRing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CoolWhipProj;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 65;
            Projectile.height = 65;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
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

            if (!parent.active || parent.type != ProjectileID.TheEyeOfCthulhu || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = parent.Center;
            Projectile.timeLeft = 2;

            // Rotasi SEARAH JARUM JAM
            Projectile.rotation += 0.15f;

            // GLOW MERAH PEKAT DALAM GELAP
            Lighting.AddLight(Projectile.Center, 1.2f, 0.0f, 0.0f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // INFLICT BLEEDING (5 Detik)
            target.AddBuff(BuffID.Bleeding, 300);

            // RESET LIFETIME EYE PHASE 2 KE 5 DETIK SAAT RING MENG-HIT MUSUH
            TheEyeOfCthulhuGlobalProjectile.RefreshEyeLifetime(Projectile.owner);

            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[parentIndex];
                if (parent.active && parent.type == ProjectileID.TheEyeOfCthulhu)
                {
                    TheEyeOfCthulhuGlobalProjectile.AddHitStackAndCheck(Main.player[Projectile.owner], parent, target);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // 1. AFTER-IMAGE MERAH PEKAT
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.6f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    new Color(180, 0, 0) * alpha,
                    Projectile.oldRot[i],
                    origin,
                    1.2f,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. MAIN SPRITE
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                null,
                new Color(220, 10, 10) * 0.95f,
                Projectile.rotation,
                origin,
                1.2f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}