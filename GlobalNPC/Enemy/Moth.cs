using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class MothRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Custom timer untuk jeda serangan 7 detik
        public int cloudTimer = 0;

        public override bool PreAI(NPC npc)
        {
            // LOKASI ID TARGET: 205 (Moth biasa)
            if (npc.type != NPCID.Moth) return true; 

            // --- FIX ERROR: Pengecekan Target yang Aman ---
            // Memastikan npc.target berada dalam range yang valid dan pemain aktif/tidak mati
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
            {
                return true; // Keluar dari fungsi jika target tidak valid untuk menghindari NullReferenceException
            }

            Player target = Main.player[npc.target];

            // Selalu menghadap player horizontal
            npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
            npc.spriteDirection = npc.direction;

            // --- LOGIKA SPAWN CLOUD (TIAP 7 DETIK) ---
            cloudTimer++;

            // LOKASI TIMING ATTACK: 420 frame = 7 Detik
            if (cloudTimer >= 420)
            {
                cloudTimer = 0; // Reset timer

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Hitung arah awal dorongan spawn projectile ke arah player
                    Vector2 shootVelocity = (target.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.26f;

                    // Spawn Projectile kustom kita (MothCloud)
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVelocity,
                        ModContent.ProjectileType<MothCloud>(),
                        15, // LOKASI DAMAGE PROJECTILE
                        0f,
                        Main.myPlayer
                    );
                }

                // Efek suara kepakan sayap/sihir halus saat melepas kabut
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item45, npc.Center); 
            }

            // Kembalikan true agar AI terbang bawaan asli Moth tetap berfungsi normal
            return true;
        }
    }
}