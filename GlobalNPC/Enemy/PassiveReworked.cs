using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace TheSanity
{
    public class PassiveReworked : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool isEnraged = false;
        public bool hasShoutedForHelp = false;
        public bool statBoosted = false; 

        private int originalAiStyle = -1;
        private bool firstTick = true;

        // Daftar hitam ID Critter yang mutlak dikecualikan dari amukan
        private static readonly HashSet<int> ExcludedCritterIDs = new HashSet<int>
        {
            583, 584, 585, // Pink, Green, Blue Fairy
            374,           // Truffle Worm
            661            // Prismatic Lacewing
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            if (entity.townNPC) return false;
            if (ExcludedCritterIDs.Contains(entity.type)) return false;

            return entity.CountsAsACritter;
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.townNPC) return;

            npc.lifeMax = 120; 
            npc.life = 120;
        }

        // =========================================================================
        // [MINION HIT FILTER]: MEMAKSA GAME MENGIZINKAN MINION ME-HIT CRITTER SAAT MARAH
        // =========================================================================
        public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
        {
            // Jika critter sedang mengamuk
            if (isEnraged)
            {
                // Jika yang mengenai dia adalah minion, sentry, atau peluru minion/whip
                if (projectile.minion || ProjectileID.Sets.MinionShot[projectile.type] || projectile.sentry)
                {
                    return true; // PAKSA TRUE! Izinkan minion memberikan damage bypass status critter asli
                }
            }
            return null; // Balikkan ke kontrol normal untuk senjata non-minion
        }

        public override void PostAI(NPC npc)
        {
            // Simpan AI Style asli bawaan saat pertama kali dibaca
            if (firstTick)
            {
                originalAiStyle = npc.aiStyle;
                firstTick = false;
            }

            // --- DYNAMIC MINION AGRO SYSTEM ---
            if (!isEnraged)
            {
                npc.chaseable = false; // Saat damai, minion abai total (tidak agro)
            }
            else
            {
                npc.chaseable = true;  // Saat marah, minion sadar ada musuh (aktif/waspada)
            }

            // Jika mulai marah, suntik status ketahanan tubuh & BAJAK AI STYLE ASLI
            if (isEnraged && !statBoosted)
            {
                statBoosted = true;
                npc.defense = 20;
                npc.knockBackResist = 0f; // Kebal Knockback
                npc.friendly = false;     // Supaya game mendeteksi critter sebagai hostile NPC secara native

                // --- PROSES RESET MEMORI AI (Mencegah Bug Gemetar/Bentrok) ---
                for (int i = 0; i < npc.ai.Length; i++)
                {
                    npc.ai[i] = 0f;
                }
                npc.localAI[0] = 0f;
                npc.localAI[1] = 0f;

                // --- TRANSFER KE AI MENYERANG BAWAAN GAME ---
                if (npc.type != NPCID.Grasshopper && npc.type != NPCID.GoldGrasshopper)
                {
                    if (originalAiStyle == 24 || originalAiStyle == 63 || originalAiStyle == 64 || originalAiStyle == 112 || originalAiStyle == 113)
                    {
                        npc.aiStyle = 14; // Kelelawar
                    }
                    else
                    {
                        npc.aiStyle = 3;  // Zombie
                    }
                }

                npc.netUpdate = true; 
            }

            if (!isEnraged) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active || Vector2.Distance(npc.Center, target.Center) > 2000f)
            {
                isEnraged = false;
                statBoosted = false;
                hasShoutedForHelp = false;
                npc.defense = 0;
                npc.knockBackResist = 1f; 
                npc.friendly = true;
                
                for (int i = 0; i < npc.ai.Length; i++) npc.ai[i] = 0f;
                
                npc.aiStyle = originalAiStyle; 
                npc.netUpdate = true;
                return;
            }

            // =========================================================================
            // [MULTIPLAYER HITBOX DETECTION]: DETEKSI KONTAK UNTUK SEMUA PLAYER
            // =========================================================================
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player pTarget = Main.player[p];

                if (pTarget.active && !pTarget.dead && npc.Hitbox.Intersects(pTarget.Hitbox) && pTarget.hurtCooldowns[0] == 0)
                {
                    int hitDirection = (npc.Center.X < pTarget.Center.X) ? 1 : -1;

                    // -------------------------------------------------------------------------
                    // [DAMAGE BALANCING LOCATION]: PENGATURAN BESARAN DAMAGE MANUAL KAMU
                    // -------------------------------------------------------------------------
                    int customDamage = 50; 

                    if (Main.masterMode) customDamage = 50; 
                    else if (Main.expertMode) customDamage = 50; 

                    pTarget.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), customDamage, hitDirection, false, false, -1, true);
                }
            }

            if (Main.rand.NextBool(6))
            {
                Dust d = Dust.NewDustPerfect(npc.Top, DustID.CrimsonTorch, Vector2.Zero, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        // =========================================================================
        // [PROVOKE SYSTEM]
        // =========================================================================
        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) => TriggerCritterAnger(npc);
        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) => TriggerCritterAnger(npc);
        public override void OnKill(NPC npc) => TriggerCritterAnger(npc);

        private void TriggerCritterAnger(NPC victimNpc)
        {
            if (victimNpc.active && victimNpc.TryGetGlobalNPC<PassiveReworked>(out var victimGlobal))
            {
                victimGlobal.isEnraged = true;
                victimNpc.netUpdate = true; 
            }

            if (hasShoutedForHelp) return;
            hasShoutedForHelp = true;

            float fiftyBlocks = 50f * 16f;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC nearbyNpc = Main.npc[i];

                if (nearbyNpc.active && i != victimNpc.whoAmI && nearbyNpc.TryGetGlobalNPC<PassiveReworked>(out var globalCritter))
                {
                    if (!globalCritter.isEnraged)
                    {
                        float distance = Vector2.Distance(victimNpc.Center, nearbyNpc.Center);

                        if (distance <= fiftyBlocks)
                        {
                            globalCritter.isEnraged = true;
                            globalCritter.hasShoutedForHelp = true; 
                            nearbyNpc.netUpdate = true; 
                        }
                    }
                }
            }
        }
    }
}