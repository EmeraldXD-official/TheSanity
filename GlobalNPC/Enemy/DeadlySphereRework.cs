using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class DeadlySphereRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DeadlySphere;
        }

        public override bool InstancePerEntity => true;

        private enum AiState
        {
            Locking,
            Dashing,
            Charging
        }

        private AiState currentState = AiState.Locking;
        private int internalTimer = 0;
        
        private int dashCount = 0;
        private int maxDashes = 3;
        private int dashTimer = 0;

        private int chargeCount = 0;
        private int maxCharges = 2;
        private Vector2 dashVelocity = Vector2.Zero;

        public override bool PreAI(NPC npc)
        {
            return true; 
        }

        public override void PostAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player player = Main.player[npc.target];

            if (player.dead || !player.active)
            {
                return;
            }

            switch (currentState)
            {
                case AiState.Locking:
                    HandleLockingState(npc, player);
                    break;

                case AiState.Dashing:
                    HandleDashingState(npc, player);
                    break;

                case AiState.Charging:
                    HandleChargingState(npc, player);
                    break;
            }
        }

        private void HandleLockingState(NPC npc, Player player)
        {
            Vector2 targetPosition = player.Center + new Vector2(0, -150); 
            Vector2 moveSpeed = targetPosition - npc.Center;
            float distance = moveSpeed.Length();
            
            if (distance > 20f)
            {
                moveSpeed.Normalize();
                moveSpeed *= 5f; 
                npc.velocity = (npc.velocity * 19f + moveSpeed) / 20f;
            }

            internalTimer++;

            if (internalTimer >= 180) 
            {
                internalTimer = 0;
                dashCount = 0;
                maxDashes = Main.rand.Next(3, 6); 
                currentState = AiState.Dashing;
                dashTimer = 0;
            }
        }

        // =========================================================================
        // FASE 2: DASHING (Mode Sat Set - Tanpa Cooldown)
        // =========================================================================
        private void HandleDashingState(NPC npc, Player player)
        {
            dashTimer++;

            // Inisialisasi arah dash kustom
            if (dashTimer == 1)
            {
                Vector2 randomOffset = Main.rand.NextVector2Circular(40f, 40f);
                Vector2 dashTarget = player.Center + randomOffset;
                
                dashVelocity = dashTarget - npc.Center;
                dashVelocity.Normalize();

                // =====================================================================
                // [GUIDE LOCATION 1: DASH SPEED]
                // Kecepatan lesatan bola (16f).
                // =====================================================================
                dashVelocity *= 16f; 
                npc.velocity = dashVelocity;
            }

            // =====================================================================
            // [GUIDE LOCATION 2: DASH ACTIVE DURATION]
            // Bola mengunci kecepatan penuh selama 30 frame (0.5 detik).
            // =====================================================================
            if (dashTimer <= 30)
            {
                npc.velocity = dashVelocity;
            }
            // =====================================================================
            // [GUIDE LOCATION 3: BRAKING WINDOW (JEDA REM TIPIS)]
            // Frame 31-40 (hanya 10 frame / 0.16 detik) untuk pengereman kinetik 
            // agar transisi antar dash tidak kaku.
            // =====================================================================
            else if (dashTimer > 30 && dashTimer <= 40)
            {
                npc.velocity *= 0.82f; 
            }

            // Jika total durasi satu kali rangkaian dash selesai (40 frame)
            if (dashTimer > 40)
            {
                dashCount++;
                dashTimer = 0; // Reset ke 0 agar frame berikutnya langsung memicu Dash baru (Sat Set!)

                // Jika jumlah total dash acak (3-5 kali) sudah terpenuhi
                if (dashCount >= maxDashes)
                {
                    internalTimer = 0;
                    chargeCount = 0;
                    maxCharges = Main.rand.Next(2, 4); 
                    currentState = AiState.Charging;
                    
                    SoundEngine.PlaySound(SoundID.Zombie49, npc.Center);
                }
            }
        }

        private void HandleChargingState(NPC npc, Player player)
        {
            internalTimer++;

            Vector2 distanceVector = npc.Center - player.Center;
            Vector2 maintainVelocity = distanceVector;
            maintainVelocity.Normalize();
            maintainVelocity *= 1.5f; 
            npc.velocity = (npc.velocity * 29f + maintainVelocity) / 30f;

            if (internalTimer < 120)
            {
                double angleStep = Math.PI * 2 / 4;
                float radius = 42f;
                for (int i = 0; i < 4; i++)
                {
                    double currentAngle = (internalTimer * 0.15f) + (i * angleStep);
                    Vector2 dustOffset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * radius;
                    int d = Dust.NewDust(npc.Center + dustOffset - new Vector2(4, 4), 8, 8, DustID.Electric, 0f, 0f, 100, default, 1f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = npc.velocity;
                }
            }

            if (internalTimer >= 120)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath37, npc.Center);

                int projDamage = 40;
                float projSpeed = 9f;

                Vector2 toPlayer = player.Center - npc.Center;
                toPlayer.Normalize();
                float baseAngle = toPlayer.ToRotation();

                bool isPlusPattern = Main.rand.NextBool();

                if (isPlusPattern)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = baseAngle + MathHelper.ToRadians(i * 90);
                        Vector2 projVel = angle.ToRotationVector2() * projSpeed;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, projVel, ProjectileID.MartianTurretBolt, projDamage, 0f, Main.myPlayer);
                    }
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = baseAngle + MathHelper.ToRadians(45 + (i * 90));
                        Vector2 projVel = angle.ToRotationVector2() * projSpeed;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, projVel, ProjectileID.MartianTurretBolt, projDamage, 0f, Main.myPlayer);
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, toPlayer * projSpeed, ProjectileID.MartianTurretBolt, projDamage, 0f, Main.myPlayer);
                }

                internalTimer = 0; 
                chargeCount++;

                if (chargeCount >= maxCharges)
                {
                    currentState = AiState.Locking;
                }
                else
                {
                    SoundEngine.PlaySound(SoundID.Zombie49, npc.Center);
                }
            }
        }
    }
}