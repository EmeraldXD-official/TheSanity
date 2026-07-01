using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HaloSmallLaser : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/WhiteBeam";

        public float Timer {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        public float DynamicLaserLength = 0f;
        public const float MaxTrackingRange = 900f; 

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 2; // Mati otomatis jika tidak di-refresh oleh pemiliknya

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20; // Hit damage konstan per detik (~3 hit per detik)
        }

        public override void AI() {
            Timer++;
            
            // 1. Cek validasi Minion pemilik
            int minionOwnerId = (int)Projectile.ai[0];
            Projectile minion = null;

            if (minionOwnerId >= 0 && minionOwnerId < Main.maxProjectiles) {
                Projectile p = Main.projectile[minionOwnerId];
                if (p.active && p.type == ModContent.ProjectileType<HaloMinion>()) {
                    minion = p;
                    Projectile.Center = minion.Center; // Ikat pangkal laser di minion
                }
            }

            if (minion == null) {
                Projectile.Kill();
                return;
            }

            // 2. Cari target musuh terdekat
            NPC target = null;
            float closestDistance = MaxTrackingRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.lifeMax > 5 && !npc.dontTakeDamage) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        target = npc;
                    }
                }
            }

            // 3. Jika target mati/hilang, matikan laser seketika
            if (target == null || !target.active) {
                Projectile.Kill();
                return;
            }

            // 4. Update arah dan panjang dinamis mengikuti target musuh
            Vector2 targetDirection = target.Center - Projectile.Center;
            Projectile.velocity = targetDirection.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = Projectile.velocity.ToRotation();

            DynamicLaserLength = targetDirection.Length();

            // Refresh umur laser selama target masih valid
            Projectile.timeLeft = 10; 
            Projectile.scale = 0.6f; 
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();

            Rectangle middleSlice = new Rectangle(texture.Width / 2, 0, 1, texture.Height);
            Vector2 drawOrigin = new Vector2(0, texture.Height / 2f);

            float stepLength = 4f;
            float distanceCovered = 0f;

            Color glowPink = new Color(255, 30, 145) * 0.65f;
            Color coreWhite = Color.White * 0.9f;

            while (distanceCovered < DynamicLaserLength) {
                float currentStep = stepLength;
                if (distanceCovered + currentStep > DynamicLaserLength) {
                    currentStep = DynamicLaserLength - distanceCovered;
                }

                Vector2 drawPos = Projectile.Center + (beamDir * distanceCovered) - Main.screenPosition;

                Vector2 glowScale = new Vector2(currentStep / middleSlice.Width, Projectile.scale * 1.5f);
                Main.EntitySpriteDraw(texture, drawPos, middleSlice, glowPink * Projectile.scale, Projectile.rotation, drawOrigin, glowScale, SpriteEffects.None, 0);

                Vector2 coreScale = new Vector2(currentStep / middleSlice.Width, Projectile.scale * 0.4f);
                Main.EntitySpriteDraw(texture, drawPos, middleSlice, coreWhite * Projectile.scale, Projectile.rotation, drawOrigin, coreScale, SpriteEffects.None, 0);

                distanceCovered += currentStep;
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            float samplePoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + (beamDir * DynamicLaserLength), Projectile.width * Projectile.scale, ref samplePoint);
        }
    }
}