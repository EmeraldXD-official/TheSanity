using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using Microsoft.Xna.Framework.Graphics;

namespace TheSanity.Items
{
    public class EclipsaBlade : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 75; 
            Item.DamageType = DamageClass.Melee;
            Item.width = 64;
            Item.height = 64;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = Item.sellPrice(gold: 10);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.scale = 2f;   
        }

        public override void MeleeEffects(Player player, Rectangle hitbox)
        {
            // Menambahkan lebih banyak dust saat swing untuk efek jejak (After-image style)
            if (Main.rand.NextBool(1)) // Spawn lebih sering (setiap frame)
            {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                int dust = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, dustType);
                
                Main.dust[dust].noGravity = true; 
                Main.dust[dust].velocity *= 1.5f;   // Kecepatan agar terlihat menyambar
                Main.dust[dust].scale = 1.5f;      // Sedikit lebih besar untuk kesan "tebal"
                Main.dust[dust].fadeIn = 1f;       // Pudar dengan halus
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            float spawnX = target.Center.X + Main.rand.Next(-100, 101);
            float spawnY = target.Center.Y - 600f;

            Projectile.NewProjectile(
                player.GetSource_OnHit(target),
                new Vector2(spawnX, spawnY),
                new Vector2(0, 10f),
                ModContent.ProjectileType<EclipsaStrike>(),
                hit.Damage,
                hit.Knockback,
                player.whoAmI
            );
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
        {
            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Items/EclipsaBlade").Value;
            spriteBatch.Draw(texture, Item.position - Main.screenPosition, null, Color.White * 0.5f, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
         public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(1570,1)
                .AddIngredient(426,1)
                .AddIngredient(2308,3)
                .AddIngredient(65,1)
                .AddIngredient(548, 5)
                .AddTile(134)
                .Register();
        }
    }
}