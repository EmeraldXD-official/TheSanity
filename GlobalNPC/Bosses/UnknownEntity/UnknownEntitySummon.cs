using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Conditions;
using TheSanity.Items.OreBar.Regilia;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    // Item pemanggil boss Unknown Entity. Gak bisa dipakai kalau bossnya udah ada di dunia
    // (CanUseItem). Spawn-nya lewat NPC.NewNPC, yang di tModLoader modern sudah network-aware
    // sendiri (otomatis authoritative di server walau dipanggil dari client MP).
    //
    // CATATAN LOKALISASI: DisplayName/Tooltip dari hjson gak bisa di-set lewat kode lagi di
    // tModLoader 1.4.4+ (itu batasan resmi). Tapi ModifyTooltips() TETAP bisa dipakai dari kode
    // buat nambahin baris tooltip - jadi kita gak perlu buka/edit file hjson sama sekali.
    // DisplayName-nya sendiri otomatis fallback ke "Unknown Shard" (dipecah dari nama class)
    // kalau hjson-nya kosong/belum ada, jadi udah pas tanpa perlu diedit manual.
    public class UnknownShard : ModItem
    {
        // GANTI path ini sesuai lokasi sprite asli item-nya di project kamu.
        public override string Texture => "TheSanity/GlobalNPC/Bosses/UnknownEntity/UnknownEntitySummon";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.sellPrice(gold: 5);
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.UseSound = SoundID.Item162;
            Item.consumable = false;
            Item.noMelee = true;
        }

        public override bool CanUseItem(Player player)
        {
            // Gak bisa dipakai kalau boss ini udah aktif di dunia (cegah double-spawn).
            return !NPC.AnyNPCs(ModContent.NPCType<UnknownEntity>());
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
            {
                // NPC.NewNPC di tModLoader sudah network-aware sendiri: kalau dipanggil dari
                // client di multiplayer, dia otomatis minta server yang spawn (gak perlu
                // NetMessage/MessageID manual - itu yang kemarin bikin error karena nama
                // member-nya beda-beda tiap versi tModLoader).
                NPC.NewNPC(player.GetSource_ItemUse(Item), (int)player.Center.X, (int)player.Center.Y - 100, ModContent.NPCType<UnknownEntity>());
            }

            return true;
        }

        // Tooltip lewat kode - gak nyentuh hjson sama sekali.
       public override void ModifyTooltips(List<TooltipLine> tooltips)
{
    tooltips.Add(new TooltipLine(Mod, "UnknownShardFlavor1", "A shard of fractured reality, pulsing with an unknown presence.")
    {
        OverrideColor = new Color(190, 130, 255)
    });
    tooltips.Add(new TooltipLine(Mod, "UnknownShardFlavor2", "Use to summon Unknown Entity.")
    {
        OverrideColor = new Color(200, 200, 200)
    });
    tooltips.Add(new TooltipLine(Mod, "UnknownShardWarning", "Cannot be used while Unknown Entity is alives.")
    {
        OverrideColor = new Color(255, 120, 120)
    });
}

        public override void AddRecipes()
        {
            // Belum ada resep default - tinggal tambahin sendiri sesuai bahan yang kamu mau,
            // contoh:
             CreateRecipe()
                 .AddIngredient(ItemID.SoulofNight, 7)
                 .AddIngredient(ItemID.SoulofLight, 10)
                 .AddIngredient(ItemID.LunarBar, 18)
                 .AddIngredient(ItemID.FragmentNebula, 10)
                 .AddIngredient<ReligiaBar>(16)
                 .AddTile(TileID.LunarCraftingStation)
                 .Register();
        }
    }
}