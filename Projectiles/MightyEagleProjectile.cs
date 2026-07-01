using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Graphics.CameraModifiers;

namespace TheSanity.Projectiles
{
    public class MightyEagleProjectile : ModProjectile
    {
        public override void SetDefaults()
        {
            // Ukuran dimensi hitbox disesuaikan dengan skala burung elang raksasamu
            Projectile.width = 150;  
            Projectile.height = 150; 
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1; // Bisa menabrak target berkali-kali
            Projectile.timeLeft = 600; 
            Projectile.aiStyle = -1;   // Menggunakan custom AI mandiri
        }

        public override void AI()
        {
            int targetIndex = (int)Projectile.ai[0];
            if (targetIndex < 0 || targetIndex >= Main.maxNPCs)
            {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIndex];

            // PHASE 1: Elang menukik tajam memburu target sarden
            if (Projectile.ai[1] == 0f)
            {
                if (target.active && !target.dontTakeDamage)
                {
                    Vector2 direction = target.Center - Projectile.Center;
                    direction.Normalize();
                    
                    float speed = 28f; 
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * speed, 0.06f);

                    // Jika jarak sangat dekat atau hitbox bersentuhan, picu ledakan hentakan layar
                    if (Vector2.Distance(Projectile.Center, target.Center) < 40f || Projectile.Hitbox.Intersects(target.Hitbox))
                    {
                        Projectile.ai[1] = 1f; 
                        BuatEfekLedakanHentakan(); 
                    }
                }
                else
                {
                    Projectile.ai[1] = 1f; 
                }
            }
            // PHASE 2: Setelah berhasil menghantam, elang langsung terbang kembali ke langit (Atas-Kiri)
            else
            {
                Vector2 exitVelocity = new Vector2(-35f, -25f); 
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, exitVelocity, 0.06f);
                
                if (Projectile.Center.Y < Main.player[Projectile.owner].Center.Y - 1200f || Projectile.Center.X < Main.player[Projectile.owner].Center.X - 1500f)
                {
                    Projectile.Kill();
                }
            }

            // =========================================================================
            // FIX ROTASI & ARAH HADAP KIRI-KANAN (KHUSUS SPRITE HADAP KIRI)
            // =========================================================================
            // Berdasarkan gambar MightyEagleProjectile.png yang bawaannya menghadap KIRI:
            // - Jika bergerak ke KANAN (velocity.X > 0), kita balik gambarnya secara horizontal (set ke -1)
            // - Jika bergerak ke KIRI (velocity.X <= 0), biarkan gambar asli apa adanya (set ke 1)
            Projectile.spriteDirection = Projectile.velocity.X > 0 ? -1 : 1;

            // Ambil sudut rotasi mentah berdasarkan arah vektor kecepatan terbang
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Aturan rotasi matematika Terraria untuk file gambar yang aslinya menghadap kiri:
            if (Projectile.spriteDirection == 1) 
            {
                // Wajib ditambah 180 derajat (MathHelper.Pi) saat terbang ke kiri agar kepalanya sejajar sempurna
                Projectile.rotation += MathHelper.Pi;
            }
            // =========================================================================

            // Efek partikel kepulan awan badai di ekor elang saat bergerak cepat
            if (Main.rand.NextBool(3))
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Cloud, 0f, 0f, 100, default, 1.3f);
            }
        }

        private void BuatEfekLedakanHentakan()
        {
            for (int i = 0; i < 30; i++)
            {
                int smoke = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 3f);
                Main.dust[smoke].velocity *= 2f;
            }
            for (int i = 0; i < 40; i++)
            {
                int fire = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 50, default, 2.5f);
                Main.dust[fire].noGravity = true;
                Main.dust[fire].velocity *= 4f;
            }
            if (Main.netMode != NetmodeID.Server)
            {
                for (int g = 0; g < 4; g++)
                {
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity * 0.3f, GoreID.Smoke1 + Main.rand.Next(3), 1.5f);
                }
            }

            Vector2 arahHentakan = Projectile.velocity;
            arahHentakan.Normalize();
            PunchCameraModifier hentakan = new PunchCameraModifier(Projectile.Center, arahHentakan, 15f, 10f, 15, 1000f, Mod.Name);
            Main.instance.CameraModifiers.Add(hentakan);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.ArmorPenetration += 9999; 
            modifiers.DamageVariationScale *= 0f; 
        }
        
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.ai[1] == 0f)
            {
                Projectile.ai[1] = 1f;
                BuatEfekLedakanHentakan();
            }
        }
    }
}