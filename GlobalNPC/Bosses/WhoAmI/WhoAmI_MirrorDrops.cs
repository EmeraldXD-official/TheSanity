using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // Ngasih 1 pecahan cermin tiap kali salah satu dari 4 boss ini dikalahkan (dijamin drop,
    // nggak digantung ke expert/master seed rate, biar progression-nya predictable). Kalau mau
    // dibikin expert-only atau ada RNG-nya, tinggal tambahin syarat di masing2 IF di bawah.
    public class WhoAmIMirrorShardDrops : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => false;

        public override void OnKill(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return; // drop cuma dieksekusi di server/singleplayer

            int dropType = 0;
            if (npc.type == NPCID.Golem) dropType = ModContent.ItemType<GolemMirrorShard>();
            else if (npc.type == NPCID.DukeFishron) dropType = ModContent.ItemType<DukeFishronMirrorShard>();
            else if (npc.type == NPCID.HallowBoss) dropType = ModContent.ItemType<EmpressMirrorShard>(); // Empress of Light
            else if (npc.type == NPCID.MoonLordCore) dropType = ModContent.ItemType<MoonLordMirrorShard>(); // "true" Moon Lord body

            if (dropType != 0)
                Item.NewItem(npc.GetSource_Loot(), npc.getRect(), dropType, 1);
        }
    }
}