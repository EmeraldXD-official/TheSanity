using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio; // 🌟 Diperlukan untuk memutar sistem SoundEngine

namespace TheSanity.Projectiles
{
    public class FatherScytheMiniProjectile : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/FatherScytheProjectile";

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 53;  
            Projectile.height = 53;
            
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;      
            Projectile.tileCollide = false; 
            
            Projectile.scale = 0.5f;        
            Projectile.aiStyle = -1;        
            Projectile.timeLeft = 600;      
        }

        public override void AI() {
            Projectile.frame = 0; 
            Projectile.rotation += 0.45f;

            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 140, default, 1.1f);
                Main.dust[dust].noGravity = true;
            }

            int targetIndex = (int)Projectile.ai[0];

            if (targetIndex < 0 || targetIndex >= Main.maxNPCs || !Main.npc[targetIndex].CanBeChasedBy(Projectile)) {
                targetIndex = -1;
                float closestDistance = 1600f; 

                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy(Projectile)) {
                        float distance = Vector2.Distance(Projectile.Center, npc.Center);
                        if (distance < closestDistance) {
                            closestDistance = distance;
                            targetIndex = i;
                        }
                    }
                }
                Projectile.ai[0] = targetIndex; 
            }

            if (targetIndex != -1) {
                NPC target = Main.npc[targetIndex];
                Vector2 trackingDirection = target.Center - Projectile.Center;
                trackingDirection.Normalize();

                float speed = 14f;      
                float inertia = 8f;     

                Projectile.velocity = (Projectile.velocity * (inertia - 1f) + trackingDirection * speed) / inertia;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int currentTargetIndex = (int)Projectile.ai[0];
            if (target.whoAmI == currentTargetIndex) {
                // 🔊 AUDIO: Memutar suara NPCHit10 saat proyektil mini berhasil mengenai musuh kunciannya
                SoundEngine.PlaySound(SoundID.NPCHit10, Projectile.position);
                
                Projectile.Kill(); 
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