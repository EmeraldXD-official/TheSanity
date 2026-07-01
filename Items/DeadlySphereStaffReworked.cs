using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class DeadlySphereMinionRework : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ProjectileID.DeadlySphere || entity.type == ProjectileID.MartianTurretBolt;
        }

        public override bool InstancePerEntity => true;

        // Tag pengenal untuk peluru kawan
        public bool isFriendlySphereBolt = false;
        
        // Timer berbasis frame untuk menghitung detik
        private int cycleTimer = 0;
        private int targetVanillaDuration = 180; // Default awal 3 detik (180 frame)
        private bool isFirstInizialization = true;

        public override void PostAI(Projectile projectile)
        {
            if (projectile.type != ProjectileID.DeadlySphere)
                return;

            // Inisialisasi awal sekali agar durasi pertamanya acak antara 3-5 detik
            if (isFirstInizialization)
            {
                targetVanillaDuration = Main.rand.Next(180, 301); // 180 frame = 3 detik, 300 frame = 5 detik
                isFirstInizialization = false;
            }

            cycleTimer++;

            // ---------------------------------------------------------------------
            // FASE 1: BIARKAN AI VANILLA BEKERJA BEBAS
            // ---------------------------------------------------------------------
            if (cycleTimer < targetVanillaDuration)
            {
                return;
            }

            // Cari target musuh terdekat untuk persiapan menembak
            NPC target = projectile.OwnerMinionAttackTargetNPC;
            if (target == null || !target.CanBeChasedBy())
            {
                float closestDist = 700f;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy())
                    {
                        float dist = Vector2.Distance(projectile.Center, npc.Center);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            target = npc;
                        }
                    }
                }
            }

            // Jika tidak ada musuh sama sekali di layar, tunda penembakan
            if (target == null)
            {
                cycleTimer = targetVanillaDuration;
                return;
            }

            // ---------------------------------------------------------------------
            // FASE 2: MEMASUKI MODE CHARGE/PENGUMPULAN ENERGI (Durasi: 60 Frame = 1 Detik)
            // ---------------------------------------------------------------------
            int chargeLimit = targetVanillaDuration + 60;

            if (cycleTimer >= targetVanillaDuration && cycleTimer < chargeLimit)
            {
                // =====================================================================
                // [GUIDE LOCATION 1: CHARGE SPEED DAMPENING]
                // =====================================================================
                projectile.velocity *= 0.4f;

                // =====================================================================
                // [GUIDE LOCATION 2: PRE-SHOT CHARGE PARTICLES EFFECT]
                // =====================================================================
                if (Main.rand.NextBool(2)) 
                {
                    Vector2 particleOffset = Main.rand.NextVector2CircularEdge(35f, 35f);
                    Vector2 dustVelocity = -particleOffset * 0.08f; 

                    Dust d = Dust.NewDustPerfect(projectile.Center + particleOffset, DustID.Electric, dustVelocity, 100, default, 0.8f);
                    d.noGravity = true; // FIX: Sudah diperbaiki di sini
                }
            }

            // ---------------------------------------------------------------------
            // FASE 3: DETIK EKSEKUSI TEMBAKAN SHOTGUN
            // ---------------------------------------------------------------------
            if (cycleTimer >= chargeLimit)
            {
                if (projectile.owner == Main.myPlayer)
                {
                    Vector2 toTarget = target.Center - projectile.Center;
                    toTarget.Normalize();

                    // =====================================================================
                    // [GUIDE LOCATION 3: SHOTGUN SPREAD & DAMAGE BALANCING]
                    // =====================================================================
                    int pelletCount = 5; 
                    float spreadAngle = 25f; 
                    float boltSpeed = 14f;
                    int boltDamage = (int)(projectile.damage * 0.9f); 

                    for (int i = 0; i < pelletCount; i++)
                    {
                        float randomSpread = Main.rand.NextFloat(-spreadAngle, spreadAngle);
                        Vector2 spreadVelocity = toTarget.RotatedBy(MathHelper.ToRadians(randomSpread)) * boltSpeed;

                        int p = Projectile.NewProjectile(projectile.GetSource_FromAI(), projectile.Center, spreadVelocity, ProjectileID.MartianTurretBolt, boltDamage, projectile.knockBack, projectile.owner);
                        
                        if (p < Main.maxProjectiles)
                        {
                            Main.projectile[p].friendly = true;
                            Main.projectile[p].hostile = false;
                            Main.projectile[p].DamageType = DamageClass.Summon;
                            
                            if (Main.projectile[p].TryGetGlobalProjectile<DeadlySphereMinionRework>(out var globalProj))
                            {
                                globalProj.isFriendlySphereBolt = true;
                            }
                        }
                    }

                    // =====================================================================
                    // [GUIDE LOCATION 4: RECOIL / SELF-KNOCKBACK FORCE]
                    // =====================================================================
                    projectile.velocity -= toTarget * 7.5f;

                    for (int g = 0; g < 12; g++)
                    {
                        Vector2 blastVel = toTarget.RotatedByRandom(MathHelper.ToRadians(30)) * Main.rand.NextFloat(2f, 6f);
                        Dust d = Dust.NewDustPerfect(projectile.Center, DustID.Electric, blastVel, 0, default, 1.1f);
                        d.noGravity = true;
                    }

                    SoundEngine.PlaySound(SoundID.Item36, projectile.Center);
                }

                // =====================================================================
                // [GUIDE LOCATION 5: NEXT VANILLA CYCLE TIME RANGE]
                // =====================================================================
                targetVanillaDuration = Main.rand.Next(180, 301);
                cycleTimer = 0; 
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.MartianTurretBolt && isFriendlySphereBolt)
            {
                target.AddBuff(BuffID.Electrified, 210);
            }
        }
    }
}