// ============================================================================
// FILE: TheSanity/GlobalNPCs/BrainBossBarModifier.cs
// ============================================================================
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.DataStructures;
using Microsoft.Xna.Framework.Graphics;
using TheSanity.NPCs;

namespace TheSanity.GlobalNPCs
{
    public class BrainBossBarModifier : GlobalBossBar
    {
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
        {
            if (npc.type != NPCID.BrainofCthulhu)
                return true;

            // Hitung jumlah Creeper tipe 0 (orbit boss) yang masih hidup
            int aliveRingCount = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC creeper = Main.npc[i];
                if (creeper.active && creeper.type == ModContent.NPCType<CustomCreeper>() && creeper.ai[0] == 0)
                {
                    aliveRingCount++;
                }
            }

            // Hitung jumlah ConfusionMinion yang masih hidup
            int aliveMinionCount = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC minion = Main.npc[i];
                if (minion.active && minion.type == ModContent.NPCType<ConfusionMinion>())
                {
                    aliveMinionCount++;
                }
            }

            // Total shield value = jumlah creeper ring + jumlah minion
            float shieldValue = aliveRingCount + aliveMinionCount;

            // Shield max = MaxRingCreepers + MaxMinions (total potensial)
            float shieldMaxValue = BrainofCthulhuRework.MaxRingCreepers + BrainofCthulhuRework.MaxMinions;

            // Jika tidak ada shield, set 0
            if (shieldValue <= 0)
            {
                drawParams.Shield = 0f;
                drawParams.ShieldMax = 0f;
            }
            else
            {
                drawParams.Shield = shieldValue;
                drawParams.ShieldMax = Math.Max(shieldMaxValue, shieldValue); // pastikan max tidak lebih kecil dari value
            }

            return true;
        }
    }
}