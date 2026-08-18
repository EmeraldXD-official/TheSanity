using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.Projectiles
{
    public class BlueMoonBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_385";

        public override void SetStaticDefaults()
        {
            // Mengambil jumlah frame asli ID 409 secara otomatis
            Main.projFrames[Type] = Main.projFrames[385];
        }

        public override void SetDefaults()
        {
            // UKURAN HITBOX AOE (Diperbesar agar musuh di sekitar target utama ikut kena)
            Projectile.width = 60; 
            Projectile.height = 60;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300; // 5 Detik

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15; // Interval DPS (4x hit per detik)
        }

        public override void AI()
        {
            int targetNPCIndex = (int)Projectile.ai[0];

            // Validasi Musuh Utama (Jika mati/hilang, projectile hancur)
            if (targetNPCIndex < 0 || targetNPCIndex >= Main.maxNPCs || !Main.npc[targetNPCIndex].active || Main.npc[targetNPCIndex].friendly)
            {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetNPCIndex];

            // BOLT TETAP TERKUNCI DI CENTER MUSUH UTAMA
            Projectile.Center = target.Center;

            // Loop Animasi Sprite Sheet
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Type])
                {
                    Projectile.frame = 0;
                }
            }

            // Partikel Air
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity *= 0.5f;
            }
        }

        // =======================================================
        // PRE-DRAW: GAMBAR TETAP PRESISI DI TENGAH TARGET UTAMA
        // =======================================================
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            int frameHeight = texture.Height / Main.projFrames[Type];
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);

            // Origin di titik tengah frame sprite (terpisah dari ukuran Hitbox)
            Vector2 origin = sourceRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                Color.White * 0.9f,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        // CanHitNPC tidak perlu di-override lagi agar semua musuh di area Hitbox terkena damage!
    }
}