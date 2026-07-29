using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // Nama class diawali 'AA' agar dieksekusi paling pertama oleh tModLoader sebelum 100+ file Rework-mu!
    public class AA_OOAFixer : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool PreAI(NPC npc)
        {
            // SARINGAN ABSOLUT: Mengecek semua musuh Old One's Army berdasarkan data yang kamu kasih
            if (npc.type == NPCID.DD2EterniaCrystal || npc.type == NPCID.DD2Betsy ||
                // Goblin & Bomber (T1 - T3)
                npc.type == NPCID.DD2GoblinT1 || npc.type == NPCID.DD2GoblinT2 || npc.type == NPCID.DD2GoblinT3 ||
                npc.type == NPCID.DD2GoblinBomberT1 || npc.type == NPCID.DD2GoblinBomberT2 || npc.type == NPCID.DD2GoblinBomberT3 ||
                // Javelinst & Wyvern (T1 - T3)
                npc.type == NPCID.DD2JavelinstT1 || npc.type == NPCID.DD2JavelinstT2 || npc.type == NPCID.DD2JavelinstT3 ||
                npc.type == NPCID.DD2WyvernT1 || npc.type == NPCID.DD2WyvernT2 || npc.type == NPCID.DD2WyvernT3 ||
                // Dark Mage & Skeleton (T1 & T3)
                npc.type == NPCID.DD2DarkMageT1 || npc.type == NPCID.DD2DarkMageT3 ||
                npc.type == NPCID.DD2SkeletonT1 || npc.type == NPCID.DD2SkeletonT3 ||
                // Wither Beast & Drakin (T2 & T3)
                npc.type == NPCID.DD2WitherBeastT2 || npc.type == NPCID.DD2WitherBeastT3 ||
                npc.type == NPCID.DD2DrakinT2 || npc.type == NPCID.DD2DrakinT3 ||
                // Kobold Walker & Flyer (T2 & T3)
                npc.type == NPCID.DD2KoboldWalkerT2 || npc.type == NPCID.DD2KoboldWalkerT3 ||
                npc.type == NPCID.DD2KoboldFlyerT2 || npc.type == NPCID.DD2KoboldFlyerT3 ||
                // Ogre (T2 & T3) & Lightning Bug (T3 Only)
                npc.type == NPCID.DD2OgreT2 || npc.type == NPCID.DD2OgreT3 ||
                npc.type == NPCID.DD2LightningBugT3)
            {
                // DETEKSI PENYELAMAT: Jika target eror, di luar array, atau player mati/tidak ada
                if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
                {
                    // Paksa musuh OOA mencari target Player sah terdekat di map secara instan
                    npc.TargetClosest(true);

                    // Pengaman darurat tingkat akhir: kalau masih gagal, paksa oper ke indeks Player 0 (Host/Singleplayer)
                    if (npc.target < 0 || npc.target >= Main.maxPlayers)
                    {
                        npc.target = 0; 
                    }
                }
            }
            return true; // Biarkan game melanjutkan AI bawaannya dengan target yang sudah kita jinakkan
        }
    }
}