using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.IceWand; // TODO: ganti "TheSanity" sesuai namespace mod kamu

namespace TheSanity.Items.IceWand
{
    // Simpan sprite IceWand.png di folder yang sama dengan file ini (Items/Weapons/IceWand.png)
    public class IceWand : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 145;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 40;
            Item.width = 40;
            Item.height = 40;

            // Sprite asli IceWand.png ukurannya 98x101px (jauh lebih besar dari
            // wand vanilla yang biasanya ~32-40px), makanya kalau di-hold jadi
            // gede banget dan posisinya kelihatan aneh di tangan player.
            // scale = 0.4 bikin ukuran tampilnya kira-kira 39x40px, pas sama
            // Item.width/height di atas. Tweak angka ini kalau masih kurang pas.
            Item.scale = 0.4f;

            // useTime kecil karena kita pakai channel, animasi "hold" ditangani manual di controller
            Item.useTime = 10;
            Item.useAnimation = 10;
            // FIX: HoldUp itu pose "angkat lurus ke atas" (kayak Wall of Flesh / Guide
            // Voodoo Doll) - makanya wand-nya keliatan ngambang jauh dari tangan pas
            // di-channel. Ganti ke Shoot: pose standar wand/staff, sprite-nya ngarah ke
            // cursor dan nempel di tangan kayak wand pada umumnya.
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.channel = true;                   // bisa ditahan (hold) tombolnya

            Item.noMelee = true;
            Item.knockBack = 0f;
            Item.value = Item.sellPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = null; // suara ice dimainkan sendiri oleh projectile, bukan saat klik

            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<IceChargeController>();
            Item.shootSpeed = 0f;
        }

        public override bool CanUseItem(Player player)
        {
            // Cegah player nge-spawn controller baru selagi controller sebelumnya masih aktif
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI &&
                    p.type == ModContent.ProjectileType<IceChargeController>())
                {
                    return false;
                }
            }
            return true;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            position = player.MountedCenter;
        }

        // FIX: HoldoutOffset cuma jalan buat item dengan useStyle Shoot (yang sekarang
        // kita pakai) - dipakai buat geser titik pegangan sprite biar pas nempel di
        // tangan player. Angka di bawah cuma starting point, kemungkinan masih perlu
        // di-tweak lagi sambil lihat langsung in-game.
        // (X positif = geser ke kanan, Y positif = geser ke bawah)
        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-4f, -2f);
        }
    }
}