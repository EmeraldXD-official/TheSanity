using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using TheSanity.Projectiles;

namespace TheSanity
{
    public class RitualTriangleSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            // Menghindari duplikasi spawn paket data di dalam server multiplayer client
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                bool isTriangleExists = false;

                // Memindai apakah proyektil RitualTriangle sudah eksis di world data
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<RitualTriangle>())
                    {
                        isTriangleExists = true;
                        break;
                    }
                }

                // =========================================================================
                // [AUTOMATIC SPAWN TRIGGER]: JIKA BELUM LAHIR, SPAWN INSTAN 1 BUAH DI KOORDINAT PLAYER
                // =========================================================================
                if (!isTriangleExists)
                {
                    Projectile.NewProjectile(
                        null, 
                        Main.LocalPlayer.Center, 
                        Vector2.Zero, 
                        ModContent.ProjectileType<RitualTriangle>(), 
                        0, // Damage 0 karena murni jebakan mekanik & kosmetik visual
                        0f, 
                        Main.myPlayer
                    );
                }
            }
        }
    }
}