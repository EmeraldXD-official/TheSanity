using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SanityDeerThing : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.DeerThing; 

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item44;
            Item.consumable = false; // Unconsumable
        }

        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(NPCID.Deerclops);
        }

        public override bool? UseItem(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int npcIndex = NPC.NewNPC(player.GetSource_ItemUse(Item), (int)player.position.X, (int)player.position.Y - 400, NPCID.Deerclops);
                
                if (npcIndex < Main.maxNPCs) {
                    Main.npc[npcIndex].target = player.whoAmI;
                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }
            return true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
            TooltipLine descLine = new TooltipLine(Mod, "SummonDesc", "[c/FF4500:Summons Deerclops anywhere]");
            tooltips.Add(unconsumableLine);
            tooltips.Add(descLine);
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient(ItemID.DeerThing, 1).Register();
            Recipe.Create(ItemID.DeerThing).AddIngredient(Type, 1).Register();
        }
    }
}