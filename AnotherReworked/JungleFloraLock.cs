using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class JungleFloraLock : GlobalTile
    {
        // =========================================================================
        // [BALANCING MECHANIC: MENGUNCI PICKAXE]
        // Mengembalikan nilai 'false' akan membuat blok kebal terhadap Pickaxe/Drill apa pun
        // =========================================================================
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
        {
            if ((type == TileID.Chlorophyte || type == TileID.LifeFruit) && !NPC.downedPlantBoss)
            {
                return false; 
            }
            return base.CanKillTile(i, j, type, ref blockDamaged);
        }

        // =========================================================================
        // [BALANCING MECHANIC: MENGUNCI EXPLOSIVES]
        // Mencegah eksploitasi player yang mencoba meledakkan Life Fruit menggunakan Dynamite
        // =========================================================================
        public override bool CanExplode(int i, int j, int type)
        {
            if ((type == TileID.Chlorophyte || type == TileID.LifeFruit) && !NPC.downedPlantBoss)
            {
                return false; 
            }
            return base.CanExplode(i, j, type);
        }
    }
}