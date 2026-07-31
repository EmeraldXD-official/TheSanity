using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Summons.WandOfMirrorImage
{
    // ================================================================================================
    // WAND OF MIRROR IMAGE - senjata summon yang manggil "bayangan" dari player sendiri.
    // Minion-nya (WandOfMirrorImageMinion) niru sprite/armor/acc player, dan bisa nge-copy senjata apa
    // pun yang lagi dipegang player buat nyerang - lihat komentar lengkap di WandOfMirrorImageMinion.cs
    // buat detail cara kerja damage-class switching & pengurangan damage 80%-nya.
    //
    // CATATAN PENTING buat yang masukin ini ke project:
    //  - Sesuaikan namespace di atas kalau struktur folder project kamu beda dari
    //    "TheSanity.Items.Summons.WandOfMirrorImage".
    //  - Taruh sprite hasil resize (WandOfMirrorImage25_192x192.png -> jadi .png biasa buat item,
    //    tModLoader gak baca .webp) di folder yang sama dengan file ini, nama file HARUS sama persis
    //    dengan nama class ("WandOfMirrorImageItem.png") kecuali kamu override Texture di bawah.
    // ================================================================================================
    public class WandOfMirrorImageItem : ModItem
    {
        // Ganti path ini kalau nama file sprite / folder kamu beda.
        public override string Texture => "TheSanity/Items/Summons/WandOfMirrorImage/WandOfMirrorImageItem";

        // Sesuai tema "Mirror Image" - 1x pakai manggil beberapa bayangan sekaligus, biar formasi
        // (lingkaran/barisan) di WandOfMirrorImageMinion.cs kelihatan bentuknya, bukan cuma 1 titik.
        // Ganti angka ini kalau mau lebih sedikit/banyak.
        private const int MirrorImageCount = 3;

        public override void SetStaticDefaults()
        {
            // Cuma boleh ada 1 "bayangan" aktif per player dalam satu waktu - minion ini niru
            // player secara penuh (termasuk senjata), jadi kalau dibiarin stack banyak bakal
            // kelewat kuat & berat buat sinkronisasi visual per minion-nya.
            ProjectileID.Sets.MinionTargettingFeature[ModContent.ProjectileType<WandOfMirrorImageMinion>()] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.sellPrice(gold: 5);
            Item.UseSound = SoundID.Item44;

            // ---- Setup standar senjata Summon (staff/wand) ----
            Item.DamageType = DamageClass.Summon;
            Item.damage = 1; // damage dasar item ini sendiri gak kepake - semua damage nyata datang
                              // dari senjata yang di-copy minion, dihitung sendiri di dalam minion.
            Item.mana = 10;
            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 0f;
            Item.buffType = ModContent.BuffType<WandOfMirrorImageBuff>();
            Item.shoot = ModContent.ProjectileType<WandOfMirrorImageMinion>();
            Item.shootSpeed = 10f;

            // 1 slot minion - niru player sepenuhnya sudah cukup kuat buat 1 slot standar.
            // Naikin ke 2 kalau di testing kerasa masih terlalu lemah dibanding cost slot-nya.
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2) return null;

            if (player.whoAmI == Main.myPlayer)
            {
                // Manggil beberapa bayangan sekaligus, tapi tetap hormatin sisa minion slot player -
                // pola ini SAMA kayak staff vanilla yang manggil >1 minion per klik (misal Optic Staff):
                // cek ownedProjectileCounts < maxMinions SEBELUM tiap spawn, dan naikin itung-itungannya
                // manual di dalam loop biar cek berikutnya akurat (data frame lalu + yang baru disepak).
                for (int n = 0; n < MirrorImageCount; n++)
                {
                    if (player.ownedProjectileCounts[Item.shoot] >= player.maxMinions) break;

                    // Sebar arah tembak awal dikit biar 3 bayangan gak numpuk persis di 1 titik pas
                    // baru muncul (posisi finalnya nanti diatur formasi di AI() minion-nya sendiri).
                    float spreadAngle = Microsoft.Xna.Framework.MathHelper.ToRadians((n - (MirrorImageCount - 1) / 2f) * 20f);
                    Microsoft.Xna.Framework.Vector2 velocity =
                        (new Microsoft.Xna.Framework.Vector2(0f, Item.shootSpeed)).RotatedBy(spreadAngle);

                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        player.Center,
                        velocity,
                        Item.shoot,
                        Item.damage,
                        Item.knockBack,
                        player.whoAmI
                    );

                    player.ownedProjectileCounts[Item.shoot]++;
                }
            }

            player.AddBuff(Item.buffType, 2);
            return true;
        }
    }
}