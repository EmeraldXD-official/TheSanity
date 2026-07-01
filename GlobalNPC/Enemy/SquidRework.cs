using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: SQUID SPREAD (1 FOCUS TARGET + REST RANDOM DIRECTION)
    // =========================================================================
    public class SquidRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int inkTimer = 0; 
        public int currentCooldown = 360; 
        public bool hasSetInitialCooldown = false; 

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Squid;
        }

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.Squid) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) 
            {
                inkTimer = 0;
                return;
            }

            // Tentukan cooldown acak pertama kali (5-7 detik)
            if (!hasSetInitialCooldown)
            {
                hasSetInitialCooldown = true;
                currentCooldown = Main.rand.Next(300, 421); 
            }

            inkTimer++; 

            // TRIGGER LOCATION: Jeda acak terpenuhi
            if (inkTimer >= currentCooldown) 
            {
                inkTimer = 0; // Reset timer
                currentCooldown = Main.rand.Next(300, 421); // Reset cooldown acak baru

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // BALANCING SHOT COUNT: Mengatur jumlah total asap (3 sampai 5)
                    int totalAsap = Main.rand.Next(3, 6); 
                    
                    float inkSpeed = 6.5f;
                    int inkDamage = 1;
                    int projType = ModContent.ProjectileType<Projectiles.InkCloud>();

                    // -------------------------------------------------------------------------
                    // TEMBAKAN PERTAMA (i = 0): DIJAMIN PASTI KE ARAH PLAYER
                    // -------------------------------------------------------------------------
                    Vector2 playerVelocity = target.Center - npc.Center;
                    playerVelocity.Normalize();
                    playerVelocity *= inkSpeed;

                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        playerVelocity, // Lurus ke target
                        projType, 
                        inkDamage,
                        0f, 
                        Main.myPlayer
                    );

                    // -------------------------------------------------------------------------
                    // TEMBAKAN SISA (i = 1 dan seterusnya): ACAK KELILING 360 DERAJAT
                    // -------------------------------------------------------------------------
                    for (int i = 1; i < totalAsap; i++)
                    {
                        // Melempar arah acak mutlak ke segala penjuru lingkaran (360 derajat)
                        Vector2 randomVelocity = Main.rand.NextVector2Unit() * inkSpeed;
                        
                        // Berikan sedikit variasi jarak lemparan biar gak monoton bentuk bulat sempurna
                        randomVelocity *= Main.rand.NextFloat(0.7f, 1.3f);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            randomVelocity, // Arah acak liar
                            projType, 
                            inkDamage,
                            0f, 
                            Main.myPlayer
                        );
                    }
                }

                // Efek suara cipratan tinta
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item111, npc.Center);
                
                npc.netUpdate = true;
            }
        }
    }
}