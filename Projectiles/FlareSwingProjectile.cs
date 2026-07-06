using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.GameContent;
using Terraria.ID;
using TheSanity.Players; 

namespace TheSanity.Projectiles
{
    public class FlareSwingProjectile : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FlareSword"; 

        private float[] oldRotations = new float[90]; 
        private Vector2[] oldPositions = new Vector2[90];
        private NPC targetToOmnislash = null;
        private bool isSlashingPhase = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 90;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2; 
        }

        public override void SetDefaults() {
            Projectile.width = 119;
            Projectile.height = 119;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true; 
            Projectile.aiStyle = -1; 
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = player.HeldItem.useTime; 
                Projectile.timeLeft = (int)Projectile.localAI[0] + 30; 
            }

            if (Projectile.timeLeft > 30) {
                if (Projectile.localAI[1] == 0f) {
                    Projectile.spriteDirection = player.direction;
                    Projectile.localAI[1] = 1f;
                }
                player.direction = Projectile.spriteDirection;
                
                Projectile.Center = player.MountedCenter;
                player.heldProj = Projectile.whoAmI;
                player.itemTime = 2;
                player.itemAnimation = 2;

                float maxDuration = Projectile.localAI[0];
                float activeTimeLeft = Projectile.timeLeft - 30;
                float progress = (maxDuration - activeTimeLeft) / maxDuration;

                float swingProgress = -MathF.Cos(progress * MathHelper.Pi) * 1.4f;
                isSlashingPhase = (progress > 0.05f && progress < 0.95f);

                float baseRotation = swingProgress * (MathHelper.Pi * 0.6f);
                if (player.direction == 1) {
                    Projectile.rotation = baseRotation + MathHelper.PiOver4;
                } else {
                    Projectile.rotation = -MathHelper.PiOver4 - baseRotation;
                }

                Vector2 bladeDirection = player.direction == 1 
                    ? (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2() 
                    : (Projectile.rotation + MathHelper.Pi + MathHelper.PiOver4).ToRotationVector2();
                
                if (isSlashingPhase && Main.rand.NextBool(4)) {
                    int dustType = Main.dayTime ? DustID.SolarFlare : DustID.Electric; 
                    float length = Main.rand.NextFloat(50f, 119f * 0.85f);
                    Vector2 dustPos = Projectile.Center + bladeDirection * length;
                    
                    Dust d = Dust.NewDustPerfect(dustPos, dustType, bladeDirection.RotatedBy(-MathHelper.PiOver2 * player.direction) * 1.5f, 100, default, 0.8f);
                    d.noGravity = true;
                }

                float armRotation = bladeDirection.ToRotation() - MathHelper.PiOver2;
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

                if (Projectile.timeLeft == 31 && targetToOmnislash != null && targetToOmnislash.active) {
                    FlarePlayer modPlayer = player.GetModPlayer<FlarePlayer>();
                    modPlayer.StartOmnislash(targetToOmnislash);
                }

                RecordTrail();
            }
            else {
                isSlashingPhase = false;
                Projectile.friendly = false;
            }
        }

        public override bool? CanDamage() => isSlashingPhase && Projectile.timeLeft > 30;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!isSlashingPhase || Projectile.timeLeft <= 30) return false;

            Player player = Main.player[Projectile.owner];
            Vector2 bladeDirection = player.direction == 1 
                ? (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2() 
                : (Projectile.rotation + MathHelper.Pi + MathHelper.PiOver4).ToRotationVector2();
            
            Vector2 bladeEnd = Projectile.Center + bladeDirection * (119f * 0.85f * Projectile.scale);
            float collisionPoint = 0f;

            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, bladeEnd, 24f * Projectile.scale, ref collisionPoint)) {
                return true;
            }
            return false;
        }

        private void RecordTrail() {
            for (int i = oldRotations.Length - 1; i > 0; i--) {
                oldRotations[i] = oldRotations[i - 1];
                oldPositions[i] = oldPositions[i - 1];
            }
            oldRotations[0] = Projectile.rotation;
            oldPositions[0] = Projectile.Center;
        }

        // ==================== UPDATE: EFEK HIT DUST & ELECTRIFIED DEBUFF ====================
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // Logika Omnislash bawaan malam hari tetap berjalan normal
            if (!Main.dayTime) { 
                targetToOmnislash = target;
            }

            // 1. INFLICT DEBUFF ELECTRIFIED
            // Memberikan debuff Electrified selama 5 detik (300 ticks) ke musuh yang terkena tebasan
            target.AddBuff(BuffID.Electrified, 300);

            // 2. ADAPTIVE HIT DUST BURST
            // Menyesuaikan tipe partikel ledakan: Siang = SolarFlare (Api Emas), Malam = Electric (Petir Biru)
            int hitDustType = Main.dayTime ? DustID.SolarFlare : DustID.Electric;

            // Memunculkan 10 partikel mencuat keluar secara acak saat musuh tertebas
            for (int i = 0; i < 10; i++) {
                int d = Dust.NewDust(target.position, target.width, target.height, hitDustType);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 2.8f; // Kecepatan muncratan partikel biar kerasa impact-nya
                Main.dust[d].scale = Main.rand.NextFloat(1f, 1.5f); // Ukuran partikel bervariasi
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            int frameWidth = 119;
            int frameHeight = 119;
            int frameY = Main.dayTime ? 119 : 0; 
            
            Rectangle sourceRect = new Rectangle(0, frameY, frameWidth, frameHeight);
            Rectangle trailSourceRect = new Rectangle(0, frameY, frameWidth, (int)(frameHeight * 0.85f));

            Vector2 origin = new Vector2(Projectile.spriteDirection == 1 ? 0 : frameWidth, frameHeight);
            SpriteEffects spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            float overallAlpha = 1f;
            if (Projectile.timeLeft <= 30) {
                overallAlpha = (float)Projectile.timeLeft / 30f;
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            int totalGhosts = 70; 
            for (int i = 1; i <= totalGhosts; i++) {
                float fractionalIndex = ((float)i / totalGhosts) * (oldPositions.Length - 1);
                
                int indexFloor = (int)Math.Floor(fractionalIndex);
                int indexCeil = (int)Math.Ceiling(fractionalIndex);
                
                if (indexCeil >= oldPositions.Length) indexCeil = oldPositions.Length - 1;
                if (oldPositions[indexFloor] == Vector2.Zero || oldPositions[indexCeil] == Vector2.Zero) continue;

                float lerpFactor = fractionalIndex - indexFloor;
                float smoothFactor = MathHelper.SmoothStep(0f, 1f, lerpFactor);

                Vector2 drawPos = Vector2.Lerp(oldPositions[indexFloor], oldPositions[indexCeil], smoothFactor);
                float drawRot = Utils.AngleLerp(oldRotations[indexFloor], oldRotations[indexCeil], smoothFactor);

                float progressFactor = (float)i / totalGhosts;
                float alphaMultiplier = progressFactor < 0.5f ? 0.95f : (1.0f - progressFactor) * 1.5f;
                float alpha = MathHelper.Clamp(alphaMultiplier * overallAlpha, 0f, 1f);

                Color trailColor = Main.dayTime 
                    ? Color.Lerp(new Color(255, 255, 220), new Color(255, 50, 0), progressFactor) 
                    : Color.Lerp(new Color(0, 255, 255), new Color(140, 0, 255), progressFactor); 

                float scaleMultiplier = 1f - (progressFactor * 0.05f);

                Main.spriteBatch.Draw(
                    texture, 
                    drawPos - Main.screenPosition, 
                    trailSourceRect, 
                    trailColor * alpha, 
                    drawRot, 
                    origin, 
                    Projectile.scale * 0.74f * scaleMultiplier, 
                    spriteEffects, 
                    0f
                );
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            if (Projectile.timeLeft > 30) {
                Main.spriteBatch.Draw(
                    texture, 
                    Projectile.Center - Main.screenPosition, 
                    sourceRect, 
                    Color.White, 
                    Projectile.rotation, 
                    origin, 
                    Projectile.scale * 0.7f, 
                    spriteEffects, 
                    0f
                );
            }

            return false; 
        }
    }
}