using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class DeathBeamProjectile : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/WhiteBeam";

        // ==================================================================================
        // 🛠️ PANDUAN BALANCING KUSTOM - DURASI & JANGKAUAN ATTACK (1 DETIK = 60 TICK)
        // ==================================================================================
        public const float MaxLaserLength = 2000f;   // Jangkauan maksimum panjang laser
        public const float TelegraphDuration = 60f; // Durasi bidikan awal (1 detik)
        public const float LaserDuration = 300f;    // Durasi aktif laser berputar (5 detik)
        public const float TotalDuration = TelegraphDuration + LaserDuration; // Total: 360f (6 detik)
        // ==================================================================================

        public float CurrentLaserLength {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        public float Timer {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        public float InitialAngle {
            get => Projectile.localAI[1];
            set => Projectile.localAI[1] = value;
        }

        public float RotationDirection {
            get => Projectile.ai[2];
            set => Projectile.ai[2] = value;
        }

        public override void SetDefaults()
        {
            Projectile.width = 26; 
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false; 
        }

        public override void AI()
        {
            Timer++;
            if (Timer >= TotalDuration)
            {
                Projectile.Kill();
                return;
            }

            if (Timer == 1)
            {
                InitialAngle = Projectile.rotation;
                
                if (RotationDirection == 0f)
                {
                    RotationDirection = Main.rand.NextBool() ? 1f : -1f;
                }
            }

            float telegraphScale = 0.15f; 
            float maxLaserScale = 1.0f;    
            
            if (Timer < TelegraphDuration)
            {
                Projectile.scale = telegraphScale;
                Projectile.rotation = InitialAngle;
            }
            else
            {
                float laserTimer = Timer - TelegraphDuration;
                float growDuration = 25f;   
                float shrinkDuration = 25f; 
                
                if (laserTimer < growDuration)
                {
                    float progressScale = laserTimer / growDuration;
                    Projectile.scale = MathHelper.Lerp(telegraphScale, maxLaserScale, progressScale);
                }
                else if (Timer > TotalDuration - shrinkDuration)
                {
                    float progressScale = (TotalDuration - Timer) / shrinkDuration;
                    Projectile.scale = MathHelper.Lerp(0f, maxLaserScale, progressScale);
                }
                else
                {
                    Projectile.scale = maxLaserScale;
                }

                // ==================================================================================
                // 🔄 SAKLAR PERGERAKAN BARU: Memulai dari kecepatan awal yang lambat, lalu berakselerasi
                // ==================================================================================
                float laserProgress = laserTimer / LaserDuration; // Berjalan dari 0f ke 1f
                
                // Menggunakan kombinasi (Pi * t) + (Pi * t^2). 
                // Saat t = 0 (awal), nilai turunan kecepatannya sama dengan formula awal kodemu (Pi).
                // Saat t = 1 (akhir), total rotasi yang dicapai pas 2 * Pi (360 derajat penuh).
                float totalRotationAngle = (MathHelper.Pi * laserProgress) + (MathHelper.Pi * laserProgress * laserProgress);
                
                Projectile.rotation = InitialAngle + (totalRotationAngle * RotationDirection);
            }

            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs)
            {
                NPC owner = Main.npc[ownerIndex];
                if (owner.active)
                {
                    Vector2 offset = (owner.type == NPCID.GolemHeadFree) ? new Vector2(0, -12) : Vector2.Zero;
                    Projectile.Center = owner.Center + offset;
                }
            }

            CurrentLaserLength = MaxLaserLength;
        }

        public override bool CanHitPlayer(Player target)
        {
            if (Timer < TelegraphDuration) return false;
            if (Timer > TotalDuration - 10f) return false; 
            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();

            Rectangle middleSlice = new Rectangle(texture.Width / 2, 0, 1, texture.Height);
            Vector2 drawOrigin = new Vector2(0, texture.Height / 2f);

            float stepLength = 8f; 
            float distanceCovered = 0f;

            Color darkOrange = new Color(200, 60, 0);
            Color brightYellow = new Color(255, 220, 0);

            while (distanceCovered < CurrentLaserLength)
            {
                float currentStep = stepLength;
                if (distanceCovered + currentStep > CurrentLaserLength) {
                    currentStep = CurrentLaserLength - distanceCovered;
                }

                Vector2 drawPos = Projectile.Center + (beamDir * distanceCovered) - Main.screenPosition;
                Color animatedColor;

                if (Timer < TelegraphDuration)
                {
                    animatedColor = new Color(255, 130, 0) * 0.7f; 
                }
                else
                {
                    float wavePhase = (Main.GlobalTimeWrappedHourly * 15f) - (distanceCovered * 0.02f);
                    float colorLerpFactor = (float)(Math.Sin(wavePhase) + 1f) / 2f;
                    animatedColor = Color.Lerp(darkOrange, brightYellow, colorLerpFactor);
                }

                animatedColor *= Projectile.Opacity;
                Vector2 segmentScale = new Vector2(currentStep / middleSlice.Width, Projectile.scale);

                if (Timer >= TelegraphDuration)
                {
                    float mouthFade = MathHelper.Clamp((distanceCovered - 40f) / 60f, 0f, 1f);

                    if (mouthFade > 0f)
                    {
                        Vector2 shadowScale = new Vector2(segmentScale.X, segmentScale.Y * 1.6f);
                        Color outlineColor = animatedColor * 0.3f * mouthFade;

                        Main.EntitySpriteDraw(texture, drawPos, middleSlice, outlineColor, Projectile.rotation, drawOrigin, shadowScale, SpriteEffects.None, 0);
                    }
                }

                Main.EntitySpriteDraw(texture, drawPos, middleSlice, animatedColor, Projectile.rotation, drawOrigin, segmentScale, SpriteEffects.None, 0);

                distanceCovered += currentStep;
            }

            return false; 
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Timer < TelegraphDuration) return false;

            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            float samplePoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + (beamDir * CurrentLaserLength), Projectile.width, ref samplePoint);
        }
    }
}