using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using TheSanity.Projectiles; // Memanggil namespace tempat VultureFeather berada

namespace TheSanity
{
    public class VultureRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int shootTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.Vulture) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead || npc.Distance(target.Center) > 800f) 
            {
                shootTimer = 0;
                return;
            }

            shootTimer++;

            // --- 1. INDIKATOR "CLING" ---
            if (shootTimer >= 150 && shootTimer < 180)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 spinPos = npc.Center + Main.rand.NextVector2CircularEdge(40f, 40f);
                    Vector2 spinVel = (npc.Center - spinPos) * 0.15f; 
                    Dust d = Dust.NewDustDirect(spinPos, 0, 0, DustID.Sand, spinVel.X, spinVel.Y, 100, default, 1.2f);
                    d.noGravity = true;
                }
            }

            // --- 2. LOGIKA MENEMBAK (Setiap 3 Detik) ---
            if (shootTimer >= 180)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // LOKASI SPEED: 9f adalah kecepatan terbang peluru
                    Vector2 shootVel = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 9f;
                    
                    // FIX FIXED: Mengganti ProjectileID.HarpyFeather dengan peluru kustom VultureFeather milikmu
                    int customProj = ModContent.ProjectileType<VultureFeather>();

                    // LOKASI DAMAGE: Di bawah ini disetel ke 10
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, customProj, 10, 1f, Main.myPlayer);
                    
                    // [MODIFIED FOR MULTIPLAYER]: Logika lama pengatur rotasi manual di sini dihapus 
                    // karena sudah dipindahkan ke GlobalProjectile di bawah agar otomatis sinkron di semua client.

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p);
                }

                // --- 3. EFEK RECOIL (4 BLOCK) ---
                Vector2 recoilDir = (npc.Center - target.Center).SafeNormalize(Vector2.Zero);
                npc.velocity += recoilDir * 7f; 

                for (int i = 0; i < 15; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                    Dust.NewDust(npc.Center, 0, 0, DustID.Sand, dustVel.X, dustVel.Y, 100, default, 1.5f);
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item17, npc.Center);
                shootTimer = 0;
            }
        }
    }

    public class VultureProjectileMod : GlobalProjectile
    {
        public override void PostAI(Projectile projectile)
        {
            // --- 4. PARTIKEL PASIR TEBAL PADA BULU ---
            if (projectile.type == ModContent.ProjectileType<VultureFeather>() && projectile.hostile)
            {
                // =========================================================================
                // MULTIPLAYER ROTATION FIX LOCATION
                // =========================================================================
                // Membuat proyektil selalu memperbarui sudut rotasinya secara real-time mengikuti arah terbang velocity-nya.
                // Ditambah MathHelper.PiOver2 (90 derajat) karena sprite lembaran bulu vanilla biasanya menghadap ke atas.
                if (projectile.velocity != Vector2.Zero)
                {
                    projectile.rotation = projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }

                for (int i = 0; i < 2; i++)
                {
                    Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Sand, 0, 0, 100, default, 1.3f);
                    d.noGravity = true;
                    d.velocity *= 0.3f;
                }
            }
        }
    }
}