using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// Setting sisi-client buat Dialogue Box. Otomatis muncul di menu Mod Config Terraria,
    /// ga perlu kode tambahan buat register (tModLoader nemuin sendiri lewat ModConfig).
    /// Semua Header/Tooltip di bawah SENGAJA ditulis pakai bahasa Inggris karena ini teks
    /// yang beneran nongol di layar Mod Config (in-game GUI), bukan komentar kode.
    /// </summary>
    public class DialogueClientConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("Appearance")]

        [DefaultValue(DialogueThemeChoice.DarkRetro)]
        [Tooltip("Your preferred Dialogue Box color theme. A mod hook can still force a specific theme via SetTheme(), but if no hook ever calls that, the theme selected here is used instead.")]
        public DialogueThemeChoice Theme;

        [Header("SkipTyping")]

        [DefaultValue(true)]
        [Tooltip("Show or hide the skip-typing toggle button (\"S\") in the button row.")]
        public bool EnableSkipTypingButton;

        [DefaultValue(false)]
        [Tooltip("If ON, every new dialogue opens already in skip-typing mode (text appears fully instantly, no typing animation).")]
        public bool SkipTypingByDefault;

        [Range(5, 200)]
        [DefaultValue(40)]
        [Slider]
        [Tooltip("Typing animation speed, in characters per second.")]
        public int TypingSpeed;

        [Header("OpenCloseAnimation")]

        [DefaultValue(false)]
        [Tooltip("If ON, the box's open/close animation (panel, icon tab, name tag, buttons) is instant with no animation.")]
        public bool DisableOpenCloseAnimation;
    }
}
