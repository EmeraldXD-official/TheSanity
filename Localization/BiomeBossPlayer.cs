using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class BossBiomePlayer : ModPlayer
    {
        // Gunakan PostUpdateMiscEffects karena hook ini dijamin ada di tModLoader 1.4.4+
        // dan berjalan tepat setelah kalkulasi biome vanilla selesai.
        public override void PostUpdateMiscEffects() {
            
            // 1. BRAIN OF CTHULHU -> Paksa Crimson Biome
            if (NPC.AnyNPCs(NPCID.BrainofCthulhu)) {
                Player.ZoneCrimson = true;
            }

            // 2. EATER OF WORLDS -> Paksa Corruption Biome
            if (NPC.AnyNPCs(NPCID.EaterofWorldsHead) || NPC.AnyNPCs(NPCID.EaterofWorldsBody) || NPC.AnyNPCs(NPCID.EaterofWorldsTail)) {
                Player.ZoneCorrupt = true;
            }

            // 3. QUEEN BEE, PLANTERA, GOLEM -> Paksa Jungle Biome
            if (NPC.AnyNPCs(NPCID.QueenBee) || NPC.AnyNPCs(NPCID.Plantera) || NPC.AnyNPCs(NPCID.Golem)) {
                Player.ZoneJungle = true;
            }

            // 4. DEERCLOPS -> Paksa Snow / Tundra Biome
            if (NPC.AnyNPCs(NPCID.Deerclops)) {
                Player.ZoneSnow = true;
            }
        }
    }
}