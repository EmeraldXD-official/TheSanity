using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoRemote
{
    // GUI sengaja dibikin polos (tombol teks doang, bukan sprite custom) -- ini item debug/
    // testing internal, bukan konten player-facing, jadi gak perlu buang waktu bikin banyak
    // texture UI cuma buat menu kayak gini.
    public class PlutoRemoteUIState : UIState
    {
        // 🛑 [DAFTAR PATTERN] Urutan & angka di sini HARUS sinkron sama switch di
        // PlutoHead.ForcePattern() (PlutoPart/PlutoRemoteForce.cs) dan sama gacha pattern di
        // PlutoHead.AI() (Main.rand.Next(1, 8)). Kalau nambah pattern baru nanti, tambahin baris
        // di sini JUGA.
        private static readonly (int id, string label)[] PatternButtons = {
            (1, "1. Normal Dash"),
            (2, "2. Trick Dash (Nuke)"),
            (3, "3. Teleport Dash (Predictive Mine)"),
            (4, "4. Electro Nova"),
            (5, "5. Arena Bomb"),
            (6, "6. Probe Swarm"),
            (7, "7. Crystal Dive (Laser Wall)"),
        };

        private UIText selectedLabel;

        public override void OnInitialize() {
            const float panelWidth = 340f;
            float panelHeight = 70f + PatternButtons.Length * 32f;

            UIPanel mainPanel = new UIPanel();
            mainPanel.Width.Set(panelWidth, 0f);
            mainPanel.Height.Set(panelHeight, 0f);
            mainPanel.Left.Set(40f, 0f);
            mainPanel.Top.Set(200f, 0f);
            mainPanel.BackgroundColor = new Color(33, 16, 16, 230);
            Append(mainPanel);

            UIText title = new UIText("PLUTO REMOTE", 1f, true) { TextColor = Color.OrangeRed };
            title.Top.Set(4f, 0f);
            title.HAlign = 0.5f;
            mainPanel.Append(title);

            selectedLabel = new UIText("(belum ada pattern kepilih)", 0.75f) { TextColor = Color.Gray };
            selectedLabel.Top.Set(34f, 0f);
            selectedLabel.HAlign = 0.5f;
            mainPanel.Append(selectedLabel);

            float y = 68f;
            foreach ((int id, string label) in PatternButtons) {
                UIText button = new UIText(label, 0.85f) { TextColor = Color.White };
                button.Top.Set(y, 0f);
                button.Left.Set(16f, 0f);

                button.OnMouseOver += (evt, el) => { button.TextColor = Color.Yellow; };
                button.OnMouseOut += (evt, el) => { button.TextColor = Color.White; };
                button.OnLeftClick += (evt, el) => {
                    Main.LocalPlayer.GetModPlayer<PlutoRemotePlayer>().SelectedPattern = id;
                    selectedLabel.SetText($"Terpilih: {label}");
                    selectedLabel.TextColor = Color.LightGreen;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                };

                mainPanel.Append(button);
                y += 32f;
            }
        }
    }
}
