using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    public class Fly : ModNPC
    {
        public int masterWhoAmI = -1;
        private bool firstFrameSync = true;

        // Variabel untuk menyimpan koordinat tujuan acak lalat di sekitar player
        private Vector2 customTargetOffset = Vector2.Zero;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 4; 

            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Hide = true
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
        }

        public override void SetDefaults()
        {
            NPC.width = 10;
            NPC.height = 10; 

            NPC.aiStyle = -1; // Tetap Custom AI

            NPC.defense = 24;     
            NPC.lifeMax = 1500;   
            
            NPC.knockBackResist = 0.5f; 
            NPC.noGravity = true;
            NPC.noTileCollide = true; 
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
        }

        public override void AI()
        {
            // ---------------------------------------------------------------------
            // VALIDASI INDUK & SINKRONISASI HP (Tetap Dipertahankan)
            // ---------------------------------------------------------------------
            if (masterWhoAmI < 0 || masterWhoAmI >= Main.maxNPCs)
            {
                NPC.active = false;
                return;
            }

            NPC master = Main.npc[masterWhoAmI];
            if (!master.active || master.type != NPCID.DrManFly)
            {
                NPC.active = false;
                return;
            }

            NPC.lifeMax = master.lifeMax;

            if (firstFrameSync)
            {
                NPC.life = master.life;
                firstFrameSync = false;
            }

            if (NPC.life < master.life)
            {
                master.life = NPC.life; 
            }
            else if (NPC.life > master.life)
            {
                NPC.life = master.life; 
            }

            if (master.life <= 0)
            {
                NPC.HitEffect();
                NPC.active = false;
                return;
            }

            // ---------------------------------------------------------------------
            // [BARU] LOGIKA TERBANG AMBURADUL / NGUWAWOR (SWARM CHAOS AI)
            // ---------------------------------------------------------------------
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            if (player.active && !player.dead)
            {
                // =====================================================================
                // [GUIDE LOCATION 3: CHAOS & SPEED BALANCING]
                // Atur nilai-nilai ini untuk mendapatkan tingkat ke-amburadulan yang pas:
                // - maxSpeed: Batas kecepatan lalat melesat (Semakin besar = semakin gesit).
                // - jitterStrength: Dorongan acak tiap frame (Membuat gerakan zig-zag/gemetar).
                // - maxDistance: Jarak terjauh lalat boleh berpencar dari player (dalam pixel).
                // - changeTargetTime: Seberapa sering lalat berganti arah tujuan mendadak (dalam frame).
                // =====================================================================
                float maxSpeed = 11f;          
                float jitterStrength = 2.8f;   
                float maxDistance = 120f;      
                int changeTargetTime = 12;     

                // Menggunakan npc.ai[0] sebagai timer internal individu tiap lalat
                NPC.ai[0]++;

                // Setiap mencapai batas waktu atau saat pertama kali spawn, acak titik target baru
                if (NPC.ai[0] >= changeTargetTime || customTargetOffset == Vector2.Zero)
                {
                    // Pilih koordinat acak secara melingkar di sekitar tubuh player
                    customTargetOffset = Main.rand.NextVector2Circular(maxDistance, maxDistance);
                    
                    // Beri sedikit random variasi timer antar lalat agar mereka tidak belok barengan
                    NPC.ai[0] = Main.rand.Next(0, 4); 
                }

                // Tentukan titik posisi mutlak yang ingin dituju lalat di dunia game
                Vector2 absoluteTarget = player.Center + customTargetOffset;
                
                // Hitung arah vector menuju titik acak tersebut
                Vector2 directionToTarget = absoluteTarget - NPC.Center;
                float distanceToTarget = directionToTarget.Length();

                if (distanceToTarget > 4f)
                {
                    directionToTarget.Normalize();
                    // Berikan akselerasi cepat menuju titik target acak tersebut
                    NPC.velocity += directionToTarget * 0.65f;
                }

                // EFEK UTAMA: Suntikkan gaya dorong (impulse) acak secara konstan di setiap frame.
                // Ini yang memicu visual lalat bergerak patah-patah/amburadul tak beraturan.
                NPC.velocity += Main.rand.NextVector2Circular(jitterStrength, jitterStrength);

                // BATAS PENGAMAN: Jika lalat terlempar terlalu jauh melebihi batas 'maxDistance' dari player
                float currentDistanceFromPlayer = Vector2.Distance(NPC.Center, player.Center);
                if (currentDistanceFromPlayer > maxDistance)
                {
                    Vector2 pullToPlayer = player.Center - NPC.Center;
                    pullToPlayer.Normalize();
                    
                    // Tarik paksa lalat kembali ke arah lingkaran area player dengan kecepatan tinggi
                    NPC.velocity += pullToPlayer * 1.5f; 
                }

                // Batasi top speed agar lalat tidak melesat hilang ke luar layar karena akumulasi random force
                if (NPC.velocity.Length() > maxSpeed)
                {
                    NPC.velocity.Normalize();
                    NPC.velocity *= maxSpeed;
                }

                // Arah hadap sprite lalat mengikuti arah gerak horizontal kecepatannya
                if (NPC.velocity.X != 0f)
                {
                    NPC.spriteDirection = NPC.direction = NPC.velocity.X > 0f ? 1 : -1;
                }
            }
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.frameCounter++; 
            
            // =====================================================================
            // [GUIDE LOCATION 4: WING FLAP SPEED]
            // Kepakan sayap diset ke angka 3 frame per gambar agar getarannya 
            // terlihat sangat panik mengimbangi gerakannya yang nguwawor.
            // =====================================================================
            if (NPC.frameCounter >= 3.0) 
            {
                NPC.frameCounter = 0;
                NPC.frame.Y += frameHeight; 
                
                if (NPC.frame.Y >= 4 * frameHeight) 
                {
                    NPC.frame.Y = 0; 
                }
            }
        }
    }
}