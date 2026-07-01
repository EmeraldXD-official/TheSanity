using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: SEA SNAIL GLOW CHARGE & DETONATING BUBBLE SPAWNER
    // =========================================================================
    public class SeaSnailRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // AMAN: Variabel timer kustom sendiri agar tidak bentrok dengan AI siput vanilla
        public int bubbleTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.SeaSnail;
        }

        // =========================================================================
        // [ATTACK & PARTICLE LOCATION]: DETIK 5 (CHARGE CYAN) & DETIK 6 (SPAWN BUBBLE)
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.SeaSnail) return;

            // Cari player terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active)
            {
                bubbleTimer = 0;
                return;
            }

            bubbleTimer++;

            // -------------------------------------------------------------------------
            // FASE CHARGING VISUAL: Detik 0 s/d 5 (Frame 0 - 300) -> Muncul Partikel Glow Cyan
            // -------------------------------------------------------------------------
            if (bubbleTimer < 300)
            {
                // Munculkan partikel secara santai tiap beberapa frame
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4))
                {
                    Dust d = Dust.NewDustDirect(
                        npc.position, npc.width, npc.height, 
                        DustID.SilverFlame, // Partikel glow cyan yang sangat terang dan bagus
                        0f, 0f, 
                        100, 
                        default, 
                        1.2f
                    );
                    d.velocity *= 0.3f;
                    d.noGravity = true;
                }
            }
            // -------------------------------------------------------------------------
            // FASE INTENSE CHARGING: Detik 5 s/d 6 (Frame 300 - 360) -> Partikel Makin Ganas!
            // -------------------------------------------------------------------------
            else if (bubbleTimer >= 300 && bubbleTimer < 360)
            {
                if (Main.netMode != NetmodeID.Server)
                {
                    // Gandakan jumlah partikel di detik terakhir sebelum meletus
                    for (int i = 0; i < 2; i++)
                    {
                        Dust d = Dust.NewDustDirect(
                            npc.position, npc.width, npc.height, 
                            DustID.Electric, // Transisi ke partikel listrik biru/cyan neon
                            0f, 0f, 
                            100, 
                            default, 
                            1.4f
                        );
                        d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
                        d.noGravity = true;
                    }
                }
            }
            // -------------------------------------------------------------------------
            // TRIGGER LOCATION: Tepat Detik ke-6 (Frame 360) -> Spawn 2-3 NPC Gelembung!
            // -------------------------------------------------------------------------
            else if (bubbleTimer >= 360)
            {
                bubbleTimer = 0; // Reset timer kustom kembali ke nol

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // LOKASI QUANTITY: Menentukan jumlah acak gelembung (2 sampai 3)
                    int jumlahGelembung = Main.rand.Next(2, 4);

                    for (int i = 0; i < jumlahGelembung; i++)
                    {
                        // Spawn NPC Detonating Bubble (ID 371) tepat di tengah tubuh siput
                        int bubbleIndex = NPC.NewNPC(
                            npc.GetSource_FromAI(), 
                            (int)npc.Center.X, 
                            (int)npc.Center.Y, 
                            NPCID.DetonatingBubble
                        );

                        if (bubbleIndex < Main.maxNPCs)
                        {
                            NPC bubble = Main.npc[bubbleIndex];
                            
                            // LOKASI BALANCING DORONGAN GELEMBUNG:
                            // Berikan kecepatan awal acak ke arah atas (X acak liar, Y dipaksa minus agar terbang naik)
                            bubble.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f));
                            
                            // Sinkronisasikan kecepatan barunya ke server multiplayer
                            bubble.netUpdate = true;
                        }
                    }
                }

                // Mainkan efek suara melepas balon/gelembung udara sabun bawaan game
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item85, npc.Center);
                npc.netUpdate = true;
            }
        }
    }
}