using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.NPCs
{
    public class ReworkedMothron : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Tetap mengawasi ketiganya agar sistem tModLoader tahu mereka saling terkait
            return entity.type == NPCID.Mothron || entity.type == NPCID.MothronSpawn || entity.type == NPCID.MothronEgg;
        }

        public override bool PreAI(NPC npc)
        {
            // PERBAIKAN: Kode pemaksaan aktif = false untuk Egg & Spawn ditiadakan di sini,
            // agar mereka tidak langsung lenyap saat dipanggil melalui mekanik OnKill di bawah.

            if (npc.type == NPCID.Mothron)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC other = Main.npc[i];
                    if (other.active && other.type == NPCID.Mothron && other.whoAmI < npc.whoAmI)
                    {
                        npc.active = false;
                        return false;
                    }
                }

                MothronReworkLogic(npc);
                return false;
            }

            return true;
        }

        // =========================================================================
        // MEKANIK BARU: LOGIKA SAAT MOTHRON MATI (ON KILL)
        // =========================================================================
        public override void OnKill(NPC npc)
        {
            if (npc.type == NPCID.Mothron)
            {
                // Pastikan proses spawn hanya terjadi di sisi Server/Singleplayer (menghindari bug desync multiplayer)
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // -------------------------------------------------------------------------
                    // LOKASI BALANCING: JUMLAH EGG/TELUR YANG JATUH (1 - 2 Telur)
                    // -------------------------------------------------------------------------
                    // Main.rand.Next(1, 3) artinya memilih angka acak antara 1 sampai 2.
                    // Jika ingin di-balance menjadi lebih banyak (misal 2-4), ganti menjadi (2, 5).
                    // -------------------------------------------------------------------------
                    int eggCount = Main.rand.Next(1, 3);
                    
                    for (int i = 0; i < eggCount; i++)
                    {
                        // Spawn telur dengan sedikit koordinat acak di sekitar posisi mati Mothron
                        Vector2 spawnOffset = Main.rand.NextVector2Circular(20f, 20f);
                        NPC.NewNPC(npc.GetSource_Death(), (int)(npc.position.X + spawnOffset.X), (int)(npc.position.Y + spawnOffset.Y), NPCID.MothronEgg);
                    }

                    // -------------------------------------------------------------------------
                    // LOKASI BALANCING: CHANCE SPAWN BABY MOTHRON (25%)
                    // -------------------------------------------------------------------------
                    // Main.rand.NextFloat() menghasilkan angka desimal acak dari 0.0 sampai 1.0.
                    // -> 0.25f berarti 25% chance.
                    // -> Jika ingin diubah menjadi 50% chance, ubah angkanya menjadi 0.50f.
                    // -------------------------------------------------------------------------
                    if (Main.rand.NextFloat() < 0.25f)
                    {
                        NPC.NewNPC(npc.GetSource_Death(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.MothronSpawn);
                        
                        // Efek suara tambahan saat bayinya mendadak lahir instan
                        SoundEngine.PlaySound(SoundID.NPCDeath13, npc.Center); 
                    }
                }
            }
        }

        private void MothronReworkLogic(NPC npc)
        {
            npc.TargetClosest(true);
            Player player = Main.player[npc.target];

            if (player.dead || !player.active)
            {
                npc.velocity.Y -= 0.5f;
                npc.EncourageDespawn(10);
                return;
            }

            bool terraBladeExists = UtilProjectilesExists(ModContent.ProjectileType<HostileTerraBlade>());
            bool nightEdgeExists = UtilProjectilesExists(ModContent.ProjectileType<HostileTrueNightEdge>());
            bool excaliburExists = UtilProjectilesExists(ModContent.ProjectileType<HostileTrueExcalibur>());
            bool brokenSwordExists = UtilProjectilesExists(ModContent.ProjectileType<HostileBrokenHeroSword>());

            npc.spriteDirection = npc.direction = (player.Center.X < npc.Center.X) ? -1 : 1;

            switch ((int)npc.ai[0])
            {
                // =========================================================================
                // STATE 0: SPAWN AWAL KEDUA TRUE BLADES
                // =========================================================================
                case 0:
                    npc.ai[1] = 0;
                    npc.ai[2] = 0;

                    if (!terraBladeExists && Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int swordDamage = 33; 
                        float spawnSpreadOffset = 400f; 
                        Vector2 leftSpawnPos = npc.Center + new Vector2(-spawnSpreadOffset, -150f);
                        Vector2 rightSpawnPos = npc.Center + new Vector2(spawnSpreadOffset, -150f);

                        if (!nightEdgeExists)
                            Projectile.NewProjectile(npc.GetSource_FromAI(), leftSpawnPos, Vector2.Zero, ModContent.ProjectileType<HostileTrueNightEdge>(), swordDamage, 0f, Main.myPlayer);
                        
                        if (!excaliburExists)
                            Projectile.NewProjectile(npc.GetSource_FromAI(), rightSpawnPos, Vector2.Zero, ModContent.ProjectileType<HostileTrueExcalibur>(), swordDamage, 0f, Main.myPlayer);
                    }

                    npc.ai[0] = 1; 
                    break;

                // =========================================================================
                // STATE 1: PHASE 1 - ATTACK PATTERN (33 Detik Pasti)
                // =========================================================================
                case 1:
                    npc.ai[1]++; 
                    npc.ai[2]++; 

                    float phase1FlySpeed = 7f;
                    float phase1DashSpeed = 14f;
                    int phase1AttackJeda = 150; 

                    if (npc.ai[1] < phase1AttackJeda - 40)
                    {
                        Vector2 targetHoverPos = player.Center + new Vector2((npc.whoAmI % 2 == 0 ? 300 : -300), -200);
                        Vector2 moveVelocity = targetHoverPos - npc.Center;
                        if (moveVelocity.Length() > 20f)
                        {
                            moveVelocity.Normalize();
                            npc.velocity = Vector2.Lerp(npc.velocity, moveVelocity * phase1FlySpeed, 0.05f);
                        }
                    }
                    else if (npc.ai[1] >= phase1AttackJeda)
                    {
                        Vector2 dashDirection = player.Center - npc.Center;
                        dashDirection.Normalize();
                        npc.velocity = dashDirection * phase1DashSpeed;
                        npc.ai[1] = 0; 
                    }

                    if (npc.ai[2] >= 1980) 
                    {
                        npc.ai[1] = 0;
                        npc.ai[0] = 2; 
                    }
                    break;

                // =========================================================================
                // STATE 2: RITUAL GABUNGAN - BROKEN HERO SWORD & BOOM
                // =========================================================================
                case 2:
                    npc.velocity *= 0.85f; 

                    int nightEdgeIdx = UtilFindProjectile(ModContent.ProjectileType<HostileTrueNightEdge>());
                    int excaliburIdx = UtilFindProjectile(ModContent.ProjectileType<HostileTrueExcalibur>());

                    if (nightEdgeIdx != -1 && excaliburIdx != -1)
                    {
                        Projectile pNight = Main.projectile[nightEdgeIdx];
                        Projectile pExcal = Main.projectile[excaliburIdx];

                        Vector2 midPoint = (pNight.Center + pExcal.Center) / 2f;
                        
                        pNight.velocity = (midPoint - pNight.Center) * 0.15f;
                        pExcal.velocity = (midPoint - pExcal.Center) * 0.15f;

                        if (Vector2.Distance(pNight.Center, pExcal.Center) < 30f && !brokenSwordExists)
                        {
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                Vector2 throwVel = midPoint - npc.Center;
                                throwVel.Normalize();
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, throwVel * 16f, ModContent.ProjectileType<HostileBrokenHeroSword>(), 0, 0f, Main.myPlayer);
                                
                                int terraDamage = 35; 
                                Projectile.NewProjectile(npc.GetSource_FromAI(), midPoint, Vector2.Zero, ModContent.ProjectileType<HostileTerraBlade>(), terraDamage, 0f, Main.myPlayer);
                            }

                            pNight.Kill();
                            pExcal.Kill();
                            
                            SoundEngine.PlaySound(SoundID.Item4, npc.Center); 
                            npc.ai[1] = 0;
                            npc.localAI[0] = 0; 
                            npc.ai[0] = 3;     
                        }
                    }
                    else
                    {
                        npc.ai[0] = 0;
                    }
                    break;

                // =========================================================================
                // STATE 3: PHASE 2 - TERRA BLADE ACTIVE & COMPANION SPAWN
                // =========================================================================
                case 3:
                    npc.ai[1]++;
                    npc.localAI[0]++; 

                    float phase2FlySpeed = 11f;     
                    float phase2DashSpeed = 22f;    
                    int phase2AttackJeda = 80;      

                    if (npc.ai[1] < phase2AttackJeda - 25)
                    {
                        Vector2 targetHoverPos = player.Center + new Vector2((npc.whoAmI % 2 == 0 ? 250 : -250), -150);
                        Vector2 moveVelocity = targetHoverPos - npc.Center;
                        if (moveVelocity.Length() > 20f)
                        {
                            moveVelocity.Normalize();
                            npc.velocity = Vector2.Lerp(npc.velocity, moveVelocity * phase2FlySpeed, 0.08f);
                        }
                    }
                    else if (npc.ai[1] >= phase2AttackJeda)
                    {
                        Vector2 dashDirection = player.Center - npc.Center;
                        dashDirection.Normalize();
                        npc.velocity = dashDirection * phase2DashSpeed;
                        npc.ai[1] = 0;
                    }

                    if (npc.localAI[0] == 1200)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int companionDamage = 28; 
                            float companionSpread = 450f;
                            Vector2 leftCompanion = npc.Center + new Vector2(-companionSpread, -100f);
                            Vector2 rightCompanion = npc.Center + new Vector2(companionSpread, -100f);

                            Projectile.NewProjectile(npc.GetSource_FromAI(), leftCompanion, Vector2.Zero, ModContent.ProjectileType<HostileTrueNightEdge>(), companionDamage, 0f, Main.myPlayer);
                            Projectile.NewProjectile(npc.GetSource_FromAI(), rightCompanion, Vector2.Zero, ModContent.ProjectileType<HostileTrueExcalibur>(), companionDamage, 0f, Main.myPlayer);
                        }
                    }

                    if (npc.localAI[0] >= 2400)
                    {
                        npc.ai[0] = 5; 
                        npc.ai[1] = 0; 
                    }
                    break;

                // =========================================================================
                // STATE 4: COOLDOWN JEDA LOOP (3 DETIK AMAN SEBELUM SPAWN ULANG)
                // =========================================================================
                case 4:
                    npc.velocity *= 0.88f; 
                    npc.ai[1]++; 

                    UtilKillAllSwords();

                    if (npc.ai[1] >= 180) 
                    {
                        npc.ai[0] = 0; 
                        npc.ai[1] = 0;
                        npc.ai[2] = 0;
                        npc.localAI[0] = 0;
                    }
                    break;

                // =========================================================================
                // STATE 5: FINALE DESPERATION CHARGE
                // =========================================================================
                case 5:
                    npc.velocity *= 0.8f; 
                    npc.ai[1]++; 

                    int finaleChargeTime = 50; 
                    int finaleDashTime = 30;   

                    if (npc.ai[1] == finaleChargeTime)
                    {
                        SoundEngine.PlaySound(SoundID.Item15, player.Center); 
                        npc.localAI[1] = player.Center.X; 
                        npc.localAI[2] = player.Center.Y; 
                    }

                    if (npc.ai[1] >= finaleChargeTime + finaleDashTime)
                    {
                        Vector2 crashPoint = new Vector2(npc.localAI[1], npc.localAI[2]);
                        
                        UtilSpawnFinaleExplosion(crashPoint);
                        UtilKillAllSwords();

                        npc.ai[0] = 4;
                        npc.ai[1] = 0;
                    }
                    break;
            }
        }

        private bool UtilProjectilesExists(int type)
        {
            return UtilFindProjectile(type) != -1;
        }

        private int UtilFindProjectile(int type)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].type == type)
                {
                    return i;
                }
            }
            return -1;
        }

        private void UtilKillAllSwords()
        {
            int tNight = ModContent.ProjectileType<HostileTrueNightEdge>();
            int tExcal = ModContent.ProjectileType<HostileTrueExcalibur>();
            int tTerra = ModContent.ProjectileType<HostileTerraBlade>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == tNight || p.type == tExcal || p.type == tTerra))
                {
                    p.Kill();
                }
            }
        }

        private void UtilSpawnFinaleExplosion(Vector2 position)
        {
            SoundEngine.PlaySound(SoundID.Item14, position); 

            for (int i = 0; i < 90; i++) 
            {
                Vector2 dustVelocity = Main.rand.NextVector2Circular(16f, 16f);
                int selectedDust;

                if (i % 3 == 0)
                {
                    selectedDust = DustID.Shadowflame; 
                }
                else if (i % 3 == 1)
                {
                    selectedDust = DustID.RainbowMk2; 
                }
                else
                {
                    selectedDust = DustID.TerraBlade; 
                }

                Dust d = Dust.NewDustDirect(position, 0, 0, selectedDust, dustVelocity.X, dustVelocity.Y, 100, default, 2.3f);
                d.noGravity = true;
                d.velocity *= 1.3f;
            }
        }
    }
}