using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects
{
    public class BossShaderLoader : ModSystem
    {
        private static Asset<Effect> _bossGlowShaderAsset;

        // Parameter di-cache sekali saat shader ter-load, bukan di-lookup by-name tiap frame.
        // Setiap proyektil/boss memanggil SetGlowParameters(...) sebelum menggambar core glow;
        // sebelumnya tiap file drawing melakukan shader.Parameters["uX"]?.SetValue(...) sendiri,
        // yang berarti string lookup berulang untuk tiap proyektil aktif di setiap frame.
        private static EffectParameter _timeParam;
        private static EffectParameter _colorParam;
        private static EffectParameter _secondaryColorParam;
        private static EffectParameter _pulseSpeedParam;
        private static EffectParameter _rimPowerParam;
        private static EffectParameter _intensityParam;

        /// <summary>
        /// Properti aman untuk mengambil instance Effect Shader.
        /// </summary>
        public static Effect BossGlowShader {
            get {
                if (Main.dedServ || _bossGlowShaderAsset == null) 
                    return null;

                // PERBAIKAN: Menggunakan .IsLoaded dan mengecek .Value != null (bukan HasValue)
                if (_bossGlowShaderAsset.IsLoaded && _bossGlowShaderAsset.Value != null) {
                    return _bossGlowShaderAsset.Value;
                }

                return null;
            }
        }

        /// <summary>
        /// Set semua parameter BossGlowShader lewat referensi EffectParameter yang sudah
        /// di-cache, alih-alih string lookup per-parameter tiap kali dipanggil. Aman dipanggil
        /// walau shader belum ter-load (no-op).
        /// </summary>
        public static void SetGlowParameters(Color primaryColor, Color secondaryColor, float pulseSpeed, float rimPower, float intensity) {
            if (BossGlowShader == null) return;

            _timeParam?.SetValue((float)Main.GlobalTimeWrappedHourly);
            _colorParam?.SetValue(primaryColor.ToVector4());
            _secondaryColorParam?.SetValue(secondaryColor.ToVector4());
            _pulseSpeedParam?.SetValue(pulseSpeed);
            _rimPowerParam?.SetValue(rimPower);
            _intensityParam?.SetValue(intensity);
        }

        public override void Load() {
            if (Main.dedServ) return;

            string shaderPath = "TheSanity/GlobalNPC/Bosses/DiscordantReligia/Effects/BossGlowShader";

            try {
                // Pengecekan aman menggunakan HasAsset sebelum di-load
                if (ModContent.HasAsset(shaderPath)) {
                    _bossGlowShaderAsset = ModContent.Request<Effect>(shaderPath, AssetRequestMode.ImmediateLoad);

                    Effect shader = BossGlowShader;
                    if (shader != null) {
                        _timeParam = shader.Parameters["uTime"];
                        _colorParam = shader.Parameters["uColor"];
                        _secondaryColorParam = shader.Parameters["uSecondaryColor"];
                        _pulseSpeedParam = shader.Parameters["uPulseSpeed"];
                        _rimPowerParam = shader.Parameters["uRimPower"];
                        _intensityParam = shader.Parameters["uIntensity"];
                    }
                }
            }
            catch (Exception ex) {
                Mod.Logger.Error("Gagal memuat BossGlowShader.fx!", ex);
                _bossGlowShaderAsset = null;
            }
        }

        public override void Unload() {
            _bossGlowShaderAsset = null;
            _timeParam = null;
            _colorParam = null;
            _secondaryColorParam = null;
            _pulseSpeedParam = null;
            _rimPowerParam = null;
            _intensityParam = null;
        }
    }
}