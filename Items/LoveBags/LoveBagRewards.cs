using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    // Reward yang PASTI didapat (tab bawah, no gacha, langsung ke-highlight).
    public struct LoveBagFixedReward
    {
        public int ItemType;
        public int Stack;
        public LoveBagFixedReward(int itemType, int stack = 1)
        {
            ItemType = itemType;
            Stack = stack;
        }
    }

    // Satu "slot" gacha: beberapa kandidat item, cuma SATU yang akhirnya dikasih ke player.
    // WinnerIndex sudah ditentukan di awal (pakai localRand) sebelum animasi mulai,
    // jadi probabilitasnya PERSIS sama kayak versi lama, cuma direveal pelan-pelan di UI.
    public class LoveBagGachaSlot
    {
        public List<(int itemType, int stack)> Candidates = new List<(int, int)>();
        public int WinnerIndex;

        // Kit-group slots (armor/weapons/hook/etc that share the Platinum-vs-Gold coinflip)
        // are revealed simultaneously in the UI. Everything else ("solo" slots like Boots,
        // Arrow, Wings) is revealed one at a time, right after the kit group settles.
        public bool IsKitGroup;

        public (int itemType, int stack) Winner => Candidates[WinnerIndex];
    }

    public class LoveBagRewardSet
    {
        public List<LoveBagFixedReward> FixedRewards = new List<LoveBagFixedReward>();
        public List<LoveBagGachaSlot> GachaSlots = new List<LoveBagGachaSlot>();
    }

    public static class LoveBagRewardBuilder
    {
        public static LoveBagRewardSet Build(Player player)
        {
            var set = new LoveBagRewardSet();

            // RNG lokal per player per panggilan (FIX gacha lama: Main.rand global bisa collide antar client).
            var localRand = new Terraria.Utilities.UnifiedRandom(
                unchecked(player.name.GetHashCode() ^ (player.whoAmI * 397) ^ (int)DateTime.Now.Ticks)
            );

            bool hasThorium = ModLoader.TryGetMod("ThoriumMod", out Mod thoriumMod);
            bool hasHomeward = ModLoader.TryGetMod("ContinentOfJourney", out Mod homewardMod);
            bool hasFargo = ModLoader.TryGetMod("Fargowiltas", out Mod fargosMutant);

            // ===================== FIXED REWARDS (SELALU DAPAT) =====================
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.LifeCrystal, 5));
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.ManaCrystal, 3));

            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.TungstenPickaxe));
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.TungstenHammer));
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.TungstenAxe));

            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.SpelunkerPotion, 3));
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.ShinePotion, 5));
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.HunterPotion, 2));
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.TrapsightPotion, 2)); // Dangersense Potion
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.MiningPotion, 3));

            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.BlandWhip));
            set.FixedRewards.Add(new LoveBagFixedReward(ItemID.FlinxStaff));

            if (hasFargo && fargosMutant.TryFind<ModItem>("BattleCry", out ModItem battleCry))
                set.FixedRewards.Add(new LoveBagFixedReward(battleCry.Type));

            if (hasThorium)
            {
                if (thoriumMod.TryFind<ModItem>("EnchantedKnife", out ModItem enchantedKnife))
                    set.FixedRewards.Add(new LoveBagFixedReward(enchantedKnife.Type, 5000));
                if (thoriumMod.TryFind<ModItem>("IceShaver", out ModItem iceShaver))
                    set.FixedRewards.Add(new LoveBagFixedReward(iceShaver.Type));
                if (thoriumMod.TryFind<ModItem>("PalmCross", out ModItem palmCross))
                    set.FixedRewards.Add(new LoveBagFixedReward(palmCross.Type));
                if (thoriumMod.TryFind<ModItem>("InspirationFragment", out ModItem inspirationFragment))
                    set.FixedRewards.Add(new LoveBagFixedReward(inspirationFragment.Type, 4));
            }

            // ===================== GACHA: KIT (Platinum vs Gold) =====================
            // Satu coinflip ("platinumWins") dipakai bareng-bareng di semua slot kit biar
            // armor + weapon + hook + mage tetap satu paket yang koheren (nggak ke-mix).
            // Semua slot ber-tag IsKitGroup = true direveal BERSAMAAN di UI (satu momen
            // "reveal besar"), baru habis itu slot solo (Boots/Arrow/Wings) jalan satu-satu.
            bool platinumWins = localRand.NextBool();

            LoveBagGachaSlot MakeKitSlot((int type, int stack) platinumOption, (int type, int stack) goldOption)
            {
                var slot = new LoveBagGachaSlot();
                slot.Candidates.Add(platinumOption);
                slot.Candidates.Add(goldOption);
                slot.WinnerIndex = platinumWins ? 0 : 1;
                slot.IsKitGroup = true;
                return slot;
            }

            set.GachaSlots.Add(MakeKitSlot((ItemID.PlatinumHelmet, 1), (ItemID.GoldHelmet, 1)));
            set.GachaSlots.Add(MakeKitSlot((ItemID.PlatinumChainmail, 1), (ItemID.GoldChainmail, 1)));
            set.GachaSlots.Add(MakeKitSlot((ItemID.PlatinumGreaves, 1), (ItemID.GoldGreaves, 1)));
            set.GachaSlots.Add(MakeKitSlot((ItemID.PlatinumBroadsword, 1), (ItemID.GoldBroadsword, 1)));
            set.GachaSlots.Add(MakeKitSlot((ItemID.PlatinumBow, 1), (ItemID.GoldBow, 1)));
            set.GachaSlots.Add(MakeKitSlot((ItemID.DiamondStaff, 1), (ItemID.RubyStaff, 1)));
            set.GachaSlots.Add(MakeKitSlot((ItemID.DiamondHook, 1), (ItemID.RubyHook, 1)));
            set.GachaSlots.Add(MakeKitSlot((ItemID.SnowballCannon, 1), (ItemID.PainterPaintballGun, 1)));

            // Snowball ammo cuma relevan kalau Platinum menang (pasangan buat Snowball Cannon).
            if (platinumWins)
                set.FixedRewards.Add(new LoveBagFixedReward(ItemID.Snowball, 5000));

            // Bard horn (Thorium) - sekarang jadi slot gacha kit BENERAN (Platinum vs Gold Bugle
            // Horn), pakai coinflip platinumWins yang sama kayak kit lain. Jadi item modded ini
            // nongol di area gacha atas (kereveal bareng kit), bukan diam-diam nyempil ke fixed.
            if (hasThorium &&
                thoriumMod.TryFind<ModItem>("PlatinumBugleHorn", out ModItem platHorn) &&
                thoriumMod.TryFind<ModItem>("GoldBugleHorn", out ModItem goldHorn))
            {
                set.GachaSlots.Add(MakeKitSlot((platHorn.Type, 1), (goldHorn.Type, 1)));
            }

            // Melee alternatif: kalau ada Homeward Journey -> Rapier & Knife-nya sekarang JUGA
            // jadi slot gacha kit beneran (Platinum vs Gold), sama-sama IsKitGroup = true biar
            // ke-reveal bareng kit lain. Kalau TIDAK ada Homeward Journey -> fallback ke slot
            // gacha Ice Blade vs Enchanted Sword (masih ikut coinflip platinumWins yang sama).
            if (hasHomeward)
            {
                if (homewardMod.TryFind<ModItem>("PlatinumRapier", out ModItem platRapier) &&
                    homewardMod.TryFind<ModItem>("GoldRapier", out ModItem goldRapier))
                {
                    set.GachaSlots.Add(MakeKitSlot((platRapier.Type, 1), (goldRapier.Type, 1)));
                }

                if (homewardMod.TryFind<ModItem>("PlatinumKnife", out ModItem platKnife) &&
                    homewardMod.TryFind<ModItem>("GoldKnife", out ModItem goldKnife))
                {
                    set.GachaSlots.Add(MakeKitSlot((platKnife.Type, 1), (goldKnife.Type, 1)));
                }
            }
            else
            {
                var meleeAltSlot = new LoveBagGachaSlot();
                meleeAltSlot.Candidates.Add((ItemID.IceBlade, 1));
                meleeAltSlot.Candidates.Add((ItemID.EnchantedSword, 1));
                meleeAltSlot.WinnerIndex = platinumWins ? 0 : 1;
                meleeAltSlot.IsKitGroup = true;
                set.GachaSlots.Add(meleeAltSlot);
            }

            // ===================== GACHA: BOOTS (independen, 3 opsi, solo reveal) =====================
            var bootsSlot = new LoveBagGachaSlot();
            bootsSlot.Candidates.Add((ItemID.SandBoots, 1));
            bootsSlot.Candidates.Add((ItemID.HermesBoots, 1));
            bootsSlot.Candidates.Add((ItemID.FlurryBoots, 1));
            bootsSlot.WinnerIndex = localRand.Next(bootsSlot.Candidates.Count);
            bootsSlot.IsKitGroup = false;
            set.GachaSlots.Add(bootsSlot);

            // ===================== GACHA: ARROW (independen, 3 opsi, solo reveal) =====================
            var arrowSlot = new LoveBagGachaSlot();
            arrowSlot.Candidates.Add((ItemID.WoodenArrow, 2000));
            arrowSlot.Candidates.Add((ItemID.FlamingArrow, 2000));
            arrowSlot.Candidates.Add((ItemID.FrostburnArrow, 2000));
            arrowSlot.WinnerIndex = localRand.Next(arrowSlot.Candidates.Count);
            arrowSlot.IsKitGroup = false;
            set.GachaSlots.Add(arrowSlot);

            // ===================== GACHA: WINGS/JUMP (1% Wings, sisanya 1 dari 4 jump item, solo reveal) =====================
            var mobilitySlot = new LoveBagGachaSlot();
            mobilitySlot.Candidates.Add((ItemID.CreativeWings, 1));     // index 0, 1% chance
            mobilitySlot.Candidates.Add((ItemID.CloudinaBottle, 1));
            mobilitySlot.Candidates.Add((ItemID.FartinaJar, 1));
            mobilitySlot.Candidates.Add((ItemID.BlizzardinaBottle, 1));
            mobilitySlot.Candidates.Add((ItemID.SandstorminaBottle, 1));

            mobilitySlot.WinnerIndex = localRand.Next(100) == 0
                ? 0
                : 1 + localRand.Next(4);
            mobilitySlot.IsKitGroup = false;
            set.GachaSlots.Add(mobilitySlot);

            return set;
        }
    }
}
