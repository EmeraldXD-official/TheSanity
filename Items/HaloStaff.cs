using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class HaloStaff : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.LockOnIgnoresCollision[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 54;
            Item.knockBack = 3f;
            Item.mana = 10;
            Item.width = 38;    // Lebar 1 frame
            Item.height = 106;  // Tinggi frame
            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item44;

            Item.noMelee = true;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = ModContent.BuffType<Buff.HaloMinionBuff>();
            Item.shoot = ModContent.ProjectileType<Projectiles.HaloMinionGede>(); 

            Item.noUseGraphic = true; 
        }

        // ================== BAGIAN YANG DI-FIX ==================
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Rectangle singleFrame = new Rectangle(0, 0, 38, 106); 
            
            // FIX: Hitung ulang origin murni dari singleFrame (19f, 53f) agar pas di tengah slot inventory
            Vector2 trueOrigin = singleFrame.Size() / 2f; 
            
            spriteBatch.Draw(texture, position, singleFrame, drawColor, 0f, trueOrigin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int WhoAmI) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Rectangle singleFrame = new Rectangle(0, 0, 38, 106);
            Vector2 origin = singleFrame.Size() / 2f;
            
            // Dioptimalkan menggunakan Item.Center agar posisi drop di tanah lebih akurat
            spriteBatch.Draw(texture, Item.Center - Main.screenPosition, singleFrame, lightColor, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
        // ========================================================

        public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);

            var gedeType = ModContent.ProjectileType<Projectiles.HaloMinionGede>();
            var kecilType = ModContent.ProjectileType<Projectiles.HaloMinion>();

            bool hasLeader = player.ownedProjectileCounts[gedeType] > 0;
            int projectileToSpawn = hasLeader ? kecilType : gedeType;
            
            var p = Projectile.NewProjectileDirect(source, Main.MouseWorld, velocity, projectileToSpawn, damage, knockback, player.whoAmI);
            p.originalDamage = Item.damage;

            return false;  
        }
    }

    public class HaloStaffHeldLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            Player drawPlayer = drawInfo.drawPlayer;
            
            if (drawPlayer.dead || drawPlayer.HeldItem.type != ModContent.ItemType<HaloStaff>()) {
                return;
            }

            if (drawPlayer.itemAnimation <= 0) {
                return;
            }

            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Items/HaloStaff").Value;
            
            int frameX = 38; 
            Rectangle srcRect = new Rectangle(frameX, 0, 38, 106);

            Vector2 itemPosition = drawPlayer.MountedCenter + new Vector2(drawPlayer.direction * 2f, 1f);
            
            float rotation = drawPlayer.itemRotation;
            SpriteEffects effects = drawPlayer.direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Vector2 origin = new Vector2(drawPlayer.direction == 1 ? 9f : 29f, 98f);

            DrawData drawData = new DrawData(
                texture,
                itemPosition - Main.screenPosition,
                srcRect,
                drawInfo.itemColor,
                rotation,
                origin,
                drawPlayer.HeldItem.scale,
                effects,
                0
            );
            drawInfo.DrawDataCache.Add(drawData);
        }
    }
}