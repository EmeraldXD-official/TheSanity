using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Microsoft.Xna.Framework;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Players
{
    public class WhaleCursePlayer : ModPlayer
    {
        public bool lockCameraToMoon = false;
        public bool isInventoryErased = false; // Flag status kutukan
        public bool immuneToErasure = false;   // BARU: Flag imunitas permanen dari Gluttony Fruit
        public int trackBossIndex = -1; // Melacak index posisi boss secara dinamis

        public override void SaveData(TagCompound tag) {
            tag.Add("isInventoryErased", isInventoryErased);
            tag.Add("immuneToErasure", immuneToErasure); // Menyimpan status anti-kutukan
        }

        public override void LoadData(TagCompound tag) {
            isInventoryErased = tag.GetBool("isInventoryErased");
            immuneToErasure = tag.GetBool("immuneToErasure"); // Memuat status anti-kutukan
        }

        // Memanipulasi kamera agar mengarah ke langit/bulan saat dipanggil
        public override void ModifyScreenPosition() {
            if (trackBossIndex != -1 && Main.npc[trackBossIndex].active && Main.npc[trackBossIndex].type == ModContent.NPCType<WhiteWhaleBoss>()) {
                NPC boss = Main.npc[trackBossIndex];
                Main.screenPosition = boss.Center - new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);
            }
            else if (lockCameraToMoon) {
                Main.screenPosition.Y = (float)Main.worldSurface * 16f - 2500f;
                Main.screenPosition.X = Player.Center.X - (Main.screenWidth / 2);
            }
        }

        // Cek jika player mati oleh White Whale
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            // Kutukan hanya aktif jika player belum memiliki imunitas permanen
            if (!immuneToErasure && damageSource.SourceNPCIndex >= 0 && Main.npc[damageSource.SourceNPCIndex].type == ModContent.NPCType<WhiteWhaleBoss>()) {
                isInventoryErased = true;
                Main.NewText("Your existence has been erased... Your items have lost their souls.", Color.Purple);
            }
        }

        // Kunci total interaksi inventory jika terkena efek erasure
        public override void PostUpdate() {
            if (isInventoryErased) {
                for (int i = 0; i < Player.inventory.Length; i++) {
                    Item item = Player.inventory[i];
                    
                    if (!item.IsAir && !IsTool(item)) {
                        if (Main.mouseItem == item) {
                            Main.mouseItem = new Item(); // Kosongkan tangan mouse agar tidak bisa dipindahkan
                        }
                    }
                }
            }
        }

        // Validasi pembatasan penggunaan item
        public override bool CanUseItem(Item item) {
            // BYPASS: Jika item yang digunakan adalah Gluttony Fruit, izinkan player memakannya meskipun sedang dikutuk!
            if (item.type == ModContent.ItemType<global::TheSanity.Items.GluttonyFruit>()) {
                return true;
            }

            if (isInventoryErased && !IsTool(item)) {
                return false; // Tidak bisa digunakan/dimakan/diminum item lain selain tool
            }
            return base.CanUseItem(item);
        }

        // Helper untuk mengecek apakah item tersebut alat potong/gali yang diizinkan
        private bool IsTool(Item item) {
            return item.pick > 0 || item.axe > 0 || item.hammer > 0;
        }
    }
}