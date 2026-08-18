using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace TheSanity.Systems
{
    // ==========================================
    // TwinsShaderSystem — load sekali di startup (client doang, dedicated server gak punya
    // GPU/GraphicsDevice jadi WAJIB di-skip) buat effect TwinsBeamEnergy.fx, terus daftarin
    // ke registry shader bawaan tModLoader (GameShaders.Misc) biar bisa dipanggil dari mana
    // aja lewat ApplyBeamEnergy() di bawah - gak perlu load ulang tiap kali beam nembak.
    //
    // ==========================================
    // WAJIB DIKOMPILE DULU SEBELUM DIPAKAI!
    // ==========================================
    // File .fx (TwinsBeamEnergy.fx) itu SOURCE CODE shader (HLSL), bukan asset yang langsung
    // bisa di-load tModLoader - harus di-compile ke .xnb dulu, PERSIS kayak texture/font
    // custom lain. Taruh source .fx-nya di:
    //   ModSources\TheSanity\Effects\TwinsBeamEnergy.fx
    // lalu compile pakai compiler shader bawaan tModLoader (folder instalasi tModLoader,
    // biasanya ada "tModLoaderFXC.exe"/script serupa buat compile .fx -> .xnb, dibundle
    // bareng tModLoader devtools) - hasil .xnb-nya HARUS ada di folder yang SAMA
    // (Effects/TwinsBeamEnergy.xnb) biar path ModContent.Request<Effect> di bawah nemu.
    // Kalau proses compile-nya gagal/gak ketemu tool-nya di instalasi kamu, cara paling
    // gampang: buka Discord tModLoader / cari "compile custom shader tmodloader .fx to xnb"
    // buat panduan versi terbaru (langkahnya suka beda dikit tiap update tModLoader).
    // ==========================================
    public class TwinsShaderSystem : ModSystem
    {
        public const string BeamEnergyShaderKey = "TheSanity:TwinsBeamEnergy";

        private static Asset<Effect> beamEnergyEffect;

        public override void Load()
        {
            // Dedicated server gak punya GraphicsDevice sama sekali - shader/effect apapun
            // WAJIB di-skip total di sana, cuma relevan buat client yang beneran nge-render.
            if (Main.dedServ)
                return;

            beamEnergyEffect = ModContent.Request<Effect>("TheSanity/Effects/TwinsBeamEnergy", AssetRequestMode.ImmediateLoad);

            GameShaders.Misc[BeamEnergyShaderKey] = new MiscShaderData(beamEnergyEffect, "BeamEnergy");
        }

        public override void Unload()
        {
            beamEnergyEffect = null;
        }

        // ==========================================
        // Helper SATU PINTU buat pasang shader-nya sebelum gambar beam - panggil ini TEPAT
        // SEBELUM draw call sprite beam-nya (Main.EntitySpriteDraw / spriteBatch.Draw), abis
        // ganti SpriteBatch ke mode custom-effect (lihat contoh wiring di
        // TwinsCursedBeam.DrawBeamSegments). uTime dihitung otomatis dari
        // Main.GameUpdateCount di sini - pemanggil GAK PERLU itung sendiri.
        //
        //   glowColor  - warna energi (dicampur ADDITIVE ke atas sprite asli)
        //   opacity    - 0..1, seberapa kuat efeknya nempel (1 = paling kuat)
        // ==========================================
        public static void ApplyBeamEnergy(Color glowColor, float opacity = 1f)
        {
            if (Main.dedServ || !GameShaders.Misc.ContainsKey(BeamEnergyShaderKey))
                return;

            MiscShaderData shader = GameShaders.Misc[BeamEnergyShaderKey];

            shader.Shader.Parameters["uTime"]?.SetValue(Main.GameUpdateCount / 60f);
            shader.Shader.Parameters["uColor"]?.SetValue(glowColor.ToVector4());
            shader.Shader.Parameters["uOpacity"]?.SetValue(opacity);

            shader.Apply();
        }

        // Effect mentah, buat kasus yang butuh pasang manual lewat SpriteBatch.Begin(...,
        // effect: ...) langsung (lihat contoh di DrawBeamSegments).
        public static Effect RawEffect => beamEnergyEffect?.Value;
    }
}
