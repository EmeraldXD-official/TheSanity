using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [ENEMY REWORK SYSTEM]: UMBRELLA SLIME PARACHUTE SMOOTH SLOW-FALL PHYSICS
    // =========================================================================
    public class UmbrellaSlimeRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Memastikan efek ini HANYA menempel pada Umbrella Slime vanilla
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return entity.type == NPCID.UmbrellaSlime;
        }

        public override void AI(NPC npc) {
            if (Main.gameMenu || Main.dedServ) return;

            // -------------------------------------------------------------------------
            // DETEKSI KONDISI MELAYANG TURUN (SLOW FALL PHYSICS)
            // -------------------------------------------------------------------------
            // npc.velocity.Y > 0f artinya Slime sudah melewati puncak lompatan dan sedang bergerak turun (jatuh)
            if (npc.velocity.Y > 0f) {
                
                // LOKASI BALANCING GRAVITASI JATUH (SLOW FALL): Default 1.5f
                // Semakin kecil angkanya, semakin lambat & melayang jatuhnya Umbrella Slime.
                float maxFallSpeed = 1.5f;

                if (npc.velocity.Y > maxFallSpeed) {
                    // Batasi kecepatan vertikalnya agar menahan gravitasi, mirip efek Buff Featherfall player
                    npc.velocity.Y = maxMathLerp(npc.velocity.Y, maxFallSpeed, 0.2f); 
                }

                // Kunci Rahasia Momentum: Kita SAMA SEKALI tidak mengubah npc.velocity.X di sini!
                // Hasilnya, dia akan meluncur turun secara diagonal dengan sangat mulus membawa kecepatan loncat aslinya.

                // -------------------------------------------------------------------------
                // VISUAL EMBLISHMENT: EFEK PAYUNG BERAYUN (SWAY EFFECT)
                // -------------------------------------------------------------------------
                // Membuat sprite slime sedikit bergoyang dinamis menggunakan gelombang Sinus saat melayang turun
                float swayRotation = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.08f;
                npc.rotation = swayRotation;

                // Tambahan efek debu udara tipis di bawah slime sebagai penanda dia sedang menahan angin
                if (Main.rand.NextBool(10)) {
                    int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Cloud, 0f, npc.velocity.Y, 150, default, 0.8f);
                    Main.dust[dust].velocity.X *= 0.2f;
                }
            }
            else {
                // Kembalikan rotasi normal bawaan AI jika sedang diam atau baru mulai melompat naik
                npc.rotation = 0f;
            }
        }

        // Fungsi pembantu matematika lokal agar transisi pengereman jatuh terasa halus
        private float maxMathLerp(float current, float target, float speed) {
            return current + (target - current) * speed;
        }
    }
}