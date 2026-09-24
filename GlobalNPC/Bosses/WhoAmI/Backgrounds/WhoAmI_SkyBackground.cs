using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ============================================================================================
    // WHOAMI - GLASS OVERLAY (POST-TILE SCREEN OVERLAY)
    // ============================================================================================
    // Overlay screen-space (fixed ke layar, gak ke-scroll ke dunia), progress-nya ngikutin PHASE
    // boss WhoAmI. Ada 3 tahap:
    //
    //   STAGE 0 - GLASS UTUH   : default, Phase 1 (sebelum HP nembus 50%)
    //   STAGE 1 - GLASS RETAK  : mulai begitu Phase 2 trigger (HP <= 50%, isPhase2 == true)
    //   STAGE 2 - GLASS PECAH  : mulai begitu ambang Phase 3 gauntlet ke-trigger (HP <= 10%)
    //
    // Threshold 50%/10% ini SENGAJA disamain persis sama kondisi trigger Phase 2 & Phase 3 di
    // WhoAmI.cs - liat NPC.life/NPC.lifeMax langsung dari instance boss real (via
    // WhoAmI.FindRealBossIndex()).
    //
    // Pindah antar stage di-crossfade halus (bukan snap) selama TransitionTicks.
    //
    // ----------------------------------------------------------------------------------------
    // PERUBAHAN vs versi lama (CustomSky):
    // Sebelumnya ini class CustomSky yang didaftarin ke SkyManager - itu artinya digambar di
    // FAR-BACKGROUND PASS, yaitu SEBELUM wall & block (tile solid) digambar. Makanya harus ada
    // hack tambahan (WhoAmIWallHider, GlobalWall.PreDraw return false) buat maksa wall-nya di-skip
    // biar keliatan - dan itu PUN tetep gak nolongin buat block/tile solid, yang bakal tetep
    // nutupin overlay-nya di depan.
    //
    // Sekarang di-refactor: overlay digambar manual dari ModSystem.PostDrawTiles() - hook yang
    // jalan SETELAH semua wall + block selesai digambar untuk frame itu. Jadi overlay otomatis
    // "force draw di atas block" tanpa perlu nyentuh wall/tile draw sama sekali.
    // WhoAmIWallHider (GlobalWall.PreDraw hack) DIHAPUS karena sekarang udah gak perlu - wall
    // digambar normal, overlay-nya nutup di atas semuanya (wall + block) pake blend Additive,
    // jadi bagian item yang di area hitam transparan, garis retak/cahaya-nya nyala nembus block.
    // ----------------------------------------------------------------------------------------
    //
    // ART REQUIREMENT (taruh di folder yang sama):
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmIGlassIntact.png     (full-viewport, ada alpha)
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmIGlassCracked.png    (full-viewport, ada alpha, retak di pinggir)
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmIGlassShattered.png  (full-viewport, ada alpha, pecah + sisa serpihan)
    // Kalau taruh di path lain, tinggal ubah 3 const *Path di bawah.
    // ============================================================================================
    public class WhoAmISkySystem : ModSystem
    {
        private const string GlassIntactPath = "TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmIGlassIntact";
        private const string GlassCrackedPath = "TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmIGlassCracked";
        private const string GlassShatteredPath = "TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmIGlassShattered";

        // Ambang HP buat pindah stage - disamain sama trigger Phase 2 (50%) & Phase 3 (10%) di WhoAmI.cs.
        private const float Phase2HpThreshold = 0.50f;
        private const float Phase3HpThreshold = 0.10f;

        // Berapa lama (tick) crossfade antar stage - 45 tick = ~0.75 detik di 60fps.
        private const int TransitionTicks = 45;

        private bool isActive;
        private bool wasBossActive;
        private int currentStage;      // 0 = utuh, 1 = retak, 2 = pecah
        private int previousStage;     // stage sebelumnya, buat di-crossfade KELUAR selama transisi
        private int transitionTimer = TransitionTicks; // mulai "selesai transisi" (gak crossfade di awal)

        public override void PostUpdateEverything()
        {
            if (Main.dedServ) return; // server gak perlu render apapun

            bool bossActive = WhoAmI.FindRealBossIndex() != -1;

            if (bossActive != wasBossActive)
            {
                if (bossActive)
                {
                    // Boss baru ketemu - reset overlay ke stage 0, gak crossfade dari state lama.
                    isActive = true;
                    currentStage = 0;
                    previousStage = 0;
                    transitionTimer = TransitionTicks;
                }
                else
                {
                    isActive = false;
                }

                wasBossActive = bossActive;
            }

            if (!isActive) return;

            int targetStage = DetermineStageFromBoss();
            if (targetStage != currentStage)
            {
                previousStage = currentStage;
                currentStage = targetStage;
                transitionTimer = 0; // mulai crossfade dari awal
            }

            if (transitionTimer < TransitionTicks) transitionTimer++;
        }

        // Baca isPhase2 & NPC.life/lifeMax langsung dari instance boss REAL (bukan mirage decoy).
        // Kalau boss udah gak ada (misal barusan mati di frame ini), tetep di stage terakhir yang
        // diketahui daripada snap balik ke 0.
        private int DetermineStageFromBoss()
        {
            int idx = WhoAmI.FindRealBossIndex();
            if (idx == -1) return currentStage;

            NPC npc = Main.npc[idx];
            if (npc.ModNPC is not WhoAmI boss) return currentStage;

            float lifePercent = npc.lifeMax > 0 ? (float)npc.life / npc.lifeMax : 1f;

            if (lifePercent <= Phase3HpThreshold) return 2; // pecah
            if (boss.isPhase2 || lifePercent <= Phase2HpThreshold) return 1; // retak
            return 0; // utuh
        }

        private static string GetPathForStage(int stage) => stage switch
        {
            2 => GlassShatteredPath,
            1 => GlassCrackedPath,
            _ => GlassIntactPath,
        };

        // Dipanggil tModLoader SETELAH semua wall + block (tile solid) selesai digambar untuk
        // frame ini - jadi overlay ini otomatis "menang" di atas block tanpa perlu hack apapun
        // di sisi wall/tile.
        public override void PostDrawTiles()
        {
            if (Main.dedServ || !isActive) return;

            var spriteBatch = Main.spriteBatch;

            // Pakai ukuran VIEWPORT AKTUAL (bukan Main.screenWidth/Height yang di-cache), dan
            // Matrix.Identity secara eksplisit - sengaja LEPAS dari transform zoom apapun yang lagi
            // aktif (termasuk dari mod kayak Better Zoom, yang biasanya nge-scale render target).
            // Overlay ini didesain sebagai layer screen-space tetap (kayak vignette/UI).
            var viewport = Main.instance.GraphicsDevice.Viewport;
            var fullScreen = new Rectangle(0, 0, viewport.Width, viewport.Height);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            float progress = TransitionTicks > 0 ? (float)transitionTimer / TransitionTicks : 1f;

            if (previousStage != currentStage && progress < 1f)
            {
                // Crossfade: stage lama fade-out, stage baru fade-in bareng-bareng.
                Texture2D fromTex = ModContent.Request<Texture2D>(GetPathForStage(previousStage), AssetRequestMode.ImmediateLoad).Value;
                Texture2D toTex = ModContent.Request<Texture2D>(GetPathForStage(currentStage), AssetRequestMode.ImmediateLoad).Value;

                spriteBatch.Draw(fromTex, fullScreen, Color.White * (1f - progress));
                spriteBatch.Draw(toTex, fullScreen, Color.White * progress);
            }
            else
            {
                Texture2D tex = ModContent.Request<Texture2D>(GetPathForStage(currentStage), AssetRequestMode.ImmediateLoad).Value;
                spriteBatch.Draw(tex, fullScreen, Color.White);
            }

            spriteBatch.End();
        }
    }
}