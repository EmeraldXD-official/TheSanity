using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Betsy.Players
{
    // Portal Betsy (NPC_549 -- bentuk lubang oval berapi) -- digambar SEBELUM semua layer vanilla
    // (BeforeFirstVanillaLayer), jadi portal ini jadi lapisan paling belakang dari SEMUA bagian
    // player (skin, armor, wings, cape, dll semua akan digambar DI ATASnya).
    // TIDAK jiggle -- cuma animasi frame-nya jalan (portal spin lewat sprite sheet), posisinya diam.
    public class BetsyPortalLayer : PlayerDrawLayer
    {
        // NPC_549.png: sprite sheet vertikal, 8 frame @ 142x204 per frame.
        private const int PortalFrameWidth = 142;
        private const int PortalFrameHeight = 204;
        private const int PortalFrameCount = 8;

        // Ganti frame animasi tiap sekian tick.
        private const int TicksPerFrame = 4;

        public override Position GetDefaultPosition() => PlayerDrawLayers.BeforeFirstVanillaLayer;

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            // Flag resmi dari BetsyMask.UpdateArmorSet() lewat BetsyArmorPlayer.fullSetActive.
            return drawInfo.drawPlayer.GetModPlayer<BetsyArmorPlayer>().fullSetActive;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;

            var portalTexture = ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(
                "BetsyArmor/Effects/NPC_549").Value;

            int frame = (int)(Main.GameUpdateCount / TicksPerFrame) % PortalFrameCount;
            Rectangle source = new Rectangle(0, frame * PortalFrameHeight, PortalFrameWidth, PortalFrameHeight);
            Vector2 origin = new Vector2(PortalFrameWidth / 2f, PortalFrameHeight / 2f);

            // Pakai player.Center (titik tengah hitbox player secara world-space), bukan
            // position + width/2 -- lebih presisi karena gak kegeser kalau lebar sprite equip
            // beda dari lebar hitbox player.
            Vector2 position = player.Center - Main.screenPosition + new Vector2(0f, -PortalFrameHeight * 0.2f);

            DrawData data = new DrawData(
                portalTexture,
                position,
                source,
                Lighting.GetColor(player.Center.ToTileCoordinates()),
                0f, // rotasi 0 tetap -- portal gak jiggle, cuma animasi frame yang jalan
                origin,
                1f,
                player.direction == -1
                    ? Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally
                    : Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
                0);

            drawInfo.DrawDataCache.Add(data);
        }
    }
}