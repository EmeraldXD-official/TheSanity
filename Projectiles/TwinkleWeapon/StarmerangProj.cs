using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class StarmerangProj : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Twinkle/Starmerang";

        private int AI_State {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private int TargetIndex {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        private int shootTimer = 0;
        private int shotCounter = 0;
        private int maxShots = 0;
        private float orbitTimer = 0f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false; 
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            
            // 1. EFEK GLOWING: Menyinari area sekitar bumerang dengan cahaya biru kosmik terang
            Lighting.AddLight(Projectile.Center, 0.4f, 0.6f, 1.0f);

            if (maxShots == 0) {
                maxShots = Main.rand.Next(5, 8);
            }

            // PERBAIKAN ROTASI: Hanya berputar seperti bumerang biasa di FASE 0 dan FASE 2
            if (AI_State == 0 || AI_State == 2) {
                Projectile.rotation += 0.4f * Projectile.direction;
            }

            // ================= FASE 0: TERBANG MENCARI MUSUH =================
            if (AI_State == 0) {
                NPC closestNPC = null;
                float closestDistance = 550f;

                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy(Projectile)) {
                        float distance = Vector2.Distance(Projectile.Center, npc.Center);
                        if (distance < closestDistance) {
                            closestNPC = npc;
                            closestDistance = distance;
                        }
                    }
                }

                if (closestNPC != null) {
                    TargetIndex = closestNPC.whoAmI;
                    AI_State = 1;
                    Projectile.netUpdate = true;
                }

                if (Projectile.timeLeft < 3600 - 45) {
                    AI_State = 2;
                }
            }
            // ================= FASE 1: MENGORBIT & MENEMBAK MUSUH =================
            else if (AI_State == 1) {
                NPC target = Main.npc[TargetIndex];

                if (!target.active || !target.CanBeChasedBy(Projectile)) {
                    AI_State = 2;
                    Projectile.netUpdate = true;
                    return;
                }

                orbitTimer += 0.06f; // Sedikit disesuaikan agar putaran mengorbit lebih smooth

                float patternSpread = (Projectile.identity % 3) * (MathHelper.TwoPi / 3f);
                float finalAngle = orbitTimer + patternSpread;
                float orbitRadius = 80f; 

                Vector2 desiredPos = target.Center + finalAngle.ToRotationVector2() * orbitRadius;
                
                // PERBAIKAN GERAKAN NATURAL: Membatasi kecepatan agar terbang meluncur mulus ke posisi orbit (TIDAK TELEPORTASI)
                Vector2 toDesired = desiredPos - Projectile.Center;
                float distance = toDesired.Length();
                float travelSpeed = 16f; // Batas kecepatan meluncur mengejar target

                if (distance > 0) {
                    toDesired.Normalize();
                    // Menggunakan Lerp kecepatan agar transisi belokan bumerang terasa luwes dan organik
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toDesired * travelSpeed, 0.15f);
                }

                // PERBAIKAN ROTASI LOCK: Menghentikan putaran bumerang, memaksa wajah depannya (Kiri) selalu menatap musuh
                Vector2 dirToTarget = target.Center - Projectile.Center;
                Projectile.rotation = dirToTarget.ToRotation(); 

                shootTimer++;
                if (shootTimer >= 20) { 
                    shootTimer = 0;
                    shotCounter++;

                    if (Main.myPlayer == Projectile.owner) {
                        Vector2 shootVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 8f;
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(), 
                            Projectile.Center, 
                            shootVel, 
                            ModContent.ProjectileType<StarmerangStar>(), 
                            (int)(Projectile.damage * 0.65f), 
                            0.5f, 
                            Projectile.owner, 
                            target.whoAmI 
                        );
                    }

                    if (shotCounter >= maxShots) {
                        AI_State = 2;
                        Projectile.netUpdate = true;
                    }
                }
            }
            // ================= FASE 2: PULANG CEPAT KE PLAYER =================
            else if (AI_State == 2) {
                Vector2 returnDirection = player.Center - Projectile.Center;
                float distanceToPlayer = returnDirection.Length();

                if (distanceToPlayer < 25f) {
                    Projectile.Kill();
                    return;
                }

                returnDirection.Normalize();
                Projectile.velocity = returnDirection * 18f;
            }
        }

        public override bool PreDraw(ref Color drawColor) {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            float correctedRotation = Projectile.rotation + MathHelper.Pi;

            // PERBAIKAN GLOWING SPRITE: Mengganti 'drawColor' menjadi 'Color.White'
            // Ini membuat sprite mengabaikan bayangan lingkungan sekitar dan menyala konstan di tempat gelap!
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, Color.White, 
                correctedRotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            
            return false; 
        }
    }
}
