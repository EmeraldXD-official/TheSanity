using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ============================================================================================
    // ANTI-CHEESE: kunci SENJATA (bukan seluruh inventory) di inventory + block SEMUA UI storage
    // (chest biasa, piggy bank, safe, defender's forge, void vault) selagi WhoAmI (boss asli ATAU
    // mirage decoy - dua-duanya sama2 NPCType<WhoAmI>()) masih ada di dunia.
    //
    // KENAPA INI PERLU: syarat summon (WhoAmI.TryCheckWeaponRequirement, WhoAmI_Helpers.cs) cuma
    // DICEK SEKALI - pas klik blood bag buat mulai fight. Tanpa lock ini, player bisa "pinjem"
    // senjata secukupnya buat lolos syarat pas summon, terus begitu boss udah beneran spawn, drop
    // semuanya / titip ke chest terdekat / piggy bank, balik lagi ke build sebenernya buat fight-nya -
    // syarat minimal 8 senjata per class jadi cuma formalitas kosong tanpa efek nyata di fight-nya.
    //
    // IMPLEMENTASI: manfaatin sistem "Favorite" bawaan vanilla (item.favorited) - item yang
    // favorited nggak bisa di-drop (baik lewat tombol drop maupun di-drag keluar inventory ke
    // dunia), nggak bisa di-quick-trash, dan nggak bisa di-quick-stack ke chest terdekat. Itu
    // nutupin "didrop" & "quick-stack ke chest" dari permintaan awal. Buat nutupin "dipindahin ke
    // chest" secara MANUAL (drag satu-satu ke slot chest, yang favorite SENDIRI nggak nge-block
    // itu), kita paksa tutup paksa Player.chest setiap tick selama boss idup - jadi UI chest/piggy
    // bank/safe/dll nggak akan pernah kebuka sama sekali selama fight, jadi nggak ada kesempatan
    // buat drag manual ke situ juga.
    //
    // Senjata yang KITA sendiri yang maksa jadi favorited (bukan yang emang udah difavoritkan
    // player sendiri sebelum fight mulai) dicatat per-slot di `autoFavoritedSlots`, dan di-un-favorite
    // lagi begitu boss-nya udah nggak ada - biar player bebas atur ulang inventory-nya kayak biasa
    // begitu fight beres, dan favorite ASLI milik player (yang udah dia set sendiri dari awal) tetap
    // dibiarin nyala, nggak ikut kelepas.
    // ============================================================================================
    public class WhoAmIInventoryLockPlayer : ModPlayer
    {
        private readonly HashSet<int> autoFavoritedSlots = new HashSet<int>();
        private bool wasLockActive = false;

        private static bool IsBossAlive => NPC.AnyNPCs(ModContent.NPCType<WhoAmI>());

        public override void PreUpdate()
        {
            bool lockActive = IsBossAlive;

            if (lockActive)
            {
                // Paksa tutup SEMUA UI storage tiap tick - chest biasa & piggy bank/safe/defender's
                // forge/void vault semuanya lewat field Player.chest yang sama (cuma beda index
                // negatifnya buat yang bukan chest dunia), jadi 1 pengecekan ini nutupin semuanya
                // sekaligus. Cuma nge-print pesan sekali per percobaan buka (begitu di-set -1 lagi,
                // pengecekan tick berikutnya udah nggak ke-trigger sampai player coba buka lagi).
                if (Player.chest != -1)
                {
                    Player.chest = -1;
                    Player.chestX = -1;
                    Player.chestY = -1;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    if (Player.whoAmI == Main.myPlayer)
                        Main.NewText("You can't access storage while the mirror still holds its reflection of you.", 255, 90, 90);
                }

                for (int i = 0; i < 50 && i < Player.inventory.Length; i++)
                {
                    Item item = Player.inventory[i];
                    if (item == null || item.IsAir) continue;
                    if (!WhoAmI.IsWeaponItem(item)) continue;

                    if (!item.favorited)
                    {
                        item.favorited = true;
                        autoFavoritedSlots.Add(i);
                    }
                }
            }
            else if (wasLockActive)
            {
                // Fight baru aja berakhir (boss mati / despawn / dibatalin dari menu kekalahan) -
                // lepas CUMA favorite yang kita paksa sendiri.
                foreach (int slot in autoFavoritedSlots)
                {
                    if (slot < 0 || slot >= Player.inventory.Length) continue;
                    Item item = Player.inventory[slot];
                    if (item != null && !item.IsAir) item.favorited = false;
                }
                autoFavoritedSlots.Clear();
            }

            wasLockActive = lockActive;
        }
    }
}
