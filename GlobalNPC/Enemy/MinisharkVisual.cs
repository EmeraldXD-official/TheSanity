using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; // Diperlukan untuk kustomisasi Draw/Origin
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    public class MinisharkVisual : ModProjectile
    {
        // Meminjam sprite Minishark asli dari vanilla Terraria
        public override string Texture => $"Terraria/Images/Item_{ItemID.Minishark}";

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false; 
            Projectile.hostile = false;  
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            
            // =========================================================================
            // VISUAL: UKURAN SPRITE SENJATA (SCALE LOCATION)
            // =========================================================================
            Projectile.scale = 0.85f; 
        }

        public override void AI()
        {
            NPC owner = Main.npc[(int)Projectile.ai[0]];

            if (!owner.active || owner.type != ModContent.NPCType<Goldfish_Walker>())
            {
                Projectile.Kill();
                return;
            }

            // =========================================================================
            // PERBAIKAN BUG OFFSET VISUAL (VISUAL OFFSET LOCATION)
            // =========================================================================
            // Menggunakan penyesuaian posisi yang dinamis agar saat berbalik badan,
            // Minishark tetap berada tepat di genggaman sirip Goldfish secara simetris.
            float offsetX = (owner.direction == 1) ? 2f : -2f; 
            float offsetY = 4f; 

            Projectile.Center = owner.Center + new Vector2(offsetX, offsetY);
            Projectile.spriteDirection = owner.direction;

            Player target = Main.player[owner.target];
            if (target != null && !target.dead && owner.HasPlayerTarget)
            {
                Vector2 shootVector = target.Center - Projectile.Center;
                
                // Disinkronkan dengan AI Goldfish: Menggunakan CanHitLine dari pusat senjata ke player
                bool canSeePlayer = Collision.CanHitLine(Projectile.Center, 1, 1, target.Center, 1, 1);

                // Kirim status "Bisa melihat player atau tidak" ke AI Goldfish (menggunakan ai[1] milik NPC)
                owner.ai[1] = canSeePlayer ? 1f : 0f;

                // Selalu membidik player, tidak peduli terhalang atau tidak
                shootVector.Normalize();
                Projectile.rotation = shootVector.ToRotation();
                
                // Jika menghadap kiri (-1), kita rotasikan sudutnya sebesar 180 derajat (Pi)
                // agar senjatanya tidak menembak terbalik/terjungkir ke belakang
                if (Projectile.spriteDirection == -1)
                {
                    Projectile.rotation += MathHelper.Pi; 
                }

                // PERBAIKAN: Kondisi !Main.dayTime dihapus, hanya mengecek apakah target terlihat (canSeePlayer)
                if (canSeePlayer) 
                {
                    // =========================================================================
                    // BALANCING: ATTACK SPEED (KECEPATAN TEMBAK)
                    // =========================================================================
                    Projectile.localAI[0]++;
                    if (Projectile.localAI[0] >= 8f) 
                    {
                        Projectile.localAI[0] = 0f; 

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            // [BULLET SPEED LOCATION]
                            Vector2 velocity = shootVector * 10f; 
                            
                            // [BULLET DAMAGE LOCATION]
                            int p = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity, ProjectileID.BulletDeadeye, 10, 1f, Main.myPlayer);
                            
                            if (p != Main.maxProjectiles)
                            {
                                Main.projectile[p].hostile = true; 
                                Main.projectile[p].friendly = false;

                                // FIX MULTIPLAYER: Server harus sinkronisasi perubahan sifat peluru ke Client!
                                if (Main.netMode == NetmodeID.Server)
                                {
                                    NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p);
                                }
                            }
                        }
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item11, Projectile.position);
                    }
                }
            }
        }

        // Kita override fungsi Draw bawaan agar titik poros (Origin) senjata dipaksa bergeser mengikuti arah hadap
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            // =========================================================================
            // DYNAMIC DRAW ORIGIN: Menghitung titik poros berdasarkan arah hadap sprite.
            // =========================================================================
            // Jika hadap kanan, poros ditarik ke kiri gambar (laras menghadap ke kanan).
            // Jika hadap kiri, poros otomatis disesuaikan agar rotasi tekstur tetap konsisten di titik genggam yang sama.
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            if (Projectile.spriteDirection == -1)
            {
                drawOrigin.X = texture.Width * 0.5f; 
            }
            else
            {
                drawOrigin.X = texture.Width * 0.3f; // Titik pegangan popor/tengah senjata saat hadap kanan
            }

            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                effects,
                0
            );
            return false; // Mengembalikan false agar sprite default yang miring tadi tidak ikut digambar ulang
        }
    }
}