using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System.Collections.Generic;
using System;

namespace TheSanity
{
    public class DemonEyeDashAI : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private static readonly HashSet<int> DemonEyeIDs = new HashSet<int>
        {
            2, -43, 190, -38, 191, -39, 192, -40, 193, -41, 194, -42, 317, 318, 133, 587, 23
        };

        public int dashTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (DemonEyeIDs.Contains(npc.type))
            {
                // =========================================================================
                // [DAMAGE OVERRIDE REMOVED]: DAMAGE SEKARANG 100% PURE DARI SYSTEM VANILLA
                // =========================================================================
                
                dashTimer++;

                // --- ABA-ABA: EFEK GLOWING PULSE DARI CODING ASLI (Detik 2.5 sampai 3) ---
                if (dashTimer >= 150 && dashTimer < 180)
                {
                    // Membuat percikan cahaya kuning tebal di sekeliling badan
                    for (int i = 0; i < 3; i++) 
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.GoldFlame, 0, 0, 100, default, 1.8f);
                        d.noGravity = true;
                        d.velocity *= 1.2f; // Partikel sedikit menjauh dari badan biar kelihatan "nge-flare"
                    }
                }

                // --- EKSEKUSI DASH: LINGKARAN PARTIKEL TEBAL DARI CODING ASLI ---
                if (dashTimer >= 180)
                {
                    Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                    if (target.active && !target.dead)
                    {
                        Vector2 dashDirection = Vector2.Normalize(target.Center - npc.Center);
                        npc.velocity = dashDirection * 15f;

                        SoundEngine.PlaySound(SoundID.Item131, npc.Center);

                        // MEMBUAT POLA LINGKARAN SEUKURAN BADAN
                        float radius = 20f; // Ukuran lingkaran mengikuti rata-rata badan Demon Eye
                        for (int i = 0; i < 20; i++) // 20 partikel membentuk lingkaran
                        {
                            // Rumus matematika untuk lingkaran
                            float angle = MathHelper.ToRadians(i * (360f / 20f));
                            Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                            
                            Dust d = Dust.NewDustPerfect(npc.Center + offset, DustID.GoldFlame, offset * 0.1f, 100, default, 2.0f);
                            d.noGravity = true;
                            d.fadeIn = 1.2f; // Efek memudar yang halus tapi terang
                        }

                        // Efek "Trail" tambahan di belakang saat baru mulai dash
                        for (int j = 0; j < 10; j++)
                        {
                            Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.GoldFlame, -npc.velocity.X * 0.5f, -npc.velocity.Y * 0.5f, 100, default, 1.5f);
                            d.noGravity = true;
                        }
                    }
                    dashTimer = 0;
                }
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (DemonEyeIDs.Contains(npc.type))
            {
                target.AddBuff(30, 300);
            }
        }

        /* GUIDE PARTIKEL (TETAP DIPERTAHANKAN):
           - Radius: 'float radius = 20f' (Ubah jika lingkaran ingin lebih lebar).
           - Tebal: Angka '20' di looping for adalah jumlah partikel penyusun lingkaran.
           - Scale: '2.0f' membuat partikel jauh lebih besar dan "nge-glow".
        */
    }
}