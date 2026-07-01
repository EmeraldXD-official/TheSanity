using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RedLaserBeamProjectile : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/WhiteBeam";

        // ==================================================================================
        // 🛠️ PANDUAN BALANCING KUSTOM - DURASI, UKURAN & JANGKAUAN LASER (1 DETIK = 60 TICK)
        // ==================================================================================
        public const float MaxLaserLength = 2400f;   // Jangkauan panjang maksimum laser
        public const float TelegraphDuration = 40f; // Durasi garis bidikan awal merah (40 Ticks)
        public const float LaserDuration = 120f;     // Sesuai Request: Durasi laser aktif tepat 2 detik (120 Ticks)
        public const float TotalDuration = TelegraphDuration + LaserDuration;
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

        public override void SetDefaults()
        {
            Projectile.width = 30; 
            Projectile.height = 30;
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
            }

            float telegraphScale = 0.15f; 
            float maxLaserScale = 1.5f; // Sedikit dilebarkan agar lebih terasa efek boss rework-nya
            
            if (Timer < TelegraphDuration)
            {
                Projectile.scale = telegraphScale;
                Projectile.rotation = InitialAngle;
            }
            else
            {
                Projectile.rotation = InitialAngle;
                float laserTimer = Timer - TelegraphDuration;
                float growDuration = 15f;
                float shrinkDuration = 15f;
                
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
            }

            // ==================================================================================
            // TWEAK: LOGIKA ANTI-CULLING (Mencegah Laser Hilang saat Menjauh)
            // ==================================================================================
            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs)
            {
                NPC owner = Main.npc[ownerIndex];
                
                // Jika Destroyer mati sebelum durasi habis, laser akan langsung ikut mati secara bersih
                if (!owner.active || owner.type != NPCID.TheDestroyer)
                {
                    Projectile.Kill();
                    return;
                }
            }

            // Kunci pusat proyektil pada Local Player agar engine Terraria selalu menganggap proyektil ini "On-Screen"
            Projectile.Center = Main.LocalPlayer.Center;
            // ==================================================================================

            CurrentLaserLength = MaxLaserLength;
        }

        public override bool CanHitPlayer(Player target)
        {
            if (Timer < TelegraphDuration) return false;
            if (Timer > TotalDuration - 8f) return false;
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

            Color merahTua = new Color(200, 0, 0);       
            Color merahTerang = new Color(255, 150, 150); 

            // TWEAK: Ambil posisi koordinat asli Destroyer untuk menggambar pangkal laser
            Vector2 bossCenter = Projectile.Center; // Fallback aman
            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs && Main.npc[ownerIndex].active)
            {
                bossCenter = Main.npc[ownerIndex].Center;
            }

            while (distanceCovered < CurrentLaserLength)
            {
                float currentStep = stepLength;
                if (distanceCovered + currentStep > CurrentLaserLength) {
                    currentStep = CurrentLaserLength - distanceCovered;
                }

                // Menggunakan bossCenter alih-alih Projectile.Center
                Vector2 drawPos = bossCenter + (beamDir * distanceCovered) - Main.screenPosition;
                Color animatedColor;

                if (Timer < TelegraphDuration)
                {
                    animatedColor = new Color(255, 0, 0) * 0.7f; 
                }
                else
                {
                    float wavePhase = (Main.GlobalTimeWrappedHourly * 22f) - (distanceCovered * 0.02f);
                    float colorLerpFactor = (float)(Math.Sin(wavePhase) + 1f) / 2f;
                    animatedColor = Color.Lerp(merahTua, merahTerang, colorLerpFactor);
                }

                animatedColor *= Projectile.Opacity;
                Vector2 segmentScale = new Vector2(currentStep / middleSlice.Width, Projectile.scale);

                if (Timer >= TelegraphDuration)
                {
                    float mouthFade = MathHelper.Clamp((distanceCovered - 40f) / 60f, 0f, 1f);
                    if (mouthFade > 0f)
                    {
                        Vector2 shadowScale = new Vector2(segmentScale.X, segmentScale.Y * 1.8f);
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

            // TWEAK: Ambil posisi koordinat asli Destroyer untuk menghitung area damage laser
            Vector2 bossCenter = Projectile.Center; // Fallback aman
            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs && Main.npc[ownerIndex].active)
            {
                bossCenter = Main.npc[ownerIndex].Center;
            }

            // Menggunakan bossCenter alih-alih Projectile.Center agar hitbox laser tetap bekerja dari tubuh bos langsung ke arah player
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), bossCenter, bossCenter + (beamDir * CurrentLaserLength), 30f * Projectile.scale, ref samplePoint);
        }
    }
}