using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// BASE LINE Dialogue Box buat mod Terraria - mendukung 1 atau 2 pembicara (P1 kiri, P2 kanan/mirror),
    /// animasi buka/tutup bertahap, animasi ketik, BGM/suara-ketik/SFX per baris, dan palet warna.
    ///
    /// URUTAN ANIMASI BUKA (stage 0..4), otomatis MUNDUR (reverse) waktu Close():
    ///   0. Panel utama kebuka dari tengah "||" melebar ke kiri & kanan
    ///   1. Icon tab (P1 & P2 kalau aktif) - garis atas-bawah tumbuh, nyambung jadi border di sisi luar
    ///   2. Name tag (P1 "\" kiri, P2 "/" kanan) - kebuka dari tengah kayak panel
    ///   3. Icon/ekspresi karakter fade in
    ///   4. Tombol Prev/Next/Skip fade in
    ///   Lalu teks dialog diketik dari kiri ke kanan.
    ///
    /// Ganti halaman (Next/Previous) TIDAK mengulang animasi buka - cuma reset animasi ketik.
    /// Animasi buka lengkap cuma jalan sekali waktu Open() pertama kali dipanggil.
    /// </summary>
    public class UIDialogueBox : UIElement
    {
        // ================= KONFIGURASI LAYOUT =================
        public int MaxWidth = 460;          // lebar MAIN PANEL (belum termasuk icon kiri/kanan)
        public int MinWidth = 240;
        public int MinHeight = 190;         // TINGGI PANEL TETAP (FIXED) - ga ikut membesar/mengecil sesuai isi teks,
                                             // cuma lebar (MinWidth..MaxWidth) yang dinamis. Naikin nilai ini kalau
                                             // mau box-nya lebih tinggi/lega lagi (icon kiri/kanan ikut fixed juga).
        public int Padding = 14;
        public int IconGap = 10;
        public int OutlineThickness = 3;
        public int NameTagHeight = 34;
        public int NameTagSlant = 14;
        public int NameTagPaddingX = 16;
        public int NameTagRaiseExtra = 8;   // px tambahan biar name tag "nongol" lebih tinggi di atas panel (nambah ke nameTagOffset dasar)
        public int ButtonSize = 26;
        public float TextScale = 1f;
        public float NameTextScale = 1f;
        public int MaxIconSize = 160;

        // ================= KONFIGURASI ANIMASI (detik) =================
        public float PanelRevealDuration = 0.22f;
        public float IconTabRevealDuration = 0.20f;
        public float NameTagRevealDuration = 0.16f;
        public float IconAppearDuration = 0.15f;
        public float ButtonsAppearDuration = 0.12f;
        private const int StageCount = 5;

        // opacity default utk yang lagi diam (dipakai kalau IconOpacityOverride/NameOpacityOverride null)
        public float InactiveSpeakerOpacity = 0.3f;
        public float SpeakerOpacitySmoothing = 6f; // makin besar makin cepat transisi opacity gantian

        // ================= KONFIGURASI TIMER DIALOG =================
        // Garis indikator timer digambar di bagian paling bawah panel (sebelum border bawah), mengecil
        // sesuai sisa waktu, warnanya ikut DialogueTheme.TimerBarColor (otomatis sesuai tema aktif).
        public float DefaultTimerDuration = 15f;     // dipakai kalau DialogueLine.TimerDuration baris ini null
        public bool TimerAutoAdvanceOnExpire = true; // true = begitu waktu habis, otomatis Next() (baris terakhir = Close())
        public int TimerBarHeight = 3;
        public int TimerBarMarginBottom = 5;         // jarak garis timer ke border bawah panel

        // ================= TEMA =================
        // null = ikutin pilihan tema si PLAYER lewat DialogueClientConfig. Dipaksa lewat SetTheme().
        private DialogueTheme? _forcedTheme = null;
        private string _paletteName = null;
        private Color? _paletteRgb = null;

        // ================= DATA =================
        public List<DialogueLine> Lines = new List<DialogueLine>();
        public int CurrentIndex { get; private set; } = 0;
        public DialogueLine Current =>
            Lines.Count > 0 && CurrentIndex >= 0 && CurrentIndex < Lines.Count ? Lines[CurrentIndex] : null;
        public bool IsOpen { get; private set; } = false;
        public bool Visible = true;

        // ================= EVENT BUAT DI-HOOK =================
        public event Action<UIDialogueBox> OnOpened;
        public event Action<UIDialogueBox> OnClosed;
        public event Action<UIDialogueBox, int> OnEntryChanged;
        public event Action<UIDialogueBox> OnNextPressed;
        public event Action<UIDialogueBox> OnPreviousPressed;

        // ---- event timer (hook dari luar buat nge-custom perilaku timer) ----
        public event Action<UIDialogueBox> OnTimerStarted;       // dipanggil pas timer baris baru mulai jalan
        public event Action<UIDialogueBox, float> OnTimerTick;   // dipanggil tiap frame timer jalan, param = sisa detik
        public event Action<UIDialogueBox> OnTimerExpired;       // dipanggil sekali pas waktu habis (sebelum auto-advance)

        // ================= STATE LAYOUT =================
        private List<string> _wrappedLines = new List<string>();
        private int _panelWidth, _panelHeight, _iconSize, _totalChars;
        private bool _p1HasIcon, _p2HasIcon;
        private readonly SlantedTagTextureCache _tagCacheP1 = new SlantedTagTextureCache();
        private readonly SlantedTagTextureCache _tagCacheP2 = new SlantedTagTextureCache();

        // ================= STATE ANIMASI BUKA/TUTUP =================
        private int _stageIndex = 0;
        private float _stageTimer = 0f;
        private bool _closing = false;
        private int _closingPointer = StageCount - 1;
        private float _closingTimer = 0f;

        // ================= STATE ANIMASI KETIK =================
        private float _typedAmount = 0f;
        private bool _skipTyping = false;
        public bool IsSkipTypingOn => _skipTyping;

        // ================= STATE OPACITY PEMBICARA (di-smooth biar transisinya halus) =================
        private float _p1OpacityCurrent = 1f, _p2OpacityCurrent = 1f;

        // ================= STATE TIMER DIALOG =================
        private float _timerRemaining = 0f;
        private float _timerTotal = 0f;
        private bool _timerActive = false;
        private bool _timerPaused = false;

        // ================= STATE EFEK AMBIENT TEMA (daun, darah, dst) =================
        private readonly ThemeEffectRenderer _themeEffect = new ThemeEffectRenderer();

        // ================= STATE AUDIO =================
        private string _resolvedBgmPath = null;      // null = ga ada musik
        private bool _typingSoundSilent = false;
        private string _resolvedTypingSoundPath = null; // null & !silent -> fallback SoundID.MenuTick
        private SlotId _bgmSoundSlot;
        private bool _bgmSoundActive = false;

        private readonly UIDialogueButton _nextButton;
        private readonly UIDialogueButton _prevButton;
        private readonly UIDialogueButton _skipToggleButton;

        public UIDialogueBox()
        {
            Width.Set(0, 0f);
            Height.Set(0, 0f);

            _nextButton = new UIDialogueButton(">");
            _nextButton.PlayClickSound = () => SoundEngine.PlaySound(SoundID.MenuOpen);
            _nextButton.OnLeftClickHandler += _ => Next();
            Append(_nextButton);

            _prevButton = new UIDialogueButton("<");
            _prevButton.PlayClickSound = () => SoundEngine.PlaySound(SoundID.MenuClose);
            _prevButton.OnLeftClickHandler += _ => Previous();
            Append(_prevButton);

            _skipToggleButton = new UIDialogueButton("S");
            _skipToggleButton.OnLeftClickHandler += _ => ToggleSkipTyping();
            Append(_skipToggleButton);

            Visible = false;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;
            base.Draw(spriteBatch);
        }

        public override void Update(GameTime gameTime)
        {
            if (Visible)
            {
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                AdvanceAnimation(dt);
                AdvanceSpeakerOpacity(dt);
                AdvanceTimer(dt);
                AdvanceThemeEffect(dt);
            }
            base.Update(gameTime);
        }

        // ============================================================
        //                     PUBLIC API (HOOK DI SINI)
        // ============================================================

        public void Open(List<DialogueLine> lines, int startIndex = 0)
        {
            if (lines == null || lines.Count == 0) return;
            Lines = lines;
            CurrentIndex = Math.Clamp(startIndex, 0, Lines.Count - 1);
            IsOpen = true;
            Visible = true;

            _closing = false;
            _stageIndex = 0;
            _stageTimer = 0f;
            _typedAmount = 0f;

            var cfg = ModContent.GetInstance<DialogueClientConfig>();
            _skipTyping = cfg?.SkipTypingByDefault ?? false;

            SnapSpeakerOpacityToTarget();
            ResolveAudioForCurrentLine();
            StartTimerForCurrentLine();
            RecalculateLayout();
            OnOpened?.Invoke(this);
            OnEntryChanged?.Invoke(this, CurrentIndex);
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            _closingPointer = StageCount - 1;
            _closingTimer = 0f;
        }

        public void Next()
        {
            OnNextPressed?.Invoke(this);
            if (CurrentIndex < Lines.Count - 1)
            {
                CurrentIndex++;
                _typedAmount = 0f;
                ResolveAudioForCurrentLine();
                StartTimerForCurrentLine();
                RecalculateLayout();
                OnEntryChanged?.Invoke(this, CurrentIndex);
            }
            else
            {
                Close();
            }
        }

        public void Previous()
        {
            if (CurrentIndex <= 0) return;
            OnPreviousPressed?.Invoke(this);
            CurrentIndex--;
            _typedAmount = 0f;
            ResolveAudioForCurrentLine();
            StartTimerForCurrentLine();
            RecalculateLayout();
            OnEntryChanged?.Invoke(this, CurrentIndex);
        }

        public void ToggleSkipTyping()
        {
            _skipTyping = !_skipTyping;
            if (_skipTyping) _typedAmount = _totalChars;
        }

        public void CompleteTypingInstantly() => _typedAmount = _totalChars;

        /// <summary>Paksa tema tertentu, prioritas di atas pilihan tema player. Panggil ClearForcedTheme() buat balikin ke pilihan player.</summary>
        public void SetTheme(DialogueTheme theme) => _forcedTheme = theme;
        public void ClearForcedTheme() => _forcedTheme = null;

        // ---- override granular per-warna (dipaksa di atas tema aktif) ----
        public void SetBackgroundColor(Color c) { var t = GetBaseTheme(); t.BackgroundColor = c; _forcedTheme = t; }
        public void SetOutlineColor(Color c) { var t = GetBaseTheme(); t.OutlineColor = c; _forcedTheme = t; }
        public void SetIconBackgroundColor(Color c) { var t = GetBaseTheme(); t.IconBackgroundColor = c; _forcedTheme = t; }
        public void SetNameBarColor(Color c) { var t = GetBaseTheme(); t.NameBarColor = c; _forcedTheme = t; }
        public void SetNameTextColor(Color c) { var t = GetBaseTheme(); t.NameTextColor = c; _forcedTheme = t; }
        public void SetDialogueTextColor(Color c) { var t = GetBaseTheme(); t.DialogueTextColor = c; _forcedTheme = t; }
        public void SetButtonColor(Color c) { var t = GetBaseTheme(); t.ButtonColor = c; _forcedTheme = t; }

        /// <summary>Set palet warna pakai nama ("Red","Blue",dst). Kalau nama & RGB dua-duanya di-set, NAMA yang menang.</summary>
        public void SetPaletteColorByName(string colorName) => _paletteName = colorName;
        public void SetPaletteColorRGB(Color rgb) => _paletteRgb = rgb;
        public void ClearPaletteColor() { _paletteName = null; _paletteRgb = null; }

        public void SetText(string text)
        {
            if (Current == null) return;
            if (Current.IsP2Speaking()) Current.P2.Dialogue = text; else Current.P1.Dialogue = text;
            _typedAmount = 0f;
            RecalculateLayout();
        }

        // ============================================================
        //                    KONTROL TIMER DIALOG (HOOK)
        // ============================================================
        public float TimerRemaining => _timerRemaining;                 // sisa detik baris aktif sekarang
        public float TimerTotal => _timerTotal;                        // total durasi timer baris aktif
        public bool IsTimerRunning => _timerActive && !_timerPaused;

        public void PauseTimer() => _timerPaused = true;
        public void ResumeTimer() => _timerPaused = false;

        /// <summary>Restart timer baris aktif dari awal lagi (pakai durasi baris ini / default box).</summary>
        public void ResetTimer() => StartTimerForCurrentLine();

        /// <summary>Matiin timer sepenuhnya buat baris aktif (garis indikator ikut hilang).</summary>
        public void StopTimer()
        {
            _timerActive = false;
            _timerPaused = false;
            _timerRemaining = 0f;
            _timerTotal = 0f;
        }

        /// <summary>Paksa durasi timer baris aktif secara langsung dari kode (override baris & default box).</summary>
        public void SetTimerDuration(float seconds)
        {
            if (seconds <= 0f) { StopTimer(); return; }
            _timerTotal = seconds;
            _timerRemaining = seconds;
            _timerActive = true;
            _timerPaused = false;
            OnTimerStarted?.Invoke(this);
        }

        private void StartTimerForCurrentLine()
        {
            DialogueLine line = Current;

            // InfiniteTime = true -> timer OFF total buat baris ini, waktu ga bisa habis sendiri,
            // cuma bisa lanjut kalau di-Next() manual (klik tombol ">" atau lewat kode).
            if (line != null && line.InfiniteTime)
            {
                StopTimer();
                return;
            }

            float duration = line?.TimerDuration ?? DefaultTimerDuration;

            if (duration <= 0f)
            {
                StopTimer();
                return;
            }

            _timerTotal = duration;
            _timerRemaining = duration;
            _timerActive = true;
            _timerPaused = false;
            OnTimerStarted?.Invoke(this);
        }

        private void AdvanceTimer(float dt)
        {
            if (!_timerActive || _timerPaused || _closing) return;

            _timerRemaining -= dt;
            if (_timerRemaining <= 0f)
            {
                _timerRemaining = 0f;
                _timerActive = false;
                OnTimerTick?.Invoke(this, 0f);
                OnTimerExpired?.Invoke(this);
                if (TimerAutoAdvanceOnExpire) Next();
            }
            else
            {
                OnTimerTick?.Invoke(this, _timerRemaining);
            }
        }

        // ============================================================
        //                    RESOLUSI TEMA & PALET
        // ============================================================

        private DialogueTheme GetBaseTheme()
        {
            if (_forcedTheme.HasValue) return _forcedTheme.Value;
            var cfg = ModContent.GetInstance<DialogueClientConfig>();
            return DialogueThemes.FromChoice(cfg?.Theme ?? DialogueThemeChoice.DarkRetro);
        }

        private DialogueTheme GetEffectiveTheme()
        {
            DialogueTheme theme = GetBaseTheme();

            Color? paletteColor = null;
            if (!string.IsNullOrWhiteSpace(_paletteName)) paletteColor = ColorUtils.FromName(_paletteName);
            if (!paletteColor.HasValue && _paletteRgb.HasValue) paletteColor = _paletteRgb;

            if (paletteColor.HasValue) theme = ColorUtils.ApplyPalette(theme, paletteColor.Value);
            return theme;
        }

        // ============================================================
        //                    AUDIO: BGM / TYPING SOUND / SFX
        // ============================================================

        private void ResolveAudioForCurrentLine()
        {
            DialogueLine line = Current;
            if (line == null) return;

            if (line.BGM != null)
            {
                if (DialogueLine.IsNone(line.BGM)) StopBgm();
                else PlayBgm(line.BGM);
            }
            // null -> BGM yang lagi jalan dibiarin lanjut (warisan)

            if (line.TypingSound != null)
            {
                if (DialogueLine.IsNone(line.TypingSound)) { _typingSoundSilent = true; _resolvedTypingSoundPath = null; }
                else { _typingSoundSilent = false; _resolvedTypingSoundPath = line.TypingSound; }
            }
            // null -> ikutin baris sebelumnya

            if (!string.IsNullOrEmpty(line.SoundEffect) && !DialogueLine.IsNone(line.SoundEffect))
                SoundEngine.PlaySound(new SoundStyle(line.SoundEffect));
        }

        private void PlayBgm(string path)
        {
            StopBgm();
            _resolvedBgmPath = path;
            var style = new SoundStyle(path) { IsLooped = true };
            _bgmSoundSlot = SoundEngine.PlaySound(style);
            _bgmSoundActive = true;
        }

        private void StopBgm()
        {
            if (_bgmSoundActive && SoundEngine.TryGetActiveSound(_bgmSoundSlot, out ActiveSound activeSound))
                activeSound.Stop();

            _bgmSoundActive = false;
            _resolvedBgmPath = null;
        }

        private void PlayTypingSoundTick()
        {
            if (_typingSoundSilent) return;
            if (_resolvedTypingSoundPath != null) SoundEngine.PlaySound(new SoundStyle(_resolvedTypingSoundPath));
            else SoundEngine.PlaySound(SoundID.MenuTick); // fallback default internal Terraria
        }

        // ============================================================
        //                    ANIMASI - LOGIKA STAGE
        // ============================================================

        private float[] GetStageDurations(DialogueClientConfig cfg)
        {
            if (cfg != null && cfg.DisableOpenCloseAnimation) return new float[StageCount];
            return new float[] { PanelRevealDuration, IconTabRevealDuration, NameTagRevealDuration, IconAppearDuration, ButtonsAppearDuration };
        }

        private void AdvanceAnimation(float dt)
        {
            var cfg = ModContent.GetInstance<DialogueClientConfig>();
            float[] durations = GetStageDurations(cfg);

            if (_closing)
            {
                if (_closingPointer >= 0)
                {
                    _closingTimer += dt;
                    if (_closingTimer >= durations[_closingPointer])
                    {
                        _closingTimer = 0f;
                        _closingPointer--;
                        if (_closingPointer < 0) FinishClose();
                    }
                }
                return;
            }

            if (_stageIndex < StageCount)
            {
                _stageTimer += dt;
                if (_stageTimer >= durations[_stageIndex])
                {
                    _stageTimer = 0f;
                    _stageIndex++;
                }
            }
            else
            {
                float speed = cfg?.TypingSpeed ?? 40;
                int prevChars = (int)_typedAmount;

                if (_skipTyping) _typedAmount = _totalChars;
                else _typedAmount = Math.Min(_typedAmount + dt * speed, _totalChars);

                int newChars = (int)_typedAmount;
                if (!_skipTyping && newChars > prevChars) PlayTypingSoundTick();
            }
        }

        private void FinishClose()
        {
            _closing = false;
            IsOpen = false;
            Visible = false;
            _stageIndex = 0;
            _stageTimer = 0f;

            if (!(Current?.KeepBgmAfterClose ?? false)) StopBgm();
            OnClosed?.Invoke(this);
        }

        private float GetStageOpenAmount(int stageIndex, float[] durations)
        {
            if (!_closing)
            {
                if (stageIndex < _stageIndex) return 1f;
                if (stageIndex > _stageIndex) return 0f;
                float d = durations[stageIndex];
                return d <= 0f ? 1f : MathHelper.Clamp(_stageTimer / d, 0f, 1f);
            }

            if (stageIndex < _closingPointer) return 1f;
            if (stageIndex > _closingPointer) return 0f;
            float dc = durations[stageIndex];
            return dc <= 0f ? 0f : 1f - MathHelper.Clamp(_closingTimer / dc, 0f, 1f);
        }

        // ============================================================
        //              OPACITY PEMBICARA (aktif vs diam, gantian)
        // ============================================================

        private void SnapSpeakerOpacityToTarget()
        {
            (float p1Target, float p2Target) = GetSpeakerOpacityTargets();
            _p1OpacityCurrent = p1Target;
            _p2OpacityCurrent = p2Target;
        }

        private (float p1, float p2) GetSpeakerOpacityTargets()
        {
            DialogueLine line = Current;
            if (line == null) return (1f, 1f);

            bool p2Speaking = line.IsP2Speaking();

            float p1Target = line.P1.IconOpacityOverride ?? (p2Speaking ? InactiveSpeakerOpacity : 1f);
            float p2Target = line.P2.IconOpacityOverride ?? (p2Speaking ? 1f : InactiveSpeakerOpacity);
            return (p1Target, p2Target);
        }

        private void AdvanceSpeakerOpacity(float dt)
        {
            (float p1Target, float p2Target) = GetSpeakerOpacityTargets();
            float t = MathHelper.Clamp(dt * SpeakerOpacitySmoothing, 0f, 1f);
            _p1OpacityCurrent = MathHelper.Lerp(_p1OpacityCurrent, p1Target, t);
            _p2OpacityCurrent = MathHelper.Lerp(_p2OpacityCurrent, p2Target, t);
        }

        // ============================================================
        //                EFEK AMBIENT PARTIKEL PER-TEMA
        // ============================================================

        private void AdvanceThemeEffect(float dt)
        {
            DialogueTheme theme = GetEffectiveTheme();
            Rectangle bounds = GetPanelBounds();
            _themeEffect.Update(dt, theme.Effect, bounds);
        }

        /// <summary>Batas area panel utama (belum termasuk icon kiri/kanan) dalam koordinat layar absolut.</summary>
        private Rectangle GetPanelBounds()
        {
            CalculatedStyle dims = GetDimensions();
            int nameTagOffset = NameTagHeight / 2 + NameTagRaiseExtra;
            int leftReserved = _p1HasIcon ? _iconSize + IconGap : 0;
            int panelX = (int)dims.X + leftReserved;
            int panelY = (int)dims.Y + nameTagOffset;
            return new Rectangle(panelX, panelY, _panelWidth, _panelHeight);
        }

        // ============================================================
        //                        LAYOUT (DINAMIS)
        // ============================================================

        public override void Recalculate()
        {
            RecalculateLayout();
            base.Recalculate();
        }

        private void RecalculateLayout()
        {
            DialogueLine line = Current;
            _p1HasIcon = line?.P1.Active ?? false;
            _p2HasIcon = line?.P2.Active ?? false;

            if (line == null)
            {
                _panelWidth = MinWidth;
                _panelHeight = MinHeight;
                _iconSize = Math.Min(MinHeight, MaxIconSize);
                _wrappedLines.Clear();
                _totalChars = 0;
            }
            else
            {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                string text = line.GetActiveText() ?? string.Empty;

                // TINGGI (Y) PANEL & ICON SENGAJA DIBUAT TETAP (FIXED) = MinHeight, GA IKUT MENYESUAIKAN
                // ISI TEKS. Cuma LEBAR (X) panel yang dinamis mengikuti baris terpanjang. Kalau mau box-nya
                // lebih tinggi/lega, tinggal naikin nilai MinHeight (default udah dibikin agak besar).
                int height = MinHeight;
                int iconSizeGuess = Math.Min(height, MaxIconSize);

                int availableTextWidth = Math.Max(MaxWidth - Padding * 2, 80);
                _wrappedLines = WrapText(font, text, availableTextWidth, TextScale);

                float longestLine = 0f;
                foreach (string l in _wrappedLines)
                    longestLine = Math.Max(longestLine, font.MeasureString(l).X * TextScale);

                int width = (int)MathHelper.Clamp(longestLine + Padding * 2, MinWidth, MaxWidth);

                _panelWidth = width;
                _panelHeight = height;
                _iconSize = iconSizeGuess;

                _totalChars = 0;
                foreach (string l in _wrappedLines) _totalChars += l.Length;
            }

            int nameTagOffset = NameTagHeight / 2 + NameTagRaiseExtra;
            int leftReserved = _p1HasIcon ? _iconSize + IconGap : 0;
            int rightReserved = _p2HasIcon ? _iconSize + IconGap : 0;

            Width.Set(leftReserved + _panelWidth + rightReserved, 0f);
            Height.Set(_panelHeight + nameTagOffset, 0f);

            int panelX = leftReserved;

            int bottomRowY = _panelHeight - ButtonSize - 8 + nameTagOffset;

            // Baris tombol, kiri ke kanan: [Prev "<"] [Skip "S"] [Next ">"] - digabung jadi satu
            // kelompok dan di-CENTER secara horizontal di tengah panel (bukan dipisah ke pojok kiri/kanan).
            const int buttonGap = 8;
            int groupWidth = ButtonSize * 3 + buttonGap * 2;
            int groupStartX = panelX + (_panelWidth - groupWidth) / 2;

            _prevButton.Left.Set(groupStartX, 0f);
            _prevButton.Top.Set(bottomRowY, 0f);
            _prevButton.Width.Set(ButtonSize, 0f);
            _prevButton.Height.Set(ButtonSize, 0f);

            _skipToggleButton.Left.Set(groupStartX + ButtonSize + buttonGap, 0f);
            _skipToggleButton.Top.Set(bottomRowY, 0f);
            _skipToggleButton.Width.Set(ButtonSize, 0f);
            _skipToggleButton.Height.Set(ButtonSize, 0f);

            _nextButton.Left.Set(groupStartX + (ButtonSize + buttonGap) * 2, 0f);
            _nextButton.Top.Set(bottomRowY, 0f);
            _nextButton.Width.Set(ButtonSize, 0f);
            _nextButton.Height.Set(ButtonSize, 0f);

            _prevButton.Recalculate();
            _nextButton.Recalculate();
            _skipToggleButton.Recalculate();
        }

        private static List<string> WrapText(DynamicSpriteFont font, string text, int maxWidth, float scale)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            foreach (string paragraph in text.Split('\n'))
            {
                string[] words = paragraph.Split(' ');
                var current = new StringBuilder();

                foreach (string word in words)
                {
                    string test = current.Length == 0 ? word : current + " " + word;
                    float testWidth = font.MeasureString(test).X * scale;

                    if (testWidth > maxWidth && current.Length > 0)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                    else
                    {
                        if (current.Length > 0) current.Append(' ');
                        current.Append(word);
                    }
                }
                lines.Add(current.ToString());
            }

            return lines;
        }

        // ============================================================
        //                        DRAWING
        // ============================================================

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            DialogueLine line = Current;
            if (line == null) return;

            var cfg = ModContent.GetInstance<DialogueClientConfig>();
            float[] durations = GetStageDurations(cfg);
            DialogueTheme theme = GetEffectiveTheme();

            CalculatedStyle dims = GetDimensions();
            int nameTagOffset = NameTagHeight / 2 + NameTagRaiseExtra;

            int leftReserved = _p1HasIcon ? _iconSize + IconGap : 0;
            int panelX = (int)dims.X + leftReserved;
            int panelY = (int)dims.Y + nameTagOffset;
            int panelRight = panelX + _panelWidth;

            Texture2D px = TextureAssets.MagicPixel.Value;

            // ---------- STAGE 0: PANEL UTAMA, kebuka dari tengah "||" melebar ke kiri & kanan ----------
            float p0 = GetStageOpenAmount(0, durations);
            if (p0 > 0f)
            {
                int revealedWidth = Math.Max((int)(_panelWidth * p0), OutlineThickness * 2);
                int offsetX = (_panelWidth - revealedWidth) / 2;
                var revealRect = new Rectangle(panelX + offsetX, panelY, revealedWidth, _panelHeight);

                spriteBatch.Draw(px, revealRect, theme.BackgroundColor);
                spriteBatch.Draw(px, new Rectangle(revealRect.X, panelY, revealRect.Width, OutlineThickness), theme.OutlineColor);
                spriteBatch.Draw(px, new Rectangle(revealRect.X, panelY + _panelHeight - OutlineThickness, revealRect.Width, OutlineThickness), theme.OutlineColor);
                spriteBatch.Draw(px, new Rectangle(revealRect.X, panelY, OutlineThickness, _panelHeight), theme.OutlineColor);
                spriteBatch.Draw(px, new Rectangle(revealRect.X + revealRect.Width - OutlineThickness, panelY, OutlineThickness, _panelHeight), theme.OutlineColor);
            }

            // ---------- EFEK AMBIENT PARTIKEL TEMA (daun/darah/ember/dst) - digambar di atas background panel ----------
            if (p0 >= 1f)
            {
                var panelBounds = new Rectangle(panelX, panelY, _panelWidth, _panelHeight);
                _themeEffect.Draw(spriteBatch, theme.Effect, theme, panelBounds.Width > 0 ? 0.85f : 0f);
            }

            // ---------- GARIS INDIKATOR TIMER: paling bawah panel, sebelum border, mengecil sesuai sisa waktu ----------
            if (p0 >= 1f && _timerTotal > 0f)
            {
                float ratio = MathHelper.Clamp(_timerRemaining / _timerTotal, 0f, 1f);
                int barMaxWidth = Math.Max(_panelWidth - OutlineThickness * 2 - 4, 0);
                int barWidth = (int)(barMaxWidth * ratio);
                int barY = panelY + _panelHeight - OutlineThickness - TimerBarMarginBottom - TimerBarHeight;
                var barRect = new Rectangle(panelX + OutlineThickness + 2, barY, barWidth, TimerBarHeight);
                if (barWidth > 0) spriteBatch.Draw(px, barRect, theme.TimerBarColor);
            }

            // ---------- STAGE 1: ICON TAB kiri (P1) & kanan (P2), kalau aktif ----------
            float p1Stage = GetStageOpenAmount(1, durations);
            if (p1Stage > 0f)
            {
                if (_p1HasIcon)
                    DrawIconTab(spriteBatch, (int)dims.X, panelY, _iconSize, p1Stage, theme, mirrored: false);
                if (_p2HasIcon)
                    DrawIconTab(spriteBatch, panelRight + IconGap, panelY, _iconSize, p1Stage, theme, mirrored: true);
            }

            // ---------- STAGE 2: NAME TAG P1 ("\", kiri) & P2 ("/", kanan) ----------
            float p2Stage = GetStageOpenAmount(2, durations);
            if (p2Stage > 0f)
            {
                if (_p1HasIcon && !string.IsNullOrEmpty(line.P1.Nametag))
                    DrawNameTag(spriteBatch, line.P1.Nametag, panelX, panelRight, (int)dims.Y, p2Stage, theme, onRightSide: false, _tagCacheP1, _p1OpacityCurrent);
                if (_p2HasIcon && !string.IsNullOrEmpty(line.P2.Nametag))
                    DrawNameTag(spriteBatch, line.P2.Nametag, panelX, panelRight, (int)dims.Y, p2Stage, theme, onRightSide: true, _tagCacheP2, _p2OpacityCurrent);
            }

            // ---------- STAGE 3: ICON/EKSPRESI KARAKTER, fade in ----------
            float p3 = GetStageOpenAmount(3, durations);
            if (p3 > 0f)
            {
                if (_p1HasIcon)
                    DrawSpeakerIconImage(spriteBatch, line.P1, (int)dims.X, panelY, _iconSize, p3, _p1OpacityCurrent);
                if (_p2HasIcon)
                    DrawSpeakerIconImage(spriteBatch, line.P2, panelRight + IconGap, panelY, _iconSize, p3, _p2OpacityCurrent);
            }

            // ---------- STAGE 4: TOMBOL, fade in ----------
            float p4 = GetStageOpenAmount(4, durations);
            bool showEnabled = p4 > 0.95f;

            // Background tombol: dibikin LEBIH TIPIS opacity-nya dibanding background panel utama
            // (biar ga lebih "tebal" dari box dialog-nya sendiri), tapi tetap kebaca ada kotaknya.
            byte bgAlpha = (byte)(theme.BackgroundColor.A * 0.55f);
            Color buttonBgColor = new Color(theme.BackgroundColor.R, theme.BackgroundColor.G, theme.BackgroundColor.B, bgAlpha);

            _nextButton.Opacity = p4;
            _nextButton.SetVisible(showEnabled);
            _nextButton.NormalColor = theme.ButtonColor;
            _nextButton.HoverColor = theme.ButtonHoverColor;
            _nextButton.TextColor = theme.ButtonTextColor;
            _nextButton.BackgroundColor = buttonBgColor;
            _nextButton.BorderColor = theme.OutlineColor;

            _prevButton.Opacity = p4;
            _prevButton.SetVisible(showEnabled && CurrentIndex > 0);
            _prevButton.NormalColor = theme.ButtonColor;
            _prevButton.HoverColor = theme.ButtonHoverColor;
            _prevButton.TextColor = theme.ButtonTextColor;
            _prevButton.BackgroundColor = buttonBgColor;
            _prevButton.BorderColor = theme.OutlineColor;

            bool skipEnabledInConfig = cfg?.EnableSkipTypingButton ?? true;
            _skipToggleButton.Opacity = p4;
            _skipToggleButton.SetVisible(showEnabled && skipEnabledInConfig);
            _skipToggleButton.IsOn = _skipTyping;
            _skipToggleButton.NormalColor = theme.ButtonColor;
            _skipToggleButton.HoverColor = theme.ButtonHoverColor;
            _skipToggleButton.TextColor = theme.ButtonTextColor;
            _skipToggleButton.BackgroundColor = buttonBgColor;
            _skipToggleButton.BorderColor = theme.OutlineColor;

            // ---------- TEKS DIALOG, diketik dari kiri ke kanan ----------
            bool allStagesDone = !_closing && _stageIndex >= StageCount;
            if (allStagesDone)
            {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                float lineH = font.MeasureString("Ay").Y * TextScale + 4f;
                float textX = panelX + Padding;
                float textY = panelY + Padding;

                int remaining = (int)_typedAmount;
                for (int i = 0; i < _wrappedLines.Count && remaining > 0; i++)
                {
                    string wLine = _wrappedLines[i];
                    int show = Math.Min(remaining, wLine.Length);
                    remaining -= show;
                    if (show <= 0) break;

                    string visiblePart = wLine.Substring(0, show);
                    spriteBatch.DrawString(font, visiblePart, new Vector2(textX, textY + i * lineH),
                        theme.DialogueTextColor, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawIconTab(SpriteBatch sb, int x, int y, int size, float progress, DialogueTheme theme, bool mirrored)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            float lineGrow = MathHelper.Clamp(progress / 0.6f, 0f, 1f);
            float fillFade = MathHelper.Clamp((progress - 0.6f) / 0.4f, 0f, 1f);
            int growWidth = Math.Max((int)(size * lineGrow), OutlineThickness);

            int staticBorderX = mirrored ? x + size - OutlineThickness : x;
            int growStartX = mirrored ? x + size - growWidth : x;

            sb.Draw(px, new Rectangle(staticBorderX, y, OutlineThickness, size), theme.OutlineColor);
            sb.Draw(px, new Rectangle(growStartX, y, growWidth, OutlineThickness), theme.OutlineColor);
            sb.Draw(px, new Rectangle(growStartX, y + size - OutlineThickness, growWidth, OutlineThickness), theme.OutlineColor);

            if (lineGrow >= 1f)
            {
                int farBorderX = mirrored ? x : x + size - OutlineThickness;
                sb.Draw(px, new Rectangle(farBorderX, y, OutlineThickness, size), theme.OutlineColor);
            }

            if (fillFade > 0f)
            {
                var inner = new Rectangle(x + OutlineThickness, y + OutlineThickness,
                    Math.Max(size - OutlineThickness * 2, 0), Math.Max(size - OutlineThickness * 2, 0));
                sb.Draw(px, inner, theme.IconBackgroundColor * fillFade);
            }
        }

        // Bentuk tag P1 (kiri) miring ke kiri di bagian atas, kayak "\" - dan P2 (kanan) miring ke kanan
        // di bagian atas, kayak "/". shapeMirrored ngikutin !onRightSide: P1 (onRightSide=false) => "\",
        // P2 (onRightSide=true) => "/". Lihat SlantedTagTextureCache.Generate buat detail bentuknya.
        private void DrawNameTag(SpriteBatch sb, string nametag, int panelLeft, int panelRight, int tagY, float progress,
            DialogueTheme theme, bool onRightSide, SlantedTagTextureCache cache, float opacity)
        {
            DynamicSpriteFont nameFont = FontAssets.MouseText.Value;
            float nameWidth = nameFont.MeasureString(nametag).X * NameTextScale;
            int tagWidth = Math.Max((int)nameWidth + NameTagPaddingX * 2 + NameTagSlant, 60);
            int tagX = onRightSide ? panelRight - tagWidth : panelLeft;

            bool shapeMirrored = !onRightSide; // P1 (kiri) = "\", P2 (kanan) = "/"
            Texture2D tagTex = cache.GetOrCreate(Main.instance.GraphicsDevice, tagWidth, NameTagHeight, NameTagSlant,
                theme.NameBarColor, theme.NameBarOutline, OutlineThickness, shapeMirrored);

            int revealedW = Math.Max((int)(tagWidth * progress), 2);
            int srcX = (tagWidth - revealedW) / 2;
            var srcRect = new Rectangle(srcX, 0, revealedW, NameTagHeight);
            var destRect = new Rectangle(tagX + srcX, tagY, revealedW, NameTagHeight);
            sb.Draw(tagTex, destRect, srcRect, Color.White * opacity);

            if (progress >= 1f)
            {
                float textH = nameFont.MeasureString(nametag).Y * NameTextScale;
                float textXOffset = NameTagPaddingX + NameTagSlant / 2f;
                Vector2 textPos = new Vector2(tagX + textXOffset, tagY + (NameTagHeight - textH) / 2f);
                sb.DrawString(nameFont, nametag, textPos, theme.NameTextColor * opacity, 0f, Vector2.Zero, NameTextScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawSpeakerIconImage(SpriteBatch sb, SpeakerData speaker, int iconX, int iconY, int iconSize, float progress, float opacity)
        {
            if (speaker.Icon == null) return;
            Texture2D tex = speaker.Icon.Value;
            int innerPad = OutlineThickness + 4;
            int destW = Math.Max(iconSize - innerPad * 2, 1);
            int destH = destW;
            int baseX = iconX + innerPad;
            int baseY = iconY + innerPad;

            if (speaker.IconBleed)
            {
                int bleed = (int)(iconSize * 0.3f);
                baseY -= bleed;
                destH += bleed;
            }

            float offsetX = speaker.IconOffsetX ?? 0f;
            float offsetY = speaker.IconOffsetY ?? 0f;

            var destRect = new Rectangle((int)(baseX + offsetX), (int)(baseY + offsetY), destW, destH);
            sb.Draw(tex, destRect, Color.White * progress * opacity);
        }
    }

    /// <summary>Tombol kecil dengan efek hover + fade opacity + suara klik yang bisa diganti per tombol.</summary>
    public class UIDialogueButton : UIElement
    {
        public string Symbol;
        public Color NormalColor = Color.White;
        public Color HoverColor = Color.Silver;
        public Color TextColor = Color.Black;
        public Color BackgroundColor = Color.Transparent; // kotak latar tombol - opacity-nya diatur dari luar (UIDialogueBox), sengaja lebih tipis dari background panel
        public Color BorderColor = Color.Transparent;      // outline tipis kotak latar, kosong (Transparent) = ga digambar
        public float Opacity = 1f;
        public bool IsOn = false;
        public Action PlayClickSound = () => SoundEngine.PlaySound(SoundID.MenuTick);
        public event Action<UIDialogueButton> OnLeftClickHandler;

        private bool _hovering;
        private bool _visible = true;

        public UIDialogueButton(string symbol)
        {
            Symbol = symbol;
        }

        public void SetVisible(bool visible) => _visible = visible;

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!_visible) return;
            base.Draw(spriteBatch);
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            _hovering = true;
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            _hovering = false;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            PlayClickSound?.Invoke();
            OnLeftClickHandler?.Invoke(this);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // Kotak latar tombol: opacity-nya SENGAJA dibuat lebih tipis daripada background panel utama
            // (lihat UIDialogueBox.DrawSelf - buttonBgColor dihitung dari theme.BackgroundColor * 0.55f),
            // biar ga lebih "tebal"/menonjol dibanding box dialog-nya sendiri. Border tipis ikut warna outline tema.
            // Warna simbol berubah pas hover (NormalColor -> HoverColor), dan nyala lebih terang
            // (mendekati putih) kalau IsOn == true (dipakai skip button pas mode skip lagi ON).
            CalculatedStyle dims = GetDimensions();
            var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
            Texture2D px = TextureAssets.MagicPixel.Value;

            if (BackgroundColor.A > 0)
            {
                spriteBatch.Draw(px, rect, BackgroundColor * Opacity);
                if (BorderColor.A > 0)
                {
                    spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), BorderColor * Opacity * 0.8f);
                    spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), BorderColor * Opacity * 0.8f);
                    spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), BorderColor * Opacity * 0.8f);
                    spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), BorderColor * Opacity * 0.8f);
                }
            }

            Color baseColor = _hovering ? HoverColor : NormalColor;
            if (IsOn) baseColor = Color.Lerp(baseColor, Color.White, 0.4f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float scale = Math.Max(Math.Min(rect.Width, rect.Height) / 22f, 0.85f);
            if (_hovering) scale *= 1.15f; // sedikit membesar pas hover, biar ada feedback tanpa perlu background

            Vector2 size = font.MeasureString(Symbol) * scale;
            var pos = new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f);

            // Font bawaan (MouseText) punya "berat" glyph yang optically lebih ke atas dari kotak ukurnya,
            // jadi simbol keliatan kurang ke bawah / kegeser ke atas kalau cuma di-center matematis doang.
            // Nudge tipis ke bawah di sini buat ngoreksi biar keliatan pas center secara visual.
            pos.Y += 3f * scale;

            spriteBatch.DrawString(font, Symbol, pos, baseColor * Opacity, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// Generator + cache texture buat name tag jajar genjang (parallelogram) miring.
    /// mirrored=false -> "/" (bagian atas digeser ke KANAN dibanding bawah - dipakai P2/kanan).
    /// mirrored=true  -> "\" (bagian atas digeser ke KIRI dibanding bawah - dipakai P1/kiri).
    /// Texture cuma di-generate ulang kalau parameter berubah.
    /// </summary>
    public class SlantedTagTextureCache
    {
        private Texture2D _cached;
        private int _w, _h, _slant, _thickness;
        private Color _fill, _outline;
        private bool _mirrored;

        public Texture2D GetOrCreate(GraphicsDevice device, int width, int height, int slant, Color fill, Color outline, int thickness, bool mirrored)
        {
            if (_cached != null && _w == width && _h == height && _slant == slant &&
                _fill == fill && _outline == outline && _thickness == thickness && _mirrored == mirrored)
                return _cached;

            _cached?.Dispose();
            _cached = Generate(device, width, height, slant, fill, outline, thickness, mirrored);
            _w = width; _h = height; _slant = slant; _fill = fill; _outline = outline; _thickness = thickness; _mirrored = mirrored;
            return _cached;
        }

        private static Texture2D Generate(GraphicsDevice device, int w, int h, int slant, Color fill, Color outline, int thickness, bool mirrored)
        {
            w = Math.Max(w, slant + 4);
            h = Math.Max(h, 4);
            var data = new Color[w * h];

            // Lebar "isi" jajar genjang tetap konstan (innerWidth) di setiap baris y - cuma posisinya
            // yang geser dari sisi kiri (x=0) ke kanan (x=slant) seiring y berjalan, sehingga bentuknya
            // benar-benar miring satu arah (parallelogram), bukan trapesium simetris kayak sebelumnya.
            int innerWidth = w - slant;

            for (int y = 0; y < h; y++)
            {
                float t = h <= 1 ? 0f : (float)y / (h - 1); // t=0 di atas, t=1 di bawah

                // mirrored=false ("/", P1): atas digeser ke KANAN (offset besar di t=0, mengecil ke t=1)
                // mirrored=true  ("\", P2): atas digeser ke KIRI (offset kecil di t=0, membesar ke t=1)
                float offset = mirrored ? slant * t : slant * (1f - t);

                float leftEdge = offset;
                float rightEdge = offset + innerWidth;

                for (int x = 0; x < w; x++)
                {
                    Color c = Color.Transparent;
                    if (x >= leftEdge && x <= rightEdge)
                    {
                        bool nearBorder = x < leftEdge + thickness || x > rightEdge - thickness ||
                                           y < thickness || y > h - 1 - thickness;
                        c = nearBorder ? outline : fill;
                    }
                    data[y * w + x] = c;
                }
            }

            var tex = new Texture2D(device, w, h);
            tex.SetData(data);
            return tex;
        }
    }
}