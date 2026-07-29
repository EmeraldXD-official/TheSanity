using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class GolemCameraPlayer : ModPlayer
    {
        private float customZoom = 1f;

        public override void ModifyScreenPosition()
        {
            // EKSKLUSIF: Hanya mencari GolemHeadFree (Kepala Lepas)
            int targetNPCIndex = NPC.FindFirstNPC(NPCID.GolemHeadFree);

            bool bossActive = false;

            if (targetNPCIndex != -1)
            {
                NPC golemTarget = Main.npc[targetNPCIndex];

                if (golemTarget.active)
                {
                    float maxDistance = 500f * 16f;
                    float currentDistance = Vector2.Distance(Player.Center, golemTarget.Center);

                    if (currentDistance <= maxDistance)
                    {
                        bossActive = true;

                        // Menggunakan racikan angka Lerp milikmu yang sudah pas!
                        Vector2 focusCenter = Vector2.Lerp(golemTarget.Center, Player.Center, 0.01f);

                        Vector2 targetScreenPos = focusCenter - new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);

                        Main.screenPosition = Vector2.Lerp(Main.screenPosition, targetScreenPos, 0.50f);
                    }
                }
            }

            // Sistem Zoom Out
            if (bossActive)
            {
                customZoom = MathHelper.Lerp(customZoom, 0.70f, 0.02f); 
            }
            else
            {
                customZoom = MathHelper.Lerp(customZoom, 1f, 0.02f);
            }

            Main.GameViewMatrix.Zoom = new Vector2(customZoom);
        }
    }
}