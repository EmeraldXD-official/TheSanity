using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class LoveBag : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.rare = ItemRarityID.Pink;
        }

        public override bool CanRightClick()
        {
            return true;
        }

        public override void RightClick(Player player)
        {
            var source = player.GetSource_OpenItem(Type);

            // ==========================================
            // [GUIDE LOCATION 1: PENGECEKAN MOD EKSTERNAL]
            // Internal name Homeward Journey: ContinentOfJourney
            // ==========================================
            bool hasThorium = ModLoader.TryGetMod("ThoriumMod", out Mod thoriumMod);
            bool hasHomeward = ModLoader.TryGetMod("ContinentOfJourney", out Mod homewardMod);

            // ==========================================
            // 1. UNIVERSAL KIT (Dapat Semua + Fargo's + Thorium)
            // ==========================================
            player.QuickSpawnItem(source, ItemID.LifeCrystal, 5);
            player.QuickSpawnItem(source, ItemID.ManaCrystal, 3);
            
            // ==========================================
            // [GUIDE LOCATION 1A: UNIVERSAL TOOLS]
            // Di sini lokasi untuk mengatur starter tools universal.
            // Kamu bisa mengganti ItemID berikut ke tier lain jika ingin balancing.
            // ==========================================
            player.QuickSpawnItem(source, ItemID.TungstenPickaxe);
            player.QuickSpawnItem(source, ItemID.TungstenHammer);
			player.QuickSpawnItem(source, ItemID.TungstenAxe);
            
            // Tambahan Potion
            player.QuickSpawnItem(source, ItemID.SpelunkerPotion, 3);
            player.QuickSpawnItem(source, ItemID.ShinePotion, 5);
            player.QuickSpawnItem(source, ItemID.HunterPotion, 2);
            player.QuickSpawnItem(source, ItemID.TrapsightPotion, 2); // Ini adalah Dangersense Potion
            player.QuickSpawnItem(source, ItemID.MiningPotion, 3);

            // Fargo's Mutant Mod (Battle Cry)
            if (ModLoader.TryGetMod("Fargowiltas", out Mod fargosMutant))
            {
                if (fargosMutant.TryFind<ModItem>("BattleCry", out ModItem battleCry))
                {
                    player.QuickSpawnItem(source, battleCry.Type);
                }
            }

            // Thorium Mod (Thrower, Healer, Bard Fragments)
            if (hasThorium)
            {
                if (thoriumMod.TryFind<ModItem>("EnchantedKnife", out ModItem enchantedKnife))
                    player.QuickSpawnItem(source, enchantedKnife.Type, 5000); // Starter Thrower
                
                if (thoriumMod.TryFind<ModItem>("IceShaver", out ModItem iceShaver))
                    player.QuickSpawnItem(source, iceShaver.Type); // Starter Healer 1
                
                if (thoriumMod.TryFind<ModItem>("PalmCross", out ModItem palmCross))
                    player.QuickSpawnItem(source, palmCross.Type); // Starter Healer 2
                
                if (thoriumMod.TryFind<ModItem>("InspirationFragment", out ModItem inspirationFragment))
                    player.QuickSpawnItem(source, inspirationFragment.Type, 4); // Bard Resources
            }

            // ==========================================
            // 2. SUMMONER KIT (Dapat Semua)
            // ==========================================
            player.QuickSpawnItem(source, ItemID.BlandWhip);
            player.QuickSpawnItem(source, ItemID.FlinxStaff);

            // ==========================================
            // 3. UTAMA KIT (Acak Kit 1 atau Kit 2)
            // ==========================================
            if (Main.rand.NextBool())
            {
                // Kit 1: Platinum
                player.QuickSpawnItem(source, ItemID.PlatinumHelmet);
                player.QuickSpawnItem(source, ItemID.PlatinumChainmail);
                player.QuickSpawnItem(source, ItemID.PlatinumGreaves);
                player.QuickSpawnItem(source, ItemID.PlatinumBroadsword);
                player.QuickSpawnItem(source, ItemID.PlatinumBow);
                
                // Mage Kit Platinum
                player.QuickSpawnItem(source, ItemID.DiamondStaff);

                // [GUIDE LOCATION 2: HOOK KIT 1]
                // Diamond Hook dimasukkan khusus ke Kit 1 (Platinum)
                player.QuickSpawnItem(source, ItemID.DiamondHook);

                // Tambahan Ranged Kit 1
                player.QuickSpawnItem(source, ItemID.SnowballCannon);
                player.QuickSpawnItem(source, ItemID.Snowball, 5000);
                
                // Tambahan Bard Class Thorium (Platinum)
                if (hasThorium && thoriumMod.TryFind<ModItem>("PlatinumBugleHorn", out ModItem platHorn))
                {
                    player.QuickSpawnItem(source, platHorn.Type);
                }

                // ==========================================
                // [GUIDE LOCATION 3: MELEE ALTERNATIF KIT 1]
                // Jika ada Homeward Journey = Platinum Rapier & Knife
                // Jika TIDAK ADA Homeward Journey = Ice Blade
                // ==========================================
                if (hasHomeward)
                {
                    if (homewardMod.TryFind<ModItem>("PlatinumRapier", out ModItem platRapier))
                        player.QuickSpawnItem(source, platRapier.Type);
                        
                    if (homewardMod.TryFind<ModItem>("PlatinumKnife", out ModItem platKnife))
                        player.QuickSpawnItem(source, platKnife.Type, 1); 
                }
                else
                {
                    player.QuickSpawnItem(source, ItemID.IceBlade);
                }
            }
            else
            {
                // Kit 2: Gold
                player.QuickSpawnItem(source, ItemID.GoldHelmet);
                player.QuickSpawnItem(source, ItemID.GoldChainmail);
                player.QuickSpawnItem(source, ItemID.GoldGreaves);
                player.QuickSpawnItem(source, ItemID.GoldBroadsword);
                player.QuickSpawnItem(source, ItemID.GoldBow);

                // Mage Kit Gold
                player.QuickSpawnItem(source, ItemID.RubyStaff);

                // [GUIDE LOCATION 4: HOOK KIT 2]
                // Ruby Hook dimasukkan khusus ke Kit 2 (Gold)
                player.QuickSpawnItem(source, ItemID.RubyHook);

                // Tambahan Ranged Kit 2
                player.QuickSpawnItem(source, ItemID.PainterPaintballGun);

                // Tambahan Bard Class Thorium (Gold)
                if (hasThorium && thoriumMod.TryFind<ModItem>("GoldBugleHorn", out ModItem goldHorn))
                {
                    player.QuickSpawnItem(source, goldHorn.Type);
                }

                // ==========================================
                // [GUIDE LOCATION 5: MELEE ALTERNATIF KIT 2]
                // Jika ada Homeward Journey = Gold Rapier & Knife
                // Jika TIDAK ADA Homeward Journey = Enchanted Sword
                // ==========================================
                if (hasHomeward)
                {
                    if (homewardMod.TryFind<ModItem>("GoldRapier", out ModItem goldRapier))
                        player.QuickSpawnItem(source, goldRapier.Type);
                        
                    if (homewardMod.TryFind<ModItem>("GoldKnife", out ModItem goldKnife))
                        player.QuickSpawnItem(source, goldKnife.Type, 1);
                }
                else
                {
                    player.QuickSpawnItem(source, ItemID.EnchantedSword);
                }
            }

            // ==========================================
            // 4. BOOTS (Acak 1 dari 3)
            // ==========================================
            int[] bootsList = new int[] { ItemID.SandBoots, ItemID.HermesBoots, ItemID.FlurryBoots };
            int chosenBoots = Main.rand.Next(bootsList);
            player.QuickSpawnItem(source, chosenBoots);

            // ==========================================
            // 5. ARROW (Acak 1 dari 3, Stack 2000)
            // ==========================================
            int[] arrowList = new int[] { ItemID.WoodenArrow, ItemID.FlamingArrow, ItemID.FrostburnArrow };
            int chosenArrow = Main.rand.Next(arrowList);
            player.QuickSpawnItem(source, chosenArrow, 2000);

            // ==========================================
            // 6. DOUBLE JUMP ATAU WINGS (1% Chance Wings)
            // ==========================================
            if (Main.rand.Next(100) == 0) 
            {
                player.QuickSpawnItem(source, ItemID.CreativeWings); // Fledgling Wings
            }
            else
            {
                int[] jumpList = new int[] { ItemID.CloudinaBottle, ItemID.FartinaJar, ItemID.BlizzardinaBottle, ItemID.SandstorminaBottle };
                int chosenJump = Main.rand.Next(jumpList);
                player.QuickSpawnItem(source, chosenJump);
            }
        }
    }
}