using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace TheSanity.Items
{
    // Visual state of a single reward slot.
    public enum LoveBagSlotVisualState
    {
        Hidden,     // not revealed yet ("?") - used by fixed rewards before the gacha finishes
        Preview,    // ghosted preview of a gacha candidate, before rolling starts
        Rolling,    // actively flickering through candidates
        Revealed    // settled on the final item
    }

    public enum LoveBagRewardKind
    {
        Gacha,
        Fixed
    }

    // Satu kotak slot: background panel (gradient) + icon item + jumlah stack + heart FX.
    public class LoveBagSlotElement : UIElement
    {
        public int DisplayItemType;
        public int DisplayStack;
        public LoveBagSlotVisualState State = LoveBagSlotVisualState.Hidden;
        public LoveBagRewardKind Kind = LoveBagRewardKind.Fixed;

        private static readonly Color EmptyColor = new Color(45, 12, 24);
        private static readonly Color RollingColorA = new Color(80, 230, 130);
        private static readonly Color RollingColorB = new Color(255, 225, 120);
        private static readonly Color FixedRevealColor = new Color(235, 175, 70);
        private static readonly Color GachaRevealColor = new Color(255, 120, 175);

        private struct HeartParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public float RotationSpeed;
        }

        private readonly List<HeartParticle> hearts = new List<HeartParticle>();
        private static readonly Random heartRand = new Random();

        public LoveBagSlotElement(int width, int height)
        {
            Width.Set(width, 0f);
            Height.Set(height, 0f);
        }

        public void SetDisplay(int itemType, int stack)
        {
            DisplayItemType = itemType;
            DisplayStack = stack;
        }

        // Call once when a slot locks onto its final item (gacha settle or fixed reveal).
        // Spawns a couple of drifting hearts.
        public void PlayRevealBurst()
        {
            CalculatedStyle dims = GetDimensions();
            Vector2 center = new Vector2(dims.X + dims.Width * 0.5f, dims.Y + dims.Height * 0.5f);

            const int heartCount = 2;
            for (int i = 0; i < heartCount; i++)
            {
                float angle = MathHelper.TwoPi * i / heartCount + MathHelper.PiOver4;
                float speed = 0.35f + (float)heartRand.NextDouble() * 0.4f;
                hearts.Add(new HeartParticle
                {
                    Position = center,
                    Velocity = angle.ToRotationVector2() * speed - new Vector2(0f, 0.4f),
                    MaxLife = 32 + heartRand.Next(10),
                    Life = 32 + heartRand.Next(10),
                    Scale = 0.22f + (float)heartRand.NextDouble() * 0.15f,
                    Rotation = 0f,
                    RotationSpeed = ((float)heartRand.NextDouble() - 0.5f) * 0.05f
                });
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            for (int i = hearts.Count - 1; i >= 0; i--)
            {
                var h = hearts[i];
                h.Position += h.Velocity;
                h.Velocity *= 0.985f;
                h.Rotation += h.RotationSpeed;
                h.Life -= 1f;
                if (h.Life <= 0f)
                {
                    hearts.RemoveAt(i);
                    continue;
                }
                hearts[i] = h;
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            Rectangle rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // ---------- background (soft vertical gradient, not a flat fill) ----------
            Color topColor, bottomColor;
            switch (State)
            {
                case LoveBagSlotVisualState.Rolling:
                    float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GameUpdateCount * 0.25f);
                    Color rollColor = Color.Lerp(RollingColorA, RollingColorB, pulse);
                    topColor = rollColor;
                    bottomColor = rollColor * 0.5f;
                    break;
                case LoveBagSlotVisualState.Revealed:
                    Color baseColor = Kind == LoveBagRewardKind.Fixed ? FixedRevealColor : GachaRevealColor;
                    topColor = Color.Lerp(baseColor, Color.White, 0.1f);
                    bottomColor = baseColor * 0.5f;
                    break;
                default:
                    topColor = EmptyColor;
                    bottomColor = EmptyColor * 0.7f;
                    break;
            }
            DrawVerticalGradient(spriteBatch, rect, topColor, bottomColor);

            // border
            Color borderColor = State == LoveBagSlotVisualState.Revealed ? Color.White * 0.6f : Color.White * 0.25f;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), borderColor);

            if (State == LoveBagSlotVisualState.Hidden)
            {
                // Not revealed yet - a question mark instead of an empty void.
                string mark = "?";
                Vector2 size = FontAssets.MouseText.Value.MeasureString(mark) * 1.1f;
                Vector2 pos = rect.Center.ToVector2() - size * 0.5f;
                Utils.DrawBorderString(spriteBatch, mark, pos, Color.White * 0.4f, 1.1f);
            }
            else if (DisplayItemType > 0)
            {
                Main.instance.LoadItem(DisplayItemType);
                Texture2D itemTexture = TextureAssets.Item[DisplayItemType].Value;

                Rectangle sourceRect = itemTexture.Bounds;
                if (Main.itemAnimations[DisplayItemType] != null)
                {
                    sourceRect = Main.itemAnimations[DisplayItemType].GetFrame(itemTexture);
                }

                float scale = Math.Min((rect.Width - 10f) / sourceRect.Width, (rect.Height - 10f) / sourceRect.Height);
                scale = Math.Min(scale, 1f);

                // Dimmer ghost look while previewing, full brightness once revealed.
                float iconAlpha = State == LoveBagSlotVisualState.Preview ? 0.55f : 1f;

                Vector2 halfSize = new Vector2(sourceRect.Width, sourceRect.Height) * 0.5f * scale;
                Vector2 pos = rect.Center.ToVector2() - halfSize;

                spriteBatch.Draw(itemTexture, pos, sourceRect, Color.White * iconAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                if (DisplayStack > 1)
                {
                    Utils.DrawBorderString(spriteBatch, DisplayStack.ToString(),
                        new Vector2(rect.X + 4, rect.Bottom - 18), Color.White, 0.85f);
                }
            }

            // ---------- drifting heart particles on top ----------
            if (hearts.Count > 0)
            {
                Texture2D heartTex = TextureAssets.Item[Terraria.ID.ItemID.Heart].Value;
                Vector2 heartOrigin = new Vector2(heartTex.Width, heartTex.Height) * 0.5f;
                foreach (var h in hearts)
                {
                    float lifeFrac = h.Life / h.MaxLife;
                    spriteBatch.Draw(heartTex, h.Position, null, Color.HotPink * lifeFrac, h.Rotation, heartOrigin, h.Scale, SpriteEffects.None, 0f);
                }
            }

            if (ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
                if (State != LoveBagSlotVisualState.Hidden && DisplayItemType > 0)
                {
                    Main.HoverItem = new Item();
                    Main.HoverItem.SetDefaults(DisplayItemType);
                    Main.instance.MouseText(Main.HoverItem.Name);
                }
            }
        }

        private static void DrawVerticalGradient(SpriteBatch spriteBatch, Rectangle rect, Color top, Color bottom)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            const int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                Color c = Color.Lerp(top, bottom, t);
                int y = rect.Y + (int)(rect.Height * (i / (float)steps));
                int h = (int)Math.Ceiling(rect.Height / (float)steps) + 1;
                spriteBatch.Draw(pixel, new Rectangle(rect.X, y, rect.Width, h), c);
            }
        }

    }
}
