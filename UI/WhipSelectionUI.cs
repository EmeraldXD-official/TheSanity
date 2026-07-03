using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Audio;
using TheSanity.Rarities;
using TheSanity.Items.Whips;

namespace TheSanity
{
    public class WhipSelectionUIState : UIState
    {
        private UIPanel containerPanel;
        private UIText titleText;

        public override void OnInitialize() {
            containerPanel = new UIPanel();
            containerPanel.SetPadding(10);
            containerPanel.Left.Set(-295f, 0.5f);
            containerPanel.Top.Set(-60f, 0.5f);
            containerPanel.Width.Set(590f, 0f);
            containerPanel.Height.Set(115f, 0f);
            containerPanel.BackgroundColor = new Color(23, 23, 33, 240);

            Color fallbackColor = new Color(60, 255, 200);
            containerPanel.BorderColor = fallbackColor;

            titleText = new UIText("Select Whip Tag Sub-Power", 0.6f, true);
            titleText.HAlign = 0.5f;
            titleText.Top.Set(10f, 0f);
            titleText.TextColor = fallbackColor;
            containerPanel.Append(titleText);

            Func<int>[] allWhips = new Func<int>[] {
                () => ItemID.BlandWhip,
                () => ItemID.ThornWhip,
                () => ModContent.ItemType<FeatherWireWhip>(),
                () => ItemID.BoneWhip,
                () => ItemID.FireWhip,
                () => ModContent.ItemType<AbyssalKrakenTentacle>(),
                () => ItemID.CoolWhip,
                () => ItemID.SwordWhip,
                () => ItemID.ScytheWhip,
                () => ItemID.MaceWhip,
                () => ItemID.RainbowWhip
            };

            float buttonWidth = 44f;
            float spacingX = 50f;
            float totalRowWidth = ((allWhips.Length - 1) * spacingX) + buttonWidth;
            float startX = (570f - totalRowWidth) / 2f;

            for (int i = 0; i < allWhips.Length; i++) {
                WhipSlotButton button = new WhipSlotButton(allWhips[i]);
                button.Left.Set(startX + (i * spacingX), 0f);
                button.Top.Set(45f, 0f);
                button.Width.Set(buttonWidth, 0f);
                button.Height.Set(44f, 0f);
                
                button.OnLeftClick += (evt, element) => {
                    WhipSlotButton clickedButton = (WhipSlotButton)element;
                    if (clickedButton.TargetWhipID > 0) {
                        Main.LocalPlayer.GetModPlayer<BlackwhipPlayer>().selectedWhipTagType = clickedButton.TargetWhipID;
                        SoundEngine.PlaySound(SoundID.MenuTick);
                        ModContent.GetInstance<BlackwhipSystem>().CloseWhipUI();
                    }
                };

                containerPanel.Append(button);
            }
            Append(containerPanel);
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);
            if (containerPanel != null && titleText != null) {
                var rarity = ModContent.GetInstance<BlackwhipRarity>();
                if (rarity != null) {
                    Color currentRarityColor = rarity.RarityColor;
                    containerPanel.BorderColor = currentRarityColor;
                    titleText.TextColor = currentRarityColor;
                }
            }
        }
    }

    public class WhipSlotButton : UIElement
    {
        private readonly Func<int> _getItemID;
        public int TargetWhipID => _getItemID();
        public bool Hovered;

        public WhipSlotButton(Func<int> getItemID) {
            _getItemID = getItemID;
        }

        public override void MouseOver(UIMouseEvent evt) {
            base.MouseOver(evt);
            Hovered = true;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void MouseOut(UIMouseEvent evt) {
            base.MouseOut(evt);
            Hovered = false;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch) {
            int currentID = TargetWhipID;
            if (currentID <= 0) return;

            CalculatedStyle style = GetInnerDimensions();
            Texture2D slotBackTex = TextureAssets.InventoryBack.Value;
            Color boxColor = Hovered ? Color.LightGreen : Color.White * 0.8f;
            
            if (Main.LocalPlayer.GetModPlayer<BlackwhipPlayer>().selectedWhipTagType == currentID) {
                var rarity = ModContent.GetInstance<BlackwhipRarity>();
                boxColor = rarity != null ? rarity.RarityColor : new Color(60, 255, 200);
            }

            spriteBatch.Draw(slotBackTex, style.Position(), null, boxColor, 0f, Vector2.Zero, style.Width / slotBackTex.Width, SpriteEffects.None, 0f);
            
            Main.instance.LoadItem(currentID);
            Texture2D whipItemTex = TextureAssets.Item[currentID].Value;
            
            float itemScale = 1f;
            if (whipItemTex.Width > style.Width || whipItemTex.Height > style.Height) {
                itemScale = style.Width / Math.Max(whipItemTex.Width, whipItemTex.Height) * 0.75f;
            }

            Vector2 innerCenter = new Vector2(style.Width, style.Height) / 2f;
            Vector2 drawPosition = style.Position() + innerCenter;
            spriteBatch.Draw(whipItemTex, drawPosition, null, Color.White, 0f, whipItemTex.Size() / 2f, itemScale, SpriteEffects.None, 0f);

            if (Hovered) {
                Item hoverDummy = new Item();
                hoverDummy.SetDefaults(currentID);
                Main.hoverItemName = hoverDummy.Name;
            }
        }
    }
}