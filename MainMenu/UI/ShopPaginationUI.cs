using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements; 
using Terraria.UI;
using Terraria.ModLoader;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.ID;

namespace TheSanity.MainMenu.UI
{
    public class ShopPaginationUI : UIState
    {
        private UIElement container;
        private UIImageButton btnNext;
        private UIImageButton btnPrevious;

        public override void OnInitialize() {
            container = new UIElement();
            
            // X (Left) sudah digeser 21 pixel ke kiri (534 - 21 = 513f)
            container.Left.Set(513f, 0f); 
            container.Top.Set(505f, 0f); 
            container.Width.Set(45f, 0f);  
            container.Height.Set(70f, 0f); 
            Append(container);

            Asset<Texture2D> texPrev = ModContent.Request<Texture2D>("TheSanity/MainMenu/UI/BottonPrevious", AssetRequestMode.ImmediateLoad);
            Asset<Texture2D> texNext = ModContent.Request<Texture2D>("TheSanity/MainMenu/UI/BottonNext", AssetRequestMode.ImmediateLoad);

            // Tombol Next (Atas)
            btnNext = new UIImageButton(texNext);
            btnNext.Left.Set(0f, 0f); 
            btnNext.Top.Set(0f, 0f); 
            btnNext.Width.Set(45f, 0f);
            btnNext.Height.Set(30f, 0f);
            btnNext.OnLeftClick += (evt, element) => {
                if (ShopPaginationSystem.CurrentPage < ShopPaginationSystem.MaxPages - 1) {
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    ShopPaginationSystem.CurrentPage++;
                    ShopPaginationSystem.RefreshActiveShopWindow();
                }
            };
            container.Append(btnNext);

            // Tombol Previous (Bawah)
            btnPrevious = new UIImageButton(texPrev);
            btnPrevious.Left.Set(0f, 0f);
            btnPrevious.Top.Set(35f, 0f); 
            btnPrevious.Width.Set(45f, 0f);
            btnPrevious.Height.Set(30f, 0f);
            btnPrevious.OnLeftClick += (evt, element) => {
                if (ShopPaginationSystem.CurrentPage > 0) {
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    ShopPaginationSystem.CurrentPage--;
                    ShopPaginationSystem.RefreshActiveShopWindow();
                }
            };
            container.Append(btnPrevious);
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            if (Main.playerInventory && Main.npcShop > 0) {
                
                // Mengunci koordinat X yang baru (513f)
                container.Left.Set(513f, 0f); 

                // Mengunci koordinat Y baru yang sudah dinaikkan (234f dikurangi 20px menjadi 214f)
                // Catatan: Jika kurang naik sedikit lagi, kecilkan angkanya (misal 210f atau 205f)
                float posisiSejajarAmmoBawah = Main.instance.invBottom + 114f;
                container.Top.Set(posisiSejajarAmmoBawah, 0f);
                
                container.Recalculate();
                
                if (ShopPaginationSystem.CurrentPage > 0) {
                    btnPrevious.SetVisibility(1f, 0.7f); 
                } else {
                    btnPrevious.SetVisibility(0.3f, 0.3f); 
                }

                if (ShopPaginationSystem.CurrentPage < ShopPaginationSystem.MaxPages - 1) {
                    btnNext.SetVisibility(1f, 0.7f); 
                } else {
                    btnNext.SetVisibility(0.3f, 0.3f); 
                }
            }
        }
    }
}