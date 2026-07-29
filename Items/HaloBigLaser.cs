using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HaloBigLaser : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/WhiteBeam";

        public const float MaxLaserLength = 1800f;  
        public const float TelegraphDuration = 25f;
        public const float LaserDuration = 85f;    // DIUBAH: Diperlama dari 55f ke 85f supaya rotasi laser lebih lambat
        public const float TotalDuration = TelegraphDuration + LaserDuration;

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

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindProjectiles.Add(index);
        }

        public override void AI() {
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles || !Main.projectile[parentIndex].active || Main.projectile[parentIndex].type != ModContent.ProjectileType<HaloMinionGede>()) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Main.projectile[parentIndex].Center;

            Timer++;
            if (Timer >= TotalDuration) {
                Projectile.Kill();
                return;
            }

            if (Timer == 1) {
                InitialAngle = Projectile.velocity.ToRotation();
                if (RotationDirection == 0) {
                    RotationDirection = Main.rand.NextBool() ? 1f : -1f;
                }
            }

            if (Timer < TelegraphDuration) {
                Projectile.rotation = InitialAngle;
                CurrentLaserLength = MathHelper.Lerp(0f, MaxLaserLength, Timer / TelegraphDuration);
                Projectile.scale = 0.3f; 
            } else {
                float progress = (Timer - TelegraphDuration) / LaserDuration;
                Projectile.rotation = InitialAngle + (progress * MathHelper.TwoPi * RotationDirection);
                CurrentLaserLength = MaxLaserLength;

                float timeLeft = TotalDuration - Timer;
                if (timeLeft < 10f) {
                    Projectile.scale = (timeLeft / 10f) * 2.5f; 
                } else {
                    Projectile.scale = 2.5f; 
                }
            }

            DelegateMethods.v3_1 = new Vector3(1f, 0.3f, 0.6f);
            Utils.PlotTileLine(Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * CurrentLaserLength, Projectile.width * Projectile.scale, DelegateMethods.CastLight);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();

            // Konfigurasi potongan frame berdasarkan dimensi sprite sheet 74x26
            int startWidth = 26; 
            int midWidth = 22;   
            int endWidth = 26;   
            int frameHeight = texture.Height; // 26

            // Menentukan source rectangle untuk memotong tekstur
            Rectangle startRect = new Rectangle(0, 0, startWidth, frameHeight);
            Rectangle midRect = new Rectangle(startWidth, 0, midWidth, frameHeight);
            Rectangle endRect = new Rectangle(startWidth + midWidth, 0, endWidth, frameHeight);

            Color glowPink = new Color(255, 40, 160) * 0.7f;
            Color coreWhite = Color.White * 0.95f;

            // Fungsi lokal untuk menggambar susunan segmen laser per layer warna
            void DrawLaserLayer(Color color, float scaleYMultiplier) {
                Vector2 baseScale = new Vector2(Projectile.scale, Projectile.scale * scaleYMultiplier);

                // 1. Gambar Bagian Awal (Start Cap)
                Vector2 startPos = Projectile.Center - Main.screenPosition;
                Main.EntitySpriteDraw(texture, startPos, startRect, color * Projectile.scale, Projectile.rotation, new Vector2(0, frameHeight / 2f), baseScale, SpriteEffects.None, 0);

                // 2. Gambar Bagian Tengah (Loop Body) secara berulang
                float distanceCovered = startWidth * Projectile.scale;
                float endTargetDistance = CurrentLaserLength - (endWidth * Projectile.scale);

                while (distanceCovered < endTargetDistance) {
                    float remainingSpace = endTargetDistance - distanceCovered;
                    float currentStepWidth = midWidth * Projectile.scale;

                    Vector2 midScale = baseScale;
                    // Jika sisa ruang lebih kecil dari lebar frame tengah, sesuaikan skala X agar pas pasangannya
                    if (remainingSpace < currentStepWidth) {
                        midScale.X = (remainingSpace / midWidth);
                        currentStepWidth = remainingSpace;
                    }

                    Vector2 midPos = Projectile.Center + beamDir * distanceCovered - Main.screenPosition;
                    Main.EntitySpriteDraw(texture, midPos, midRect, color * Projectile.scale, Projectile.rotation, new Vector2(0, frameHeight / 2f), midScale, SpriteEffects.None, 0);

                    distanceCovered += currentStepWidth;
                }

                // 3. Gambar Bagian Ujung (End Cap)
                // Memastikan panjang laser sudah melewati batas minimum cap sebelum menggambar ujungnya
                if (CurrentLaserLength >= (startWidth + endWidth) * Projectile.scale) {
                    Vector2 endPos = Projectile.Center + beamDir * endTargetDistance - Main.screenPosition;
                    Main.EntitySpriteDraw(texture, endPos, endRect, color * Projectile.scale, Projectile.rotation, new Vector2(0, frameHeight / 2f), baseScale, SpriteEffects.None, 0);
                }
            }

            // Layer luar: Efek Glow Pink (Selalu aktif dari masa telegraf)
            DrawLaserLayer(glowPink, 1.6f);

            // Layer dalam: Core Putih (Hanya muncul saat laser menembak/setelah telegraf)
            if (Timer >= TelegraphDuration) {
                DrawLaserLayer(coreWhite, 0.5f);
            }

            return false; 
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer < TelegraphDuration) return false;

            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            float samplePoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + (beamDir * CurrentLaserLength), Projectile.width * Projectile.scale, ref samplePoint);
        }
    }
}