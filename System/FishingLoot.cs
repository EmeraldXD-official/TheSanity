using Terraria;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using TheSanity.Items;

namespace TheSanity.Systems
{
    public class FishingLoot : ModPlayer
    {
        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn,
            ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
        {
            if (Main.eclipse) 
            {
                if (Main.rand.NextBool(5))
                {
                    itemDrop = ModContent.ItemType<TomeOfEclipsa>();
                    sonar.Text = "Eclipse Catch!";
                    sonar.Color = Color.Purple;
                    sonar.DurationInFrames = 240;
                }
            }
            if (Player.ZoneBeach && !attempt.inLava && !attempt.inHoney)
            {
                // 10% peluang untuk mendapatkan CoralPistol
                if (Main.rand.NextBool(10)) 
                {
                    itemDrop = ModContent.ItemType<CoralPistol>();
                    
                    // Notifikasi Sonar
                    sonar.Text = "Deep Sea Relic!";
                    sonar.Color = Color.LightBlue;
                    sonar.DurationInFrames = 240;
                }
            }
        }
    }
}