using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity; 
using TheSanity.Projectiles; 

namespace TheSanity.NPCs
{
    public class DreadnautilusBossAI : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public float axeAttackTimer = 0f;
        public bool hasTriggeredPhase2Start = false; 

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.BloodNautilus;
        }

        public override void PostAI(NPC npc)
        {
            if (!npc.active) return;

            Player player = Main.player[npc.target];
            if (!player.active || player.dead) return;

            // ==========================================
            // FORCE STATUS BOSS & HP BAR
            // ==========================================
            npc.boss = true; 

            // Menghitung rasio darah untuk penentuan Phase 2 & deteksi serangan kapak
            float healthRatio = (float)npc.life / npc.lifeMax;
            bool isAxeAttackActive = (healthRatio <= 0.60f && axeAttackTimer >= 2400f);

            // ==========================================
            // LOGIKA 1: SPAWN INDIKASI (1x Goblin Shark & 1x Blood Eel)
            // ==========================================
            if (npc.localAI[0] == 0)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X - 50, (int)npc.Center.Y, NPCID.GoblinShark);
                    NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X + 50, (int)npc.Center.Y, NPCID.BloodEelHead);
                }
                npc.localAI[0] = 1; 
            }

            // ==========================================
            // LOGIKA 2: DETEKSI ATTACK VANILLA (TEARS) & SPAWN HOMING BAT
            // ==========================================
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ProjectileID.BloodNautilusTears)
                {
                    // --- ATTACK INI TIDAK BERLAKU JIKA ATTACK AXE AKTIF ---
                    if (!isAxeAttackActive)
                    {
                        if (Main.rand.NextBool(45)) 
                        {
                            int tileX = (int)(player.Bottom.X / 16f);
                            int tileY = (int)(player.Bottom.Y / 16f);

                            while (!Main.tile[tileX, tileY].HasTile && tileY < Main.maxTilesY - 50)
                            {
                                tileY++;
                            }
                            
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                int spikeDamage = 45; 
                                Vector2 spikePos = new Vector2(tileX * 16, (tileY - 1) * 16);
                                Vector2 launchVelocity = new Vector2(0f, -8f); 
                                
                                Projectile.NewProjectile(npc.GetSource_FromAI(), spikePos, launchVelocity, ModContent.ProjectileType<BloodSpikeHostile>(), spikeDamage, 1f, Main.myPlayer);
                                
                                // ========================================================
                                // NEW BALANCING: 1 TEAR SPAWN 3 HOMING BLOOD BATS (ONLY PHASE 2)
                                // ========================================================
                                // Ditambahkan validasi 'healthRatio <= 0.60f' agar kelelawar hanya keluar di fase 2
                                if (healthRatio <= 0.60f)
                                {
                                    int batDamage = 45; // Mengatur damage hantaman kelelawar
                                    for (int b = 0; b < 3; b++)
                                    {
                                        // Diberi sedikit pencaran velositas awal agar keluar dengan estetik sebelum mengejar player
                                        Vector2 batVelocity = Main.rand.NextVector2Circular(4f, 4f); // Kecepatan awal hamburan kelelawar
                                        int batIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), p.Center, batVelocity, ModContent.ProjectileType<HomingBloodBat>(), batDamage, 1f, Main.myPlayer);
                                        
                                        // Kunci target ke player saat ini
                                        Main.projectile[batIndex].ai[1] = npc.target;
                                    }
                                }
                            }
                        }
                    }

                    // Serangan peluru jejak di ekor sirip
                    if (!isAxeAttackActive && npc.velocity.Length() > 2f && Main.rand.NextBool(30))
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            // --- LOKASI SPEED & DAMAGE EKOR ---
                            Vector2 directionAway = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
                            float trailSpeed = 5f; // Kecepatan proyektil ekor sirip
                            int trailDamage = 40; // Damage proyektil ekor sirip

                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, directionAway * trailSpeed, ModContent.ProjectileType<BloodSpikeHostile>(), trailDamage, 1f, Main.myPlayer);
                        }
                    }
                }
            }

            // ==========================================
            // LOGIKA 3: PHASE 2 (HEALTH < 60%)
            // ==========================================
            if (healthRatio <= 0.60f)
            {
                // ========================================================
                // PURE FORCE KILL BLOOD SQUID (AKTIF SETIAP DETIK DI PHASE 2)
                // ========================================================
                for (int k = 0; k < Main.maxNPCs; k++)
                {
                    NPC targetNpc = Main.npc[k];
                    if (targetNpc.active && targetNpc.type == NPCID.BloodSquid)
                    {
                        targetNpc.active = false; // Lenyapkan instansi cumi murni
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            // Efek visual cipratan darah tebal saat cumi dipaksa mati
                            for (int d = 0; d < 10; d++)
                            {
                                Dust.NewDust(targetNpc.position, targetNpc.width, targetNpc.height, DustID.Blood);
                            }
                        }
                    }
                }

                if (npc.localAI[1] == 0) 
                {
                    npc.localAI[2] = npc.Center.X; 
                    npc.localAI[3] = npc.Center.Y; 
                    npc.localAI[1] = 1;
                }

                Vector2 currentArenaCenter = new Vector2(npc.localAI[2], npc.localAI[3]);
                
                currentArenaCenter = Vector2.Lerp(currentArenaCenter, npc.Center, 0.025f); 
                npc.localAI[2] = currentArenaCenter.X;
                npc.localAI[3] = currentArenaCenter.Y;

                float arenaRadius = 1440f; 

                // VISUAL BORDER PARTIKEL TEBAL
                for (int i = 0; i < 360; i += 3) 
                {
                    float radians = MathHelper.ToRadians(i);
                    Vector2 particlePos = currentArenaCenter + new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians)) * arenaRadius;
                    
                    if (Main.rand.NextBool(3)) 
                    {
                        Dust d = Dust.NewDustPerfect(particlePos, DustID.Blood, Vector2.Zero, 0, Color.Purple, 1.8f);
                        d.noGravity = true;
                    }
                }

                // LOCK PLAYER DI DALAM ARENA
                float distanceToArena = Vector2.Distance(player.Center, currentArenaCenter);
                if (distanceToArena > arenaRadius)
                {
                    Vector2 pullDirection = (currentArenaCenter - player.Center).SafeNormalize(Vector2.Zero);
                    player.velocity = pullDirection * 14f; // Kecepatan tarikan pembatas arena
                    player.controlHook = false; 
                }
                
                player.wingTime = player.wingTimeMax;

                // ==========================================
                // LOGIKA 4: PATTERN SPAM BLOODY AXE
                // ==========================================
                if (!hasTriggeredPhase2Start)
                {
                    axeAttackTimer = 2400f; 
                    hasTriggeredPhase2Start = true;

                    for (int pId = 0; pId < Main.maxProjectiles; pId++)
                    {
                        if (Main.projectile[pId].active && Main.projectile[pId].type == ProjectileID.BloodNautilusTears)
                        {
                            Main.projectile[pId].Kill();
                        }
                    }
                }

                axeAttackTimer++; 

                float attackStartTime = 2400f; 
                float initialFreezeDuration = 240f; 
                float attackDuration = 420f;        
                float transitDuration = 120f;       
                float hoverCooldownDuration = 1200f; 

                // ANTI-DESPAWN BOSS
                if (axeAttackTimer >= attackStartTime)
                {
                    npc.timeLeft = 3600; 
                }

                // FASE 4A: DIAM MEGAH DI AWAL TRANSISI (4 DETIK)
                if (axeAttackTimer >= attackStartTime && axeAttackTimer < attackStartTime + initialFreezeDuration)
                {
                    npc.velocity = Vector2.Zero;
                    
                    npc.ai[0] = 0f; 
                    npc.ai[1] = 0f;
                    npc.ai[2] = 0f;
                    npc.ai[3] = 0f;

                    if (Main.rand.NextBool(2))
                    {
                        Vector2 auraPos = npc.Center + Main.rand.NextVector2CircularEdge(150f, 150f);
                        Vector2 auraVel = (npc.Center - auraPos).SafeNormalize(Vector2.Zero) * 4f;
                        Dust.NewDustPerfect(auraPos, DustID.Blood, auraVel, 0, Color.Red, 1.3f).noGravity = true;
                    }
                }

                // FASE 4B: PROSES TEMBAK KAPAK (7 KALI BERUNTUN)
                float actualShootStart = attackStartTime + initialFreezeDuration;
                if (axeAttackTimer >= actualShootStart && axeAttackTimer <= actualShootStart + attackDuration)
                {
                    float distanceToCenter = Vector2.Distance(npc.Center, currentArenaCenter);
                    if (distanceToCenter > 20f)
                    {
                        npc.velocity = (currentArenaCenter - npc.Center).SafeNormalize(Vector2.Zero) * 18f; // Kecepatan boss kembali ke tengah arena
                    }
                    else
                    {
                        npc.velocity = Vector2.Zero;
                        npc.rotation += 0.35f; 
                    }

                    float currentAttackFrame = axeAttackTimer - actualShootStart;
                    
                    if (currentAttackFrame == 60  || currentAttackFrame == 120 || currentAttackFrame == 180 || 
                        currentAttackFrame == 240 || currentAttackFrame == 300 || currentAttackFrame == 360 || 
                        currentAttackFrame == 420)
                    {
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center); 

                        float rotationOffset = (currentAttackFrame / 60f) * 0.35f; 

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            for (int a = 0; a < 8; a++) 
                            {
                                // --- LOKASI SPEED & DAMAGE KAPAK SAAT SPAWN ---
                                float angle = (MathHelper.TwoPi / 8f) * a + rotationOffset;
                                Vector2 axeVelocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 7f; // Speed awal kapak keluar: 7f
                                int axeDamage = 60; // Damage awal kapak: 60

                                int pIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, axeVelocity, ModContent.ProjectileType<BloodyAxeProjectile>(), axeDamage, 1f, Main.myPlayer);
                                
                                Main.projectile[pIndex].ai[0] = currentArenaCenter.X;
                                Main.projectile[pIndex].ai[1] = currentArenaCenter.Y;
                                Main.projectile[pIndex].localAI[0] = 99f; 
                                Main.projectile[pIndex].localAI[1] = 0f;   
                            }
                        }
                    }
                }

                // FASE 4C: TRANSISI GERAKAN KAPAK TERAKHIR
                float transitStart = actualShootStart + attackDuration;
                if (axeAttackTimer > transitStart && axeAttackTimer <= transitStart + transitDuration)
                {
                    npc.velocity = Vector2.Zero;
                }

                // FASE 4D: COOLDOWN DIAM TOTAL BOSS (20 DETIK)
                float cooldownStart = transitStart + transitDuration;
                if (axeAttackTimer > cooldownStart && axeAttackTimer <= cooldownStart + hoverCooldownDuration) 
                {
                    npc.velocity = Vector2.Zero; 
                    
                    npc.ai[0] = 0f; 
                    npc.ai[1] = 0f;
                    npc.ai[2] = 0f;
                    npc.ai[3] = 0f;

                    for (int j = 0; j < 6; j++)
                    {
                        Vector2 auraPos = npc.Center + Main.rand.NextVector2CircularEdge(120f, 120f);
                        Vector2 auraVel = (npc.Center - auraPos).SafeNormalize(Vector2.Zero) * 5f;
                        Dust.NewDustPerfect(auraPos, DustID.Blood, auraVel, 0, Color.MediumPurple, 1.4f).noGravity = true;
                    }

                    if (axeAttackTimer == cooldownStart + hoverCooldownDuration)
                    {
                        for (int pId = 0; pId < Main.maxProjectiles; pId++)
                        {
                            if (Main.projectile[pId].active && Main.projectile[pId].type == ModContent.ProjectileType<BloodyAxeProjectile>())
                            {
                                Main.projectile[pId].Kill();
                            }
                        }
                        axeAttackTimer = 0f; 
                    }
                }
            }
        }
    }

    public class BloodyAxeBehaviorUpdate : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ModContent.ProjectileType<BloodyAxeProjectile>();
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.active && projectile.localAI[0] == 99f)
            {
                projectile.scale = 2.0f; 

                float arenaRadius = 1440f; 
                Vector2 arenaCenter = new Vector2(projectile.ai[0], projectile.ai[1]);
                float distanceToCenter = Vector2.Distance(projectile.Center, arenaCenter);

                if (projectile.timeLeft < 300) 
                {
                    projectile.timeLeft = 300; 
                }

                // KONDISI A: Pertama kali menyentuh border luar arena
                if (projectile.localAI[1] == 0f && distanceToCenter >= arenaRadius - 40f)
                {
                    projectile.localAI[1] = 1f; 

                    // --- LOKASI SPEED PANTUL KAPAK ---
                    float freeSpeed = 4.0f; // Kecepatan kapak setelah memantul dari tepi arena
                    Vector2 bounceDirection = (arenaCenter - projectile.Center).SafeNormalize(Vector2.Zero);
                    bounceDirection = bounceDirection.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f));
                    projectile.velocity = bounceDirection * freeSpeed;
                }

                // KONDISI B: Mode Bebas Berkelana di dalam ruangan arena
                if (projectile.localAI[1] == 1f)
                {
                    projectile.rotation += 0.25f;

                    // DINAMIKA KECEPATAN KAPAK BERDASARKAN JARAK CENTER
                    if (distanceToCenter < arenaRadius * 0.7f)
                    {
                        // --- LOKASI SPEED KAPAK DI AREA TENGAH ---
                        float slowSpeed = 2.0f; // Kecepatan melambat di dekat pusat arena
                        if (projectile.velocity != Vector2.Zero)
                        {
                            projectile.velocity = projectile.velocity.SafeNormalize(Vector2.Zero) * slowSpeed;
                        }
                    }
                    else 
                    {
                        // --- LOKASI SPEED KAPAK DI AREA PINGGIR ---
                        float normalFreeSpeed = 4.0f; // Kecepatan normal di pinggiran arena
                        if (projectile.velocity != Vector2.Zero)
                        {
                            projectile.velocity = projectile.velocity.SafeNormalize(Vector2.Zero) * normalFreeSpeed;
                        }
                    }

                    // Pengaman dinding luar aura lingkaran arena
                    if (distanceToCenter >= arenaRadius - 35f)
                    {
                        Vector2 pushBackIn = (arenaCenter - projectile.Center).SafeNormalize(Vector2.Zero);
                        pushBackIn = pushBackIn.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f));
                        
                        projectile.velocity = pushBackIn * 4.0f; // Kecepatan dorong balik masuk arena
                    }
                }
            }
        }
    }

    // ==========================================
    // GLOBAL PROJECTILE: REPLACE SHOT & REPLACE MECHANICS
    // ==========================================
    public class DreadnautilusProjectileReplacer : GlobalProjectile
    {
        public override bool PreAI(Projectile projectile)
        {
            // Mencegat proyektil tembakan bawaan Dreadnautilus
            if (projectile.type == ProjectileID.BloodNautilusShot)
            {
                // Mencari NPC Dreadnautilus terdekat untuk cek status HP dan pola serangannya
                NPC boss = null;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == NPCID.BloodNautilus)
                    {
                        boss = Main.npc[i];
                        break;
                    }
                }

                if (boss != null)
                {
                    float healthRatio = (float)boss.life / boss.lifeMax;
                    
                    // Mengambil script GlobalNPC dari boss untuk membaca axeAttackTimer secara akurat
                    if (boss.TryGetGlobalNPC<DreadnautilusBossAI>(out var classScript))
                    {
                        bool isAxeAttackActive = (healthRatio <= 0.60f && classScript.axeAttackTimer >= 2400f);

                        // --- BALANCING LOCATION: REPLACE TARGET IN PHASE 2 ---
                        // Jika masuk Phase 2 (< 60% HP) DAN pola Kapak tidak sedang berputar-putar meluncur
                        if (healthRatio <= 0.60f && !isAxeAttackActive)
                        {
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                // --- LOKASI DAMAGE CONVERSION BLOODY BALL ---
                                int ballDamage = 50; // Mengatur damage konversi BloodyBall pengganti shot vanilla
                                
                                // --- BALANCING LOCATION: KECEPATAN MELUNCUR BLOODY BALL ---
                                // Mengurangi kecepatan bawaan vanilla agar tidak meluncur terlalu cepat keluar arena.
                                // Diubah menjadi 60% dari kecepatan aslinya (projectile.velocity * 0.6f).
                                // Jika dirasa masih kurang lambat, kurangi angkanya (misal dikali 0.4f atau 0.5f).
                                Vector2 reducedVelocity = projectile.velocity * 0.6f;

                                Projectile.NewProjectile(
                                    projectile.GetSource_FromThis(), 
                                    projectile.Center, 
                                    reducedVelocity, 
                                    ModContent.ProjectileType<BloodyBall>(), 
                                    ballDamage, 
                                    projectile.knockBack, 
                                    projectile.owner
                                );
                            }

                            // Matikan / hilangkan proyektil vanilla aslinya agar langsung digantikan total
                            projectile.active = false;
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}