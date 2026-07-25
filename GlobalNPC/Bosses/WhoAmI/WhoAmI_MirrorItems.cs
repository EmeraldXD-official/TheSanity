using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ============================================================================================
    // PERFECT MIRROR (PAINTING) - SUMMON ITEM CHAIN
    // ============================================================================================
    // Alur lengkapnya (lihat juga WhoAmI_MirrorTile.cs buat tile lukisannya & WhoAmI_MirrorDrops.cs
    // buat drop pecahannya):
    //   1. Kalahkan Golem, Duke Fishron, Empress of Light, dan Moon Lord -> masing2 ngedrop 1
    //      pecahan cermin (GolemMirrorShard, DukeFishronMirrorShard, EmpressMirrorShard,
    //      MoonLordMirrorShard).
    //   2. Craft keempat pecahan itu jadi PerfectMirrorItem (di Ancient Manipulator, karena
    //      bahannya post-Moon Lord).
    //   3. Taruh PerfectMirrorItem ke dunia SEPERTI LUKISAN BIASA (butuh dinding di belakangnya) -
    //      nggak ada lagi "wadah kosong" terpisah, item ini sendiri yang createTile ke
    //      WhoAmIMirrorPaintingTile (lihat WhoAmI_MirrorPainting.cs). Sekali ditaruh, langsung siap
    //      dipakai buat summon - nggak ada state "kosong" lagi.
    //   4. Craft EmptyBloodBagItem, lalu right-klik buat ngambil 1 max HP permanen jadi
    //      BloodBagItem (lihat WhoAmIBloodBagRitualPlayer, karena "pakai tanpa target" kayak gini
    //      butuh dideteksi manual - item biasa cuma bisa di-"pakai" lewat left click).
    //   5. LEFT-klik (pakai item seperti biasa) DI DEPAN lukisan Perfect Mirror yang udah ditaruh
    //      sambil pegang BloodBagItem buat mulai fight (lihat BloodBagItem.UseItem, yang manggil
    //      WhoAmIMirrorPaintingTile.FindPaintingNear + TrySummon).
    //
    // Semua nilai stat/recipe di bawah ini CUMA PLACEHOLDER masuk akal - silakan di-tweak sesuai
    // balancing mod kalian (harga, recipe tier, dsb).
    // ============================================================================================

    public class GolemMirrorShard : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Items/GolemMirrorShard";
        public override void SetDefaults()
        {
            Item.width = 49;
            Item.height = 57;
            Item.rare = ItemRarityID.Yellow;
            Item.maxStack = 20;
            Item.value = Item.sellPrice(gold: 1);
        }
    }

    public class DukeFishronMirrorShard : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Items/DukeFishronMirrorShard";
        public override void SetDefaults()
        {
            Item.width = 59;
            Item.height = 98;
            Item.rare = ItemRarityID.Yellow;
            Item.maxStack = 20;
            Item.value = Item.sellPrice(gold: 1);
        }
    }

    public class EmpressMirrorShard : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Items/EmpressMirrorShard";
        public override void SetDefaults()
        {
            Item.width = 56;
            Item.height = 103;
            Item.rare = ItemRarityID.Red;
            Item.maxStack = 20;
            Item.value = Item.sellPrice(gold: 2);
        }
    }

    public class MoonLordMirrorShard : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Items/MoonLordMirrorShard";
        public override void SetDefaults()
        {
            Item.width = 54;
            Item.height = 108;
            Item.rare = ItemRarityID.Red;
            Item.maxStack = 20;
            Item.value = Item.sellPrice(gold: 2);
        }
    }

    // Cermin lengkap - sekarang jadi ITEM PENARUH TILE biasa, kayak lukisan/painting vanilla.
    // Ditaruh dengan cara dipilih sebagai held item lalu LEFT-CLICK ke tembok yang ada dindingnya
    // (persis kayak naruh painting vanilla) - createTile-nya nunjuk ke WhoAmIMirrorPaintingTile
    // (lihat WhoAmI_MirrorTile.cs). Nggak ada lagi UseItem custom / step "install ke wadah kosong":
    // begitu ditaruh, lukisannya langsung siap dipakai buat summon (lihat BloodBagItem di bawah).
    public class PerfectMirrorItem : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Items/PerfectMirrorItem";

        public override void SetDefaults()
        {
            Item.width = 116;
            Item.height = 116;
            Item.rare = ItemRarityID.Purple;
            Item.maxStack = 99;
            Item.value = Item.sellPrice(gold: 10);
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = false;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<WhoAmIMirrorPaintingTile>();
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<GolemMirrorShard>(), 1)
                .AddIngredient(ModContent.ItemType<DukeFishronMirrorShard>(), 1)
                .AddIngredient(ModContent.ItemType<EmpressMirrorShard>(), 1)
                .AddIngredient(ModContent.ItemType<MoonLordMirrorShard>(), 1)
                .AddTile(TileID.LunarCraftingStation) // Ancient Manipulator
                .Register();
        }
    }

    // Botol kosong - jadi "wadah" buat ngambil darah/max HP player sendiri.
    public class EmptyBloodBagItem : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Items/EmptyBloodBagItem";
        public override void SetDefaults()
        {
            Item.width = 35;
            Item.height = 50;
            Item.rare = ItemRarityID.Blue;
            Item.maxStack = 99;
            Item.value = Item.sellPrice(silver: 5);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Bottle, 1)
                .AddIngredient(ItemID.Glass, 2)
                .AddTile(TileID.Bottles)
                .Register();
        }
    }

    // Hasil dari EmptyBloodBagItem setelah di-right-klik (lihat WhoAmIBloodBagRitualPlayer) -
    // "kunci" buat mulai fight. Dikonsumsi 1 stack setiap berhasil mulai summon (lihat UseItem).
    public class BloodBagItem : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Items/BloodBagItem";

        // Radius (dalam pixel) seberapa jauh dari lukisan Perfect Mirror player boleh berdiri buat
        // bisa "pakai" blood bag ini di depannya. Dibikin agak longgar biar gak perlu nempel piksel.
        private const float MirrorUseRange = 220f;

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 51;
            Item.rare = ItemRarityID.Blue;
            Item.maxStack = 99;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 24;
            Item.useTime = 24;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.consumable = true;
        }

        // Blood bag ini "dipakai" dengan cara di-left-klik SAMBIL nunjuk ke lukisan Perfect Mirror
        // yang udah ditaruh di dunia (BloodBagItem dipilih sebagai held item, terus klik kiri kayak
        // pakai item biasa, sambil berdiri DI DEPAN lukisannya). Kalau nggak lagi nunjuk lukisan
        // yang valid, item ini nggak ngapa2in (return false = nggak dikonsumsi, biar nggak kebuang
        // percuma kalau player salah pencet).
        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer) return null;

            Point16? paintingPos = WhoAmIMirrorPaintingTile.FindPaintingNear(Main.MouseWorld, player.Center, MirrorUseRange);
            if (paintingPos == null)
            {
                if (player.controlUseItem)
                    Main.NewText("You need to be close to a Perfect Mirror painting to use this.", 255, 90, 90);
                return false;
            }

            bool started = WhoAmIMirrorPaintingTile.TrySummon(paintingPos.Value, player);
            if (!started)
            {
                Main.NewText("Something is already emerging from the mirror...", 255, 90, 90);
                return false;
            }

            return true; // konsumsi 1 stack, summon udah jalan di TrySummon()
        }
    }

    // ================== RIGHT-CLICK RITUAL: EMPTY BLOOD BAG -> BLOOD BAG ==================
    // Item vanilla cuma bisa "dipakai" lewat LEFT click (ModItem.UseItem). Nggak ada hook bawaan
    // buat "item di-pakai lewat RIGHT click tanpa target apapun", jadi itu dideteksi manual di sini:
    // selama player pegang EmptyBloodBagItem di tangan & nge-klik kanan (edge, bukan ditahan), dan
    // gak lagi nunjuk tile yang punya interaksi klik-kanan sendiri (chest, door, dsb - biar gak
    // ke-trigger bareng), konversi 1 stack-nya jadi BloodBagItem sambil motong 1 max HP permanen.
    public class WhoAmIBloodBagRitualPlayer : ModPlayer
    {
        // Cooldown singkat murni buat jaga2 dari input ganda dalam 1 frame yang sama, bukan buat
        // membatasi seberapa sering player boleh melakukan ritualnya (silakan dipakai berkali2,
        // itu memang risikonya sendiri buat player - tiap kali motong 1 max HP).
        private int ritualCooldown = 0;

        public override void PostUpdate()
        {
            if (ritualCooldown > 0) ritualCooldown--;
        }

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet)
        {
            if (Player.whoAmI != Main.myPlayer) return;
            if (ritualCooldown > 0) return;
            if (!Main.mouseRight || !Main.mouseRightRelease) return;

            Item held = Player.HeldItem;
            if (held == null || held.type != ModContent.ItemType<EmptyBloodBagItem>()) return;

            // Kalau player lagi nunjuk tile yang punya interaksi klik-kanan sendiri (peti, pintu,
            // dsb), biarin tile itu yang nanganin klik-kanannya - jangan rebutan sama ritual ini.
            if (Player.tileEntityAnchor.InUse) return;

            ritualCooldown = 10;

            // FIX/NOTE PENTING: Terraria nggak punya konsep "kurangi HP dasar" yang bersih - max HP
            // total = 100 (dasar) + bonus dari Life Crystal/Life Fruit (disimpan di statLifeMax2).
            // Jadi "ambil 1 max HP" di sini diimplementasikan sebagai motong statLifeMax2 sebanyak 1
            // (di-floor di 0, gak akan bikin max HP di bawah 100 dasar). Kalau HP saat ini kebetulan
            // udah pas di max HP lama, HP sekarang ikut disesuaikan turun juga biar gak nyangkut di
            // atas cap barunya.
            if (Player.statLifeMax2 > 0)
            {
                Player.statLifeMax2--;
                Player.statLifeMax = 100 + Player.statLifeMax2;
                if (Player.statLife > Player.statLifeMax) Player.statLife = Player.statLifeMax;

                // Konsumsi 1 Empty Blood Bag dari slot yang sedang dipegang, lalu kirim 1
                // BloodBagItem lewat QuickSpawnItem (bukan menimpa slot held langsung) supaya tetap
                // aman kalau stack Empty Blood Bag di slot itu lebih dari 1.
                held.stack--;
                if (held.stack <= 0) held.TurnToAir();

                Item newItem = new Item();
                newItem.SetDefaults(ModContent.ItemType<BloodBagItem>());
                Player.QuickSpawnItem(new EntitySource_Misc("WhoAmIBloodBagRitual"), newItem, 1);

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item34, Player.Center);
                CombatText.NewText(Player.getRect(), new Microsoft.Xna.Framework.Color(180, 20, 20), "-1 Max Life", true);
            }
            else
            {
                Main.NewText("You have no max life left to give.", 255, 90, 90);
            }
        }
    }
}