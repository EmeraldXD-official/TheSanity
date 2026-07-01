using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class SharkRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int attackTimer = 0;
        public bool isDiving = false;
        public bool isJumping = false;

        public override bool PreAI(NPC npc)
        {
            // --- 1. CEK ID HIU ---
            bool isShark = npc.type == 542 || npc.type == 543 || npc.type == 544 || npc.type == 545 || npc.type == 65;
            if (!isShark) return true;

            // --- FIX ERROR: Pengecekan Target yang Aman ---
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
            {
                return true; // Keluar agar tidak lanjut ke logika yang butuh target
            }

            Player target = Main.player[npc.target];

            // --- 2. LOGIKA TRANSIKI KE SKILL ---
            float dist = Vector2.Distance(npc.Center, target.Center);

            if (!isJumping && !isDiving)
            {
                attackTimer++;
                if (attackTimer >= 240 && dist < 400f) // Setiap 4 detik
                {
                    isJumping = true;
                    attackTimer = 0;
                    npc.velocity.Y = -16f; // Loncat lebih tinggi
                    npc.netUpdate = true; // Sinkronisasi multiplayer
                }
            }

            // --- 3. OVERRIDE AI ASLI ---
            // Jika sedang loncat atau diving, JANGAN jalankan AI bawaan Terraria
            if (isJumping || isDiving)
            {
                ExecuteDiveSkill(npc, target);
                return false; // Mencegah AI asli menimpa velocity kita
            }

            return true;
        }

        private void ExecuteDiveSkill(NPC npc, Player target)
        {
            npc.noTileCollide = true; // Tembus tanah saat skill aktif

            if (isJumping && !isDiving)
            {
                // Melayang ke atas dan tracking posisi X player
                if (npc.Center.Y > target.Center.Y - 300f)
                {
                    npc.velocity.Y = -12f;
                }
                else
                {
                    npc.velocity.Y *= 0.8f; // Ngerem di atas
                    
                    // Kejar posisi X
                    float diffX = target.Center.X - npc.Center.X;
                    npc.velocity.X = Math.Sign(diffX) * 9f;

                    // Kalau sudah pas di atas kepala (toleransi 30 pixel)
                    if (Math.Abs(diffX) < 30f)
                    {
                        isDiving = true;
                        isJumping = false;
                        npc.netUpdate = true;
                    }
                }
            }
            else if (isDiving)
            {
                // LOKASI MOMENTUM: 22f (Sangat Cepat)
                npc.velocity.X = 0;
                npc.velocity.Y = 22f;

                // Efek visual Sandnado/Dust
                Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Sandnado, 0, 0, 100, default, 1.3f);
                d.noGravity = true;

                // Reset jika sudah melewati player atau menyentuh dasar
                if (npc.Center.Y > target.Center.Y + 100f || (npc.velocity.Y == 0 && npc.oldVelocity.Y > 0))
                {
                    isDiving = false;
                    npc.noTileCollide = false;
                    npc.velocity *= 0.5f; // Ngerem pas mendarat
                    npc.netUpdate = true;
                    
                    // Suara hantaman
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, npc.Center);
                }
            }
        }
    }
}