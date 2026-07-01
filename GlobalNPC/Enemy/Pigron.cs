using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.GlobalNPC.Enemy
{
    public class PigronRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer serangan batuk bubble khusus tiap individu Pigron
        private int bubbleAttackTimer = 0;
        private int bubbleCooldown = 300; // Jeda antar serangan batuk (5 detik)

        // ========================================================================
        // 1. PENGATURAN SPAWN RATE DI OCEAN BIOME (PRE-HARDMODE & HARDMODE)
        // ========================================================================
        public override void EditSpawnPool(System.Collections.Generic.IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            // Cek apakah player sedang berada di Ocean Biome (Ujung peta kiri/kanan)
            if (spawnInfo.Player.ZoneBeach)
            {
                // LOKASI SPELUANG SPAWN (CHANCE): 
                // Hardmode = 25% (0.25f), Pre-Hardmode = 5% (0.05f)
                float spawnChance = Main.hardMode ? 0.25f : 0.05f;

                // Daftarkan ketiga ID Pigron ke ekosistem pantai
                if (!pool.ContainsKey(NPCID.PigronCorruption)) pool.Add(NPCID.PigronCorruption, spawnChance);
                if (!pool.ContainsKey(NPCID.PigronCrimson)) pool.Add(NPCID.PigronCrimson, spawnChance);
                if (!pool.ContainsKey(NPCID.PigronHallow)) pool.Add(NPCID.PigronHallow, spawnChance);
            }
        }

        // ========================================================================
        // 2. MEKANIK SERANGAN BATUK BUBBLE & EFEK KNOCKBACK MANDIRI
        // ========================================================================
        public override bool PreAI(NPC npc)
        {
            // Validasi agar hanya mendeteksi ketiga jenis Pigron
            if (npc.type != NPCID.PigronCorruption && npc.type != NPCID.PigronCrimson && npc.type != NPCID.PigronHallow) return true;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target == null || target.dead || !npc.HasValidTarget) return true;

            // Jalankan cooldown frame
            if (bubbleCooldown > 0) bubbleCooldown--;

            // Trigger Serangan: Cooldown habis dan berjarak cukup dekat dengan player (di bawah 45 block)
            if (bubbleCooldown == 0 && Vector2.Distance(npc.Center, target.Center) < 720f)
            {
                bubbleAttackTimer++;

                // LOKASI INTERVAL BATUK: Mengeluarkan gelembung setiap 3 frame sekali
                if (bubbleAttackTimer % 3 == 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVel = target.Center - npc.Center;
                        shootVel.Normalize();
                        
                        // LOKASI SPEED BUBBLE: Kecepatan semburan gelembung acak (4f sampai 8f) biar estetik
                        shootVel *= Main.rand.NextFloat(4f, 8f);
                        shootVel = shootVel.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)); // Sedikit sebaran sudut acak

                        // LOKASI DAMAGE BASE BUBBLE: Dikunci di 30 sesuai request (Biar Master Mode otomatis mengalikan sendiri)
                        int bubbleDamage = 30;
                        if (Main.expertMode) bubbleDamage = (int)(bubbleDamage / (Main.masterMode ? 6f : 4f));

                        // Spawn Projectile ID 405 = CuteFishronBubble (Gelembung air hostile Duke Fishron)
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.Bubble, bubbleDamage, 1f, Main.myPlayer);
                        if (p != Main.maxProjectiles)
                        {
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].hostile = true;
                        }
                    }

                    // --- EFEK JEDA/BATUK KNOCKBACK MANDIRI ---
                    // Memberikan dorongan mundur berlawanan arah dengan target player setiap kali meletupkan gelembung
                    Vector2 knockbackDir = npc.Center - target.Center;
                    knockbackDir.Normalize();
                    
                    // LOKASI SPEED KNOCKBACK BATUK: 3.5f (Bisa dinaikkan kalau mau efek terpentalnya lebih jauh)
                    npc.velocity = knockbackDir * 3.5f; 

                    // Efek suara gelembung keluar
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item85, npc.Center); 
                }

                // LOKASI JUMLAH BUTIR BUBBLE: Durasi total menyerang diacak antara 60 hingga 120 frame (Menghasilkan ~20 hingga 40 butir bubble)
                if (bubbleAttackTimer >= Main.rand.Next(60, 121))
                {
                    bubbleAttackTimer = 0;
                    bubbleCooldown = 300; // Reset Cooldown balik ke 5 detik
                    npc.netUpdate = true;
                }
            }

            return true; // Tetap gunakan AI terbang/tembus dinding bawaan Pigron agar dia lincah
        }

        // ========================================================================
        // 3. MEKANIK KEMATIAN: SPAWN SHARKNADO DI LOKASI PIGRON MATI
        // ========================================================================
        public override void OnKill(NPC npc)
        {
            if (npc.type != NPCID.PigronCorruption && npc.type != NPCID.PigronCrimson && npc.type != NPCID.PigronHallow) return;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // LOKASI DAMAGE SHARKNADO: Base damage tornado air (set 40 biar ngeri di Master Mode)
                int nadoDamage = 40;
                if (Main.expertMode) nadoDamage = (int)(nadoDamage / (Main.masterMode ? 6f : 4f));

                // Spawn Projectile ID 386 = Sharknado Bolt (Akan langsung mekar jadi pusaran air Sharknado raksasa di tempat)
                // Kecepatan di-set Vector2.Zero dan arah Y ke bawah (0, 4f) agar langsung mengunci posisi tanah tempat dia mati
                int p = Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, new Vector2(0f, 4f), ProjectileID.SharknadoBolt, nadoDamage, 3f, Main.myPlayer);
                if (p != Main.maxProjectiles)
                {
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].hostile = true;
                }
            }
        }
    }
}