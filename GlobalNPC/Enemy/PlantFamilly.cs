using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class PlantRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        
        // =========================================================================
        // [GUIDE & BALANCING LOKASI TIMER ATTACK]
        // =========================================================================
        public int shootTimer = 0;   // Mengatur jeda tembakan kelompok Snatcher/Clinger
        public int fungiTimer = 0;   // Mengatur jeda sihir spora kelompok Fungi Bulb
        // =========================================================================

        public override void PostAI(NPC npc)
        {
            // [PROTEKSI MUTLAK]: Saring musuh agar OOA / Crystal tidak masuk dan merusak indeks array!
            if (npc.type == NPCID.DD2EterniaCrystal || npc.type == NPCID.DD2GoblinT1 || 
                npc.type == NPCID.DD2GoblinT2 || npc.type == NPCID.DD2GoblinT3 || 
                npc.type == NPCID.DD2DrakinT2 || npc.type == NPCID.DD2DrakinT3) 
            {
                return;
            }

            // Validasi indeks target player (0 - 255). Mencegah IndexOutOfRangeException akibat OOA target!
            if (npc.target < 0 || npc.target >= Main.maxPlayers) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead || npc.Distance(target.Center) > 800f) return;

            // --- 1. KELOMPOK PENEMBAK SEED (Snatcher, Man Eater, Angry Trapper) ---
            if (npc.type == NPCID.Snatcher || npc.type == NPCID.ManEater || npc.type == NPCID.AngryTrapper) // Menggunakan ID resmi agar scannable
            {
                shootTimer++;
                if (shootTimer >= 150) // <--- Ubah angka ini untuk mengatur kecepatan tembak Snatcher (Default: 150)
                {
                    shootTimer = 0;
                    if (Collision.CanHit(npc.position, npc.width, npc.height, target.position, target.width, target.height))
                    {
                        ShootJungleSeed(npc, target);
                    }
                }
            }

            // --- 2. KELOMPOK FUNGI BULB (Anomura Fungus, Mushi Ladybug) ---
            if (npc.type == NPCID.AnomuraFungus || npc.type == NPCID.MushiLadybug)
            {
                fungiTimer++;
                if (fungiTimer >= 180) // <--- Ubah angka ini untuk mengatur jeda spawn spora Fungi (Default: 180)
                {
                    fungiTimer = 0;
                    if (npc.Distance(target.Center) < 320f)
                    {
                        ExecuteFungiMagic(npc, target);
                    }
                }
            }

            // --- 3. CLINGER ---
            if (npc.type == NPCID.Clinger)
            {
                shootTimer++;
                if (shootTimer >= 120) // <--- Ubah angka ini untuk mengatur kecepatan semburan api Clinger (Default: 120)
                {
                    shootTimer = 0;
                    ShootClingerFire(npc, target);
                }
            }
        }

        private void ShootJungleSeed(NPC npc, Player target)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            
            int projType = Main.rand.NextFloat() < 0.20f ? ProjectileID.Stinger : ProjectileID.Seed; // Menggunakan ProjectileID resmi
            Vector2 dir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);

            if (npc.type == NPCID.AngryTrapper) 
            {
                for (int i = 0; i < 3; i++)
                {
                    // BALANCING: * 8f adalah kecepatan proyektil, npc.damage / 2 adalah damage tembakan Angry Trapper
                    Vector2 vel = dir.RotatedByRandom(MathHelper.ToRadians(15)) * 8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel, projType, npc.damage / 2, 1f, Main.myPlayer);
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item64, npc.Center);
            }
            else 
            {
                // BALANCING: * 6f adalah kecepatan proyektil, angka 5 adalah damage tembakan Snatcher/Man Eater
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 6f, projType, 5, 1f, Main.myPlayer);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item63, npc.Center);
            }
        }

        private void ExecuteFungiMagic(NPC npc, Player target)
        {
            for (int i = 0; i < 25; i++)
            {
                Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.MagicMirror, 0, 0, 100, default, 1.5f);
                d.noGravity = true;
                d.velocity *= 2.5f;
            }
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item4, npc.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector2 spawnPos = target.Center + Main.rand.NextVector2Circular(80, 80);
                    int n = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, NPCID.FungiSpore);
                    
                    if (Main.npc[n].active)
                    {
                        Main.npc[n].velocity = Main.rand.NextVector2Circular(2f, 2f); // Mengatur kecepatan gerak acak spora baru
                        Main.npc[n].netUpdate = true;

                        for (int j = 0; j < 10; j++)
                        {
                            Dust d = Dust.NewDustDirect(spawnPos, 10, 10, DustID.Flare_Blue, 0, 0, 150, default, 1.2f);
                            d.noGravity = true;
                        }
                    }
                }
            }
        }

        private void ShootClingerFire(NPC npc, Player target)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // BALANCING: * 7f adalah kecepatan luncur api, npc.damage / 3 adalah damage dari semburan kutukan Clinger
            Vector2 fireVel = (target.Center - npc.Center).SafeNormalize(Vector2.Zero) * 7f;
            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, fireVel, ProjectileID.CursedFlameHostile, npc.damage / 3, 1f, Main.myPlayer);
            
            if (Main.projectile[p].active)
            {
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
            }
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item20, npc.Center);
        }
    }
}