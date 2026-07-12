using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public class WhoAmICutscenePlayer : ModPlayer
    {
        private bool wasCutsceneActive = false;

        // ================== FIX: kamera "ngesot"/lompat di detik-detik terakhir cutscene ==================
        // Bug lama: begitu WhoAmI.IsCutsceneActive balik ke false, kontrol kamera langsung dilepas ke
        // vanilla di FRAME YANG SAMA. Karena screenPosition sebelumnya dikunci ke titik tengah
        // boss+player (bukan ke player doang seperti kamera normal), pelepasan mendadak ini kelihatan
        // sebagai lompatan kamera yang tiba-tiba persis pas cutscene lagi di detik-detik terakhirnya.
        // Fix: begitu cutscene berhenti, jangan langsung serahkan kontrol - blend screenPosition
        // pelan-pelan (ease-out, ~1/3 detik) dari posisi terakhir cutscene menuju posisi normal vanilla.
        //
        // CATATAN: cuma POSISI kamera aja yang di-lock/di-blend di sini. Zoom SENGAJA nggak pernah
        // disentuh (nggak ada Main.GameZoomTarget di file ini sama sekali) biar mod zoom manual kayak
        // Better Zoom tetap pegang kontrol penuh atas zoom-nya, nggak ketimpa/direbut cutscene ini.
        private const int ReleaseBlendDuration = 20;
        private int releaseBlendTimer = 0;
        private Vector2 releaseBlendFrom = Vector2.Zero;

        public override void PostUpdateBuffs()
        {
            int bossIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
            if (bossIndex != -1)
            {
                var boss = Main.npc[bossIndex].ModNPC as WhoAmI;
                if (boss != null && boss.activePotionType == 1 && Main.npc[bossIndex].target == Player.whoAmI)
                {
                    Player.gravDir = -1f;
                    return;
                }
            }
            Player.gravDir = 1f;
        }

        public override void ModifyScreenPosition()
        {
            if (WhoAmI.IsCutsceneActive)
            {
                wasCutsceneActive = true;

                Main.screenPosition = WhoAmI.CutsceneCameraTarget - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                if (WhoAmI.CutsceneShakeIntensity > 0f)
                    Main.screenPosition += Main.rand.NextVector2Circular(WhoAmI.CutsceneShakeIntensity, WhoAmI.CutsceneShakeIntensity);

                // Cutscene masih aktif -> selalu siap buat blend keluar kapan pun dia berhenti.
                releaseBlendTimer = ReleaseBlendDuration;
            }
            else
            {
                if (wasCutsceneActive)
                {
                    // Momen persis cutscene berhenti: simpan posisi terakhirnya sebagai titik awal
                    // blend, JANGAN langsung lompat ke posisi vanilla.
                    wasCutsceneActive = false;
                    releaseBlendFrom = Main.screenPosition;
                    releaseBlendTimer = ReleaseBlendDuration;
                }

                if (releaseBlendTimer > 0)
                {
                    Vector2 vanillaTarget = Player.Center - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                    float t = 1f - (releaseBlendTimer / (float)ReleaseBlendDuration);
                    float ease = 1f - (float)Math.Pow(1f - t, 3); // ease-out biar lembut di akhir
                    Main.screenPosition = Vector2.Lerp(releaseBlendFrom, vanillaTarget, ease);
                    releaseBlendTimer--;
                }
            }
        }
    }
}