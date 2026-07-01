using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.Projectiles
{
    public class HaloMinionGede : ModProjectile
    {
        private int shootCooldown = 0;

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            Main.projFrames[Projectile.type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
        }

        public override bool? CanCutTiles() => false;

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (player.dead || !player.active) {
                Projectile.Kill();
                return;
            }

            if (player.HasBuff(ModContent.BuffType<HaloMinionBuff>())) {
                Projectile.timeLeft = 2; 
            } else {
                Projectile.Kill();
                return;
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type]) {
                    Projectile.frame = 0;
                }
            }

            NPC target = null;
            float maxDistance = 900f;

            if (player.HasMinionAttackTargetNPC) {
                NPC npc = Main.npc[player.MinionAttackTargetNPC];
                if (npc.CanBeChasedBy(Projectile) && Vector2.Distance(Projectile.Center, npc.Center) < maxDistance) {
                    target = npc;
                }
            }

            if (target == null) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy(Projectile)) {
                        float distance = Vector2.Distance(Projectile.Center, npc.Center);
                        if (distance < maxDistance) {
                            target = npc;
                            maxDistance = distance;
                        }
                    }
                }
            }

            Vector2 targetPosition;
            float speed;
            float inertia;

            if (target != null && target.active) {
                targetPosition = target.Center + new Vector2(0, -220f);
                speed = 15f;    
                inertia = 10f;   
                Projectile.spriteDirection = (target.Center.X > Projectile.Center.X) ? 1 : -1;
            } else {
                targetPosition = player.Center + new Vector2(0, -90f);
                speed = 10f;
                inertia = 15f;
                Projectile.spriteDirection = player.direction;
            }

            Vector2 toTargetPosition = targetPosition - Projectile.Center;
            float distanceToTarget = toTargetPosition.Length();

            if (distanceToTarget > 2000f) {
                Projectile.Center = player.Center;
            }

            if (distanceToTarget > 20f) {
                toTargetPosition.Normalize();
                toTargetPosition *= speed;
                Projectile.velocity = (Projectile.velocity * (inertia - 1f) + toTargetPosition) / inertia;
            }

            if (target != null && target.active && !target.friendly) {
                shootCooldown++;
                if (shootCooldown >= 180) {
                    shootCooldown = 0;
                    if (Main.myPlayer == Projectile.owner) {
                        Vector2 shootDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootDir, 
                            ModContent.ProjectileType<HaloBigLaser>(), Projectile.damage * 2, Projectile.knockBack, Projectile.owner, Projectile.whoAmI);
                    }
                }
            }

            // BARU: Memancarkan cahaya pink/magenta yang lebih besar di sekitar minion gede saat malam hari
            if (!Main.dayTime) {
                Lighting.AddLight(Projectile.Center, 0.9f, 0.15f, 0.5f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            int frameWidth = texture.Width / 6;
            int currentFrame = Projectile.frame;
            
            Rectangle srcRect = new Rectangle(currentFrame * frameWidth, 0, frameWidth, texture.Height);
            Vector2 drawOrigin = srcRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            // DIUBAH: Jika malam hari (!Main.dayTime), paksa menggunakan Color.White agar minion terlihat glowing terang
            Color drawColor = !Main.dayTime ? Color.White : lightColor;

            Main.EntitySpriteDraw(texture, drawPos, srcRect, drawColor, Projectile.rotation, drawOrigin, Projectile.scale, effects, 0);
            return false;
        }
    }
}