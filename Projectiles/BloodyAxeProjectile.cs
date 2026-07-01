using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity.Projectiles
{
    public class BloodyAxeProjectile : ModProjectile
    {
        // Langsung mengambil asset internal Haemorrhaxe dari vanilla Terraria
        public override string Texture => $"Terraria/Images/Item_{ItemID.BloodHamaxe}";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults()
        {
            // LOKASI HITBOX KECIL
            Projectile.width = 24;
            Projectile.height = 24;
            
            Projectile.aiStyle = -1; 
            Projectile.hostile = true; 
            Projectile.friendly = false;
            
            // Set false agar tidak hancur menabrak ubin saat dilempar/proses TP
            Projectile.tileCollide = false; 
            
            Projectile.penetrate = -1; // Tembus player agar kapak tetap melaju
            Projectile.timeLeft = 180;  // Bertahan 3 detik di udara

            // Menggunakan ukuran asli (1.0f) atau silakan ganti jika ingin disesuaikan
            Projectile.scale = 1f; 
        }

        public override void AI()
        {
            // LOKASI SPEED PUTARAN KAPAK
            Projectile.rotation += 0.30f * Projectile.direction;

            // Efek partikel darah khas Blood Moon saat kapak berputar terbang
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, Color.Red, 1.1f);
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Memberikan debuff bleeding jika player terkena kapak terbang ini
            target.AddBuff(BuffID.Bleeding, 180);
        }
    }
}