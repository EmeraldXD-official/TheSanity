using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class AmarokSword : ModProjectile
    {
        // Transparan karena sprite pedang digambar di PreDraw Amarok (agar tepat di BELAKANG Yoyo)
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PurificationPowder;

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; // Infinite Piercing
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2; // Dijaga tetap hidup selama Amarok aktif
            
            // Memberikan cooldown hit agar damage pedang masuk secara konsisten
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[0];
            int swordIndex = (int)Projectile.ai[1];

            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];

            // Jika Amarok hancur/hilang, pedang ikut hancur
            if (!parent.active || parent.type != ProjectileID.Amarok || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            // Menjaga proyektil tetap hidup
            Projectile.timeLeft = 2;

            // Hitung Sudut Rotasi (3 pedang terpisah 120 derajat mengikuti rotasi Amarok)
            float baseAngle = parent.rotation + swordIndex * (MathHelper.TwoPi / 3f);

            // Posisi Hitbox Pedang (Ditempatkan di tengah bilah pedang ~30 pixel keluar)
            Vector2 bladeCenterOffset = baseAngle.ToRotationVector2() * 30f;
            Projectile.Center = parent.Center + bladeCenterOffset;

            // PARTIKEL DI PUCUK BILAH PEDANG (Tip Position ~52 pixel dari pusat)
            Vector2 tipPos = parent.Center + baseAngle.ToRotationVector2() * 52f;
            
            // Dust Frost/Ice Torch di pucuk pedang saat berputar
            Dust d = Dust.NewDustPerfect(tipPos, DustID.IceTorch, baseAngle.ToRotationVector2() * 1.5f, 100, default, 1.2f);
            d.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Inflict Frostbite (BuffID.Frostburn2) selama 4 detik (240 ticks)
            target.AddBuff(BuffID.Frostburn2, 240);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Return false agar sprite PurificationPowder tidak digambar
            return false;
        }
    }
}