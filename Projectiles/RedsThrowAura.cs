using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RedsThrowAura : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NebulaArcanum;

        private float auraRotation = 0f;
        private const int TargetHitboxSize = 100;

        private int pulseTimer = 0;
        private float pulseScale = 0f;
        private float pulseAlpha = 0f;

        private float hitExpandScale = 0f;

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
            // Debuff OnFire3 (Hellfire)
            target.AddBuff(BuffID.OnFire3, 180);
            TriggerHitExpand();
        }

        public void TriggerHitExpand()
        {
            // Tambah skala secara bertahap (di-clamp) supaya tidak kaget/kedat-kedut saat hit cepat
            hitExpandScale = MathHelper.Clamp(hitExpandScale + 0.12f, 0f, 0.25f); 
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
            if (!parent.active || parent.type != ProjectileID.RedsYoyo)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = parent.Center;
            Projectile.timeLeft = 30;

            auraRotation += 0.04f;

            // Transisi penyusutan skala dibuat sangat halus (Lerp) agar mulus
            hitExpandScale = MathHelper.Lerp(hitExpandScale, 0f, 0.08f);

            pulseTimer++;
            if (pulseTimer >= 40)
            {
                pulseTimer = 0;
                pulseScale = 0.2f;
                pulseAlpha = 1f;
            }

            if (pulseAlpha > 0f)
            {
                pulseScale += 0.05f;
                pulseAlpha -= 0.05f;
            }

            if (Main.rand.NextBool(2))
            {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f);
                Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame, Vector2.Zero, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // 1. Texture Belakang: NebulaArcanum
            Texture2D nebulaTex = TextureAssets.Projectile[ProjectileID.NebulaArcanum].Value;
            Vector2 nebulaOrigin = nebulaTex.Size() / 2f;

            // 2. Texture Depan: DD2DarkMageRaise
            Texture2D darkMageTex = TextureAssets.Projectile[ProjectileID.DD2DarkMageRaise].Value;
            Vector2 darkMageOrigin = darkMageTex.Size() / 2f;

            // 3. Texture Glow Pulse
            Texture2D glowTex = TextureAssets.Extra[89].Value;
            Vector2 glowOrigin = glowTex.Size() / 2f;

            Vector2 drawCenter = Projectile.Center - Main.screenPosition;

            float baseNebulaScale = (float)TargetHitboxSize / nebulaTex.Width;
            float idleBreathing = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.08f;

            // ==========================================
            // 1. LAPISAN BELAKANG (NEBULA ARCANUM - EMAS TERANG)
            // ==========================================
            Color goldAuraColor = new Color(255, 200, 30, 0) * 0.90f;
            float outerNebulaScale = (baseNebulaScale * 1.15f) + idleBreathing + hitExpandScale;

            Main.EntitySpriteDraw(
                nebulaTex, drawCenter, null, goldAuraColor,
                auraRotation, nebulaOrigin, outerNebulaScale,
                SpriteEffects.None, 0
            );

            // ==========================================
            // 2. GELOMBANG GLOW PULSE EMAS
            // ==========================================
            if (pulseAlpha > 0f)
            {
                Color pulseColor = new Color(255, 200, 30, 0) * (pulseAlpha * 0.75f);
                float waveScale = (TargetHitboxSize / (float)glowTex.Width) * pulseScale * 1.8f;

                Main.EntitySpriteDraw(
                    glowTex, drawCenter, null, pulseColor,
                    auraRotation, glowOrigin, waveScale,
                    SpriteEffects.None, 0
                );
            }

            // ==========================================
            // 3. LAPISAN DEPAN (DD2DarkMageRaise - DIPUTAR KE SAMPING <>)
            // ==========================================
            Color blackCoreColor = new Color(15, 15, 15, 240);
            float innerDarkMageScale = ((float)TargetHitboxSize / darkMageTex.Width) * 0.55f;

            // MathHelper.PiOver2 (90 DERAJAT) MEMUTAR SPRITE AGAR MENGARAH KE SAMPING (<>)
            Main.EntitySpriteDraw(
                darkMageTex, drawCenter, null, blackCoreColor,
                MathHelper.PiOver2, darkMageOrigin, innerDarkMageScale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}