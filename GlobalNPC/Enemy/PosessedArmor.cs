using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class ReworkedPossessedArmor : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer untuk menghitung seberapa lama player menatap NPC ini
        private int stareTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.PossessedArmor;
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.PossessedArmor)
            {
                // =========================================================================
                // [KNOCKBACK IMMUNITY LOCATION]: MEMBUAT NPC KEBAL TERHADAP SENGGATAN SENJATA
                // =========================================================================
                npc.knockBackResist = 0f; // 0f berarti kebal total 100% dari knockback apa pun
            }
        }

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.PossessedArmor) return;

            // Selalu kunci target ke player terdekat yang hidup
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            // Pengecekan apakah player sedang memiliki Debuff Obstructed (ID 163)
            bool playerIsObstructed = target.HasBuff(BuffID.Obstructed);

            // 1. Logika Mendeteksi Apakah Player Melihat ke Arah NPC
            bool playerIsLooking = false;

            // Jika player di sebelah kiri NPC dan menghadap ke kanan (X player < X NPC && direction == 1)
            if (target.Center.X < npc.Center.X && target.direction == 1)
            {
                playerIsLooking = true;
            }
            // Jika player di sebelah kanan NPC dan menghadap ke kiri (X player > X NPC && direction == -1)
            else if (target.Center.X > npc.Center.X && target.direction == -1)
            {
                playerIsLooking = true;
            }

            // =========================================================================
            // [BEHAVIOR CORE]: PENGATURAN STATUS GERAK & KEBAL BERDASARKAN TATAPAN
            // =========================================================================
            // Jika player terkena Obstructed, Possessed Armor BEBAS bergerak & menyerang mau dilihat atau tidak!
            if (playerIsObstructed)
            {
                npc.dontTakeDamage = false;
                stareTimer = 0; // Reset timer tatapan selama masa buta
                // Biarkan AI bawaan vanilla berjalan normal (berjalan mengejar player)
            }
            // Jika tidak terkena Obstructed, cek apakah sedang dilihat player
            else if (playerIsLooking)
            {
                npc.dontTakeDamage = true; // Kebal dari segala jenis serangan player
                npc.velocity.X = 0f;       // Diam membeku di tempat (X)
                if (npc.velocity.Y > 0) npc.velocity.Y = 0f; // Diam membeku saat jatuh (Y)
                
                npc.ai[0] = 0; // Mengunci internal timer AI bawaan agar tidak melompat/berjalan

                // =========================================================================
                // [TIMER STARE LOCATION]: DURASI MENATAP SEBELUM TERKENA DEBUFF
                // =========================================================================
                stareTimer++;
                if (stareTimer >= 180) // 180 Frame = Tepat 3 Detik player menatap tanpa berkedip
                {
                    // Berikan debuff Obstructed (ID 163) selama 5 Detik (5 * 60 Frame = 300 Frame)
                    target.AddBuff(BuffID.Obstructed, 300); 
                    stareTimer = 0; // Reset timer
                }
            }
            else
            {
                // Jika player tidak melihat, NPC bisa diserang dan bergerak normal
                npc.dontTakeDamage = false;
                stareTimer = 0; // Reset timer karena player berpaling arah
            }
        }

        // =========================================================================
        // [ON-KILL MECHANIC LOCATION]: MEMUNCULKAN GHOST SAAT POSSESSED ARMOR MATI
        // =========================================================================
        public override void OnKill(NPC npc)
        {
            if (npc.type == NPCID.PossessedArmor)
            {
                // Memastikan spawn hanya dieksekusi oleh Server utama saat bermain Multiplayer
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Memunculkan Ghost (ID 288) tepat di posisi tengah (Center) baju zirah yang hancur
                    int ghostID = NPC.NewNPC(npc.GetSource_Death(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.Ghost);
                    
                    if (ghostID < Main.maxNPCs)
                    {
                        Main.npc[ghostID].velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -4f); // Kasih efek lompat kecil ke atas pas keluar
                        Main.npc[ghostID].netUpdate = true; // Sinkronisasi ke server
                    }
                }
            }
        }

        // =========================================================================
        // [TELEPORT MECHANIC LOCATION]: SKILL TELEPORT SAAT DI-HIT DALAM KONDISI OBSTRUCTED
        // =========================================================================
        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            if (npc.type == NPCID.PossessedArmor && player.HasBuff(BuffID.Obstructed))
            {
                ExecuteObstructedTeleport(npc, player);
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            Player player = Main.player[npc.target];
            if (npc.type == NPCID.PossessedArmor && player.active && player.HasBuff(BuffID.Obstructed))
            {
                ExecuteObstructedTeleport(npc, player);
            }
        }

        private void ExecuteObstructedTeleport(NPC npc, Player player)
        {
            float randomX = player.Center.X + Main.rand.NextFloat(-250f, 250f);
            float randomY = player.Center.Y + Main.rand.NextFloat(-150f, 0f);
            Vector2 teleportTarget = new Vector2(randomX, randomY);

            for (int i = 0; i < 20; i++)
            {
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.5f);
            }

            npc.position = teleportTarget;
            npc.velocity = Vector2.Zero; 
            npc.netUpdate = true;        

            for (int i = 0; i < 20; i++)
            {
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.5f);
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, npc.position);
        }
    }
}