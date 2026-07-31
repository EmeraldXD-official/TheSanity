using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Betsy.Players
{
    // Menempelkan sprite sheet BetsyChestplate_Back.png (20 frame, berkibar) di punggung player
    // secara otomatis ketika full set Betsy dipakai -- persis seperti mekanisme cangkang
    // Turtle/Beetle Shell yang built-in di vanilla.
    public class BetsyBackLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.BackAcc, PlayerDrawLayers.Skin);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            return player.head == ModContent.ItemType<Items.ArmorBoss.Betsy.Armor.BetsyMask>()
                && player.body == ModContent.ItemType<Items.ArmorBoss.Betsy.Armor.BetsyChestplate>()
                && player.legs == ModContent.ItemType<Items.ArmorBoss.Betsy.Armor.BetsyLeggings>();
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;

            // NOTE: idealnya texture khusus back-cape ("BetsyChestplate_Back", 20 frame @ 40x56)
            // di-cache sekali di field static, bukan di-request tiap frame draw (ini cukup untuk versi awal).
            int frameHeight = 56;
            int totalFrames = 20;
            int currentFrame = (int)(Main.GameUpdateCount / 5) % totalFrames; // ganti frame tiap 5 tick

            var backTexture = ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(
                "BetsyArmor/Items/Armor/BetsyChestplate_Back").Value;

            Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, backTexture.Width, frameHeight);
            Vector2 position = player.position - Main.screenPosition + new Vector2(player.width / 2f - backTexture.Width / 2f, player.height - frameHeight);
            Vector2 origin = new Vector2(backTexture.Width / 2f, frameHeight / 2f);

            DrawData data = new DrawData(
                backTexture,
                position,
                sourceRect,
                Lighting.GetColor(player.Center.ToTileCoordinates()),
                0f,
                origin,
                1f,
                player.direction == -1 ? Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally : Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
                0);

            drawInfo.DrawDataCache.Add(data);
        }
    }
}
