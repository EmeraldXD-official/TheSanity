using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: BEE & SMALL BEE MINI STINGER GUNNER (2 SECONDS COOLDOWN)
    // =========================================================================
    public class BeeRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // AMAN: Jam weker kustom terpisah untuk masing-masing individu lebah
        public int stingerTimer = 0;

        // FILTER SYSTEM: Memaksa script ini aktif ke Bee BESAR dan Bee KECIL sekaligus
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Bee || entity.type == NPCID.BeeSmall;
        }

        // =========================================================================
        // [ATTACK LOCATION]: COOLDOWN TEPAT 2 DETIK (120 FRAMES) - MINI STINGER
        // =========================================================================
        public override void AI(NPC npc)
        {
            // Validasi tipe lebah aman
            if (npc.type != NPCID.Bee && npc.type != NPCID.BeeSmall) return;

            // Cari player terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active)
            {
                stingerTimer = 0;
                return;
            }

            stingerTimer++;

            // TRIGGER LOCATION: Tepat 120 Frame = 2 Detik Sekali Menembak!
            if (stingerTimer >= 120)
            {
                stingerTimer = 0; // Reset timer ke nol

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // LOKASI BALANCING DAMAGE & KECEPATAN JARUM LEBAH
                    float stingerSpeed = 6.5f;
                    int baseDamage = 5; // Damage disetel kecil biar tidak terlalu menyiksa player

                    // Hitung arah bidikan dari tengah tubuh lebah ke dada player
                    Vector2 shootVelocity = target.Center - npc.Center;
                    shootVelocity.Normalize();
                    shootVelocity *= stingerSpeed;

                    // Spawn proyektil jarum Hornet vanilla
                    int projIndex = Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVelocity,
                        ProjectileID.Stinger, // ID 55 (Jarum Penyengat)
                        baseDamage,
                        1f,
                        Main.myPlayer
                    );

                    // -------------------------------------------------------------------------
                    // KUNCI CODES: MEMBUAT UKURAN PROYEKTIL STINGER MENJADI KECIL IMUT
                    // -------------------------------------------------------------------------
                    if (projIndex < Main.maxProjectiles)
                    {
                        Projectile stinger = Main.projectile[projIndex];
                        
                        // LOKASI UKURAN GRAPIC: 0.6f artinya ukuran jarum menyusut jadi 60% dari aslinya!
                        stinger.scale = 0.6f; 
                        
                        // Perkecil juga hitbox tabrakannya sedikit agar seimbang dengan visualnya yang mini
                        stinger.width = 8;
                        stinger.height = 8;
                        
                        stinger.netUpdate = true; // Sinkronisasikan ukuran mini ini ke server
                    }
                }

                // Efek suara panah/tembakan jarum tajam lirih bawaan vanilla
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item17, npc.Center);
                npc.netUpdate = true;
            }
        }
    }
}