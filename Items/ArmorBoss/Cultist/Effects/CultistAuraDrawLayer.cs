using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Effects
{
    /// <summary>
    /// Draws the Celestial Seal aura around the player while the full Cultist set is worn:
    /// an outer ring ("Lightning_Ritual") and an inner diamond seal ("Extra_34"), rotating
    /// in opposite directions - outer clockwise, inner counter-clockwise - like two gears
    /// turning against each other.
    /// </summary>
    public class CultistAuraDrawLayer : PlayerDrawLayer
    {
        // radians per tick - tweak these to speed up/slow down either layer independently
        private const float OuterRotationSpeed = 0.012f;  // clockwise (positive)
        private const float InnerRotationSpeed = -0.02f;  // counter-clockwise (negative)

        public override Position GetDefaultPosition()
        {
            // draw it behind the player's body/head, in front of the skin
            return new Between(PlayerDrawLayers.Skin, PlayerDrawLayers.Head);
        }

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            var cp = drawInfo.drawPlayer.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>();
            return cp.HasSetBonus;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Terraria.Player player = drawInfo.drawPlayer;
            Vector2 center = player.Center - Main.screenPosition
                             + new Vector2(0f, player.gfxOffY);

            float outerRotation = Main.GameUpdateCount * OuterRotationSpeed;
            float innerRotation = Main.GameUpdateCount * InnerRotationSpeed;

            Texture2D ringTexture = ModContent.Request<Texture2D>("TheSanity/Items/ArmorBoss/Cultist/Effects/LightningRitualRing").Value;
            Texture2D sealTexture = ModContent.Request<Texture2D>("TheSanity/Items/ArmorBoss/Cultist/Effects/ExtraSeal").Value;

            Color auraColor = Color.White * 0.85f;

            // scale the aura so it roughly matches the graze-aura radius (see CultistPlayer.GrazeRadius)
            float auraScale = 0.55f;

            DrawData ringData = new DrawData(
                ringTexture,
                center,
                null,
                auraColor,
                outerRotation,
                ringTexture.Size() / 2f,
                auraScale,
                SpriteEffects.None,
                0)
            {
                shader = 0
            };

            DrawData sealData = new DrawData(
                sealTexture,
                center,
                null,
                auraColor,
                innerRotation,
                sealTexture.Size() / 2f,
                auraScale,
                SpriteEffects.None,
                0)
            {
                shader = 0
            };

            drawInfo.DrawDataCache.Add(ringData);
            drawInfo.DrawDataCache.Add(sealData);
        }
    }
}
