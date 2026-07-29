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

        public override void Load() {
            if (Main.dedServ) return;

            string shaderPath = "TheSanity/GlobalNPC/Bosses/DiscordantReligia/Effects/BossGlowShader";

            try {
                // Pengecekan aman menggunakan HasAsset sebelum di-load
                if (ModContent.HasAsset(shaderPath)) {
                    _bossGlowShaderAsset = ModContent.Request<Effect>(shaderPath, AssetRequestMode.ImmediateLoad);
                }
            }
            catch (Exception ex) {
                Mod.Logger.Error("Gagal memuat BossGlowShader.fx!", ex);
                _bossGlowShaderAsset = null;
            }
        }

        public override void Unload() {
            _bossGlowShaderAsset = null;
        }
    }
}