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

namespace TheSanity
{
    public class WhipSelectionUIState : UIState
    {
        private UIPanel containerPanel;
        private UIText titleText;

        public override void OnInitialize() {
            containerPanel = new UIPanel();
            containerPanel.SetPadding(10);
            containerPanel.Left.Set(-245f, 0.5f); 
            containerPanel.Top.Set(-60f, 0.5f);   
            containerPanel.Width.Set(490f, 0f);   
            containerPanel.Height.Set(115f, 0f);
            containerPanel.BackgroundColor = new Color(23, 23, 33, 240); 

            // SOLUSI: Gunakan warna teal default saat loading awal agar tidak NullReferenceException
            Color fallbackColor = new Color(60, 255, 200);
            containerPanel.BorderColor = fallbackColor; 

            titleText = new UIText("Select Whip Tag Sub-Power", 0.6f, true);
            titleText.HAlign = 0.5f; 
            titleText.Top.Set(10f, 0f); 
            titleText.TextColor = fallbackColor;
            containerPanel.Append(titleText);

            int[] allVanillaWhips = {
                ItemID.BlandWhip,   
                ItemID.ThornWhip,   
                ItemID.BoneWhip,    
                ItemID.FireWhip,    
                ItemID.CoolWhip,    
                ItemID.SwordWhip,   
                ItemID.ScytheWhip,  
                ItemID.MaceWhip,    
                ItemID.RainbowWhip  
            };

            float startX = 13f;   
            float spacingX = 50f; 

            for (int i = 0; i < allVanillaWhips.Length; i++) {
                int whipItemID = allVanillaWhips[i];
                WhipSlotButton button = new WhipSlotButton(whipItemID);
                button.Left.Set(startX + (i * spacingX), 0f);
                button.Top.Set(45f, 0f); 
                button.Width.Set(44f, 0f);
                button.Height.Set(44f, 0f);
                
                button.OnLeftClick += (evt, element) => {
                    Main.LocalPlayer.GetModPlayer<BlackwhipPlayer>().selectedWhipTagType = whipItemID;
                    SoundEngine.PlaySound(SoundID.MenuTick); 
                    ModContent.GetInstance<BlackwhipSystem>().CloseWhipUI(); 
                };

                containerPanel.Append(button);
            }
            Append(containerPanel);
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            if (containerPanel != null && titleText != null) {
                // Ambil rarity dengan aman menggunakan null-check
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
        public int TargetWhipID;
        public bool Hovered;

        public WhipSlotButton(int whipItemID) {
            TargetWhipID = whipItemID;
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
            CalculatedStyle style = GetInnerDimensions();
            
            Texture2D slotBackTex = TextureAssets.InventoryBack.Value;
            Color boxColor = Hovered ? Color.LightGreen : Color.White * 0.8f;
            
            if (Main.LocalPlayer.GetModPlayer<BlackwhipPlayer>().selectedWhipTagType == TargetWhipID) {
                // Ambil rarity secara aman di dalam proses draw game
                var rarity = ModContent.GetInstance<BlackwhipRarity>();
                boxColor = rarity != null ? rarity.RarityColor : new Color(60, 255, 200); 
            }

            spriteBatch.Draw(slotBackTex, style.Position(), null, boxColor, 0f, Vector2.Zero, style.Width / slotBackTex.Width, SpriteEffects.None, 0f);

            Main.instance.LoadItem(TargetWhipID);
            Texture2D whipItemTex = TextureAssets.Item[TargetWhipID].Value;
            
            float itemScale = 1f;
            if (whipItemTex.Width > style.Width || whipItemTex.Height > style.Height) {
                itemScale = style.Width / Math.Max(whipItemTex.Width, whipItemTex.Height) * 0.75f;
            }

            Vector2 innerCenter = new Vector2(style.Width, style.Height) / 2f;
            Vector2 drawPosition = style.Position() + innerCenter;

            spriteBatch.Draw(whipItemTex, drawPosition, null, Color.White, 0f, whipItemTex.Size() / 2f, itemScale, SpriteEffects.None, 0f);

            if (Hovered) {
                Item hoverDummy = new Item();
                hoverDummy.SetDefaults(TargetWhipID);
                Main.hoverItemName = hoverDummy.Name;
            }
        }
    }
}