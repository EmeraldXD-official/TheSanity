using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Systems;

namespace TheSanity.Items
{
    public class RoyalBonePack : ModItem
    {
        public override string Texture => "TheSanity/Items/RoyalBonePack"; 

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 30;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<CostumeRarity.SanityRarity>();
            Item.value = Item.sellPrice(0, 9, 15, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            // Aktifkan flag pendukung di ModPlayer kamu
            player.GetModPlayer<RoyalBonePackPlayer>().hasRoyalBonePack = true;

            // 1. EFEK ROYAL GEL: Menjinakkan seluruh Slime (Termasuk modded Calamity/Thorium)
            for (int i = 1; i < NPCLoader.NPCCount; i++) {
                if (ContentSamples.NpcsByNetId.TryGetValue(i, out NPC sampleNPC)) {
                    if (sampleNPC.aiStyle == 1) { 
                        player.npcTypeNoAggro[i] = true;
                    }
                }
            }

            // 2. EFEK HIVE PACK: Memperkuat lebah dan tawon
            player.strongBees = true;

            // 3. EFEK BONE GLOVE: Melempar tulang silang saat menyerang
            player.boneGloveItem = Item;

            // =========================================================================
            // 🌟 SISTEM VISUAL AKSESORIS (Sama seperti referensi Evil Belt)
            // =========================================================================
            if (!hideVisual) {
                // Visual Hive Pack (Dipasang di slot punggung / Back)
                player.back = ContentSamples.ItemsByType[ItemID.HiveBackpack].backSlot;

                // Visual Bone Glove (Dipasang di slot kedua belah tangan / Hand On & Hand Off)
                player.handon = ContentSamples.ItemsByType[ItemID.BoneGlove].handOnSlot;
                player.handoff = ContentSamples.ItemsByType[ItemID.BoneGlove].handOffSlot;
                
                // PERBAIKAN UTAMA: Memaksa visual kepala player memakai topi kepala rusa dari Bone Helm
                player.head = ContentSamples.ItemsByType[ItemID.BoneHelm].headSlot;
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.RoyalGel)
                .AddIngredient(ItemID.HiveBackpack)
                .AddIngredient(ItemID.BoneGlove)
                .AddIngredient(ItemID.BoneHelm)
                .AddIngredient(ItemID.FlinxFur, 2)
                .AddIngredient(ItemID.Bone, 4)
                .AddIngredient(ItemID.Gel, 10)
                .AddIngredient(ItemID.Stinger, 2)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}