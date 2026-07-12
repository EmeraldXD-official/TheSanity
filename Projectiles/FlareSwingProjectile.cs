using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.GameContent;
using Terraria.ID;
using TheSanity.Players; 
using Luminance.Assets;             // LazyAsset<T> - lazy-loaded texture wrapper
using Luminance.Common.Utilities;   // Utilities.UseBlendState() - swap blend state tanpa manual End()/Begin()
using Luminance.Common.Easings;     // EasingCurves & PiecewiseCurve - dipakai untuk kurva overdrive flash

namespace TheSanity.Projectiles
{
    public class FlareSwingProjectile : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FlareSword"; 

        // Glow mask sengaja disamakan pola dengan sprite utama (sama layout frame day/night).
        // LazyAsset dari Luminance otomatis handle lazy loading tanpa manual caching/IsLoaded check.
        private static readonly LazyAsset<Texture2D> GlowTexture = LazyAsset<Texture2D>.Request("TheSanity/Items/FlareSword_Glow");

        // Kurva "overdrive" untuk flash glow di puncak swing: naik cepat (Quartic Out) dari 0->1
        // di paruh pertama swing (0 - 0.5), lalu jatuh cepat (Quartic In) dari 1->0 di paruh kedua (0.5 - 1).
        // Hasilnya: spike tajam persis di apex tebasan, bukan landai.
        private static readonly PiecewiseCurve SwingOverdriveCurve = new PiecewiseCurve()
            .Add(EasingCurves.Quartic, EasingType.Out, 1f, 0.5f)
            .Add(EasingCurves.Quartic, EasingType.In, 0f, 1f);

        private float[] oldRotations = new float[90]; 
        private Vector2[] oldPositions = new Vector2[90];
        private NPC targetToOmnislash = null;
        private bool isSlashingPhase = false;

        // Progress swing (0..1) disimpan sebagai field supaya bisa diakses dari PreDraw
        // untuk menghitung "overdrive flash" tepat di puncak tebasan.
        private float currentSwingProgress = 0f;

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
                currentSwingProgress = progress; // simpan untuk dipakai di PreDraw (overdrive glow)

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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Main.dayTime) { 
                targetToOmnislash = target;
            }

            // INFLICT DEBUFF ELECTRIFIED
            target.AddBuff(BuffID.Electrified, 300);

            // ADAPTIVE HIT DUST BURST
            int hitDustType = Main.dayTime ? DustID.SolarFlare : DustID.Electric;

            for (int i = 0; i < 10; i++) {
                int d = Dust.NewDust(target.position, target.width, target.height, hitDustType);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 2.8f; 
                Main.dust[d].scale = Main.rand.NextFloat(1f, 1.5f); 
            }
        }

        // ==================== LUMINANCE: WARNA GLOW DINAMIS (DAY/NIGHT) ====================
        private Color GetGlowColor() {
            return Main.dayTime
                ? new Color(255, 190, 70)   // Golden/Orange siang
                : new Color(110, 210, 255); // Cyan-ish malam (di-blend ke ungu via overdrive/flash lerp)
        }

        // ==================== LUMINANCE: OVERDRIVE FLASH DI APEX SWING ====================
        // Mengevaluasi SwingOverdriveCurve (Luminance PiecewiseCurve) di progress swing saat ini.
        // Hasilnya 0 -> 1 -> 0, memuncak tajam persis di apex (progress = 0.5), mensimulasikan
        // "flash" tenaga penuh pas blade ada di titik tebasan paling kuat.
        private float GetSwingOverdrive() {
            if (!isSlashingPhase) return 0f;
            return SwingOverdriveCurve.Evaluate(currentSwingProgress);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Texture2D glowTexture = GlowTexture.Value;
            
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

            // Ganti ke Additive lewat Luminance Utilities (bungkus End()+Begin() yang aman & konsisten)
            Main.spriteBatch.UseBlendState(BlendState.Additive);

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

            // Balik ke AlphaBlend untuk sprite dasar
            Main.spriteBatch.UseBlendState(BlendState.AlphaBlend);

            if (Projectile.timeLeft > 30) {
                // LAYER 1: Base blade sprite, normal AlphaBlend
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

                // LAYER 2: Glow mask, Additive - menyala di celah/inti blade.
                // Intensitasnya "overdrive" tepat di apex swing (via SwingOverdriveCurve) untuk
                // kesan hantaman penuh tenaga, lalu balik ke glow ambient begitu swing selesai.
                float overdrive = GetSwingOverdrive();
                Color glowColor = Color.Lerp(GetGlowColor(), Color.White, overdrive * 0.85f);

                float glowAlpha = MathHelper.Clamp(0.5f + overdrive * 1.1f, 0f, 1.6f) * overallAlpha;
                float glowScale = Projectile.scale * 0.7f * (1f + overdrive * 0.3f); // sedikit "membesar" saat flash

                Main.spriteBatch.UseBlendState(BlendState.Additive);

                Main.spriteBatch.Draw(
                    glowTexture,
                    Projectile.Center - Main.screenPosition,
                    sourceRect,
                    glowColor * glowAlpha,
                    Projectile.rotation,
                    origin,
                    glowScale,
                    spriteEffects,
                    0f
                );

                // Kembalikan ke AlphaBlend supaya state akhir spriteBatch konsisten untuk draw call berikutnya
                Main.spriteBatch.UseBlendState(BlendState.AlphaBlend);
            }

            return false; 
        }
    }
}