// =============================================================================
// CATATAN API: file ini pakai beberapa API tModLoader (UIScrollbar.SetView/GetValue,
// Main.instance.LoadNPC, ContentSamples.NpcsByNetId, TextureAssets.MagicPixel, dst)
// yang penulisannya bisa dikit beda antar versi tModLoader. Kalau ada error compile,
// cek nama method yang paling deket di versi kamu (biasanya cuma beda nama parameter
// atau namespace kecil, bukan konsepnya).
// =============================================================================
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // TEMA WARNA: abu-abu, abu-abu gelap, biru pucat, cyan pucat
    // + helper gambar gradient/border/aksen sudut biar GUI ga polos,
    // semua cuma pake MagicPixel (1x1 putih) jadi ga butuh texture UI custom.
    // =========================================================
    internal static class SoulUITheme
    {
        public static readonly Color PanelDark = new Color(24, 27, 32);
        public static readonly Color PanelMid = new Color(45, 51, 60);
        public static readonly Color PanelHover = new Color(64, 72, 84);
        public static readonly Color BorderSubtle = new Color(120, 150, 170);
        public static readonly Color AccentCyan = new Color(120, 210, 225);
        public static readonly Color CyanBright = new Color(150, 230, 240);
        public static readonly Color BluePale = new Color(150, 180, 225);
        public static readonly Color TextBright = new Color(222, 236, 240);
        public static readonly Color TextDim = new Color(130, 140, 150);
        public static readonly Color DeadRed = new Color(150, 30, 30);

        public static void DrawVerticalGradient(SpriteBatch sb, Rectangle rect, Color top, Color bottom)
        {
            if (rect.Height <= 0 || rect.Width <= 0)
                return;

            int slices = (int)MathHelper.Clamp(rect.Height / 4f, 4f, 24f);
            for (int s = 0; s < slices; s++)
            {
                float t = s / (float)Math.Max(slices - 1, 1);
                Color c = Color.Lerp(top, bottom, t);
                int y = rect.Y + (int)(rect.Height * (s / (float)slices));
                int h = (int)(rect.Height / (float)slices) + 1;
                sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, y, rect.Width, h), c);
            }
        }

        public static void DrawBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness)
        {
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        public static void DrawCornerAccents(SpriteBatch sb, Rectangle rect, Color color)
        {
            const int len = 10;
            Texture2D px = TextureAssets.MagicPixel.Value;

            sb.Draw(px, new Rectangle(rect.X, rect.Y, len, 2), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, len), color);

            sb.Draw(px, new Rectangle(rect.Right - len, rect.Y, len, 2), color);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, len), color);

            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, len, 2), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - len, 2, len), color);

            sb.Draw(px, new Rectangle(rect.Right - len, rect.Bottom - 2, len, 2), color);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Bottom - len, 2, len), color);
        }
    }

    // =========================================================
    // STATE UTAMA
    // Layout (kanvas 660x520): header | [grid NPC 380px] [scrollbar] [soul bar 28px] [detail 190px]
    // =========================================================
    public class SoulCollectorUIState : UIState
    {
        public SoulCollectorAltarEntity CurrentAltar { get; private set; }
        public int SelectedNpcType { get; private set; }
        public string HoverText;

        private UIElement _mainPanel;
        private UIElement _header;
        private NPCGridPanel _npcGrid;
        private UIScrollbar _scrollbar;
        private SoulBarElement _soulBar;
        private NPCDetailElement _detailPanel;

        public override void OnInitialize()
        {
            var root = new UIElement();
            root.Width.Set(660f, 0f);
            root.Height.Set(520f, 0f);
            root.HAlign = 0.5f;
            root.VAlign = 0.5f;
            // Geser seluruh GUI ke kanan (biar ga numpuk sama panel/inventory
            // yang biasanya nongol di sisi kiri layar).
            root.Left.Set(140f, 0f);
            Append(root);

            _mainPanel = new UIElement();
            _mainPanel.Width.Set(0f, 1f);
            _mainPanel.Height.Set(0f, 1f);
            root.Append(_mainPanel);

            _header = new UIElement();
            _header.Width.Set(0f, 1f);
            _header.Height.Set(36f, 0f);
            _mainPanel.Append(_header);

            var closeButton = new SoulCloseButton();
            closeButton.Width.Set(24f, 0f);
            closeButton.Height.Set(24f, 0f);
            closeButton.Left.Set(-30f, 1f);
            closeButton.Top.Set(6f, 0f);
            _header.Append(closeButton);

            _npcGrid = new NPCGridPanel();
            _npcGrid.Left.Set(10f, 0f);
            _npcGrid.Top.Set(44f, 0f);
            _npcGrid.Width.Set(380f, 0f);
            _npcGrid.Height.Set(-54f, 1f);
            _mainPanel.Append(_npcGrid);

            _scrollbar = new UIScrollbar();
            _scrollbar.Left.Set(398f, 0f);
            _scrollbar.Top.Set(44f, 0f);
            _scrollbar.Height.Set(-54f, 1f);
            _mainPanel.Append(_scrollbar);
            _npcGrid.SetScrollbar(_scrollbar);

            _soulBar = new SoulBarElement(this);
            _soulBar.Left.Set(414f, 0f);
            _soulBar.Top.Set(44f, 0f);
            _soulBar.Width.Set(28f, 0f);
            _soulBar.Height.Set(-54f, 1f);
            _mainPanel.Append(_soulBar);

            _detailPanel = new NPCDetailElement(this);
            _detailPanel.Left.Set(452f, 0f);
            _detailPanel.Top.Set(44f, 0f);
            _detailPanel.Width.Set(190f, 0f);
            _detailPanel.Height.Set(-54f, 1f);
            _mainPanel.Append(_detailPanel);

            PopulateNPCList();
        }

        private void PopulateNPCList()
        {
            _npcGrid.RemoveAllChildren();

            // NPCLoader.NPCCount = total tipe NPC yang ke-load, termasuk NPC dari mod lain.
            // Filter cuma Town NPC (sample.townNPC) -> flag ini udah ke-set dari SetDefaults()
            // tiap tipe NPC pas game load, jadi valid dicek walau NPC-nya belum pernah muncul.
            //
            // Town pet (Town Slime/Cat/Dog/Bunny) SENGAJA GA DITAMPILIN di grid ini --
            // mereka udah dikecualikan dari lock arrival natural (lihat
            // TownNPCRespawnLockGlobalNPC.ExemptFromLockTypes), jadi ga ada
            // gunanya (dan malah bisa bikin bingung/salah pencet) nawarin
            // tombol Revive buat NPC yang emang bisa balik sendiri.
            for (int type = 1; type < NPCLoader.NPCCount; type++)
            {
                if (ContentSamples.NpcsByNetId.TryGetValue(type, out NPC sample)
                    && sample.type == type
                    && sample.townNPC
                    && !TownNPCRespawnLockGlobalNPC.ExemptFromLockTypes.Contains(type))
                {
                    _npcGrid.AddIcon(new NPCIconElement(type, this));
                }
            }
        }

        public void SetAltar(SoulCollectorAltarEntity altar)
        {
            CurrentAltar = altar;
        }

        public void SelectNPC(int npcType)
        {
            SelectedNpcType = npcType;
        }

        // Dipanggil dari tombol "Revive" di detail panel. Syarat: NPC udah pernah
        // muncul & sekarang mati (ga alive), dan altar yang lagi kebuka punya
        // cukup Soul (>= ReviveCost).
        public void TryRevive()
        {
            int type = SelectedNpcType;

            if (CurrentAltar == null || type == 0 || !SoulTrackerSystem.DiscoveredNPCs.Contains(type))
                return;

            // Ga boleh mulai ritual baru kalau masih ada ritual revive lain
            // yang lagi jalan (di altar manapun) -- biar ga numpuk 2 animasi
            // ritual bareng-bareng.
            if (SoulCollectorAltarEntity.AnyRitualActive)
                return;

            if (SoulTrackerSystem.IsAlive(type))
                return;

            if (SoulTrackerSystem.GetKillCount(type) <= 0)
                return; // belum pernah mati, ga ada yang perlu di-revive

            if (CurrentAltar.SoulCount < SoulCollectorAltarEntity.ReviveCost)
                return;

            // Host/singleplayer: eksekusi langsung di sisi otoritatif.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                CurrentAltar.RemoveSoul(SoulCollectorAltarEntity.ReviveCost);

                // BUKAN langsung spawn NPC lagi di sini -- sekarang mulai
                // animasi RITUAL-nya dulu (soul dilempar kanan/kiri lalu narik
                // ke titik konvergensi). NPC-nya baru beneran ke-spawn nanti
                // di tengah-tengah ritual itu sendiri, lihat
                // SoulCollectorAltarEntity.BeginReviveRitual() / SpawnRitualNPC().
                CurrentAltar.BeginReviveRitual(type, Main.LocalPlayer);
            }
            else
            {
                // Client MP asli: kirim request ke server, server yang
                // validasi ulang & eksekusi RemoveSoul + BeginReviveRitual
                // beneran (lihat SoulNetworking.HandleReviveRequest).
                //
                // CATATAN KETERBATASAN: animasi ritual (soul-soul terbang)
                // itu di-tick manual di SoulCollectorAltarEntity.Update(),
                // yang MASIH di-skip di sisi client (belum ada sync buat
                // IsRitualActive/posisi tiap soul). Jadi di client non-host,
                // yang bakal keliatan cuma: Soul count langsung kepotong ->
                // nunggu durasi ritual -> NPC-nya nongol (nyambung normal
                // lewat netcode NPC bawaan) -- TANPA nampilin animasi
                // terbangnya. NPC-nya sendiri tetap muncul valid buat semua
                // orang, cuma bagian visual buildup-nya yang belum ikut
                // disinkronin.
                SoulNetworking.SendReviveRequest(CurrentAltar, type);
            }

            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void Update(GameTime gameTime)
        {
            HoverText = null;
            base.Update(gameTime);

            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
            if (rect.Contains(Main.mouseX, Main.mouseY))
                Main.LocalPlayer.mouseInterface = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = _mainPanel.GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            SoulUITheme.DrawVerticalGradient(spriteBatch, rect, SoulUITheme.PanelDark, new Color(38, 43, 50));
            SoulUITheme.DrawBorder(spriteBatch, rect, SoulUITheme.BorderSubtle, 2);
            SoulUITheme.DrawCornerAccents(spriteBatch, rect, SoulUITheme.AccentCyan);

            CalculatedStyle headerDims = _header.GetDimensions();
            var headerRect = new Rectangle((int)headerDims.X, (int)headerDims.Y, (int)headerDims.Width, (int)headerDims.Height);
            SoulUITheme.DrawVerticalGradient(spriteBatch, headerRect, new Color(55, 62, 72), SoulUITheme.PanelDark);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(headerRect.X, headerRect.Bottom - 1, headerRect.Width, 1), SoulUITheme.AccentCyan);

            Utils.DrawBorderString(spriteBatch, "Soul Collector Altar",
                new Vector2(headerRect.X + 12, headerRect.Y + 9), SoulUITheme.TextBright, 1f);

            base.Draw(spriteBatch);

            if (!string.IsNullOrEmpty(HoverText))
            {
                Vector2 pos = new Vector2(Main.mouseX + 18, Main.mouseY + 18);
                Vector2 size = FontAssets.MouseText.Value.MeasureString(HoverText);
                var bg = new Rectangle((int)pos.X - 6, (int)pos.Y - 4, (int)size.X + 12, (int)size.Y + 8);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, bg, SoulUITheme.PanelDark * 0.95f);
                SoulUITheme.DrawBorder(spriteBatch, bg, SoulUITheme.BorderSubtle, 1);
                Utils.DrawBorderString(spriteBatch, HoverText, pos, SoulUITheme.TextBright, 0.9f);
            }
        }
    }

    // =========================================================
    // Tombol close (X) pojok kanan atas
    // =========================================================
    internal class SoulCloseButton : UIElement
    {
        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            SoulCollectorUISystem.Instance.CloseUI();
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
            Color c = IsMouseHovering ? new Color(220, 100, 100) : SoulUITheme.BorderSubtle;

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, SoulUITheme.PanelMid);
            SoulUITheme.DrawBorder(spriteBatch, rect, c, 2);
            Utils.DrawBorderString(spriteBatch, "X",
                new Vector2(dims.X + dims.Width / 2f, dims.Y + dims.Height / 2f), c, 1f, 0.5f, 0.5f);
        }
    }

    // =========================================================
    // Panel grid NPC (kiri, besar) - scrollable, custom grid (bukan list 1 kolom),
    // biar kelihatan kayak grid icon head NPC.
    // =========================================================
    internal class NPCGridPanel : UIElement
    {
        private readonly List<NPCIconElement> _icons = new List<NPCIconElement>();
        private UIScrollbar _scrollbar;
        private const float CellSize = 42f;
        private const float CellPad = 4f;

        public NPCGridPanel()
        {
            OverflowHidden = true; // biar icon yang kescroll ke luar area ga digambar nembus panel lain
        }

        public void SetScrollbar(UIScrollbar bar)
        {
            _scrollbar = bar;
        }

        public void AddIcon(NPCIconElement icon)
        {
            _icons.Add(icon);
            Append(icon);
        }

        public void RemoveAllChildren()
        {
            _icons.Clear();
            RemoveAllChildren2();
        }

        // Nama beda dari method bawaan UIElement (kalau ada) biar ga ketimpa nama.
        private void RemoveAllChildren2()
        {
            var children = new List<UIElement>(Elements);
            foreach (UIElement child in children)
                RemoveChild(child);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            LayoutIcons();
        }

        public override void ScrollWheel(UIScrollWheelEvent evt)
        {
            base.ScrollWheel(evt);
            if (_scrollbar != null)
                _scrollbar.ViewPosition -= evt.ScrollWheelValue;
        }

        private void LayoutIcons()
        {
            CalculatedStyle dims = GetDimensions();
            if (dims.Width <= 0)
                return;

            int columns = Math.Max(1, (int)(dims.Width / (CellSize + CellPad)));
            float scrollOffset = _scrollbar?.GetValue() ?? 0f;

            for (int idx = 0; idx < _icons.Count; idx++)
            {
                int col = idx % columns;
                int row = idx / columns;
                NPCIconElement icon = _icons[idx];

                icon.Width.Set(CellSize, 0f);
                icon.Height.Set(CellSize, 0f);
                icon.Left.Set(col * (CellSize + CellPad), 0f);
                icon.Top.Set(row * (CellSize + CellPad) - scrollOffset, 0f);
            }

            int totalRows = (int)Math.Ceiling(_icons.Count / (float)columns);
            float contentHeight = totalRows * (CellSize + CellPad);

            _scrollbar?.SetView(dims.Height, Math.Max(contentHeight, dims.Height));
        }
    }

    // =========================================================
    // Satu slot icon NPC di dalam grid.
    // - Belum pernah muncul -> abu-abu, ga ada icon, cuma "?"
    // - Lagi hidup           -> icon normal
    // - Udah pernah mati & sekarang ga ada yang hidup -> overlay merah pekat
    // =========================================================
    internal class NPCIconElement : UIElement
    {
        public readonly int NpcType;
        private readonly SoulCollectorUIState _parentState;

        public NPCIconElement(int npcType, SoulCollectorUIState parentState)
        {
            NpcType = npcType;
            _parentState = parentState;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            _parentState.SelectNPC(NpcType);
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            bool discovered = SoulTrackerSystem.DiscoveredNPCs.Contains(NpcType);
            bool alive = discovered && SoulTrackerSystem.IsAlive(NpcType);
            int kills = SoulTrackerSystem.GetKillCount(NpcType);

            Color slotBg = IsMouseHovering ? SoulUITheme.PanelHover : SoulUITheme.PanelMid;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, slotBg);
            SoulUITheme.DrawBorder(spriteBatch, rect,
                _parentState.SelectedNpcType == NpcType ? SoulUITheme.AccentCyan : SoulUITheme.BorderSubtle,
                _parentState.SelectedNpcType == NpcType ? 2 : 1);

            if (!discovered)
            {
                Utils.DrawBorderString(spriteBatch, "?",
                    new Vector2(dims.X + dims.Width / 2f, dims.Y + dims.Height / 2f), SoulUITheme.TextDim, 1f, 0.5f, 0.5f);
                return;
            }

            if (!TextureAssets.Npc[NpcType].IsLoaded)
                Main.instance.LoadNPC(NpcType);

            Texture2D npcTex = TextureAssets.Npc[NpcType].Value;
            if (npcTex == null)
                return;

            int frameHeight = npcTex.Height / Math.Max(Main.npcFrameCount[NpcType], 1);
            var sourceRect = new Rectangle(0, 0, npcTex.Width, frameHeight);

            float scale = Math.Min((dims.Width - 8f) / sourceRect.Width, (dims.Height - 8f) / sourceRect.Height);
            scale = MathHelper.Clamp(scale, 0.05f, 2f);

            Vector2 drawPos = new Vector2(dims.X + dims.Width / 2f, dims.Y + dims.Height / 2f);
            Vector2 origin = new Vector2(sourceRect.Width, sourceRect.Height) / 2f;

            bool deadOnly = !alive && kills > 0;
            Color tint = deadOnly ? Color.White * 0.55f : Color.White;

            spriteBatch.Draw(npcTex, drawPos, sourceRect, tint, 0f, origin, scale, SpriteEffects.None, 0f);

            if (deadOnly)
            {
                // Overlay merah pekat di atas icon buat kesan "mati", ga tergantung warna asli sprite-nya.
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, SoulUITheme.DeadRed * 0.55f);
            }

            if (IsMouseHovering && ContentSamples.NpcsByNetId.TryGetValue(NpcType, out NPC sample))
                _parentState.HoverText = sample.FullName;
        }
    }

    // =========================================================
    // Bar vertikal (atas ke bawah) menunjukkan SoulCount / 100.000 altar yang lagi dibuka.
    // Hover -> muncul tooltip "X / 100.000".
    // =========================================================
    internal class SoulBarElement : UIElement
    {
        private readonly SoulCollectorUIState _parentState;

        public SoulBarElement(SoulCollectorUIState parentState)
        {
            _parentState = parentState;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            SoulUITheme.DrawVerticalGradient(spriteBatch, rect, SoulUITheme.PanelDark, SoulUITheme.PanelMid);
            SoulUITheme.DrawBorder(spriteBatch, rect, SoulUITheme.BorderSubtle, 2);

            int soulCount = _parentState.CurrentAltar?.SoulCount ?? 0;
            float fillPct = MathHelper.Clamp(soulCount / (float)SoulCollectorAltarEntity.MaxSoulCapacity, 0f, 1f);

            int fillHeight = (int)((rect.Height - 6) * fillPct);
            var fillRect = new Rectangle(rect.X + 3, rect.Bottom - fillHeight - 3, rect.Width - 6, Math.Max(fillHeight, 0));

            if (fillRect.Height > 0)
                SoulUITheme.DrawVerticalGradient(spriteBatch, fillRect, SoulUITheme.CyanBright, SoulUITheme.BluePale);

            // Garis takik tiap 25%
            Texture2D px = TextureAssets.MagicPixel.Value;
            for (int t = 1; t < 4; t++)
            {
                int y = rect.Y + rect.Height * t / 4;
                spriteBatch.Draw(px, new Rectangle(rect.X, y, rect.Width, 1), SoulUITheme.BorderSubtle * 0.6f);
            }

            if (IsMouseHovering)
                _parentState.HoverText = $"{soulCount:N0} / {SoulCollectorAltarEntity.MaxSoulCapacity:N0} Soul";
        }
    }

    // =========================================================
    // Tombol hijau "Revive" - muncul di pojok kanan-bawah detail panel,
    // cuma kalau NPC yang dipilih udah pernah muncul & sekarang mati.
    // Biaya: SoulCollectorAltarEntity.ReviveCost (500) diambil dari altar
    // yang lagi kebuka.
    // =========================================================
    internal class ReviveButton : UIElement
    {
        private readonly SoulCollectorUIState _parentState;

        public ReviveButton(SoulCollectorUIState parentState)
        {
            _parentState = parentState;
        }

        private bool HasEnoughSoul =>
            (_parentState.CurrentAltar?.SoulCount ?? 0) >= SoulCollectorAltarEntity.ReviveCost;

        // Ga boleh diklik kalau lagi ada ritual revive lain yang jalan
        // (di altar manapun) -- lihat SoulCollectorAltarEntity.AnyRitualActive.
        private bool RitualBusy => SoulCollectorAltarEntity.AnyRitualActive;

        private bool CanAfford => HasEnoughSoul && !RitualBusy;

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            if (!CanAfford)
            {
                // Soul kurang atau lagi ada ritual jalan -> cuma bunyi "ga bisa".
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }

            _parentState.TryRevive();
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            if (dims.Width <= 0f || dims.Height <= 0f)
                return;

            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            bool canAfford = CanAfford;
            Color baseColor = canAfford ? new Color(40, 150, 65) : new Color(60, 60, 60);
            Color hoverColor = canAfford ? new Color(60, 190, 90) : baseColor;
            Color fill = IsMouseHovering ? hoverColor : baseColor;
            Color border = canAfford ? new Color(120, 230, 150) : SoulUITheme.BorderSubtle;

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, fill);
            SoulUITheme.DrawBorder(spriteBatch, rect, border, 2);

            Utils.DrawBorderString(spriteBatch, "Revive",
                new Vector2(dims.X + dims.Width / 2f, dims.Y + dims.Height / 2f),
                canAfford ? Color.White : SoulUITheme.TextDim, 0.85f, 0.5f, 0.5f);

            if (IsMouseHovering)
            {
                _parentState.HoverText = RitualBusy
                    ? "A revive ritual is already in progress..."
                    : $"Revive ({SoulCollectorAltarEntity.ReviveCost:N0} Soul)";
            }
        }
    }

    // =========================================================
    // Panel detail (kanan, ~setengah lebar grid NPC) - nampilin info NPC yang lagi dipilih.
    // =========================================================
    internal class NPCDetailElement : UIElement
    {
        private readonly SoulCollectorUIState _parentState;
        private readonly ReviveButton _reviveButton;
        private readonly SoulConvertPanel _convertPanel;

        public NPCDetailElement(SoulCollectorUIState parentState)
        {
            _parentState = parentState;

            _reviveButton = new ReviveButton(parentState);
            _reviveButton.Left.Set(-92f, 1f);
            _reviveButton.Top.Set(-32f, 1f);
            // Ukuran di-set 0 by default, baru di-nyalain di Update() kalau syaratnya kepenuhin
            // (biar ga kepencet pas lagi ga relevan / ga ngambang di posisi kosong).
            Append(_reviveButton);

            // Panel convert Soul -> Soul Token, persis DI ATAS tombol Revive.
            // Sengaja dibikin gede & selalu keliatan (ga nyempil kecil kayak
            // sebelumnya), biar jelas ini fitur utama bukan tempelan.
            _convertPanel = new SoulConvertPanel(parentState);
            _convertPanel.Left.Set(8f, 0f);
            _convertPanel.Width.Set(-16f, 1f);
            _convertPanel.Top.Set(-88f, 1f);
            _convertPanel.Height.Set(48f, 0f);
            Append(_convertPanel);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            int type = _parentState.SelectedNpcType;
            bool discovered = type != 0 && SoulTrackerSystem.DiscoveredNPCs.Contains(type);
            bool alive = discovered && SoulTrackerSystem.IsAlive(type);
            int kills = discovered ? SoulTrackerSystem.GetKillCount(type) : 0;

            bool canRevive = discovered && !alive && kills > 0;

            if (canRevive)
            {
                _reviveButton.Width.Set(80f, 0f);
                _reviveButton.Height.Set(24f, 0f);
            }
            else
            {
                _reviveButton.Width.Set(0f, 0f);
                _reviveButton.Height.Set(0f, 0f);
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            SoulUITheme.DrawVerticalGradient(spriteBatch, rect, SoulUITheme.PanelMid, SoulUITheme.PanelDark);
            SoulUITheme.DrawBorder(spriteBatch, rect, SoulUITheme.BorderSubtle, 2);
            SoulUITheme.DrawCornerAccents(spriteBatch, rect, SoulUITheme.AccentCyan);

            int type = _parentState.SelectedNpcType;

            if (type == 0 || !SoulTrackerSystem.DiscoveredNPCs.Contains(type))
            {
                Utils.DrawBorderString(spriteBatch, "Pick an NPC on the left",
                    new Vector2(rect.Center.X, rect.Y + 20), SoulUITheme.TextDim, 0.85f, 0.5f, 0f);
                return;
            }

            var cursor = new Vector2(rect.X + 12, rect.Y + 12);

            if (ContentSamples.NpcsByNetId.TryGetValue(type, out NPC sample))
                Utils.DrawBorderString(spriteBatch, sample.FullName, cursor, SoulUITheme.TextBright, 0.95f);
            cursor.Y += 26;

            if (!TextureAssets.Npc[type].IsLoaded)
                Main.instance.LoadNPC(type);

            Texture2D tex = TextureAssets.Npc[type].Value;
            if (tex != null)
            {
                int frameHeight = tex.Height / Math.Max(Main.npcFrameCount[type], 1);
                var src = new Rectangle(0, 0, tex.Width, frameHeight);
                float scale = Math.Min((rect.Width - 24f) / src.Width, 70f / src.Height);
                scale = MathHelper.Clamp(scale, 0.05f, 3f);

                var pos = new Vector2(rect.Center.X, cursor.Y + 35);
                spriteBatch.Draw(tex, pos, src, Color.White, 0f, new Vector2(src.Width, src.Height) / 2f, scale, SpriteEffects.None, 0f);
            }
            cursor.Y += 80;

            bool alive = SoulTrackerSystem.IsAlive(type);
            int kills = SoulTrackerSystem.GetKillCount(type);

            string status = alive ? "Status: Alive" : (kills > 0 ? "Status: Dead" : "Status: Seen before");
            Color statusColor = alive ? SoulUITheme.CyanBright : (kills > 0 ? new Color(220, 90, 90) : SoulUITheme.TextDim);

            Utils.DrawBorderString(spriteBatch, status, cursor, statusColor, 0.8f);
            cursor.Y += 20;

            Utils.DrawBorderString(spriteBatch, $"Killed: {kills}x", cursor, SoulUITheme.TextBright, 0.8f);
        }
    }

    // =========================================================
    // Panel convert Soul -> Soul Token, DI ATAS tombol Revive (detail panel
    // kanan). Sengaja dibikin gede & jelas, bukan nyempil kecil di header lagi:
    //   "100"  [icon Soul]  >  [slot Soul Token]   [tombol Convert]
    //
    // 100 Soul (dari altar yang lagi kebuka) -> 1 Soul Token. Hasil convert-nya
    // TIDAK auto masuk inventory player -- nangkring dulu di slot khusus
    // (SoulTokenSlotElement), baru diambil manual lewat klik/drag kayak slot
    // biasa. Icon Souls yang dipake DI SINI make texture item Souls yang udah
    // ada (Souls.cs), bukan bikin sprite baru.
    // =========================================================
    internal class SoulConvertPanel : UIElement
    {
        private readonly SoulCollectorUIState _parentState;

        public SoulConvertPanel(SoulCollectorUIState parentState)
        {
            _parentState = parentState;

            var slot = new SoulTokenSlotElement(parentState);
            slot.Left.Set(78f, 0f);
            slot.Top.Set(9f, 0f);
            slot.Width.Set(32f, 0f);
            slot.Height.Set(32f, 0f);
            Append(slot);

            var convertButton = new ConvertButton(parentState);
            convertButton.Left.Set(114f, 0f);
            convertButton.Top.Set(7f, 0f);
            convertButton.Width.Set(56f, 0f);
            convertButton.Height.Set(34f, 0f);
            Append(convertButton);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            SoulUITheme.DrawVerticalGradient(spriteBatch, rect, SoulUITheme.PanelMid, SoulUITheme.PanelDark);
            SoulUITheme.DrawBorder(spriteBatch, rect, SoulUITheme.BorderSubtle, 1);

            float midY = rect.Y + rect.Height / 2f;

            // --- "100" ---
            Utils.DrawBorderString(spriteBatch, "100",
                new Vector2(rect.X + 6, midY), SoulUITheme.TextBright, 0.85f, 0f, 0.5f);

            // --- Icon Soul (statis, cuma penunjuk "bahan" convert -- bukan tombol) ---
            int soulType = ModContent.ItemType<Souls>();
            if (!TextureAssets.Item[soulType].IsLoaded)
                Main.instance.LoadItem(soulType);

            Texture2D soulTex = TextureAssets.Item[soulType].Value;
            if (soulTex != null)
            {
                // Souls.cs sprite-nya spritesheet 4-frame vertikal -> ambil frame 0 doang buat icon statis.
                int frameH = soulTex.Height / 4;
                var src = new Rectangle(0, 0, soulTex.Width, frameH);

                float scale = Math.Min(26f / src.Width, 26f / src.Height);
                Vector2 pos = new Vector2(rect.X + 45, midY);
                spriteBatch.Draw(soulTex, pos, src, Color.White, 0f,
                    new Vector2(src.Width, src.Height) / 2f, scale, SpriteEffects.None, 0f);
            }

            // --- Panah miring ">" (arah konversi: "100 Soul > Token") ---
            Utils.DrawBorderString(spriteBatch, ">",
                new Vector2(rect.X + 68, midY), SoulUITheme.AccentCyan, 1.1f, 0.5f, 0.5f);

            if (IsMouseHovering)
                _parentState.HoverText = $"{SoulCollectorAltarEntity.ConvertSoulCost} Soul > 1 Soul Token";
        }
    }

    // =========================================================
    // Slot 1-item khusus buat Soul Token hasil convert. Nyimpen datanya di
    // SoulCollectorAltarEntity.TokenSlotItem (per-altar, ke-save/load bareng
    // TileEntity-nya). CUMA nerima item tipe SoulToken:
    //  - Klik pas cursor kosong & slot ada isinya -> ambil ke cursor.
    //  - Klik pas cursor bawa Soul Token & slot kosong/belom full -> taro/stack.
    //  - Klik pas cursor bawa item LAIN -> ditolak (ga ngapa-ngapain, cuma bunyi).
    // =========================================================
    internal class SoulTokenSlotElement : UIElement
    {
        private readonly SoulCollectorUIState _parentState;

        public SoulTokenSlotElement(SoulCollectorUIState parentState)
        {
            _parentState = parentState;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            SoulCollectorAltarEntity altar = _parentState.CurrentAltar;
            if (altar == null)
                return;

            int tokenType = ModContent.ItemType<SoulToken>();
            Item cursor = Main.mouseItem;
            Item stored = altar.TokenSlotItem;

            // =========================================================
            // Client MP asli (non-host): TileEntity yang dipegang UI ini
            // instance LOKAL milik client -- ga boleh diubah langsung
            // (server ga akan pernah tau, bisa dupe/ilang item). Kirim
            // request ke server lewat SoulNetworking, server yang eksekusi
            // beneran & broadcast balik TokenSlotItem terbaru lewat
            // altar.Sync(). Perubahan LOKAL di bawah ini cuma buat cursor
            // milik player sendiri (item PLAYER SENDIRI aman diubah lokal --
            // konsisten sama model trust inventory/cursor bawaan Terraria)
            // dan buat update optimis biar UI kerasa responsif, BUKAN sumber
            // kebenaran -- bakal langsung ke-koreksi otomatis begitu balasan
            // Sync() dari server sampe.
            // =========================================================
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (cursor.IsAir)
                {
                    // Cursor kosong -> minta ambil isi slot (kalau ada).
                    // TIDAK ditaro ke Main.mouseItem lokal di sini -- server
                    // yang ngasih itemnya lewat QuickSpawnItem (masuk
                    // inventory, bukan cursor, lihat SoulNetworking), biar
                    // ga ada window dupe/ilang di antara klik & balasan server.
                    if (!stored.IsAir)
                    {
                        SoulNetworking.SendTokenSlotTakeRequest(altar);
                        altar.TokenSlotItem = new Item(); // optimis, dikoreksi otomatis lewat Sync()
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    return;
                }

                if (cursor.type != tokenType)
                {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    return;
                }

                // Item(int type) constructor otomatis manggil SetDefaults(type),
                // jadi ini cara paling gampang buat "ngintip" maxStack Soul
                // Token tanpa ikut ngubah slot beneran.
                int tokenMaxStack = stored.IsAir ? new Item(tokenType).maxStack : stored.maxStack;
                int spaceMp = stored.IsAir ? tokenMaxStack : tokenMaxStack - stored.stack;
                int moveMp = Math.Min(spaceMp, cursor.stack);
                if (moveMp <= 0)
                    return;

                // Kurangin cursor milik PLAYER SENDIRI secara lokal (aman --
                // ini item dia sendiri) baru kirim request deposit ke server.
                cursor.stack -= moveMp;
                if (cursor.stack <= 0)
                    Main.mouseItem = new Item();

                SoulNetworking.SendTokenSlotDepositRequest(altar, moveMp);

                // Update optimis tampilan slot lokal (dikoreksi otomatis
                // lewat Sync() begitu balasan server sampe).
                if (stored.IsAir)
                {
                    Item optimistic = new Item();
                    optimistic.SetDefaults(tokenType);
                    optimistic.stack = moveMp;
                    altar.TokenSlotItem = optimistic;
                }
                else
                {
                    stored.stack += moveMp;
                }

                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            // ---------------------------------------------------------
            // Host / singleplayer: eksekusi langsung, otoritatif.
            // ---------------------------------------------------------
            if (cursor.IsAir)
            {
                // Cursor kosong -> ambil isi slot (kalau ada) ke cursor.
                if (!stored.IsAir)
                {
                    Main.mouseItem = stored;
                    altar.TokenSlotItem = new Item();
                    altar.Sync();
                    SoundEngine.PlaySound(SoundID.Grab);
                }
                return;
            }

            // Cursor lagi bawa item -> slot ini CUMA nerima Soul Token, tipe
            // lain ditolak mentah-mentah biar ga bisa dipake "nyimpen" barang lain.
            if (cursor.type != tokenType)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }

            if (stored.IsAir)
            {
                altar.TokenSlotItem = cursor.Clone();
                Main.mouseItem = new Item();
                altar.Sync();
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else if (stored.stack < stored.maxStack)
            {
                int space = stored.maxStack - stored.stack;
                int move = Math.Min(space, cursor.stack);

                stored.stack += move;
                cursor.stack -= move;
                if (cursor.stack <= 0)
                    Main.mouseItem = new Item();

                altar.Sync();
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
            if (rect.Contains(Main.mouseX, Main.mouseY))
                Main.LocalPlayer.mouseInterface = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            Color slotBg = IsMouseHovering ? SoulUITheme.PanelHover : SoulUITheme.PanelMid;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, slotBg);
            SoulUITheme.DrawBorder(spriteBatch, rect, SoulUITheme.BorderSubtle, 1);

            Item stored = _parentState.CurrentAltar?.TokenSlotItem;

            if (stored != null && !stored.IsAir)
            {
                int type = stored.type;
                if (!TextureAssets.Item[type].IsLoaded)
                    Main.instance.LoadItem(type);

                Texture2D tex = TextureAssets.Item[type].Value;
                if (tex != null)
                {
                    float scale = Math.Min((rect.Width - 6f) / tex.Width, (rect.Height - 6f) / tex.Height);
                    scale = MathHelper.Clamp(scale, 0.05f, 2f);

                    Vector2 pos = new Vector2(rect.Center.X, rect.Center.Y);
                    spriteBatch.Draw(tex, pos, null, Color.White, 0f,
                        new Vector2(tex.Width, tex.Height) / 2f, scale, SpriteEffects.None, 0f);
                }

                if (stored.stack > 1)
                    Utils.DrawBorderString(spriteBatch, stored.stack.ToString(),
                        new Vector2(rect.Right - 3, rect.Bottom - 2), SoulUITheme.TextBright, 0.7f, 1f, 1f);

                if (IsMouseHovering)
                    _parentState.HoverText = stored.stack > 1 ? $"Soul Token ({stored.stack})" : "Soul Token";
            }
            else if (IsMouseHovering)
            {
                _parentState.HoverText = "Soul Token slot (empty)";
            }
        }
    }

    // =========================================================
    // Tombol biru "Convert" - trigger SoulCollectorAltarEntity.TryConvertSouls()
    // buat altar yang lagi kebuka. Redup/nonaktif kalau Soul kurang dari
    // ConvertSoulCost ATAU slot token udah full stack sama Soul Token.
    // =========================================================
    internal class ConvertButton : UIElement
    {
        private readonly SoulCollectorUIState _parentState;

        public ConvertButton(SoulCollectorUIState parentState)
        {
            _parentState = parentState;
        }

        private bool CanConvert => _parentState.CurrentAltar?.CanConvert ?? false;

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            if (!CanConvert)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }

            // Host/singleplayer: eksekusi langsung di sisi otoritatif.
            // Client MP asli: ga boleh eksekusi lokal (bakal cuma ngubah copy
            // lokalnya doang) -- kirim request ke server lewat SoulNetworking,
            // server yang eksekusi TryConvertSouls() beneran & broadcast balik
            // hasilnya (SoulCount/TokenSlotItem baru) ke semua client lewat
            // altar.Sync() yang udah ada.
            if (Main.netMode != NetmodeID.MultiplayerClient)
                _parentState.CurrentAltar.TryConvertSouls();
            else
                SoulNetworking.SendConvertRequest(_parentState.CurrentAltar);

            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            bool canConvert = CanConvert;
            Color baseColor = canConvert ? new Color(60, 85, 150) : new Color(55, 55, 60);
            Color hoverColor = canConvert ? new Color(85, 115, 190) : baseColor;
            Color fill = IsMouseHovering ? hoverColor : baseColor;
            Color border = canConvert ? SoulUITheme.AccentCyan : SoulUITheme.BorderSubtle;

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, fill);
            SoulUITheme.DrawBorder(spriteBatch, rect, border, 1);

            Utils.DrawBorderString(spriteBatch, "Convert",
                new Vector2(rect.Center.X, rect.Center.Y),
                canConvert ? Color.White : SoulUITheme.TextDim, 0.75f, 0.5f, 0.5f);

            if (IsMouseHovering)
                _parentState.HoverText = $"Convert {SoulCollectorAltarEntity.ConvertSoulCost} Soul > 1 Soul Token";
        }
    }
}