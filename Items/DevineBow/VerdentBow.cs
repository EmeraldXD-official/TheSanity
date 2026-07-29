using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures; // Wajib untuk EntitySource_ItemUse_WithAmmo

namespace TheSanity.Items.DevineBow
{
    public class VerdentBow : ModItem
    {
        // Pakai icon custom "verdentBow_icon.png" (taruh file ini di folder yang sama
        // dengan VerdentBow.cs, sejajar dengan Items/DevineBow/). Kalau kamu taruh di
        // folder lain, sesuaikan path-nya di bawah ini.
        public override string Texture => "TheSanity/Items/DevineBow/verdentBow_icon";

        // ==================== PENGATURAN TIMING COMBO ====================
        private const int BurstUseTime   = 6;   // Kecepatan tembak saat burst 3 panah (sangat cepat, ~10 tembakan/detik)
        private const int ChargeDuration = 60;  // 1 detik charge (60 tick = 1 detik di 60 FPS)
        private const int PostVolleyDelay = 30; // 0.5 detik jeda setelah volley 5 panah selesai
        private const int HomingDelay    = 20;  // Durasi fase "menyebar" sebelum VerdentArrow mulai homing (~0.33 detik)
        private const float MuzzleOffset = 24f; // Jarak titik keluar panah dari posisi player, searah arah tembak (naikkan/turunkan sesuai ukuran sprite bow)
        private const float VolleyDamageMultiplier = 1.25f; // Bonus damage untuk 5 panah volley pasca-charge, sebagai "reward" nunggu 1 detik

        // ==================== STATE MESIN COMBO ====================
        // 0,1,2 = burst 3 panah non-homing (ditembak bergantian, cepat)
        // 3     = fase charge (tidak menembak, munculkan efek cahaya ShineFlare selama 1 detik)
        // 4     = tembak 5 VerdentArrow sekaligus (menyebar dulu, lalu homing), lalu masuk jeda 0.5 detik
        private int comboState = 0;

        public override void SetDefaults()
        {
            // ==================== STAT — TIER POST-PLANTERA HARDMODE ====================
            // Referensi kasar: setara Chlorophyte Shotbow / Hallowed Repeater / Tsunami,
            // tapi damage per-panah dibuat sedikit lebih rendah karena senjata ini menembak
            // banyak panah sekaligus (burst + volley) sehingga DPS total tetap tinggi.
            // Silakan naik/turunkan angka ini sambil dites langsung in-game.
            Item.damage = 120;
            Item.knockBack = 3f;
            Item.crit = 27; // Bonus crit chance di atas base 4% pemain (total jadi ~8%)
            Item.rare = ItemRarityID.Yellow; // Rarity setara drop Plantera/Golem, sesuaikan kalau mau lebih tinggi/rendah
            Item.value = Item.sellPrice(gold: 12);
            Item.DamageType = DamageClass.Ranged;
            Item.width = 64;
            Item.height = 69;

            // Nilai awal (fase burst), akan diubah dinamis tiap fase di dalam Shoot()
            Item.useTime = BurstUseTime;
            Item.useAnimation = BurstUseTime;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.channel = true; // Wajib channel agar combo bisa berjalan otomatis selama klik ditahan

            // PENTING: Item.shoot TIDAK BOLEH 0. Kalau 0, game menganggap item ini tidak
            // menembak apa pun, dan seluruh cabang logika shoot (termasuk hook Shoot() di
            // bawah) akan DILEWATI TOTAL tanpa error apapun. Nilai di bawah ini cuma "dummy"
            // karena kita selalu override manual projectile-nya sendiri di Shoot() dan
            // return false supaya vanilla tidak ikut menembak dobel.
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 14f; // Sedikit lebih cepat dari versi sebelumnya, cocok untuk tier hardmode akhir
            Item.useAmmo = AmmoID.Arrow; // Menerima SEMUA jenis panah sebagai ammo, tapi akan selalu dikonversi jadi VerdentArrow

            // Item.UseSound sengaja TIDAK diisi di sini karena kita mainkan SFX manual per-fase
            // di dalam Shoot() (suara burst, charge, dan volley berbeda-beda).
        }

        public override Vector2? HoldoutOffset()
        {
            // Menggeser posisi sprite BOW itu sendiri saat dipegang/dipakai player.
            // X positif = geser ke arah hadap player, Y positif = geser ke bawah.
            // Ubah angka ini pelan-pelan (misal -6,-2 -> -10,-4) sambil lihat in-game
            // sampai posisi busur pas menempel di tangan, tidak melayang jauh.
            return new Vector2(-10f, -2f);
        }

        public override void UpdateInventory(Player player)
        {
            // Dipanggil TIAP TICK untuk item ini, baik sedang dipegang maupun cuma
            // duduk di inventory — beda dengan HoldItem() yang cuma jalan saat item aktif dipegang.
            bool isActivelyFiring = player.HeldItem == Item && player.itemAnimation > 0;

            if (!isActivelyFiring)
            {
                // PENTING: kita override manual Item.useTime/useAnimation tiap fase combo
                // di dalam Shoot() (6 -> 60 -> 30). Field itu MENEMPEL PERMANEN di objek
                // Item yang sama yang dipakai untuk hitung tooltip "Speed" dan basis reforge.
                // Kalau tidak direset, item bisa "nyangkut" di nilai charge (60) atau delay (30)
                // saat kamu buka inventory, sehingga Speed% di tooltip dihitung dari nilai
                // yang salah (bukan baseline burst 6) -> muncul angka aneh seperti -400%.
                // Reset ke baseline di sini memastikan tooltip & reforge selalu konsisten.
                Item.useTime = BurstUseTime;
                Item.useAnimation = BurstUseTime;

                // Reset combo ke awal juga, supaya kalau pemain berhenti lalu menembak lagi,
                // combo tidak melanjutkan dari fase terakhir yang tertinggal (misal langsung charge).
                comboState = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            int arrowType = ModContent.ProjectileType<VerdentArrow>();

            // Titik keluar panah digeser ke depan searah arah tembak sejauh MuzzleOffset,
            // supaya panah terlihat keluar dari ujung busur, bukan dari tengah badan player.
            Vector2 muzzlePosition = position + velocity.SafeNormalize(Vector2.UnitY * player.direction) * MuzzleOffset;

            switch (comboState)
            {
                case 0:
                case 1:
                case 2:
                    {
                        // ---- FASE BURST: 3 panah non-homing, ditembak bergantian dengan cepat ----
                        Vector2 burstVel = velocity.RotatedByRandom(MathHelper.ToRadians(8));
                        // ai[0] = 0f  -> non-homing biasa
                        Projectile.NewProjectile(source, muzzlePosition, burstVel, arrowType, damage, knockback, player.whoAmI, 0f, 0f);

                        // SFX tembakan cepat. Pitch di-random tipis biar 3 tembakan beruntun tidak monoton.
                        // Ganti SoundID.Item5 kalau mau suara lain (lihat catatan SoundStyle kustom di bawah).
                        SoundEngine.PlaySound(SoundID.Item5 with { Pitch = Main.rand.NextFloat(-0.15f, 0.05f), Volume = 0.8f }, player.Center);

                        Item.useTime = BurstUseTime;
                        Item.useAnimation = BurstUseTime;

                        comboState++;
                        break;
                    }

                case 3:
                    {
                        // ---- FASE CHARGE: tidak menembak, hanya memunculkan efek cahaya selama 1 detik ----
                        int chargeType = ModContent.ProjectileType<VerdentCharge>();
                        Projectile.NewProjectile(source, muzzlePosition, Vector2.Zero, chargeType, 0, 0f, player.whoAmI);

                        // SFX mulai charge. Ganti SoundID.Item29 kalau punya sound custom sendiri, contoh:
                        // public static readonly SoundStyle ChargeSound = new SoundStyle("TheSanity/Sounds/VerdentCharge");
                        // lalu panggil: SoundEngine.PlaySound(ChargeSound, player.Center);
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 1f }, player.Center);

                        Item.useTime = ChargeDuration;
                        Item.useAnimation = ChargeDuration;

                        comboState++;
                        break;
                    }

                case 4:
                default:
                    {
                        // ---- FASE VOLLEY: 5 VerdentArrow ditembak SEKALIGUS dalam formasi menyebar (fan),
                        //      lalu masing-masing baru mulai homing setelah HomingDelay tick ----
                        int volleyDamage = (int)(damage * VolleyDamageMultiplier);

                        for (int i = 0; i < 5; i++)
                        {
                            float angleOffset = MathHelper.ToRadians(-20 + i * 10); // -20, -10, 0, 20, 40 derajat
                            Vector2 volleyVel = velocity.RotatedBy(angleOffset);

                            // ai[0] = 2f          -> mode "menyebar dulu baru homing"
                            // ai[1] = HomingDelay -> berapa tick sebelum homing aktif
                            Projectile.NewProjectile(source, muzzlePosition, volleyVel, arrowType, volleyDamage, knockback, player.whoAmI, 2f, HomingDelay);
                        }

                        // SFX lepas tembakan besar, volume lebih keras dari burst biasa
                        SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.6f, Pitch = 0.1f }, player.Center);

                        Item.useTime = PostVolleyDelay;
                        Item.useAnimation = PostVolleyDelay;

                        comboState = 0; // Setelah jeda 0.5 detik, combo kembali mulai dari burst 3 panah
                        break;
                    }
            }

            // PENTING: Harus mengembalikan false agar Terraria tidak menembak arrow standar
            // dan agar amunisi tetap terkonsumsi oleh sistem item
            return false;
        }
        public override void AddRecipes()
{
    CreateRecipe()
        .AddIngredient(1508,15)      
        .AddIngredient(1006,12)      
        .AddIngredient(682, 1)      
        .AddIngredient(661, 1) 
        .AddTile(354)                    
        .Register();
}
    }
}