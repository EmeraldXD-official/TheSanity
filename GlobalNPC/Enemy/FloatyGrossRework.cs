using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: FLOATY GROSS CHARGING AURA & BLOODYBACK LAUNCHER
    // =========================================================================
    public class FloatyGrossRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // npc.localAI[1] -> Digunakan sebagai Timer Charging (7 Detik)

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.FloatyGross;
        }

        // =========================================================================
        // [ATTACK & CHARGING TIME LOCATION]: TIMER 7 DETIK (420 FRAMES)
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.FloatyGross) return;

            // Cari player terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            // Timer internal berjalan murni di localAI
            npc.localAI[1]++;

            // -------------------------------------------------------------------------
            // VISUAL EFFECT: Aura Merah Menyusut Setiap Frame (Hanya di Client)
            // -------------------------------------------------------------------------
            if (Main.netMode != NetmodeID.Server)
            {
                float maxTime = 420f; // 7 Detik
                float progress = npc.localAI[1] / maxTime;

                // Tentukan letak dasar kemunculan peluru (Bagian bawah sprite musuh)
                Vector2 spawnPosition = npc.Bottom;

                // Hitung radius aura: dari 120 pixel menyusut ke 5 pixel seiring progress mendekati 1f
                float currentRadius = MathHelper.Lerp(120f, 5f, progress);

                // Buat lingkaran partikel merah di sekeliling bawah sprite
                for (int i = 0; i < 2; i++)
                {
                    Vector2 auraOffset = Main.rand.NextVector2CircularEdge(currentRadius, currentRadius);
                    Dust d = Dust.NewDustDirect(
                        spawnPosition + auraOffset, 
                        0, 0, 
                        DustID.Crimson, // Aura partikel merah pekat Crimson
                        0f, 0f, 
                        150, 
                        Color.Red, 
                        1.0f
                    );
                    d.velocity *= 0.05f; // Jaga partikel agar tidak buyar liar
                    d.noGravity = true;
                }
            }

            // TRIGGER LOCATION: Tepat 420 Frame = 7 Detik!
            if (npc.localAI[1] >= 420)
            {
                npc.localAI[1] = 0; // Reset timer charging

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // LOKASI SPEED & DAMAGE TEMBAKAN BLOODYBACK
                    float shootSpeed = 4f; 
                    int baseDamage = 30; 

                    // Hitung arah dorongan awal peluru ke arah target player
                    Vector2 velocity = target.Center - npc.Bottom;
                    velocity.Normalize();
                    velocity *= shootSpeed;

                    // Spawn proyektil kustom Bloodyback tepat dari bagian bawah sprite musuh
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Bottom, // Sesuai maumu, muncul dari sprite bawah Floaty Gross
                        velocity,
                        ModContent.ProjectileType<Bloodyback>(), 
                        baseDamage,
                        1f,
                        Main.myPlayer
                    );
                }

                // Efek suara dentuman sihir kegelapan saat peluru lepas landas
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, npc.Bottom);
                npc.netUpdate = true;
            }
        }
    }
}