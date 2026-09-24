using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // WhoAmI_Phase3RiftShockwave.cs
    // Wrapper tipis di atas Effects/WhoAmIShockwave.xnb (compile hasil dari WhoAmIShockwave.fx - fork
    // ShockwaveEffect.fx yang DIKUSTOM khusus buat boss WhoAmI, ada tambahan "mirror echo" nyambung ke
    // tema cermin/refleksi boss ini). KHUSUS dipakai buat "hukuman" pas player telat nyampe celah pada
    // wave Summoner di Phase 3 Cartesian Gauntlet (lihat ResolveRift di WhoAmI_Phase3Cartesian.cs).
    //
    // Default tint di Trigger() dibiarkan wajib diisi caller (bukan hardcode di sini) - di pemanggilan
    // dari ResolveRift dipakein ArchetypeGlowColor(3) (magenta 230,110,230), sama persis kayak warna
    // rift/Summon yang udah dipakai di teks & partikel celah, biar nyambung satu tema.
    //
    // CATATAN MULTIPLAYER: murni VFX layar (screen shader), jadi nggak butuh sinkronisasi paket -
    // sama kayak VFX Phase 3 lainnya (SpawnRiftFailureVFX dkk), efeknya cuma keliatan di client yang
    // ngalamin sendiri.
    // ================================================================================================
    public class Phase3RiftShockwaveSystem : ModSystem
    {
        private const string FilterKey = "TheSanity:Phase3RiftShockwave";

        // Durasi animasi ripple dalam TICK (bukan detik) - progress dinaikin 1 tiap tick, dan uColor.z
        // (speed) yang ngatur seberapa cepat ripple-nya "kejar" progress ini secara visual di shader.
        private const float MaxProgressTicks = 50f;

        private static Asset<Effect> _shaderAsset;
        private static bool _active;
        private static float _progress;

        public override void Load()
        {
            if (Main.dedServ) return; // server headless nggak pernah gambar apa2 - jangan load shader

            _shaderAsset = ModContent.Request<Effect>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/WhoAmIShockwave", AssetRequestMode.ImmediateLoad);
            Filters.Scene[FilterKey] = new Filter(new ScreenShaderData(_shaderAsset, "Shockwave"), EffectPriority.High);
        }

        public override void Unload()
        {
            _shaderAsset = null;
        }

        /// <summary>
        /// Nembakin shockwave DARI <paramref name="worldPos"/> (buat rift-failure Phase 3, ini HARUS
        /// posisi boss - NPC.Center - bukan posisi player atau titik celahnya, sesuai request).
        /// </summary>
        /// <param name="tint">Warna yang dicampur ke pita gelombang.</param>
        /// <param name="tintStrength">0 = polos distorsi doang, 1 = pita gelombang ketutup penuh warna tint.</param>
        /// <param name="mirrorEchoStrength">Kekuatan "bayangan cermin" khas WhoAmIShockwave.fx yang
        /// ngintip lewat pita ripple. 0 = mati total (efeknya jadi persis shockwave polos biasa).</param>
        /// <param name="maxRange">Jangkauan maksimum shockwave dalam PIXEL LAYAR. 0 = tanpa batas
        /// (nyapu sejauh rumus ripple normalnya).</param>
        public static void Trigger(Vector2 worldPos, Color tint, float tintStrength = 0.45f,
            float rippleCount = 3f, float density = 850f, float speed = 22f, float opacityStrength = 6f,
            float mirrorEchoStrength = 0.35f, float maxRange = 0f)
        {
            if (Main.dedServ || Main.gameMenu || _shaderAsset == null) return;

            _progress = 0f;
            _active = true;

            Filters.Scene.Activate(FilterKey, worldPos);

            ScreenShaderData shaderData = Filters.Scene[FilterKey].GetShader();
            shaderData.UseColor(rippleCount, density, speed); // uColor.xyz = jumlah gelombang, kerapatan, kecepatan rambat
            shaderData.UseOpacity(opacityStrength);            // kekuatan distorsi
            shaderData.UseTargetPosition(worldPos);
            shaderData.UseProgress(0f);

            // Parameter custom (uTint, uMaxRange, uMirrorEchoStrength) nggak ada method Use___ bawaan
            // tModLoader buat ini - set langsung ke Effect.Parameters, persis kayak nge-set parameter
            // shader custom lainnya.
            shaderData.Shader.Parameters["uTint"].SetValue(new Vector4(tint.R / 255f, tint.G / 255f, tint.B / 255f, tintStrength));
            shaderData.Shader.Parameters["uMaxRange"].SetValue(maxRange);
            shaderData.Shader.Parameters["uMirrorEchoStrength"].SetValue(mirrorEchoStrength);
        }

        public override void PostUpdateEverything()
        {
            if (!_active || Main.dedServ) return;

            if (!Filters.Scene[FilterKey].IsActive())
            {
                _active = false;
                return;
            }

            _progress += 1f;
            if (_progress >= MaxProgressTicks)
            {
                _active = false;
                Filters.Scene.Deactivate(FilterKey);
                return;
            }

            Filters.Scene[FilterKey].GetShader().UseProgress(_progress);
        }
    }
}