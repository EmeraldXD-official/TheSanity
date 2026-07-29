using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using TheSanity.Mecanic;
using TheSanity.GlobalNPCs;

namespace TheSanity
{
    public class SanitySystem : ModSystem
    {
        internal UserInterface SanityUserInterface;
        internal SanityUI SanityBarUI;

        private float visualBlueAlpha = 0f;

        public override void Load() {
            if (!Main.dedServ) {
                SanityBarUI = new SanityUI();
                SanityBarUI.Activate();
                SanityUserInterface = new UserInterface();
                SanityUserInterface.SetState(SanityBarUI);
            }
        }

        public override void Unload() {
            SanityBarUI = null;
            SanityUserInterface = null;
            visualBlueAlpha = 0f;
        }

        // ============================================================
        // ✅ RESET static state setiap kali world dimuat ulang
        // ============================================================
        public override void OnWorldLoad()
        {
            EaterOfWorldsHealthManager.ResetStaticState();
        }

        public override void UpdateUI(GameTime gameTime) {
            if (SanityUserInterface?.CurrentState != null) {
                SanityUserInterface.Update(gameTime);
            }
        }

        // ============================================================
        // ✅ Panggil update death animation setiap tick
        // ============================================================
        public override void PostUpdateEverything() {
            EaterOfWorldsHealthManager.UpdateDeathAnimation();
        }

        // =========================================================================
        // MENGHITUNG & MENGGAMBAR EFEK LAYAR BIRU (TRANSISI HALUS / FADE EFFECT)
        // =========================================================================
        private void DrawWaterOverlay(SpriteBatch spriteBatch) {
            if (Main.netMode == NetmodeID.Server || Main.myPlayer == -1)
                return;

            Player localPlayer = Main.LocalPlayer;
            if (localPlayer == null || !localPlayer.active)
                return;

            float targetBlueAlpha = 0f;

            if (localPlayer.HasBuff(BuffID.Wet)) {
                targetBlueAlpha = 0.40f; 
            }

            if (localPlayer.wet && !localPlayer.lavaWet && !localPlayer.honeyWet) {
                int headTileX = (int)(localPlayer.Center.X / 16f);
                int headTileY = (int)((localPlayer.position.Y + 8f) / 16f);
                
                if (headTileX >= 0 && headTileX < Main.maxTilesX && headTileY >= 0 && headTileY < Main.maxTilesY) {
                    if (Main.tile[headTileX, headTileY].LiquidAmount > 150) {
                        targetBlueAlpha = 0.80f; 
                    }
                }
            }

            float fadeInSpeed = 0.015f;
            float fadeOutSpeed = 0.010f;

            if (visualBlueAlpha < targetBlueAlpha) {
                visualBlueAlpha += fadeInSpeed;
                if (visualBlueAlpha > targetBlueAlpha) 
                    visualBlueAlpha = targetBlueAlpha;
            } 
            else if (visualBlueAlpha > targetBlueAlpha) {
                visualBlueAlpha -= fadeOutSpeed;
                if (visualBlueAlpha < targetBlueAlpha) 
                    visualBlueAlpha = targetBlueAlpha;
            }

            if (visualBlueAlpha > 0f) {
                Texture2D blankTex = TextureAssets.MagicPixel.Value;
                Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                Color waterColor = new Color(0, 100, 200) * visualBlueAlpha;
                spriteBatch.Draw(blankTex, screenRect, waterColor);
            }
        }

        // =========================================================================
        // POST DRAW INTERFACE: MENGGAMBAR EFFECT LAYAR HITAM SANITY
        // =========================================================================
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (Main.netMode == NetmodeID.Server || Main.myPlayer == -1)
                return;

            Player localPlayer = Main.LocalPlayer;
            if (localPlayer == null || !localPlayer.active)
                return;

            Texture2D blankTex = TextureAssets.MagicPixel.Value;
            Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            float currentSanity = 100f; 
            // currentSanity = localPlayer.GetModPlayer<SanityPlayer>().Sanity;

            float blackAlpha = 0f;

            if (currentSanity < 70f) {
                float progress = (70f - currentSanity) / 70f;
                blackAlpha = MathHelper.Clamp(progress * 0.75f, 0f, 0.75f); 
            }

            if (blackAlpha > 0f) {
                spriteBatch.Draw(blankTex, screenRect, Color.Black * blackAlpha);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int resourceBarIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));
            if (resourceBarIndex != -1) {
                
                layers.Insert(resourceBarIndex, new LegacyGameInterfaceLayer(
                    "TheSanity: Water Overlay",
                    delegate {
                        DrawWaterOverlay(Main.spriteBatch);
                        return true;
                    },
                    InterfaceScaleType.UI)
                );

                layers.Insert(resourceBarIndex + 1, new LegacyGameInterfaceLayer(
                    "TheSanity: Sanity Bar",
                    delegate {
                        if (SanityUserInterface?.CurrentState != null) {
                            SanityUserInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }
}