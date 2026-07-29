using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;
namespace TheSanity.Items.Acc.VoidNecklace
{
    [AutoloadEquip(EquipType.Neck)] // 👈 Atribut untuk menampilkan visual di leher pemain
    public class VoidNecklace : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 28;
            Item.accessory = true; // Ditetapkan sebagai Aksesoris[cite: 1]
            Item.value = Item.sellPrice(0, 1, 50, 0); //[cite: 1]
            Item.rare = ItemRarityID.Orange; //[cite: 1]
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 🔮 Bonus Stat All-Class:
            player.GetDamage(DamageClass.Generic) += 0.05f; // +5% Semua Damage[cite: 1]
            player.GetArmorPenetration(DamageClass.Generic) += 8; // +8 Armor Penetration[cite: 1]
        }

        public override void AddRecipes()
        {
            // 🛠️ Resep Crafting (Menggunakan AmbariumBar + Soul of Night)[cite: 1]
            CreateRecipe()
                .AddIngredient<AmbariumBar>(10) // Sesuaikan namespace AmbariumBar milikmu[cite: 1]
                .AddIngredient(ItemID.SoulofNight, 6) //[cite: 1]
                .AddIngredient(3212) //[cite: 1]
                .AddTile(TileID.Anvils) //[cite: 1]
                .Register(); //[cite: 1]
        }
    }
}