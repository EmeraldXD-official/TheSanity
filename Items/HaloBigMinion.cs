using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.Projectiles
{
    public class HaloMinionGede : ModProjectile
    {
        private int shootCooldown = 0;

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            Main.projFrames[Projectile.type] = 6; //

            // BARU: Mengaktifkan penyimpanan posisi lama untuk efek bayangan pink
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12; // Jumlah bayangan belakang
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;     // Menyimpan posisi dan rotasi terdahulu
        }

        public override void SetDefaults() {
            Projectile.width = 64; //[cite: 3]
            Projectile.height = 64; //[cite: 3]
            Projectile.tileCollide = false; //[cite: 3]
            Projectile.friendly = true; //[cite: 3]
            Projectile.minion = true; //[cite: 3]
            Projectile.minionSlots = 0f; // Tetap 0f agar disummon di akhir tanpa memakan slot[cite: 3]
            Projectile.penetrate = -1; //[cite: 3]
        }

        public override bool? CanCutTiles() => false; //[cite: 3]

        public override void AI() {
            Player player = Main.player[Projectile.owner]; //[cite: 3]

            if (player.dead || !player.active) { //[cite: 3]
                Projectile.Kill(); //[cite: 3]
                return; //[cite: 3]
            }

            if (player.HasBuff(ModContent.BuffType<HaloMinionBuff>())) { //[cite: 3]
                Projectile.timeLeft = 2;  //[cite: 3]
            } else {
                Projectile.Kill(); //[cite: 3]
                return; //[cite: 3]
            }

            Projectile.frameCounter++; //[cite: 3]
            if (Projectile.frameCounter >= 5) { //[cite: 3]
                Projectile.frameCounter = 0; //[cite: 3]
                Projectile.frame++; //[cite: 3]
                if (Projectile.frame >= Main.projFrames[Projectile.type]) { //[cite: 3]
                    Projectile.frame = 0; //[cite: 3]
                }
            }

            NPC target = null; //[cite: 3]
            float maxDistance = 900f; //[cite: 3]

            if (player.HasMinionAttackTargetNPC) { //[cite: 3]
                NPC npc = Main.npc[player.MinionAttackTargetNPC]; //[cite: 3]
                if (npc.CanBeChasedBy(Projectile) && Vector2.Distance(Projectile.Center, npc.Center) < maxDistance) { //[cite: 3]
                    target = npc; //[cite: 3]
                }
            }

            if (target == null) { //[cite: 3]
                for (int i = 0; i < Main.maxNPCs; i++) { //[cite: 3]
                    NPC npc = Main.npc[i]; //[cite: 3]
                    if (npc.CanBeChasedBy(Projectile)) { //[cite: 3]
                        float distance = Vector2.Distance(Projectile.Center, npc.Center); //[cite: 3]
                        if (distance < maxDistance) { //[cite: 3]
                            target = npc; //[cite: 3]
                            maxDistance = distance; //[cite: 3]
                        }
                    }
                }
            }

            Vector2 targetPosition; //[cite: 3]
            float speed; //[cite: 3]
            float inertia; //[cite: 3]

            if (target != null && target.active) { //[cite: 3]
                targetPosition = target.Center + new Vector2(0, -220f); // Posisi asli di atas musuh[cite: 3]
                speed = 15f;     //[cite: 3]
                inertia = 10f;    //[cite: 3]
                Projectile.spriteDirection = (target.Center.X > Projectile.Center.X) ? 1 : -1; //[cite: 3]
            } else {
                targetPosition = player.Center + new Vector2(0, -90f); // Posisi asli di atas player[cite: 3]
                speed = 10f; //[cite: 3]
                inertia = 15f; //[cite: 3]
                Projectile.spriteDirection = player.direction; //[cite: 3]
            }

            Vector2 toTargetPosition = targetPosition - Projectile.Center; //[cite: 3]
            float distanceToTarget = toTargetPosition.Length(); //[cite: 3]

            if (distanceToTarget > 2000f) { //[cite: 3]
                Projectile.Center = player.Center; //[cite: 3]
            }

            if (distanceToTarget > 20f) { //[cite: 3]
                toTargetPosition.Normalize(); //[cite: 3]
                toTargetPosition *= speed; //[cite: 3]
                Projectile.velocity = (Projectile.velocity * (inertia - 1f) + toTargetPosition) / inertia; //[cite: 3]
            }

            if (target != null && target.active && !target.friendly) { //[cite: 3]
                shootCooldown++; //[cite: 3]
                if (shootCooldown >= 180) { //[cite: 3]
                    shootCooldown = 0; //[cite: 3]
                    if (Main.myPlayer == Projectile.owner) { //[cite: 3]
                        Vector2 shootDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX); //[cite: 3]
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootDir, 
                            ModContent.ProjectileType<HaloBigLaser>(), Projectile.damage * 2, Projectile.knockBack, Projectile.owner, Projectile.whoAmI); //[cite: 3]
                    }
                }
            }

            if (!Main.dayTime) { //[cite: 3]
                Lighting.AddLight(Projectile.Center, 0.9f, 0.15f, 0.5f); //[cite: 3]
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value; //[cite: 3]
            int frameWidth = texture.Width / 6; // Tetap dibagi 6 frame sesuai kode aslimu[cite: 3]
            int currentFrame = Projectile.frame; //[cite: 3]
            
            Rectangle srcRect = new Rectangle(currentFrame * frameWidth, 0, frameWidth, texture.Height); //[cite: 3]
            Vector2 drawOrigin = srcRect.Size() / 2f; //[cite: 3]
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None; //[cite: 3]
            
            // BARU: Menggambar Bayangan / Afterimage Terraprisma berwarna Pink
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                // Menyesuaikan titik tengah koordinat gambar dari oldPos (Top-Left)
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                
                // Menghitung transparansi gradasi (makin lama posisinya, makin memudar halus)
                float trailAlpha = (1f - ((float)i / Projectile.oldPos.Length)) * 0.6f;

                Color trailColor = new Color(255, 40, 160) * trailAlpha; // Pink bercahaya
                float oldRotation = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, oldDrawPos, srcRect, trailColor, oldRotation, drawOrigin, Projectile.scale, effects, 0);
            }

            // Minion Gede Utama (Asli)
            Vector2 drawPos = Projectile.Center - Main.screenPosition; //[cite: 3]
            Color drawColor = !Main.dayTime ? Color.White : lightColor; //[cite: 3]

            Main.EntitySpriteDraw(texture, drawPos, srcRect, drawColor, Projectile.rotation, drawOrigin, Projectile.scale, effects, 0); //[cite: 3]
            return false; //[cite: 3]
        }
    }
}