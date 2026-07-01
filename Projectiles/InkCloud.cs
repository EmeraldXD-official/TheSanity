using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles // Perjelas sub-folder namespace-nya
{
    public class InkCloud : ModProjectile
    {
        // KUNCI UTAMA: Kita pakai tekstur transparan bawaan vanilla agar aman tanpa file .png
        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200; // 20 Detik
            Projectile.aiStyle = -1;
        }

        public override void AI()
        {
            // Efek ngerem konstan
            Projectile.velocity *= 0.95f;

            // TRICK VISUAL: Paksa dust memancar dari pusat bodi agar kelihatan jelas
            if (Main.netMode != NetmodeID.Server)
            {
                // Taruh dust konstan setiap frame agar awannya langsung padat
                for (int i = 0; i < 3; i++)
                {
                    Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(20f, 20f);
                    Dust smoke = Dust.NewDustDirect(
                        spawnPos, 
                        0, 0, 
                        DustID.Granite, 
                        0f, 0f, 
                        100, 
                        Color.Black, // Warna hitam pekat tinta
                        Main.rand.NextFloat(1.8f, 2.8f) // Ukuran asap agak dibesarkan biar pekat
                    );
                    smoke.velocity = Main.rand.NextVector2Circular(0.6f, 0.6f);
                    smoke.noGravity = true;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Obstructed, 180); // Debuff buta 3 detik
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            modifiers.Knockback *= 0f;
        }
    }
}