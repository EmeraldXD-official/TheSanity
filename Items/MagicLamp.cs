using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Items
{
    public class MagicLamp : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 116;
            Item.height = 113;
            Item.scale = 0.4f; 

            Item.damage = 45;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 6;
            Item.knockBack = 2f;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 4;
            Item.useAnimation = 4;
            Item.useTurn = true;

            Item.channel = true;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = false;

            Item.value = Item.sellPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item43;

            Item.shoot = ModContent.ProjectileType<Projectiles.MagicLampTrail>();
            Item.shootSpeed = 0f;
        }

        public override void HoldItem(Player player)
        {
            // Arah hadap saat channeling ngikutin posisi CURSOR (kiri/kanan dari player),
            // BUKAN tombol gerak (A/D). Ini supaya player tetap nembak ke arah yang di-aim
            // walau lagi jalan/mundur ke arah berlawanan (sebelumnya pakai A/D bikin stutter
            // kalau gerak melawan arah tembakan).
            //
            // CATATAN: ini AMAN dari bug "kubah" yang dulu pernah muncul, karena yang dipakai
            // di sini cuma NILAI DISKRIT ±1 (kiri/kanan dari player.direction), bukan sudut
            // penuh dari Main.MouseWorld seperti penyebab bug sebelumnya (lihat komentar di
            // MagicLampTrail.GetSpoutPosition). Offset ujung teko tetap cuma bisa "kiri" atau
            // "kanan", tidak pernah continuous, jadi tidak ada radiate/fan dari titik yang sama.
            if (player.channel)
            {
                if (Main.MouseWorld.X < player.Center.X)
                    player.direction = -1;
                else if (Main.MouseWorld.X > player.Center.X)
                    player.direction = 1;
                // kalau mouse persis di tengah player -> direction ga diubah, tetep arah terakhir
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            return !HasActiveTrail(player);
        }

        private static bool HasActiveTrail(Player player)
        {
            int trailType = ModContent.ProjectileType<Projectiles.MagicLampTrail>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != trailType || proj.owner != player.whoAmI)
                    continue;

                if (proj.ModProjectile is Projectiles.MagicLampTrail trail && !trail.IsDying)
                    return true;
            }
            return false;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            Texture2D texture = TextureAssets.Item[Item.type].Value;
            spriteBatch.Draw(texture, position, frame, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int shovelStyle)
        {
            scale = 1f;
            return true;
        }
    }
}