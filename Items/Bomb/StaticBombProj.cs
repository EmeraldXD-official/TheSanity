using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Bomb
{
    public class StaticBombProj : ModProjectile
    {
        // Otomatis membaca gambar StaticBomb.png di folder yang sama
        public override string Texture => "TheSanity/Items/Bomb/StaticBomb";

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;      // Meledak saat menabrak musuh
            Projectile.timeLeft = 180;     // Detik sebelum meledak otomatis (3 detik)
        }

        public override void AI() {
            // Rotasi berputar saat melayang di udara
            Projectile.rotation += Projectile.velocity.X * 0.08f;

            // Gravitasi parabola granat
            Projectile.velocity.Y += 0.2f;

            // Percikan listrik kecil di udara saat terbang
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 100, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            // Memantul jika mengenai tanah/dinding sebelum meledak
            if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.4f;
            if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
            
            return false;
        }

        public override void Kill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center); // SFX Ledakan
            SoundEngine.PlaySound(SoundID.NPCHit4, Projectile.Center); // SFX Listrik/Besi

            // 1. Partikel & Debu Ledakan
            for (int i = 0; i < 25; i++) {
                Vector2 speed = Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Electric, speed.X, speed.Y, 0, default, 1.4f);
                d.noGravity = true;

                Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Smoke, speed.X * 0.5f, speed.Y * 0.5f, 100, Color.Gray, 1.2f);
            }

            // 2. MELEPAS PECAHAN PAKU SINYAL (FRIENDLY NAIL SHRAPNEL)
            if (Main.myPlayer == Projectile.owner) {
                int shardCount = Main.rand.Next(3, 6);
                for (int i = 0; i < shardCount; i++) {
                    Vector2 shardVel = Main.rand.NextVector2Circular(7f, 7f);
                    
                    int projIndex = Projectile.NewProjectile(
                        Projectile.GetSource_Death(),
                        Projectile.Center,
                        shardVel,
                        ProjectileID.Nail,
                        (int)(Projectile.damage * 0.6f),
                        Projectile.knockBack * 0.5f,
                        Projectile.owner
                    );

                    // REGISTRASI FRIENDLY SECARA PAKSA (Agar Melukai Musuh & Tidak Melukai Player)
                    if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                        Projectile spawnedNail = Main.projectile[projIndex];
                        spawnedNail.friendly = true;
                        spawnedNail.hostile = false;
                        spawnedNail.DamageType = DamageClass.Ranged;
                        spawnedNail.owner = Projectile.owner;
                        spawnedNail.netUpdate = true;
                    }
                }
            }
        }
    }
}