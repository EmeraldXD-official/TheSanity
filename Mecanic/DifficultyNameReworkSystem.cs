using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.Localization;

namespace TheEye.Mecanic
{
    public class DifficultyNameReworkSystem : ModSystem
    {
        public override void OnLocalizationsLoaded()
        {
            if (ModLoader.HasMod("TheEyeOfSanity"))
            {
                ForceOverrideVanillaText("UI.Master", "Insanity");
                ForceOverrideVanillaText("UI.MasterMode", "Insanity Mode");
                ForceOverrideVanillaText("UI.MasterModeOnly", "Insanity Mode Only");
                ForceOverrideVanillaText("GameUI.Master", "Insanity");
            }
        }

        private void ForceOverrideVanillaText(string key, string newText)
        {
            var field = typeof(LanguageManager).GetField("_localizedTexts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                var dict = (Dictionary<string, LocalizedText>)field.GetValue(LanguageManager.Instance);
                var ctor = typeof(LocalizedText).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(string) }, null);
                if (ctor != null)
                    dict[key] = (LocalizedText)ctor.Invoke(new object[] { key, newText });
            }
        }
    }
}