using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO; // Wajib untuk sinkronisasi jaringan
using System.IO; // Wajib untuk BinaryWriter/Reader

namespace TheSanity
{
    // =========================================================================
    // [ENEMY REWORK SYSTEM]: FLYING FISH WATER STREAM BULLET ATTACK
    // =========================================================================
    public class FlyingFishRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Gunakan custom variable, jangan menumpang di npc.ai[] karena tertimpa Vanilla AI
        private int waterStreamTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return entity.type == NPCID.FlyingFish;
        }

        // =========================================================================
        // FIX MULTIPLAYER: SINKRONISASI VARIABEL CUSTOM
        // =========================================================================
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter writer)
        {
            writer.Write(waterStreamTimer);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader reader)
        {
            waterStreamTimer = reader.ReadInt32();
        }

        // Gunakan PostAI agar pergerakan terbang vanilla selesai dihitung dulu
        public override void PostAI(NPC npc) {
            if (Main.gameMenu) return;

            // Pastikan timer dan proyektil hanya dikendalikan oleh Server / Singleplayer
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                waterStreamTimer++;

                // [LOKASI SPEED BALANCING]: Jeda Tembakan (3 Detik = 180 Frame)
                if (waterStreamTimer >= 180) {
                    waterStreamTimer = 0;

                    // Cari target player terdekat yang valid
                    npc.TargetClosest(true);
                    Player target = null; 
                    if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

                    if (target != null && target.active && !target.dead && Vector2.Distance(npc.Center, target.Center) < 700f) {
                        
                        // -------------------------------------------------------------------------
                        // DETEKSI ARAH DAN KECEPATAN PELURU ARUS AIR
                        // -------------------------------------------------------------------------
                        // [LOKASI VELOCITY BALANCING]: Kecepatan laju peluru air (Default: 7f)
                        float projectileSpeed = 7f; 
                        
                        int shootDir = npc.direction != 0 ? npc.direction : (target.Center.X > npc.Center.X ? 1 : -1);
                        Vector2 shootVelocity = new Vector2(shootDir * projectileSpeed, 0f);

                        // Kompensasi visual: Geser titik spawn agar pas keluar dari mulut ikannya
                        Vector2 spawnPosition = npc.Center + new Vector2(shootDir * 14f, -2f);

                        int projType = ProjectileID.WaterStream;

                        // [LOKASI DAMAGE BALANCING]: Atur besaran damage peluru air musuh di sini
                        int damage = 12; 
                        float knockback = 2f;

                        int projectileIndex = Projectile.NewProjectile(
                            npc.GetSource_FromAI(), 
                            spawnPosition, 
                            shootVelocity, 
                            projType, 
                            damage, 
                            knockback, 
                            Main.myPlayer // <-- FIX: Gunakan Main.myPlayer agar Server yang menjadi owner
                        );

                        // -------------------------------------------------------------------------
                        // SUNTIK SIFAT HOSTILE & SYNC JARINGAN
                        // -------------------------------------------------------------------------
                        if (projectileIndex < Main.maxProjectiles) {
                            Projectile proj = Main.projectile[projectileIndex];
                            
                            proj.friendly = false;
                            proj.hostile = true;
                            
                            // Berikan ID tanda pengenal unik (ai[1] = 99f) agar dibaca oleh GlobalProjectile
                            proj.ai[1] = 99f; 

                            // Kirim data proyektil yang baru lahir ini ke seluruh Client yang terhubung
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, projectileIndex);
                        }

                        // Di tModLoader, PlaySound yang dipanggil server dengan koordinat posisi akan otomatis disebarkan ke client
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item21, npc.Center);

                        // Pemicu agar server menyebarkan perubahan nilai 'waterStreamTimer' kembali ke angka 0
                        npc.netUpdate = true;
                    }
                }
            }
        }
    }

    // =========================================================================
    // [PROJECTILE ALTERATION]: INFLICT WET DEBUFF UPON PLAYER COLLISION
    // =========================================================================
    public class FlyingFishBulletImpact : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info) {
            if (projectile.type == ProjectileID.WaterStream && projectile.ai[1] == 99f) {
                
                // [LOKASI DEBUFF DURASI BALANCING]: Berikan debuff Wet selama 5 Detik (5 * 60 Frame = 300)
                int debuffDuration = 300; 
                
                target.AddBuff(BuffID.Wet, debuffDuration);
            }
        }
    }
}