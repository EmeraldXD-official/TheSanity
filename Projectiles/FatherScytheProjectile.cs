using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio; // 🌟 Diperlukan untuk memutar sistem SoundEngine

namespace TheSanity.Projectiles
{
    public class FatherScytheProjectile : ModProjectile
    {
        private Vector2 spawnPosition;
        private bool hasStopped = false;
        private int baseDamage;
        private int spinDirection = 1; 

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;     
        }

        public override void SetDefaults() {
            Projectile.width = 106;
            Projectile.height = 106;
            
            Projectile.friendly = true; 
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;      
            Projectile.tileCollide = false; 
            Projectile.timeLeft = 1200;     
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                spawnPosition = Projectile.position;
                baseDamage = Projectile.damage;
                spinDirection = Projectile.velocity.X >= 0 ? 1 : -1;
                Projectile.scale = 0.2f; 
                Projectile.localAI[0] = 1;
            }

            if (!hasStopped) {
                float distanceTraveled = Vector2.Distance(spawnPosition, Projectile.position);
                float progress = distanceTraveled / 400f; 
                if (progress > 1f) progress = 1f;

                Projectile.scale = MathHelper.Lerp(0.2f, 1.0f, progress);
            }
            else {
                Projectile.scale = 1.0f;
            }

            Projectile.rotation += 0.6f * spinDirection; 

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type]) {
                    Projectile.frame = 0;
                }
            }

            Projectile.ai[0]++;
            float damageMultiplier = 1f + (Projectile.ai[0] * 0.005f);
            Projectile.damage = (int)(baseDamage * damageMultiplier);

            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 120, default, 1.4f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!hasStopped) {
                Projectile.velocity = Vector2.Zero; 
                hasStopped = true;
                Projectile.timeLeft = 180; 

                // 🔊 AUDIO: Memutar kombinasi suara NPCHit10 dan NPCDeath9 saat sabit utama sukses meng-hit/menancap musuh
                SoundEngine.PlaySound(SoundID.NPCHit10, Projectile.position);
                SoundEngine.PlaySound(SoundID.NPCDeath9, Projectile.position);
            }
        }

        public override void OnKill(int timeLeft) {
            // 🔊 AUDIO: Memutar suara Item89 tepat saat durasi habis dan proyektil utama pecah
            SoundEngine.PlaySound(SoundID.Item89, Projectile.position);

            int mainTargetIndex = -1;
            float closestDistance = 1200f; 

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(Projectile)) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        mainTargetIndex = i; 
                    }
                }
            }

            float baseAngle = Main.rand.NextBool() ? 0f : MathHelper.PiOver4;

            for (int i = 0; i < 4; i++) {
                float angle = baseAngle + (MathHelper.PiOver2 * i); 
                Vector2 launchVelocity = angle.ToRotationVector2() * 9.5f; 

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    launchVelocity,
                    ModContent.ProjectileType<FatherScytheMiniProjectile>(),
                    (int)(Projectile.damage * 0.55f), 
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    mainTargetIndex 
                );
            }

            for (int i = 0; i < 15; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 1.6f);
                Main.dust[dust].velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle sourceRectangle = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = sourceRectangle.Size() / 2f;

            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float alphaMultiplier = 1f - ((float)i / Projectile.oldPos.Length);
                Color shadowColor = Color.Red * alphaMultiplier * 0.4f; 

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, shadowColor, trailRot, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            Color drawColor = Projectile.GetAlpha(lightColor);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, sourceRectangle, drawColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false; 
        }
    }
}