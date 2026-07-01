using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using TheSanity.Projectiles;

namespace TheSanity
{
    public class SunRingSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            // Logika pemanggilan hanya berjalan di sisi server/singleplayer agar tidak duplikasi di multiplayer
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                bool isSunRingExists = false;

                // Cari di seluruh dunia apakah projectile RingSun sudah dibuat atau belum
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<RingSunProjectile>())
                    {
                        isSunRingExists = true;
                        break;
                    }
                }

                // Jika belum ada di dunia, panggil paksa 1 biji visual ring sun-nya
                if (!isSunRingExists)
                {
                    Projectile.NewProjectile(
                        null, 
                        Main.LocalPlayer.Center, // Spawn awal di koordinat player, nanti AI-nya bakal langsung teleport otomatis ke Altar/Golem
                        Vector2.Zero, 
                        ModContent.ProjectileType<RingSunProjectile>(), 
                        0, // Damage 0 murni kosmetik
                        0f, 
                        Main.myPlayer
                    );
                }
            }
        }
    }
}