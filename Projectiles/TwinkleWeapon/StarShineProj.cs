using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class StarShineProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.rotation += 0.4f * Projectile.direction;
            
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Enchanted_Gold, 0f, 0f, 150, default, 1.2f);
            }

            // Sistem Homing Kecepatan Tinggi
            NPC targetKeren = AmbilMusuhTerdekat(500f); // Range deteksi dinaikkan sedikit karena peluru lebih cepat
            if (targetKeren != null) {
                Vector2 targetDirection = (targetKeren.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                
                // 🔥 KEBUT: Kecepatan dikunci di 12f, nilai lerp dinaikkan ke 0.05f agar belokan tetap akurat di speed tinggi
                Projectile.velocity = Vector2.Normalize(Vector2.Lerp(Projectile.velocity, targetDirection * 12f, 0.05f)) * 12f;
            }
            else {
                // Jika tidak ada musuh, pastikan peluru konstan terbang lurus di kecepatan cepat (12f)
                if (Projectile.velocity.Length() < 12f) {
                    Projectile.velocity = Vector2.Normalize(Projectile.velocity) * 12f;
                }
            }
        }

        private NPC AmbilMusuhTerdekat(float maxRange) {
            NPC closestNPC = null;
            float closestDistance = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy()) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closestNPC = npc;
                    }
                }
            }
            return closestNPC;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int jumlahBintang = Main.rand.Next(3, 6); 
            
            for (int i = 0; i < jumlahBintang; i++) {
                Vector2 posisiLangit = target.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), -320f);
                Vector2 kecepatanAwal = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), 7f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(), 
                    posisiLangit, 
                    kecepatanAwal, 
                    ModContent.ProjectileType<StarRainProj>(), 
                    (int)(Projectile.damage * 0.5f), 
                    Projectile.knockBack * 0.5f, 
                    Projectile.owner, 
                    target.whoAmI
                );
            }
        }
    }
}