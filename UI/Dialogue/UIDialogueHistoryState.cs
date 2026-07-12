using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// GUI riwayat dialog yang sudah lewat. Buka/tutup lewat hotkey (lihat contoh wiring
    /// di DialogueUISystem_Example.cs) - mainkan SoundID.MenuOpen/MenuClose di situ, bukan di sini,
    /// biar konsisten satu tempat sama tombol Next/Previous.
    ///
    /// BISA DIGESER: klik-tahan bar judul "Dialogue History (hold & drag here)" di atas panel lalu drag ke posisi yang kamu mau.
    /// Posisi defaultnya sekarang dibikin ke tengah-atas, ga nempel pinggir layar kayak sebelumnya.
    /// </summary>
    public class UIDialogueHistoryState : UIState
    {
        private UIList _list;
        private UIPanel _mainPanel;
        private UIElement _dragHandle;

        private bool _dragging = false;
        private Vector2 _dragGrabOffset;

        public override void OnInitialize()
        {
            _mainPanel = new UIPanel();
            // Default posisi: agak ke tengah-atas layar, bukan mepet pojok kanan kayak sebelumnya (-260,1f).
            _mainPanel.Left.Set(-250, 0.5f);
            _mainPanel.Top.Set(90, 0f);
            _mainPanel.Width.Set(500, 0f);
            _mainPanel.Height.Set(560, 0f);
            _mainPanel.BackgroundColor = new Color(20, 20, 25, 230);
            Append(_mainPanel);

            // ---- Drag handle: strip di paling atas panel, klik-tahan-geser di sini buat mindahin panel ----
            _dragHandle = new UIElement();
            _dragHandle.Width.Set(0, 1f);
            _dragHandle.Height.Set(34, 0f);
            _dragHandle.OnLeftMouseDown += DragHandle_OnLeftMouseDown;
            _dragHandle.OnLeftMouseUp += DragHandle_OnLeftMouseUp;
            _mainPanel.Append(_dragHandle);

            var title = new UIText("Dialogue History (hold & drag here)", 0.8f);
            title.Top.Set(8, 0f);
            title.HAlign = 0.5f;
            title.IgnoresMouseInteraction = true; // biar klik-nya tetap kena _dragHandle di bawahnya, bukan ke teksnya
            _mainPanel.Append(title);

            var refreshButton = new UITextPanel<string>("Refresh", 0.75f);
            refreshButton.Width.Set(80, 0f);
            refreshButton.Height.Set(26, 0f);
            refreshButton.Top.Set(4, 0f);
            refreshButton.Left.Set(-88, 1f);
            refreshButton.OnLeftClick += (evt, el) =>
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                RefreshList();
            };
            _mainPanel.Append(refreshButton);

            _list = new UIList();
            _list.Top.Set(44, 0f);
            _list.Width.Set(-25, 1f);
            _list.Height.Set(-54, 1f);
            _list.ListPadding = 6f;
            _mainPanel.Append(_list);

            var scrollbar = new UIScrollbar();
            scrollbar.Top.Set(44, 0f);
            scrollbar.Left.Set(-24, 1f);
            scrollbar.Height.Set(-54, 1f);
            _mainPanel.Append(scrollbar);
            _list.SetScrollbar(scrollbar);

            RefreshList();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!_dragging) return;

            // Jaga-jaga kalau mouse-up ke-miss (misal lepas di luar elemen) - berhenti drag begitu tombol kiri lepas.
            if (!Main.mouseLeft)
            {
                _dragging = false;
                return;
            }

            float newX = Main.mouseX - _dragGrabOffset.X;
            float newY = Main.mouseY - _dragGrabOffset.Y;

            // Clamp biar panel ga bisa digeser sampai hilang total ke luar layar.
            CalculatedStyle dims = _mainPanel.GetDimensions();
            newX = MathHelper.Clamp(newX, -dims.Width + 60, Main.screenWidth - 60);
            newY = MathHelper.Clamp(newY, 0, Main.screenHeight - 40);

            _mainPanel.Left.Set(newX, 0f);
            _mainPanel.Top.Set(newY, 0f);
            Recalculate();
        }

        private void DragHandle_OnLeftMouseDown(UIMouseEvent evt, UIElement listeningElement)
        {
            _dragging = true;
            CalculatedStyle dims = _mainPanel.GetDimensions();
            _dragGrabOffset = new Vector2(Main.mouseX - dims.X, Main.mouseY - dims.Y);
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void DragHandle_OnLeftMouseUp(UIMouseEvent evt, UIElement listeningElement)
        {
            _dragging = false;
        }

        public void RefreshList()
        {
            _list.Clear();
            foreach (DialogueHistoryRecord record in DialogueHistoryManager.GetSorted())
                _list.Add(new UIHistoryEntryElement(record, this));
        }
    }

    /// <summary>Satu baris di GUI riwayat: preview teks + tombol Pin & Hapus.</summary>
    public class UIHistoryEntryElement : UIElement
    {
        private readonly DialogueHistoryRecord _record;
        private readonly UIDialogueHistoryState _owner;

        public UIHistoryEntryElement(DialogueHistoryRecord record, UIDialogueHistoryState owner)
        {
            _record = record;
            _owner = owner;

            Width.Set(0, 1f);
            Height.Set(64, 0f);

            var panel = new UIPanel();
            panel.Width.Set(0, 1f);
            panel.Height.Set(0, 1f);
            panel.BackgroundColor = record.Pinned ? new Color(60, 55, 20, 230) : new Color(30, 30, 35, 220);
            Append(panel);

            string preview = $"{record.SpeakerName}: {record.Text}";
            if (preview.Length > 90) preview = preview.Substring(0, 90) + "...";

            var text = new UIText(preview, 0.75f) { TextColor = Color.White };
            text.Left.Set(8, 0f);
            text.Top.Set(8, 0f);
            panel.Append(text);

            var pinButton = new UITextPanel<string>(record.Pinned ? "Unpin" : "Pin", 0.65f);
            pinButton.Width.Set(70, 0f);
            pinButton.Height.Set(24, 0f);
            pinButton.Left.Set(-160, 1f);
            pinButton.Top.Set(-30, 1f);
            pinButton.OnLeftClick += (evt, el) =>
            {
                DialogueHistoryManager.TogglePin(record);
                SoundEngine.PlaySound(SoundID.MenuTick);
                _owner.RefreshList();
            };
            panel.Append(pinButton);

            var deleteButton = new UITextPanel<string>("Delete", 0.65f);
            deleteButton.Width.Set(70, 0f);
            deleteButton.Height.Set(24, 0f);
            deleteButton.Left.Set(-80, 1f);
            deleteButton.Top.Set(-30, 1f);
            deleteButton.OnLeftClick += (evt, el) =>
            {
                DialogueHistoryManager.Remove(record);
                SoundEngine.PlaySound(SoundID.MenuTick);
                _owner.RefreshList();
            };
            panel.Append(deleteButton);
        }
    }
}
