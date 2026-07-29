using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.Projectiles
{
    public class HaloMinion : ModProjectile
    {
        private int shootCooldown = 0; 

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;

            // BARU: Mengaktifkan cache posisi terdahulu untuk efek bayangan (Trail) ala Terraprisma
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10; // Jumlah bayangan belakang (makin besar makin panjang)
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;     // Menyimpan posisi dan rotasi terdahulu
        }

        public override void SetDefaults() {
            Projectile.width = 32; 
            Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
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
            if (Projectile.frameCounter >= 6) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type]) {
                    Projectile.frame = 0;
                }
            }

            int minionType = Projectile.type;
            int currentSlotsUsed = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == minionType) {
                    currentSlotsUsed++;
                }
            }

            int minionPositionIndex = 0;
            for (int i = 0; i < Projectile.whoAmI; i++) {
                if (Main.projectile[i].active && Main.projectile[i].owner == Projectile.owner && Main.projectile[i].type == Projectile.type) {
                    minionPositionIndex++;
                }
            }

            NPC target = null;
            float maxDistance = 700f;

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
                float angle = (float)minionPositionIndex * (MathHelper.TwoPi / Math.Max(1, currentSlotsUsed)) + Main.GlobalTimeWrappedHourly * 1.5f;
                targetPosition = target.Center + angle.ToRotationVector2() * 140f;
                speed = 14f;
                inertia = 12f;
                Projectile.spriteDirection = (target.Center.X > Projectile.Center.X) ? 1 : -1;
            } else {
                // DIUBAH: Sekarang minion akan memutari player (Orbit) secara melingkar saat santai
                float orbitSpeed = 2.5f; // Kecepatan putaran mengelilingi player
                float orbitRadius = 65f + (currentSlotsUsed * 5f); // Jarak melingkar dari player (makin banyak minion, makin melebar dikit biar rapi)
                
                // Rumus matematika lingkaran bergerak seiring waktu (Main.GlobalTimeWrappedHourly)
                float idleAngle = (float)minionPositionIndex * (MathHelper.TwoPi / Math.Max(1, currentSlotsUsed)) + Main.GlobalTimeWrappedHourly * orbitSpeed;
                
                targetPosition = player.Center + idleAngle.ToRotationVector2() * orbitRadius;
                speed = 9f;    
                inertia = 16f; // Lebih responsif mengikuti pola lingkaran luar player
                
                // Arah hadap ditentukan dari arah pergerakan horizontal minion agar terlihat dinamis
                if (Math.Abs(Projectile.velocity.X) > 0.2f) {
                    Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;
                } else {
                    Projectile.spriteDirection = player.direction;
                }
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
            } else {
                if (Projectile.velocity.Length() > 2f) {
                    Projectile.velocity *= 0.95f;
                }
            }

            if (target != null && target.active && !target.friendly) {
                bool alreadyHasLaser = false;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == ModContent.ProjectileType<HaloSmallLaser>() && p.ai[0] == Projectile.whoAmI) {
                        alreadyHasLaser = true;
                        break;
                    }
                }

                if (!alreadyHasLaser && Main.myPlayer == Projectile.owner) {
                    Vector2 shootDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    float distanceToTargetCenter = Vector2.Distance(Projectile.Center, target.Center);
                    
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootDir * 9f, 
                        ModContent.ProjectileType<HaloSmallLaser>(), Projectile.damage, Projectile.knockBack, Projectile.owner, Projectile.whoAmI, distanceToTargetCenter);
                }
            }

            if (!Main.dayTime) {
                Lighting.AddLight(Projectile.Center, 0.5f, 0.1f, 0.3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            int frameWidth = texture.Width / 3;
            int currentFrame = Projectile.frame;
            
            Rectangle srcRect = new Rectangle(currentFrame * frameWidth, 0, frameWidth, texture.Height);
            Vector2 drawOrigin = srcRect.Size() / 2f;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            // BARU: Menggambar Bayangan / Afterimage ala Terraprisma berwarna Pink
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue; // Lewati jika posisi lama belum terekam

                // Catatan: oldPos merekam posisi Top-Left hitbox, jadi kita sesuaikan ke koordinat tengah minion
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                
                // Menghitung opasitas yang memudar (semakin lama posisinya, semakin transparan)
                float trailAlpha = 1f - ((float)i / Projectile.oldPos.Length);
                trailAlpha *= 0.55f; // Disesuaikan agar bayangannya halus dan tidak terlalu tebal menutupi minion asli

                // Modifikasi warna pink menyala (RGB: 255, 60, 170) dikalikan dengan tingkat transparansi trail
                Color trailColor = new Color(255, 60, 170) * trailAlpha;
                float oldRotation = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, oldDrawPos, srcRect, trailColor, oldRotation, drawOrigin, Projectile.scale, effects, 0);
            }

            // Minion Utama (Asli)
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color drawColor = !Main.dayTime ? Color.White : lightColor;

            Main.EntitySpriteDraw(texture, drawPos, srcRect, drawColor, Projectile.rotation, drawOrigin, Projectile.scale, effects, 0);
            return false;
        }
    }
}