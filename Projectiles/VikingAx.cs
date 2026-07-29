using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity.Projectiles
{
    public class VikingAx : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.TitaniumWaraxe}";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults()
        {
            // LOKASI HITBOX KECIL: Diubah dari 30x30 menjadi 20x20 agar lebih adil buat dihindari
            Projectile.width = 20;
            Projectile.height = 20;
            
            Projectile.aiStyle = -1; 
            Projectile.hostile = true; 
            Projectile.friendly = false;
            Projectile.tileCollide = true; 
            Projectile.penetrate = 1; 
            Projectile.timeLeft = 300; 

            // LOKASI UKURAN SPRITE: Mengecilkan gambar kapak sebesar 30% (Skala 0.7f)
            Projectile.scale = 0.7f; 
        }

        public override void AI()
        {
            // LOKASI SPEED PUTARAN KAPAK
            Projectile.rotation += 0.25f * Projectile.direction;

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Frostburn, 180);

            if (Main.rand.NextFloat() < 0.05f) 
            {
                int frozenDuration = Main.masterMode ? 85 : 180;
                target.AddBuff(BuffID.Frozen, frozenDuration);
            }
        }
    }
}