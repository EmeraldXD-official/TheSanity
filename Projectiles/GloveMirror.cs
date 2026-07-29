using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace TheSanity
{
    // ==========================================
    // 1. PROYEKTIL VISUAL: TITAN GLOVE
    // ==========================================
    public class TitanGloveVisual : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.TitanGlove}";

        public override void SetDefaults()
        {
            Projectile.width = 26;       
            Projectile.height = 28;
            Projectile.aiStyle = -1;     
            Projectile.penetrate = -1;   
            Projectile.friendly = false; 
            Projectile.hostile = false;  
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            Projectile.scale = 1.15f; // Sedikit diperbesar agar visual terjangan sarung tangan terlihat jelas
        }

        public override void AI()
        {
            // [TITAN GLOVE AIM ROTATION LOCATION]
            // Kita ambil data NPC Mimic yang memanggil proyektil ini (disimpan di ai[0])
            NPC owner = Main.npc[(int)Projectile.ai[0]];

            if (owner.active && owner.type == NPCID.Mimic)
            {
                Player target = Main.player[owner.target];
                if (target != null && target.active && !target.dead)
                {
                    // Hitung sudut arah menuju player
                    Vector2 aimVector = target.Center - Projectile.Center;
                    
                    // PiOver2 (90 derajat) ditambahkan sebagai offset agar ujung kepalan sarung tangan lurus menunjuk player
                    Projectile.rotation = aimVector.ToRotation() + MathHelper.PiOver2; 
                }
            }

            if (Main.rand.NextBool(4)) 
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.AmberBolt, 0f, 0f, 100, default, 1.3f);
            }

            Projectile.timeLeft = 180; 
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            return false; 
        }
    } // Kurung penutup class TitanGloveVisual

    // ==========================================
    // 2. PROYEKTIL VISUAL: MAGIC MIRROR
    // ==========================================
    public class MagicMirrorVisual : ModProjectile
    {
        // Meminjam sprite Magic Mirror asli dari vanilla Terraria (ID: 50)
        public override string Texture => $"Terraria/Images/Item_{ItemID.MagicMirror}";

        public override void SetDefaults()
        {
            Projectile.width = 20;       
            Projectile.height = 20;
            Projectile.aiStyle = -1;     
            Projectile.penetrate = -1;   
            
            // --- SETTING KOSMETIK (TIDAK MELUKAI) ---
            Projectile.friendly = false; 
            Projectile.hostile = false;  
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
        }

        public override void AI()
        {
            // BALANCING GUIDE: Efek denyut sihir (Membesar dan mengecil secara dinamis)
            Projectile.ai[0] += 0.05f;
            Projectile.scale = 1f + (float)Math.Sin(Projectile.ai[0]) * 0.15f;

            // Membuat efek partikel debu sihir biru berkilau khas cermin
            if (Main.rand.NextBool(3))
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, 0f, 0f, 150, default(Color), 0.8f);
                Main.dust[d].velocity *= 0.5f;
            }

            Projectile.timeLeft = 180;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            // Mengunci poros rotasi di tengah tekstur cermin
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            return false;
        }
    } // Kurung penutup class MagicMirrorVisual
} // Kurung penutup namespace TheSanity