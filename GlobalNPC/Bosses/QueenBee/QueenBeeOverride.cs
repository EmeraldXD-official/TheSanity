using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TheSanity.Projectiles; // Pastikan namespace projectile kamu benar

namespace TheSanity.NPCs
{
    public class QueenBeeOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // --- VARIABEL KONTROL SYSTEM ---
        private int previousAI0 = -1;
        private int aiTimer = 0;

        // --- TIMER KHUSUS EVENT NUKE (30 DETIK) ---
        private int nukeTimer = 0; 

        // --- KUNCI PENGAMAN BIAR TIDAK CRASH (RECURSION GUARD) ---
        public bool isSpawningCustomStingers = false;

        // --- TRACKER ATTACK & FASE ---
        public bool shotgunAlternate = false; // True = Shotgun, False = Barrage

        // --- STATE MACHINE OVERRIDE KHUSUS ---
        // 0 = Normal (Sepenuhnya dikendalikan Gerakan Vanilla)
        // 1 = Active Nuke Phase (Boss TETAP MENYERANG & BERGERAK BEBAS)
        // 2 = Death Animation Sequence (Override Total)
        private int specialState = 0;
        private Vector2 deathCenterPoint = Vector2.Zero;
        private Vector2 lastHoneyCloudPos = Vector2.Zero;

        // --- VARIABLE KONTROL MECHANIC SHIELD & GOLDEN STINGER ---
        private bool spawnedGoldenStingers = false; // Pengunci agar spawn hanya terjadi 1x
        private float shieldScale = 0f;             // Tracker ukuran Aura Shield Emas (Luar)
        private float stonedScale = 0f;             // Tracker ukuran Aura Stoned Abu-abu (Dalam)

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.QueenBee;
        }

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player player = Main.player[npc.target];

            if (!player.active || player.dead)
            {
                specialState = 0;
                nukeTimer = 0;
                return true; 
            }

            // Hitung persentase HP di awal agar bisa digunakan di semua sistem
            float hpPercent = (float)npc.life / npc.lifeMax;

            // =========================================================================
            // [LOGIC MECHANIC: SPAWN 3 GOLDEN STINGER SAAT HP DI BAWAH 20%]
            // =========================================================================
            if (hpPercent <= 0.20f && !spawnedGoldenStingers)
            {
                spawnedGoldenStingers = true;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // -----------------------------------------------------------------
                    // [GUIDE BALANCING: RADIUS SPAWN GOLDEN STINGER]
                    // - 240f : Jarak radius spawn sejauh 15 Block melingkari Queen Bee.
                    // -----------------------------------------------------------------
                    float spawnRadius = 240f; 
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = MathHelper.ToRadians(i * 120f); 
                        Vector2 spawnOffset = new Vector2(spawnRadius, 0f).RotatedBy(angle);
                        Vector2 spawnPos = npc.Center + spawnOffset;

                        NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<GoldenStinger>());
                    }
                }
                npc.netUpdate = true;
            }

            // Real-time Scanner Keberadaan Minion Golden Stinger di Arena
            bool anyStingerAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<GoldenStinger>())
                {
                    anyStingerAlive = true;
                    break;
                }
            }

            // =========================================================================
            // [LOGIC MECHANIC: STATUS KEBAL (INVINCIBLE) SAAT MINION HIDUP]
            // =========================================================================
            if (spawnedGoldenStingers && anyStingerAlive)
            {
                if (specialState != 2) // Pengaman agar tidak menabrak status kebal milik Death Animation
                {
                    npc.dontTakeDamage = true;
                }
            }
            else
            {
                if (specialState != 2)
                {
                    npc.dontTakeDamage = false;
                }
            }

            // =========================================================================
            // [LOGIC MECHANIC: PENGATURAN INDEPENDEN SKALA DUA AURA]
            // - auraShieldActive : HANYA true pas minion stinger spawn dan masih hidup.
            // - auraStonedActive : Selalu true di semua fase pertarungan (All Phases).
            // =========================================================================
            bool auraShieldActive = spawnedGoldenStingers && anyStingerAlive && (specialState != 2);
            float targetShieldScale = auraShieldActive ? 1f : 0f;
            shieldScale = MathHelper.Lerp(shieldScale, targetShieldScale, 0.07f);
            
            if (!auraShieldActive && shieldScale < 0.01f) 
            {
                shieldScale = 0f; 
            }

            bool auraStonedActive = (specialState != 2);
            float targetStonedScale = auraStonedActive ? 1f : 0f;
            stonedScale = MathHelper.Lerp(stonedScale, targetStonedScale, 0.07f);

            if (!auraStonedActive && stonedScale < 0.01f)
            {
                stonedScale = 0f;
            }

            // =========================================================================
            // [LOGIC MECHANIC: HITBOX SINKRONISASI DEBUFF STONED DI SEMUA FASE]
            // - Terikat penuh dengan 'stonedScale' (Aura abu-abu) agar konstan di semua fase.
            // =========================================================================
            if (stonedScale > 0.05f) 
            {
                // ---------------------------------------------------------------------
                // [GUIDE BALANCING: UKURAN MAKSIMAL AURA STONED UNTUK HITBOX DEBUFF]
                // - 1.66f : Pengali skala core abu-abu dalam (~10 Block total diameter).
                // ---------------------------------------------------------------------
                float maxStonedScale = 1.66f; 
                float currentStonedRadius = 48f * maxStonedScale * stonedScale; 
                float distanceToPlayer = Vector2.Distance(player.Center, npc.Center);

                if (distanceToPlayer <= currentStonedRadius)
                {
                    // BuffID.Stoned : Efek membatu mendadak.
                    // 10 frame durasi refresh per frame agar fleksibel saat player lolos keluar.
                    player.AddBuff(BuffID.Stoned, 10);
                }
            }
            // =========================================================================


            // --- 1. LOCK DARAH 1% UNTUK DEATH ANIMATION ---
            if (npc.life <= (npc.lifeMax * 0.01f) && specialState != 2)
            {
                specialState = 2;
                aiTimer = 0;
                deathCenterPoint = npc.Center;
                npc.dontTakeDamage = true;
                npc.damage = 0;
                npc.netUpdate = true;
            }

            // --- 2. JALUR OVERRIDE KHUSUS (STATE MACHINE) ---
            if (specialState == 1) // --- STATE 1: ACTIVE NUKE ATTACK MODE ---
            {
                aiTimer++;

                // Lakukan scanning apakah proyektil bom (BeeNuke) masih aktif berproses di arena
                bool nukeExists = false;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<BeeNuke>())
                    {
                        nukeExists = true;
                        break;
                    }
                }

                // ---------------------------------------------------------------------
                // [GUIDE SYSTEM: TRANSISI BERAKHIRNYA FASE NUKE]
                // - Jika BeeNuke sudah meledak/hilang, dan durasi fase minimal sudah berjalan
                //   selama 2 detik (120 Ticks), kembalikan boss ke state normal.
                // ---------------------------------------------------------------------
                if (!nukeExists && aiTimer > 120)
                {
                    specialState = 0; 
                    nukeTimer = 0; // Reset total timer untuk siklus 30 detik berikutnya
                    npc.netUpdate = true;
                }

                // FIXED: 'return false;' dan pembatasan gerak dihapus! 
                // Biarkan kode bocor ke bawah agar AI serangan, gerakan, dan animasi default vanilla tetap jalan 100%.
            }
            
            if (specialState == 2) // --- STATE 2: DEATH ANIMATION ---
            {
                HandleDeathAnimation(npc);
                return false; 
            }

            // --- 3. RUNNING TIMER UNTUK NUKE ---
            if (specialState == 0)
            {
                nukeTimer++;
                // ---------------------------------------------------------------------
                // [GUIDE BALANCING: TIMER SPAWN BOM NUKE]
                // - 1800 Ticks = Berarti bom akan dilempar/dibuat setiap 30 detik sekali.
                // ---------------------------------------------------------------------
                if (nukeTimer >= 1800) 
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<BeeNuke>(), 0, 0f, Main.myPlayer);
                    }

                    specialState = 1;  // Pindah ke State Nuke Aktif
                    aiTimer = 0;       
                    nukeTimer = 0;     
                    npc.netUpdate = true;
                    // FIXED: Jangan return false di sini agar AI vanilla langsung menyambung tanpa patah di frame ini.
                }
            }

            // --- 4. INTERSEPSI & MONITORING LOGIKA VANILLA ---
            if (previousAI0 != 3 && npc.ai[0] == 3)
            {
                shotgunAlternate = !shotgunAlternate;
            }
            
            if (previousAI0 == 3 && npc.ai[0] != 3)
            {
                if (!shotgunAlternate && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // [LOC] [VAL] DAMAGE & TOTAL SPAWN HIVE SAAT BERHENTI MENYENGAT
                    int hiveCount = (hpPercent <= 0.75f) ? 3 : 1; 
                    for (int i = 0; i < hiveCount; i++)
                    {
                        Vector2 hiveVel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -2f));
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, hiveVel, ModContent.ProjectileType<MiniBeeHive>(), 30, 0f, Main.myPlayer);
                    }
                }
            }

            if (hpPercent <= 0.75f && npc.ai[0] == 0)
            {
                if (Vector2.Distance(lastHoneyCloudPos, npc.Center) >= 48f) 
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // [LOC] [VAL] DAMAGE AWAN BALL MADU CUSTOM
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<HoneyCloud>(), 18, 0f, Main.myPlayer);
                    }
                    lastHoneyCloudPos = npc.Center;
                }
            }

            previousAI0 = (int)npc.ai[0];
            return true; 
        }

        // =========================================================================
        // [RENDERING SYSTEM DUA LAPISAN AURA MANDIRI]
        // =========================================================================
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D shieldTex = ModContent.Request<Texture2D>("TheSanity/Projectiles/AuraRing").Value;
            Vector2 origin = shieldTex.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);

            // LAPISAN 1: AURA SHIELD UTAMA (LUAR - EMAS BESAR)
            if (shieldScale > 0.001f)
            {
                float maxShieldScale = 2.5f; 
                float currentShieldScale = maxShieldScale * shieldScale;
                Color shieldColor = new Color(255, 215, 0, 90) * shieldScale; 
                float shieldRotation = -Main.GlobalTimeWrappedHourly * 1.0f;  

                spriteBatch.Draw(shieldTex, drawPos, null, shieldColor, shieldRotation, origin, currentShieldScale, SpriteEffects.None, 0f);
            }

            // LAPISAN 2: AURA STONED CORE (DALAM - ABU-ABU KECIL)
            if (stonedScale > 0.001f)
            {
                float maxStonedScale = 1.66f; 
                float currentStonedScale = maxStonedScale * stonedScale;
                Color stonedColor = new Color(130, 130, 130, 160) * stonedScale; 
                float stonedRotation = Main.GlobalTimeWrappedHourly * 1.6f;     

                spriteBatch.Draw(shieldTex, drawPos, null, stonedColor, stonedRotation, origin, currentStonedScale, SpriteEffects.None, 0f);
            }
        }
        // =========================================================================

        public void TriggerCustomAttack(NPC npc, Vector2 spawnPos, Vector2 vanillaVelocity)
        {
            isSpawningCustomStingers = true;
            float hpPercent = (float)npc.life / npc.lifeMax;

            if (shotgunAlternate) 
            {
                // [LOC] [VAL] BALANCING: TEMBAKAN MODE SHOTGUN (SPREAD)
                int count = (hpPercent <= 0.75f) ? 10 : 5; 
                float spreadAngle = MathHelper.ToRadians(count * 5f);
                Vector2 baseVel = vanillaVelocity.SafeNormalize(Vector2.Zero) * 9.5f; // KECEPATAN STINGER SHOTGUN

                for (int i = 0; i < count; i++)
                {
                    Vector2 shootVel = baseVel.RotatedBy(MathHelper.Lerp(-spreadAngle / 2f, spreadAngle / 2f, i / (float)(count - 1)));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, shootVel, ProjectileID.QueenBeeStinger, 22, 0f, Main.myPlayer); // DAMAGE: 22
                }
            }
            else 
            {
                // [LOC] [VAL] BALANCING: TEMBAKAN MODE BARRAGE (BERUNTUN CEPAT)
                int count = (hpPercent <= 0.75f) ? 4 : 2; 
                for (int i = 0; i < count; i++)
                {
                    Vector2 shootVel = vanillaVelocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.06f) * 14f; // KECEPATAN STINGER BARRAGE: 14f
                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, shootVel, ProjectileID.QueenBeeStinger, 20, 0f, Main.myPlayer); // DAMAGE: 20
                }
            }
            isSpawningCustomStingers = false;
        }

        private void HandleDeathAnimation(NPC npc)
        {
            npc.life = 1;
            npc.velocity = Vector2.Zero;
            aiTimer++;

            if (aiTimer % 10 == 0)
            {
                float sway = Main.rand.Next(5, 8) * 16f;
                npc.Center = deathCenterPoint + new Vector2(Main.rand.NextBool() ? sway : -sway, Main.rand.Next(-16, 17));
            }

            if (aiTimer >= 150) 
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    isSpawningCustomStingers = true; 
                    // [LOC] [VAL] BALANCING: LEDAKAN SENGAT DEBAT DEATH ANIMATION
                    for (int i = 0; i < 36; i++)
                    {
                        Vector2 vel = new Vector2(0f, 8f).RotatedBy(MathHelper.ToRadians(i * 10)); // KECEPATAN SENGAT KEMATIAN: 8f
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel, ProjectileID.QueenBeeStinger, 25, 0f, Main.myPlayer); // DAMAGE: 25
                    }
                    isSpawningCustomStingers = false;

                    for (int i = 0; i < 5; i++)
                    {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Main.rand.NextVector2CircularEdge(6f, 6f), ModContent.ProjectileType<MiniBeeHive>(), 35, 0f, Main.myPlayer);
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<BeeNuke>(), 0, 0f, Main.myPlayer);
                    for (int i = 0; i < 15; i++)
                    {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Main.rand.NextVector2Circular(5f, 5f), ModContent.ProjectileType<HoneyCloud>(), 20, 0f, Main.myPlayer);
                    }
                }

                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect(0, 10d);
                npc.checkDead();
                npc.active = false;
            }

            AnimateCustomHover(npc);
        }

        private void AnimateCustomHover(NPC npc)
        {
            int frameHeight = 152;
            int frame = 4 + ((int)(Main.timeForVisualEffects / 5) % 8);
            npc.frame = new Rectangle(0, frame * frameHeight, 172, frameHeight);
            if (npc.velocity.X != 0f) npc.spriteDirection = npc.velocity.X < 0 ? -1 : 1;
        }
    }

    public class QueenBeeStingerInterceptor : GlobalProjectile
    {
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.type == ProjectileID.QueenBeeStinger && source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc && npc.type == NPCID.QueenBee)
            {
                if (npc.TryGetGlobalNPC<QueenBeeOverride>(out var qb))
                {
                    if (qb.isSpawningCustomStingers)
                        return;

                    projectile.active = false;
                    qb.TriggerCustomAttack(npc, projectile.Center, projectile.velocity);
                }
            }
        }
    }
}