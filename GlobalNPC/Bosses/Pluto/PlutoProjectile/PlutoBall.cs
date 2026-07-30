using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    public class PlutoBall : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PlutoBall";

        public override void SetDefaults() {
            // Ukuran default bola, silakan ganti jika sprite aslimu jauh lebih besar/kecil
            Projectile.width = 24;
            Projectile.height = 24;
            
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            
            Projectile.timeLeft = 420; // Waktu hidup bola pantul agak panjang agar bisa memantul maksimal
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 🛑 [LOKASI BALANCING DEBUFF PLAYER - Durasi 2 detik (2 * 60 frame)]
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 2 * 60);
        }

        public override void AI() {
            // 1. OVERRIDE DAMAGE NYEBELIN (TARGET MASTER MODE: 20)
            if (Main.masterMode) {
                // 🛑 [LOKASI BALANCING DAMAGE MASTER MODE]
                // 7 x 3 = 21 Damage (Kecil tapi konstan mengganggu player)
                Projectile.damage = 7; 
            }
            else if (Main.expertMode) {
                Projectile.damage = 10; // 10 x 2 = 20 Damage
            }
            else {
                Projectile.damage = 15; // Classic Mode
            }

            // 2. PENENTUAN MAKSIMAL PANTULAN SECARA RANDOM SAAT PERTAMA LAHIR
            if (Projectile.localAI[0] == 0) {
                // Menerapkan batas pantul acak antara 3 sampai 5 kali pantulan
                Projectile.localAI[0] = Main.rand.Next(3, 6); 
            }

            // Memberikan efek putaran visual konstan pada bola saat meluncur bebas
            Projectile.rotation += 0.15f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            // Hitung total berapa kali bola ini sudah sukses memantul lantai/dinding
            Projectile.ai[0]++; 

            // Efek Suara Item70 disetiap momentum benturan tile terjadi
            SoundEngine.PlaySound(SoundID.Item70, Projectile.Center);

            // Jika jumlah pantulan sudah melampaui batas target acak yang ditentukan di AI()
            if (Projectile.ai[0] >= Projectile.localAI[0]) {
                Projectile.Kill(); // Hancurkan bola karena kuota pantul habis
            }
            else {
                // RUMUS FISIKA PANTULAN (REBOUND)
                // Membalikkan arah kecepatan vektor sumbu X atau Y tergantung sisi mana yang menabrak block
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * 0.95f; // Mengurangi 5% momentum agar pantulan natural
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.95f;
                }
            }

            return false; // Return false agar proyektil tidak mati secara instan oleh sistem bawaan vanilla
        }

        public override bool PreDraw(ref Color lightColor) {
            // IMPLEMENTASI GLOW IN THE DARK BOLA
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            spriteBatch.Draw(
                texture, 
                Projectile.Center - Main.screenPosition, 
                null, 
                Color.White, // Selalu menyala terang menembus gelap goa dunia malam Pluto
                Projectile.rotation, 
                origin, 
                Projectile.scale, 
                SpriteEffects.None, 
                0
            );

            return false;
        }
    }
}