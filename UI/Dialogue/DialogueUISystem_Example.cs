using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// CONTOH pemasangan lengkap: Dialogue Box + GUI Riwayat + hotkey. Sesuaikan bebas.
    /// </summary>
    public class DialogueUISystem : ModSystem
    {
        public UIDialogueBox DialogueBox;

        private UserInterface _dialogueInterface;
        private DialogueUIState _dialogueState;

        private UserInterface _historyInterface;
        private UIDialogueHistoryState _historyState;
        private bool _historyOpen = false;

        public static ModKeybind HistoryKeybind;

        public override void Load()
        {
            if (Main.dedServ) return;

            HistoryKeybind = KeybindLoader.RegisterKeybind(Mod, "Open Dialogue History", "H");

            DialogueBox = new UIDialogueBox();
            // Posisi default: anchor tengah-horizontal (0.5f) + pixel offset. Buat geser SELURUH GUI
            // dialogue box ke kiri/kanan, cukup ubah angka pertama di Left.Set(...) di bawah ini:
            // makin NEGATIF (mis. -340 -> -420) = geser makin ke KIRI, makin ke arah 0/positif = ke KANAN.
            DialogueBox.Left.Set(-340, 0.5f);
            DialogueBox.Top.Set(430, 0f);
            // begitu ada baris baru tampil (termasuk pas Open()), catat ke riwayat
            DialogueBox.OnEntryChanged += (box, index) =>
            {
                DialogueLine line = box.Current;
                if (line == null) return;
                bool p2Speaking = line.IsP2Speaking();
                string speaker = p2Speaking ? line.P2.Nametag : line.P1.Nametag;
                string text = line.GetActiveText();
                DialogueHistoryManager.Add(speaker, text);
            };

            // ---- CONTOH HOOK TIMER: cukup subscribe event ini, ga wajib dipakai kalau ga butuh ----
            // OnTimerExpired -> pas waktu baris habis (sebelum auto-advance ke baris berikutnya)
            DialogueBox.OnTimerExpired += box =>
            {
                // contoh: bunyiin SFX peringatan pas waktu abis, atau apapun logika custom kamu
                // SoundEngine.PlaySound(SoundID.MenuTick);
            };
            // OnTimerTick -> dipanggil tiap frame timer jalan, param float = sisa detik (buat custom UI lain misalnya)
            DialogueBox.OnTimerTick += (box, secondsLeft) =>
            {
                // contoh: cuma dipakai kalau mau nampilin angka detik di tempat lain, dibiarin kosong disini
            };

            _dialogueState = new DialogueUIState(DialogueBox);
            _dialogueState.Activate();
            _dialogueInterface = new UserInterface();
            _dialogueInterface.SetState(_dialogueState);

            _historyState = new UIDialogueHistoryState();
            _historyState.Activate();
            _historyInterface = new UserInterface();
        }

        public override void UpdateUI(GameTime gameTime)
        {
            _dialogueInterface?.Update(gameTime);
            _historyInterface?.Update(gameTime);

            if (HistoryKeybind != null && HistoryKeybind.JustPressed)
                ToggleHistory();
        }

        private void ToggleHistory()
        {
            _historyOpen = !_historyOpen;
            if (_historyOpen)
            {
                _historyState.RefreshList();
                _historyInterface.SetState(_historyState);
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
            else
            {
                _historyInterface.SetState(null);
                SoundEngine.PlaySound(SoundID.MenuClose);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (index == -1) return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "TheSanity: Dialogue Box",
                delegate
                {
                    _dialogueInterface?.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI));

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "TheSanity: Dialogue History",
                delegate
                {
                    _historyInterface?.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI));
        }

        // ================================================================
        // CONTOH format P1/P2 sesuai spesifikasi kamu, plus BGM/TypingSound/SFX
        // ================================================================
        public void ContohBukaObrolanDuaOrang()
        {
            var lines = new List<DialogueLine>
            {
                new DialogueLine
                {
                    BGM = "TheSanity/Music/EmeraldTheme", // path asset musik, ganti sesuai punya kamu
                    TypingSound = null,          // null = pakai default (fallback MenuTick)
                    SoundEffect = null,
                    KeepBgmAfterClose = false,
                    P1 = new SpeakerData
                    {
                        Active = true,
                        Nametag = "Sanity",
                        // Icon = ModContent.Request<Texture2D>("TheSanity/Assets/NPCs/Sanity_Normal"),
                        IconBleed = false,
                        Dialogue = "Halo! Ini contoh obrolan dua orang di dialogue box.",
                    },
                    P2 = new SpeakerData
                    {
                        Active = true,
                        Nametag = "Traveler",
                        Dialogue = "", // P2 belum ngomong di baris ini, cuma nongol dengan opacity redup
                    },
                },
                new DialogueLine
                {
                    // BGM ga di-set -> lanjut lagu yang sama dari baris sebelumnya
                    P1 = new SpeakerData { Active = true, Nametag = "Sanity", Dialogue = "" },
                    P2 = new SpeakerData
                    {
                        Active = true,
                        Nametag = "Traveler",
                        Dialogue = "Oh, hai juga! Sekarang giliranku yang nyala terang, punyamu jadi redup.",
                    },
                },
                new DialogueLine
                {
                    BGM = DialogueLine.NoneToken, // "NONE" -> musik berhenti mulai baris ini
                    TimerDuration = 10f, // contoh: baris ini kasih waktu 10 detik (baris lain ikut default 15 detik)
                    P1 = new SpeakerData { Active = true, Nametag = "Sanity", Dialogue = "Musiknya berhenti sekarang." },
                    P2 = new SpeakerData { Active = true, Nametag = "Traveler", Dialogue = "" },
                },
                new DialogueLine
                {
                    // contoh baris yang nunggu keputusan player - timer ga bakal habis sendiri
                    InfiniteTime = true,
                    P1 = new SpeakerData { Active = true, Nametag = "Sanity", Dialogue = "Nah, ini baris yang nungguin kamu klik sendiri, ga ada batas waktu." },
                    P2 = new SpeakerData { Active = true, Nametag = "Traveler", Dialogue = "" },
                },
            };

            DialogueBox.Open(lines);
            DialogueBox.SetPaletteColorByName("Red"); // contoh: tetap Dark Retro tapi jadi "Dark Red"
        }

        // Contoh: matiin timer sepenuhnya buat baris yang lagi aktif (misal nunggu keputusan player)
        public void ContohMatikanTimerBarisAktif() => DialogueBox.StopTimer();

        // Contoh: kasih waktu custom 15 detik dari kode, di luar TimerDuration bawaan baris/DefaultTimerDuration
        public void ContohSetTimerCustom() => DialogueBox.SetTimerDuration(15f);

        // Contoh: skip animasi ketik yang sedang jalan tanpa menyalakan toggle permanen
        public void ContohForceSelesaikanKetikan() => DialogueBox.CompleteTypingInstantly();
    }

    public class DialogueUIState : UIState
    {
        private readonly UIDialogueBox _box;

        public DialogueUIState(UIDialogueBox box)
        {
            _box = box;
        }

        public override void OnInitialize()
        {
            Append(_box);
        }
    }
}
