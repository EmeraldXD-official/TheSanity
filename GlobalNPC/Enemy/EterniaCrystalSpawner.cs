using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class EterniaCrystalSpawner : global::Terraria.ModLoader.GlobalNPC
    {
        // Menggunakan InstancePerEntity agar variabel pelacak aman
        public override bool InstancePerEntity => true;

        // Variabel untuk memastikan perisai HANYA dicreate 1 kali saja saat kristal spawn
        private bool hasSpawnedShield = false;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Script ini hanya bekerja pada musuh/objek DD2 Eternia Crystal bawaan vanilla
            return entity.type == NPCID.DD2EterniaCrystal;
        }

        public override void AI(NPC npc)
        {
            // Pastikan tidak berjalan di sisi client multiplayer untuk mencegah desync/duplikasi proyektil
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // Jika kristal aktif DAN kita belum pernah memunculkan tameng sama sekali
            if (npc.active && !hasSpawnedShield)
            {
                hasSpawnedShield = true; // Kunci status agar tidak menembakkan proyektil terus-menerus (anti-spam)

                // =========================================================================
                // LOGIKA PANGGIL TAMENG KE PUSAT KRISTAL
                // =========================================================================
                int shieldProjectile = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),              // Sumber AI dari kristal
                    npc.Center,                          // Posisi lahir tepat di titik tengah kristal
                    Vector2.Zero,                        // Kecepatan Nol (karena tamengnya diam mengorbit)
                    ModContent.ProjectileType<EterniaShieldHub>(), // Memanggil class tameng kamu
                    0,                                   // Damage tabrakan tameng (0 karena murni proteksi)
                    0f,                                  // Knockback (0f karena menggunakan sistem pushback kustom)
                    Main.myPlayer                        // Pemilik proyektil (server/host player)
                );

                // Sinkronisasi data ke server multiplayer jika diperlukan
                if (shieldProjectile != Main.maxProjectiles)
                {
                    Main.projectile[shieldProjectile].netUpdate = true;
                }
            }
        }
    }
}