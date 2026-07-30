using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =========================================================================
    // 🛑 [LOKASI LOAD SHADER RIM-LIGHT] Compile & register PlutoElectroRimLight.fx
    // (taruh file .fx-nya di "Effects/PlutoElectroRimLight.fx" relatif dari root
    // mod source-mu -- tModLoader otomatis nge-compile .fx yang ada di folder mod
    // pas build, sama kayak texture/asset lain).
    //
    // Didaftarin ke GameShaders.Misc biar konsisten sama pola yang udah dipakai
    // di project ini (lihat SAYA_SETUJU.txt: "GameShaders.Misc[...] buat
    // per-sprite"), walaupun pemakaiannya di PlutoHead.cs manggil parameter
    // shader-nya langsung (bukan lewat method Use...() bawaan) soalnya uniform-nya
    // custom (uLightDir, uLightColor, dll -- bukan yang dikenal MiscShaderData).
    // =========================================================================
    public class PlutoElectroRimLightSystem : ModSystem
    {
        public const string ShaderKey = "TheSanity:PlutoElectroRimLight";

        // Dipakai langsung dari PlutoHead.cs buat ambil Effect & set parameter custom-nya.
        public static MiscShaderData RimLightShaderData { get; private set; }

        // 🛑 Effect mentahnya juga disimpan terpisah (bukan cuma lewat MiscShaderData),
        // soalnya parameter shader kita custom (uLightDir, uLightColor, dll) yang ga
        // dikenal sama method Use...() bawaan MiscShaderData -- jadi PlutoHead.cs akses
        // .Parameters[...] langsung dari sini, lebih simpel & ga gantung ke detail
        // internal MiscShaderData (yang formatnya bisa beda2 dikit antar versi
        // tModLoader, Ref<Effect> vs Asset<Effect>).
        public static Effect RimLightEffect { get; private set; }

        public override void Load() {
            if (Main.dedServ) return; // server headless ga perlu load shader/graphics

            // 🛑 Sesuaikan path ini kalau kamu naruh file .fx-nya bukan langsung di
            // "Effects/PlutoElectroRimLight.fx" dari root mod source. Kalau tModLoader
            // versi kamu ternyata masih pakai Ref<Effect> (bukan Asset<Effect>) buat
            // constructor MiscShaderData, tinggal ganti baris "new MiscShaderData(...)"
            // di bawah -- baris RimLightEffect di atasnya ga kepengaruh sama sekali.
            Asset<Effect> shaderAsset = ModContent.Request<Effect>(
                $"{Mod.Name}/Effects/PlutoElectroRimLight",
                AssetRequestMode.ImmediateLoad
            );

            RimLightEffect = shaderAsset.Value;
            RimLightShaderData = new MiscShaderData(shaderAsset, "RimLightPass");
            GameShaders.Misc[ShaderKey] = RimLightShaderData;
        }

        public override void Unload() {
            RimLightShaderData = null;
            RimLightEffect = null;
        }
    }
}
