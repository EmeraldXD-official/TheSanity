using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.GlobalNPC.Enemy
{
    public class BeetleAuraRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Variabel pelacak khusus untuk mengunci efek Confused milik Lac Beetle agar awet 3 detik setelah keluar aura
        private bool playerWasInLacAura = false;

        public override bool PreAI(NPC npc)
        {
            // Validasi: Hanya jalankan logika jika musuh merupakan salah satu dari 3 varian Beetle
            if (npc.type != NPCID.CochinealBeetle && npc.type != NPCID.CyanBeetle && npc.type != NPCID.LacBeetle) return true;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (target == null || target.dead || !npc.HasValidTarget) return true;

            float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);
            float auraRadius = 320f; // Seragam 20 Block = 320 Pixel untuk semua variant

            // ========================================================================
            // VARIANT CYAN BEETLE (ID 218) - TEMATIK ES & PEMBEKUAN
            // ========================================================================
            if (npc.type == NPCID.CyanBeetle)
            {
                // --- 1. VISUAL AURA TEBAL & TERSEDOT (CYAN/BIRU MUDA) ---
                // LOKASI DENSITY VISUAL: Memunculkan 5 partikel sekaligus setiap kali trigger agar efeknya sangat pekat
                if (Main.rand.NextBool(2)) 
                {
                    for (int i = 0; i < 5; i++)
                    {
                        double angle = Main.rand.NextDouble() * Math.PI * 2d;
                        Vector2 spawnOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * auraRadius;
                        Vector2 dustPos = npc.Center + spawnOffset;

                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.IceTorch, 0f, 0f, 100, default, 1.4f);
                        d.noGravity = true;

                        Vector2 suctionDirection = npc.Center - d.position;
                        suctionDirection.Normalize();
                        // Diberikan variasi kecepatan hisap acak biar tidak terlalu kaku alirannya
                        d.velocity = suctionDirection * Main.rand.NextFloat(3.5f, 6.5f); 
                    }
                }

                // --- 2. EFEK DEBUFF AREA AURA ---
                if (distanceToPlayer <= auraRadius)
                {
                    target.AddBuff(BuffID.Frostburn2, 2); 
                }
            }

            // ========================================================================
            // VARIANT COCHINEAL BEETLE (ID 217) - TEMATIK DARAH & BATU
            // ========================================================================
            if (npc.type == NPCID.CochinealBeetle)
            {
                // --- 1. VISUAL AURA TEBAL & TERSEDOT (MERAH) ---
                if (Main.rand.NextBool(2))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        double angle = Main.rand.NextDouble() * Math.PI * 2d;
                        Vector2 spawnOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * auraRadius;
                        Vector2 dustPos = npc.Center + spawnOffset;

                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Torch, 0f, 0f, 100, default, 1.5f);
                        d.noGravity = true;

                        Vector2 suctionDirection = npc.Center - d.position;
                        suctionDirection.Normalize();
                        d.velocity = suctionDirection * Main.rand.NextFloat(4.0f, 7.0f); 
                    }
                }

                // --- 2. EFEK DEBUFF AREA AURA ---
                if (distanceToPlayer <= auraRadius)
                {
                    target.AddBuff(BuffID.Bleeding, 2);
                }
            }

            // ========================================================================
            // VARIANT LAC BEETLE (ID 219) - TEMATIK UNGU/PINK HIPNOTIS & RACUN
            // ========================================================================
            if (npc.type == NPCID.LacBeetle)
            {
                // --- 1. VISUAL AURA TEBAL & TERSEDOT (PINK/VIOLET MISTIS) ---
                if (Main.rand.NextBool(2))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        double angle = Main.rand.NextDouble() * Math.PI * 2d;
                        Vector2 spawnOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * auraRadius;
                        Vector2 dustPos = npc.Center + spawnOffset;

                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.PurpleTorch, 0f, 0f, 100, default, 1.5f);
                        d.noGravity = true;

                        Vector2 suctionDirection = npc.Center - d.position;
                        suctionDirection.Normalize();
                        d.velocity = suctionDirection * Main.rand.NextFloat(4.0f, 7.0f); 
                    }
                }

                // --- 2. EFEK DEBUFF AREA AURA ---
                if (distanceToPlayer <= auraRadius)
                {
                    target.AddBuff(BuffID.Confused, 2);
                    playerWasInLacAura = true; 
                }
                else
                {
                    if (playerWasInLacAura)
                    {
                        target.AddBuff(BuffID.Confused, 180); 
                        playerWasInLacAura = false; 
                    }
                }
            }

            return true; 
        }

        // ========================================================================
        // MEKANIK CONTACT DAMAGE: SUNTIKAN KUTUKAN SAAT PLAYER MENABRAK BEETLE
        // ========================================================================
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.CyanBeetle)
            {
                int frozenDuration = 60; 
                target.AddBuff(BuffID.Frozen, frozenDuration);
            }

            if (npc.type == NPCID.CochinealBeetle)
            {
                int stonedDuration = 60;
                target.AddBuff(BuffID.Stoned, stonedDuration);
            }

            if (npc.type == NPCID.LacBeetle)
            {
                int venomDuration = 180; // 3 Detik Acid Venom (ID 70)
                target.AddBuff(BuffID.Venom, venomDuration);
            }
        }
    }
}