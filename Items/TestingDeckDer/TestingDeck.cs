using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.TestingDeckDer
{
    public class TestingDeck : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Cyan;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.UseSound = SoundID.Item8; // Suara laser/sihir saat spawn
            Item.autoReuse = true; // Tekan tahan LMB untuk spawn terus menerus
        }

        public override bool AltFunctionUse(Player player) => true; // Mengaktifkan fungsi klik kanan

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2) // Jika Klik Kanan
            {
                Item.useStyle = ItemUseStyleID.HoldUp;
                Item.UseSound = SoundID.MenuOpen;
            }
            else // Jika Klik Kiri (LMB)
            {
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.UseSound = SoundID.Item8;
            }
            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer) return true;

            if (player.altFunctionUse == 2)
            {
                // Klik kanan membuka GUI
                TestingDeckSystem.ToggleUI();
            }
            else
            {
                // Klik Kiri (LMB): Mengeluarkan projectile aktif ke arah / posisi Cursor
                int type = TestingDeckSystem.SelectedProjectileType;
                if (type > 0)
                {
                    Vector2 spawnPosition = Main.MouseWorld; // Tepat di posisi kursor
                    
                    // Membuat velocity/kecepatan default menghadap arah kursor dari arah player
                    Vector2 velocity = Main.MouseWorld - player.Center;
                    if (velocity != Vector2.Zero) velocity.Normalize();
                    velocity *= 8f; // Kecepatan gerak projectile standard

                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        spawnPosition,
                        velocity,
                        type,
                        100, // Damage standard testing
                        3f,  // Knockback
                        player.whoAmI
                    );
                }
                else
                {
                    Main.NewText("Belum ada projectile yang dipilih! Buka GUI dengan Klik Kanan atau Hotkey.", Color.Yellow);
                }
            }
            return true;
        }
    }
}