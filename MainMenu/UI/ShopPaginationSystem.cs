using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.MainMenu.UI
{
    public class ShopPaginationSystem : ModSystem
    {
        internal UserInterface ShopPageUserInterface;
        internal ShopPaginationUI ShopPageUI;

        public static int CurrentPage = 0;
        public static int MaxPages = 1;
        private static int lastTargetNPCShop = -1;
        private static int lastTalkNPC = -1;
        private static bool cacheBuiltForCurrentShop = false;

        // Flag ini kita set true terus selama ada toko aktif agar UI mendeteksi kecocokan
        public static bool IsExtensionShopActive = false;

        public static List<Item> MasterShopCache = new List<Item>();
        internal static Dictionary<int, List<Func<Item>>> ExternalDLCRegistry = new Dictionary<int, List<Func<Item>>>();

        public override void Load() {
            if (!Main.dedServ) {
                ShopPageUserInterface = new UserInterface();
                ShopPageUI = new ShopPaginationUI();
                ShopPageUI.Activate();
                ShopPageUserInterface.SetState(ShopPageUI);
            }
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.playerInventory && Main.npcShop > 0) {
                DetectAndBuildShopCache();
                ShopPageUserInterface?.Update(gameTime);
            } else {
                if (cacheBuiltForCurrentShop || lastTargetNPCShop != -1) {
                    lastTargetNPCShop = -1;
                    lastTalkNPC = -1;
                    cacheBuiltForCurrentShop = false;
                    IsExtensionShopActive = false; 
                    CurrentPage = 0;
                    MaxPages = 1;
                    MasterShopCache.Clear();
                }
            }
        }

        private static void DetectAndBuildShopCache() {
            int currentTalkNPC = Main.LocalPlayer.talkNPC;

            if (lastTargetNPCShop != Main.npcShop || lastTalkNPC != currentTalkNPC) {
                lastTargetNPCShop = Main.npcShop;
                lastTalkNPC = currentTalkNPC;
                cacheBuiltForCurrentShop = false;
                CurrentPage = 0;
                MasterShopCache.Clear();
            }

            if (!cacheBuiltForCurrentShop) {
                Chest activeChest = Main.instance.shop[Main.npcShop];
                NPC currentNPC = currentTalkNPC >= 0 ? Main.npc[currentTalkNPC] : null;

                // OTOMATISASI: Semua toko yang terbuka otomatis dianggap valid menggunakan sistem halaman
                IsExtensionShopActive = true;

                MasterShopCache.Clear();

                // 1. Ambil semua item bawaan asli dari NPC tersebut (Vanilla maupun Mod lain)
                for (int i = 0; i < activeChest.item.Length; i++) {
                    if (activeChest.item[i] != null && activeChest.item[i].type != ItemID.None) {
                        MasterShopCache.Add(activeChest.item[i].Clone());
                    }
                }

                // 2. Ambil item tambahan dari DLC kustom kita secara aman (jika ada)
                if (currentNPC != null && ExternalDLCRegistry.TryGetValue(currentNPC.type, out var dlcItems)) {
                    foreach (var itemFactory in dlcItems) {
                        Item extraItem = itemFactory();
                        if (extraItem != null && extraItem.type != ItemID.None) {
                            MasterShopCache.Add(extraItem);
                        }
                    }
                }

                // 3. Kalkulasi total halaman berdasarkan jumlah item asli di dalam cache
                MaxPages = (int)Math.Ceiling(MasterShopCache.Count / 40f);

                // FIX REQ: Jika halaman hasil hitung kurang dari 3, kita paksa minimal jadi 3 halaman.
                // Ini membuat tombol 'Next' bisa diklik menuju ke halaman kosong (sesuai request-mu).
                if (MaxPages < 3) {
                    MaxPages = 3; 
                }

                cacheBuiltForCurrentShop = true;
                RefreshActiveShopWindow();
            }
        }

        public static void RefreshActiveShopWindow() {
            if (Main.npcShop <= 0) return;

            Chest activeChest = Main.instance.shop[Main.npcShop];
            
            // Bersihkan isi grid toko vanilla terlebih dahulu
            for (int i = 0; i < activeChest.item.Length; i++) {
                activeChest.item[i] = new Item();
            }

            // Isi grid toko berdasarkan halaman aktif saat ini
            int startIndex = CurrentPage * 40;
            for (int i = 0; i < 40; i++) {
                int itemIndex = startIndex + i;
                
                if (itemIndex < MasterShopCache.Count) {
                    // Jika item tersedia di cache, tampilkan itemnya
                    activeChest.item[i] = MasterShopCache[itemIndex].Clone();
                } else {
                    // Jika slot melebihi item yang ada (halaman kosong), set sebagai item kosong biasa
                    activeChest.item[i] = new Item();
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex != -1) {
                layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                    "TheSanity: Shop Pagination Buttons",
                    delegate {
                        if (Main.playerInventory && Main.npcShop > 0 && ShopPageUserInterface?.CurrentState != null) {
                            ShopPageUserInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }

    public static class SanityShopAPI
    {
        public static void RegisterDLCShopItem(int npcNetID, Func<Item> itemFactory) {
            if (!ShopPaginationSystem.ExternalDLCRegistry.ContainsKey(npcNetID)) {
                ShopPaginationSystem.ExternalDLCRegistry[npcNetID] = new List<Func<Item>>();
            }
            ShopPaginationSystem.ExternalDLCRegistry[npcNetID].Add(itemFactory);
        }

        public static void RegisterDLCShopItem(int npcNetID, int itemType) {
            RegisterDLCShopItem(npcNetID, () => {
                Item item = new Item();
                item.SetDefaults(itemType);
                return item;
            });
        }
    }
}