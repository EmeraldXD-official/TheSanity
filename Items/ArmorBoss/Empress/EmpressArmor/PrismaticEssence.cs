using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Empress.EmpressArmor
{
    // Placeholder material for crafting the Empress armor set.
    // Swap this out for whatever your actual boss/material drop is —
    // e.g. a drop from your own Capricorn/Empress-tier boss.
    public class PrismaticEssence : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 32;
            Item.value = Terraria.Item.sellPrice(silver: 50);
            Item.rare = ItemRarityID.LightRed;
            Item.maxStack = 999;
        }
    }

    public class PrismaticEssenceDrop : Terraria.ModLoader.GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.HallowBoss)
            {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<PrismaticEssence>(), 1, 32, 32));
            }
        }
    }
}
