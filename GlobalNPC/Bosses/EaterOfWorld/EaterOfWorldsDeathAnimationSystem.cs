using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPCs
{
    public class EaterOfWorldsDeathAnimationSystem : ModSystem
    {
        public override void PostUpdateNPCs()
        {
            // Jalankan update animasi kematian setiap frame
            EaterOfWorldsHealthManager.UpdateDeathAnimation();

            // =========================================================================
            // DETEKSI SAPU JAGAT (SOLUSI FARMING)
            // Cek apakah ada segmen Eater of Worlds yang hidup di world saat ini
            // =========================================================================
            bool anyWormExist = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && (n.type == NPCID.EaterofWorldsHead || 
                                 n.type == NPCID.EaterofWorldsBody || 
                                 n.type == NPCID.EaterofWorldsTail))
                {
                    anyWormExist = true;
                    break;
                }
            }

            // Jika di map SUDAH TIDAK ADA cacing sama sekali, tapi data lama masih nyangkut,
            // PAKSA RESET total agar pemanggilan berikutnya bersih 100%!
            if (!anyWormExist && (EaterOfWorldsHealthManager.DeathAnimationActive || 
                                  NPCs.EaterJantung.HeartDestroyed || 
                                  EaterOfWorldsHealthManager.FinalSegmentKilled))
            {
                EaterOfWorldsHealthManager.ResetStaticState();
            }
        }
    }
}