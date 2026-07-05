using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace TheSanity.Projectiles
{
    public class BladeOfTheDarknessImpactProj : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/BladeOfTheDarknessImpactProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;     
        }

        public override void SetDefaults() {
            Projectile.width = 45;
            Projectile.height = 45;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;      // Bisa menembus musuh tanpa batas (Infinite Pierce)
            Projectile.timeLeft = 120;      
        }

        public override void AI() {
            // Homing dihapus total. Proyektil meluncur lurus sesuai velocity arah jatuhnya.
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.DisableCrit(); 
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 180);

            // 🎆 EFEK LEDAKAN KOBOLD EXPLOSION & PARTIKEL MENYEBAR
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion, target.Center);

            for (int i = 0; i < 20; i++) {
                Vector2 speed = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, speed, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity *= 1.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float alphaMultiplier = 1f - ((float)i / Projectile.oldPos.Length);
                Color shadowColor = Color.Black * alphaMultiplier * 0.5f;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, drawPos, null, shadowColor, trailRot, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}