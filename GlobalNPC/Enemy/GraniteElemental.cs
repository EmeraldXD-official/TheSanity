using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class GraniteFlyerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private int shootTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.GraniteFlyer) return;

            // Selalu bisa tembus block
            npc.noTileCollide = true;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            shootTimer++;

            // --- VISUAL 4 PARTIKEL MENYATU ---
            if (shootTimer >= 60 && shootTimer < 120) 
            {
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.ToRadians(90 * i);
                    float progress = (shootTimer - 60f) / 60f;
                    float distance = MathHelper.Lerp(160f, 0f, progress); // Dari 10 block ke pusat
                    
                    Vector2 dustPos = npc.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Granite, 0, 0, 100, default, 1.3f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            // --- TEMBAK & RECOIL ---
            if (shootTimer >= 120)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 shootVel = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 9f;
                    
                    // LOKASI DAMAGE: 30 (Akan menjadi 90 di Master Mode)
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ModContent.ProjectileType<GraniteBigBlast>(), 18, 2f, Main.myPlayer);
                    
                    // RECOIL INSTAN 2 BLOCK
                    Vector2 recoilDir = -shootVel;
                    recoilDir.Normalize();
                    npc.position += recoilDir * 32f; 
                    
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item62, npc.Center); 
                }
                shootTimer = 0;
            }
        }
    }
}