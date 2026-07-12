using Microsoft.Xna.Framework;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>Dipakai di Client Config biar player bisa pilih tema lewat dropdown.</summary>
    public enum DialogueThemeChoice
    {
        DarkRetro, Dark, Blood, YellowOrangeSunset, GreenForest,
        BlueOcean, WhiteMoon, PurpleRoyal, CyberNeon, DesertSand
    }

    /// <summary>
    /// Menyimpan semua warna yang dipakai satu tema Dialogue Box.
    /// Bisa diganti total lewat UIDialogueBox.SetTheme(...), atau di-override satu-satu
    /// lewat method Set-xxx-Color() di UIDialogueBox kalau cuma mau ubah 1 bagian.
    /// </summary>
    public struct DialogueTheme
    {
        public string Name;

        public Color BackgroundColor;      // latar panel dialog utama
        public Color OutlineColor;         // outline panel utama & icon box
        public Color IconBackgroundColor;  // latar kotak icon
        public Color NameBarColor;         // latar bar nama (yang miring)
        public Color NameBarOutline;       // outline bar nama
        public Color NameTextColor;        // warna teks nama
        public Color DialogueTextColor;    // warna teks isi dialog
        public Color ButtonColor;          // warna tombol next/prev normal
        public Color ButtonHoverColor;     // warna tombol saat di-hover mouse
        public Color ButtonTextColor;      // warna simbol ">>>" / "<<<"
        public Color TimerBarColor;        // warna garis indikator timer di bawah panel
        public ThemeEffectKind Effect;     // efek partikel ambient tema ini (daun, darah, dst - lihat DialogueThemeEffects.cs)

        public DialogueTheme(string name, Color background, Color outline, Color iconBg,
            Color nameBar, Color nameBarOutline, Color nameText, Color dialogueText,
            Color button, Color buttonHover, Color buttonText,
            Color? timerBarColor = null, ThemeEffectKind effect = ThemeEffectKind.None)
        {
            Name = name;
            BackgroundColor = background;
            OutlineColor = outline;
            IconBackgroundColor = iconBg;
            NameBarColor = nameBar;
            NameBarOutline = nameBarOutline;
            NameTextColor = nameText;
            DialogueTextColor = dialogueText;
            ButtonColor = button;
            ButtonHoverColor = buttonHover;
            ButtonTextColor = buttonText;
            // default TimerBarColor kalau ga di-set eksplisit: ikut warna outline tema (tetap "sesuai tema" otomatis)
            TimerBarColor = timerBarColor ?? outline;
            Effect = effect;
        }
    }

    /// <summary>
    /// 10 preset tema siap pakai. Default dipakai UIDialogueBox = DarkRetro.
    /// Panggil: DialogueBox.SetTheme(DialogueThemes.Blood); dst.
    /// </summary>
    public static class DialogueThemes
    {
        public static DialogueTheme DarkRetro => new DialogueTheme(
            "Dark Retro",
            background: new Color(15, 15, 20, 200),
            outline: new Color(90, 200, 190, 255),
            iconBg: new Color(10, 10, 14, 220),
            nameBar: new Color(20, 20, 26, 235),
            nameBarOutline: new Color(90, 200, 190, 255),
            nameText: new Color(120, 255, 235),
            dialogueText: Color.White,
            button: new Color(90, 200, 190, 255),
            buttonHover: new Color(150, 255, 245),
            buttonText: Color.Black,
            effect: ThemeEffectKind.StaticNoise
        );

        public static DialogueTheme Dark => new DialogueTheme(
            "Dark",
            background: new Color(10, 10, 10, 190),
            outline: new Color(80, 80, 80),
            iconBg: new Color(5, 5, 5, 210),
            nameBar: new Color(25, 25, 25, 230),
            nameBarOutline: new Color(90, 90, 90),
            nameText: Color.White,
            dialogueText: new Color(220, 220, 220),
            button: new Color(80, 80, 80),
            buttonHover: new Color(140, 140, 140),
            buttonText: Color.White
        );

        public static DialogueTheme Blood => new DialogueTheme(
            "Blood",
            background: new Color(25, 5, 5, 210),
            outline: new Color(150, 10, 10),
            iconBg: new Color(15, 0, 0, 220),
            nameBar: new Color(45, 5, 5, 235),
            nameBarOutline: new Color(180, 20, 20),
            nameText: new Color(255, 90, 90),
            dialogueText: new Color(235, 210, 210),
            button: new Color(150, 10, 10),
            buttonHover: new Color(220, 30, 30),
            buttonText: Color.White,
            effect: ThemeEffectKind.BloodDrip
        );

        public static DialogueTheme YellowOrangeSunset => new DialogueTheme(
            "Yellow & Orange Sunset",
            background: new Color(40, 20, 5, 200),
            outline: new Color(255, 150, 30),
            iconBg: new Color(35, 15, 0, 220),
            nameBar: new Color(70, 35, 5, 235),
            nameBarOutline: new Color(255, 180, 40),
            nameText: new Color(255, 210, 90),
            dialogueText: new Color(255, 235, 200),
            button: new Color(255, 150, 30),
            buttonHover: new Color(255, 200, 90),
            buttonText: Color.Black,
            effect: ThemeEffectKind.Embers
        );

        public static DialogueTheme GreenForest => new DialogueTheme(
            "Green Forest",
            background: new Color(8, 25, 12, 205),
            outline: new Color(60, 170, 80),
            iconBg: new Color(5, 18, 8, 220),
            nameBar: new Color(15, 40, 20, 235),
            nameBarOutline: new Color(80, 190, 100),
            nameText: new Color(150, 255, 170),
            dialogueText: new Color(215, 240, 215),
            button: new Color(60, 170, 80),
            buttonHover: new Color(110, 220, 130),
            buttonText: Color.Black,
            effect: ThemeEffectKind.FallingLeaves
        );

        public static DialogueTheme BlueOcean => new DialogueTheme(
            "Blue Ocean",
            background: new Color(5, 15, 30, 205),
            outline: new Color(60, 140, 220),
            iconBg: new Color(4, 12, 24, 220),
            nameBar: new Color(10, 30, 55, 235),
            nameBarOutline: new Color(80, 170, 240),
            nameText: new Color(140, 210, 255),
            dialogueText: new Color(215, 230, 245),
            button: new Color(60, 140, 220),
            buttonHover: new Color(110, 190, 255),
            buttonText: Color.Black,
            effect: ThemeEffectKind.Bubbles
        );

        public static DialogueTheme WhiteMoon => new DialogueTheme(
            "White Moon",
            background: new Color(235, 235, 240, 210),
            outline: new Color(180, 180, 195),
            iconBg: new Color(245, 245, 250, 225),
            nameBar: new Color(215, 215, 225, 240),
            nameBarOutline: new Color(160, 160, 180),
            nameText: new Color(40, 40, 60),
            dialogueText: new Color(30, 30, 40),
            button: new Color(180, 180, 200),
            buttonHover: new Color(140, 140, 170),
            buttonText: Color.Black,
            effect: ThemeEffectKind.Snowfall
        );

        public static DialogueTheme PurpleRoyal => new DialogueTheme(
            "Purple Royal",
            background: new Color(20, 8, 30, 205),
            outline: new Color(160, 70, 220),
            iconBg: new Color(15, 5, 22, 220),
            nameBar: new Color(35, 12, 50, 235),
            nameBarOutline: new Color(180, 100, 240),
            nameText: new Color(220, 170, 255),
            dialogueText: new Color(230, 215, 245),
            button: new Color(160, 70, 220),
            buttonHover: new Color(200, 130, 255),
            buttonText: Color.White,
            effect: ThemeEffectKind.Sparkle
        );

        public static DialogueTheme CyberNeon => new DialogueTheme(
            "Cyber Neon",
            background: new Color(6, 6, 14, 205),
            outline: new Color(255, 40, 200),
            iconBg: new Color(4, 4, 10, 225),
            nameBar: new Color(10, 10, 22, 235),
            nameBarOutline: new Color(60, 230, 255),
            nameText: new Color(60, 230, 255),
            dialogueText: new Color(230, 230, 255),
            button: new Color(255, 40, 200),
            buttonHover: new Color(60, 230, 255),
            buttonText: Color.Black,
            effect: ThemeEffectKind.NeonGlitch
        );

        public static DialogueTheme DesertSand => new DialogueTheme(
            "Desert Sand",
            background: new Color(45, 35, 15, 205),
            outline: new Color(210, 170, 90),
            iconBg: new Color(38, 28, 10, 220),
            nameBar: new Color(65, 50, 20, 235),
            nameBarOutline: new Color(225, 190, 110),
            nameText: new Color(240, 210, 150),
            dialogueText: new Color(245, 230, 205),
            button: new Color(210, 170, 90),
            buttonHover: new Color(235, 205, 140),
            buttonText: Color.Black,
            effect: ThemeEffectKind.SandDrift
        );

        /// <summary>Semua tema dalam satu array, urutan sesuai daftar di atas (index 0 = DarkRetro).</summary>
        public static DialogueTheme[] All => new[]
        {
            DarkRetro, Dark, Blood, YellowOrangeSunset, GreenForest,
            BlueOcean, WhiteMoon, PurpleRoyal, CyberNeon, DesertSand
        };

        /// <summary>Dipakai buat baca pilihan tema dari DialogueClientConfig.</summary>
        public static DialogueTheme FromChoice(DialogueThemeChoice choice)
        {
            var all = All;
            int i = (int)choice;
            return i >= 0 && i < all.Length ? all[i] : DarkRetro;
        }
    }
}
