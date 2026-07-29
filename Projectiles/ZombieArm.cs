using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace TheSanity
{
    public class ZombieArmProjectile : ModProjectile
    {
        // --- TRIK RAHASIA: PINJAM SPRITE ZOMBIE ARM ASLI VANILLA ---
        // Game akan otomatis mengambil tekstur senjata Zombie Arm langsung dari memori inti
        public override string Texture => $"Terraria/Images/Item_{ItemID.ZombieArm}";

        public override void SetDefaults()
        {
            // Menyesuaikan ukuran hitbox peluru agar pas dengan panjang tangan zombie
            Projectile.width = 22; 
            Projectile.height = 22;
            
            Projectile.hostile = true;     // [HOSTILE LOCK] Menyerang player
            Projectile.friendly = false;   // Tidak melukai musuh
            
            // Mengikuti mekanik baru: Hancur saat menabrak dinding/block gua
            Projectile.tileCollide = true; 
            
            Projectile.aiStyle = -1;       // Menggunakan AI kustom buatan sendiri
            Projectile.timeLeft = 300;     // Batas hidup peluru di layar (5 detik)
        }

        public override void AI()
        {
            // --- MEKANIK MOVEMENT LURUS ---
            Projectile.position += Projectile.velocity;

            // --- MEKANIK ROTASI BERPUTAR HELIKOPTER ---
            // [ROTATION SPEED BALANCING LOCATION]
            // Mengubah angka 0.3f untuk mempercepat atau memperlambat putaran tangan terbangnya (IRL)
            Projectile.rotation += 0.3f; 

            // --- EFEK VISUAL PARTIKEL PEMBUSUKAN ---
            if (Main.rand.NextBool(4))
            {
                // Menggunakan DustID.Blood (Darah) atau DustID.ScourgeOfTheCorruptor (Efek daging busuk hijau)
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 120, default, 1f);
                d.velocity *= 0.2f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // --- BALANCING LOCATION: EFEK DEBUFF JIKA TERKENA TANGAN ZOMBIE ---
            // Memberikan debuff Bleeding (ID 30) selama 5 detik (300 frame), menghentikan regenerasi HP player
            target.AddBuff(BuffID.Bleeding, 300);

            // Partikel darah tambahan saat sukses menggapai/memukul player
            for (int i = 0; i < 6; i++)
            {
                Dust.NewDust(target.position, target.width, target.height, DustID.Blood);
            }
        }

        // --- MEKANIK FISIK: HANCUR SAAT MENABRAK WALL / BLOCK ---
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Suara tulang patah/tumpul membentur tanah solid gua
            Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath2, Projectile.Center);

            // Memunculkan sisa partikel hancur tulang/daging saat menabrak block
            for (int i = 0; i < 10; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 1.1f);
                d.velocity = Main.rand.NextVector2Circular(4f, 4f);
            }

            return true; // Hancurkan proyektil seketika
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Mengambil buffer texture Zombie Arm dari library game
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            // Menetapkan poros putaran tepat di titik tengah texture agar berputar stabil (tidak oleng)
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // Gambar manual proyektilnya di layar
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

            return false; // Matikan gambar duplikat bawaan engine
        }
    }
}