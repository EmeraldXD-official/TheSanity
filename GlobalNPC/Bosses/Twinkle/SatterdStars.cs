using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class SatterdStars : ModItem
    {
        // Jalur langsung ke file gambar SatterdStars.png di foldermu
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Twinkle/SatterdStars";

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 3; // Jumlah untuk duplikasi di Journey Mode
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999; // Batas stack tModLoader v1.4 terbaru
            Item.rare = ModContent.RarityType<CostumeRarity.TwinkleRarity>(); // Memakai Rarity Twinkle kustom yang berpartikel emas!
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp; // Gaya mengangkat tangan ke atas saat dipakai
            Item.consumable = true; // Item berkurang/habis setelah dipakai
        }

        public override bool CanUseItem(Player player) {
            // Mengunci agar item tidak bisa dipakai jika boss "Twinkle" sudah aktif/hidup di map
            return !NPC.AnyNPCs(ModContent.NPCType<Twinkle>()); 
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                // Memutar suara raungan boss vanilla saat item diaktifkan
                SoundEngine.PlaySound(SoundID.Roar, player.position);

                int npcType = ModContent.NPCType<Twinkle>();
                
                // KODE DIPERBAIKI: NPC.SpawnOnPlayer secara internal SUDAH otomatis 
                // mengurusi sinkronisasi Multiplayer ke server jika kamu main bareng teman.
                NPC.SpawnOnPlayer(player.whoAmI, npcType);
            }
            return true;
        }

        // --- SISTEM RESEP CRAFTING ---
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.ManaCrystal, 1)        // 1 Mana Crystal
                .AddIngredient(ItemID.TissueSample, 2)       // 2 Tissue Sample (Crimson)
                .AddIngredient(ItemID.ShadowScale, 2)        // 2 Shadowscale (Corruption)
                .AddIngredient(ItemID.PurificationPowder, 5) // 5 Purification Powder
                .AddTile(TileID.Anvils)                      // Dibuat di Iron/Lead Anvil
                .Register();
        }
    }
}