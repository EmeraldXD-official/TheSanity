using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class BoneLeeRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =========================================================================
        // VARIABEL KUSTOM MANDIRI (MURNI UNTUK SKILL DASH LUNCUR & DODGE)
        // =========================================================================
        private int dashCooldownTimer = 0;   // Cooldown antar skill dash (3 detik)
        private bool isDashing = false;       // Apakah saat ini sedang meluncur kencang?
        private int dashTimer = 0;           // Menghitung frame selama proses meluncur aktif
        private Vector2 dashVelocity = Vector2.Zero; // Menyimpan arah kecepatan konstan saat meluncur

        private bool hasDodgedFirstAttack = false; // Melacak dodge pertama (100%)

        // =========================================================================
        // FIX TARGET: Pastikan kode ini hanya menyuntik NPC Bone Lee asli!
        // =========================================================================
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.BoneLee; 
        }

        // =========================================================================
        // 1. LOGIKA SKILL DASH MELUNCUR (BUKAN TELEPORT)
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.BoneLee) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player player = Main.player[npc.target];
            if (!player.active || player.dead) return;

            if (dashCooldownTimer > 0 && !isDashing)
            {
                dashCooldownTimer--;
            }

            // --- JIKA SEDANG DALAM PROSES MELUNCUR (DASHING) ---
            if (isDashing)
            {
                npc.velocity = dashVelocity;

                if (Main.rand.NextBool(2))
                {
                    int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.DungeonWater, 0f, 0f, 150, default(Color), 1.3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = Vector2.Zero;
                }

                dashTimer--;
                if (dashTimer <= 0)
                {
                    isDashing = false;
                    npc.velocity *= 0.2f; // Rem mendadak setelah menempuh jarak dash
                }
                return;
            }

            // --- PEMICU (TRIGGER) SKILL DASH ---
            // -------------------------------------------------------------------------
            // [AI DISTANCE BALANCING]: Jarak pemicu dash (20 Blok)
            // -------------------------------------------------------------------------
            float triggerDistance = 20f * 16f; 
            float currentDistance = Vector2.Distance(npc.Center, player.Center);

            if (dashCooldownTimer <= 0 && currentDistance <= triggerDistance)
            {
                // -------------------------------------------------------------------------
                // [AI COOLDOWN BALANCING]: Cooldown skill dash (180 Ticks = 3 Detik)
                // -------------------------------------------------------------------------
                dashCooldownTimer = 180; 

                Vector2 direction = (player.Center - npc.Center).SafeNormalize(Vector2.UnitX);

                // -------------------------------------------------------------------------
                // [DASH SPEED & DURATION BALANCING]: Kecepatan luncur dan total frame durasi
                // Jarak 40 blok, kecepatan 20f = aktif selama 32 frame (32 * 20 = 640 pixel / 16 = 40 blok)
                // -------------------------------------------------------------------------
                float dashSpeed = 20f;
                dashTimer = 32; 

                isDashing = true;
                dashVelocity = direction * dashSpeed;
                npc.velocity = dashVelocity;

                for (int d = 0; d < 15; d++)
                {
                    int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke, 0f, 0f, 100, default(Color), 1.2f);
                    Main.dust[dust].velocity = Main.rand.NextVector2Circular(4f, 4f);
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item60, npc.Center);
                npc.netUpdate = true;
            }
        }

        // =========================================================================
        // 2. REWORK LOGIKA DODGE: FIX MUTLAK 0 DAMAGE & SEMBUNYIKAN ANGKA BAWAAN
        // =========================================================================
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type != NPCID.BoneLee) return;

            // Cek apakah ini serangan pertama SEKALI semenjak dia spawn
            if (!hasDodgedFirstAttack)
            {
                hasDodgedFirstAttack = true; // Kunci status agar tidak loop spam
                
                // --- PROSES FORCE DODGE MUTLAK (0 DAMAGE) ---
                modifiers.SetMaxDamage(0);      // Mengunci batas maksimum damage ke angka 0
                modifiers.FinalDamage *= 0;     // Memastikan kalkulasi akhir tetap 0
                modifiers.DisableKnockback();   // Menahan efek guncangan knockback dari player
                modifiers.HideCombatText();     // Menyembunyikan text angka merah 1 / 0 bawaan game
                
                TriggerImmortalDodge(npc);
                npc.netUpdate = true;
                return;
            }

            // Serangan berikutnya: Peluang hoki murni 10% (0.10f)
            // -------------------------------------------------------------------------
            // [DODGE CHANCE BALANCING]: Ubah nilai desimal di bawah untuk mengatur peluang hindaran berikutnya
            // -------------------------------------------------------------------------
            if (Main.rand.NextFloat() < 0.10f)
            {
                // --- PROSES FORCE DODGE MUTLAK (0 DAMAGE) ---
                modifiers.SetMaxDamage(0);      // Mengunci batas maksimum damage ke angka 0
                modifiers.FinalDamage *= 0;     // Memastikan kalkulasi akhir tetap 0
                modifiers.DisableKnockback();   // Menahan efek guncangan knockback dari player
                modifiers.HideCombatText();     // Menyembunyikan text angka merah 1 / 0 bawaan game
                
                TriggerImmortalDodge(npc);
                npc.netUpdate = true;
                return;
            }
        }

        // Fungsi internal untuk memicu i-frame imortal dan efek visualnya saat dodge sukses
        private void TriggerImmortalDodge(NPC npc)
        {
            // --- LOKASI BALANCING DURASI IMMORTAL / INVINCIBLE IFRAME ---
            // Berikan i-frame bawaan game agar kebal mutlak dari segala rentetan serangan peluru/pedang
            // 30 Frame = 0.5 Detik Kebal Total
            npc.immune[Main.myPlayer] = 30; 

            // Munculkan teks melayang berwarna hitam tepat satu kali
            CombatText.NewText(npc.getRect(), Color.Black, "Dodge!", true);

            // Efek asap ninja
            for (int i = 0; i < 15; i++)
            {
                int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke, 0f, 0f, 100, default(Color), 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3f, 3f);
            }

            // Suara hindaran ninja
            Terraria.Audio.SoundEngine.PlaySound(SoundID.DoubleJump, npc.Center);
        }
    }
}