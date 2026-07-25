using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    public class TestingDeckUIState : UIState
    {
        private UIPanel mainPanel;
        private UIList projectileList;
        private UIScrollbar scrollbar;
        private UISearchBar searchBar;
        private UIPanel resizeHandle;

        private bool isDragging;
        private Vector2 dragOffset;

        private bool isResizing;
        private Vector2 startMousePos;
        private Vector2 startSize;

        private const float MinWidth = 350f;
        private const float MinHeight = 450f;
        private const float MaxWidth = 900f;
        private const float MaxHeight = 950f;

        public override void OnInitialize()
        {
            mainPanel = new UIPanel();
            mainPanel.Width.Set(450f, 0f);
            mainPanel.Height.Set(590f, 0f);
            mainPanel.Left.Set(80f, 0f);
            mainPanel.Top.Set(150f, 0f);
            mainPanel.BackgroundColor = new Color(30, 30, 50, 240);
            
            mainPanel.OnLeftMouseDown += DragStart;
            mainPanel.OnLeftMouseUp += DragEnd;
            Append(mainPanel);

            searchBar = new UISearchBar("Ketik nama projectile di sini...");
            searchBar.Width.Set(-20f, 1f);
            searchBar.Left.Set(10f, 0f);
            searchBar.Top.Set(10f, 0f);
            searchBar.OnContentsChanged += (text) => RefreshList();
            mainPanel.Append(searchBar);

            projectileList = new UIList();
            projectileList.Width.Set(-30f, 1f);
            projectileList.Height.Set(-115f, 1f); 
            projectileList.Left.Set(10f, 0f);
            projectileList.Top.Set(55f, 0f);
            projectileList.ListPadding = 5f;
            mainPanel.Append(projectileList);

            scrollbar = new UIScrollbar();
            scrollbar.Height.Set(-115f, 1f);
            scrollbar.Top.Set(55f, 0f);
            scrollbar.HAlign = 1f;
            mainPanel.Append(scrollbar);
            projectileList.SetScrollbar(scrollbar);

            // =========================================================================
            // TOMBOL MERAH PEMBERSIH / NUKE STATE PROJECTILE SECARA TOTAL
            // =========================================================================
            var clearButton = new UIPanel();
            clearButton.Width.Set(-20f, 1f);
            clearButton.Height.Set(36f, 0f);
            clearButton.Left.Set(10f, 0f);
            clearButton.Top.Set(-46f, 1f); 
            clearButton.BackgroundColor = new Color(160, 35, 35, 230);
            clearButton.BorderColor = new Color(220, 60, 60);
            
            clearButton.OnMouseOver += (evt, element) => clearButton.BackgroundColor = new Color(200, 45, 45, 240);
            clearButton.OnMouseOut += (evt, element) => clearButton.BackgroundColor = new Color(160, 35, 35, 230);

            var clearText = new UIText("⚡ CLEAR ALL ACTIVE PROJECTILES & STATES ⚡", 0.85f);
            clearText.HAlign = 0.5f;
            clearText.VAlign = 0.5f;
            clearText.TextColor = Color.White;
            clearButton.Append(clearText);

            clearButton.OnLeftClick += (evt, element) =>
            {
                int killedCount = 0;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active)
                    {
                        Main.projectile[i].active = false; 
                        Main.projectile[i].type = 0;
                        killedCount++;
                    }
                }
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.NPCDeath10);
                Main.NewText($"Sukses! {killedCount} Projectile aktif & looped state berhasil dibersihkan.", Color.GreenYellow);
            };
            mainPanel.Append(clearButton);

            // =========================================================================
            // 📐 HANDLE RESIZE (Kotak penarik ukuran di pojok kanan bawah)
            // =========================================================================
            resizeHandle = new UIPanel();
            resizeHandle.Width.Set(14f, 0f);
            resizeHandle.Height.Set(14f, 0f);
            resizeHandle.Left.Set(-14f, 1f); 
            resizeHandle.Top.Set(-14f, 1f);  
            resizeHandle.BackgroundColor = new Color(100, 100, 150, 200);
            resizeHandle.BorderColor = Color.Transparent;
            
            resizeHandle.OnLeftMouseDown += ResizeStart;
            resizeHandle.OnLeftMouseUp += ResizeEnd;
            mainPanel.Append(resizeHandle);

            RefreshList();
        }

        #region Logika Drag & Resize Event
        private void DragStart(UIMouseEvent evt, UIElement element)
        {
            if (evt.Target == mainPanel)
            {
                isDragging = true;
                dragOffset = Main.MouseScreen - new Vector2(mainPanel.Left.Pixels, mainPanel.Top.Pixels);
            }
        }

        private void DragEnd(UIMouseEvent evt, UIElement element)
        {
            isDragging = false;
        }

        private void ResizeStart(UIMouseEvent evt, UIElement element)
        {
            isResizing = true;
            startMousePos = Main.MouseScreen;
            startSize = new Vector2(mainPanel.Width.Pixels, mainPanel.Height.Pixels);
        }

        private void ResizeEnd(UIMouseEvent evt, UIElement element)
        {
            isResizing = false;
        }
        #endregion

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Mengunci input klik mouse interface game world agar tidak bocor keluar dari panel[cite: 19]
            if (mainPanel != null && mainPanel.ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            if (isDragging)
            {
                mainPanel.Left.Set(Main.MouseScreen.X - dragOffset.X, 0f);
                mainPanel.Top.Set(Main.MouseScreen.Y - dragOffset.Y, 0f);
                mainPanel.Recalculate(); 
            }

            if (isResizing)
            {
                Vector2 mouseDelta = Main.MouseScreen - startMousePos;
                
                float newWidth = MathHelper.Clamp(startSize.X + mouseDelta.X, MinWidth, MaxWidth);
                float newHeight = MathHelper.Clamp(startSize.Y + mouseDelta.Y, MinHeight, MaxHeight);

                mainPanel.Width.Set(newWidth, 0f);
                mainPanel.Height.Set(newHeight, 0f);
                mainPanel.Recalculate(); 
            }

            if (!Main.mouseLeft)
            {
                isDragging = false;
                isResizing = false;
            }
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            // Reset status fokus mengetik apabila UI ditutup paksa
            searchBar?.ResetTypingState();
        }

        public void RefreshList()
        {
            if (projectileList == null) return;
            projectileList.Clear();

            string query = (searchBar?.CurrentString ?? "").Trim().ToLowerInvariant();

            for (int i = 1; i < ProjectileLoader.ProjectileCount; i++)
            {
                string projName = Lang.GetProjectileName(i).Value;
                
                if (string.IsNullOrEmpty(projName))
                {
                    var modProj = ModContent.GetModProjectile(i);
                    projName = modProj != null ? modProj.Name : $"Unknown #{i}";
                }

                if (!string.IsNullOrEmpty(query) && !projName.ToLowerInvariant().Contains(query))
                    continue;

                projectileList.Add(new UIProjectileEntry(i, projName));
            }
        }
    }
}