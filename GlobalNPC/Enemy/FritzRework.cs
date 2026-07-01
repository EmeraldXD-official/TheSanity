using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    public class FritzRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private float distanceCounter = 0f;

        public override void PostAI(NPC npc)
        {
            if (npc.type == NPCID.Fritz)
            {
                float distanceMoved = Vector2.Distance(npc.position, npc.oldPosition);

                if (distanceMoved > 0f && distanceMoved < 100f)
                {
                    distanceCounter += distanceMoved;
                }

                // -------------------------------------------------------------------------
                // [BALANCING LOCATION 1: AMBANG BATAS 5 BLOCK]
                // - 1 Block = 16f Pixel. Jadi 5 Block = 80f Pixel.
                // - Ganti angka 80f di bawah ini jika ingin mengubah frekuensi jarak block-nya.
                // -------------------------------------------------------------------------
                float distanceThreshold = 80f;

                while (distanceCounter >= distanceThreshold)
                {
                    distanceCounter -= distanceThreshold;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // -------------------------------------------------------------------------
                        // [BALANCING LOCATION 2: KECEPATAN LEMPAR KE ATAS]
                        // =========================================================================
                        Vector2 launchVelocity = new Vector2(0f, -7f);

                        // [BALANCING LOCATION 3: DAMAGE PELURU]
                        int damage = 50;

                        // -------------------------------------------------------------------------
                        // PENENTUAN SPRITE GORE SECARA ACAK (3, 4, atau 5)
                        // Kita acak di sini dan kirim datanya ke Projectile via parameter ai0.
                        // -------------------------------------------------------------------------
                        int randomGoreSlot = Main.rand.Next(3, 6); // Menghasilkan angka 3, 4, atau 5

                        Projectile.NewProjectile(
                            npc.GetSource_FromThis(),
                            npc.Top, 
                            launchVelocity,
                            ModContent.ProjectileType<FritzGoreProjectile>(),
                            damage,
                            1f, 
                            Main.myPlayer,
                            ai0: randomGoreSlot // Menyuntikkan ID Gore terpilih secara aman
                        );
                    }
                }
            }
        }
    }
}