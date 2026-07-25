using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

// MENGHOOK BUFFER SENSE KAMU
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    public class PinkFlame : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PinkFlame";

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 96;
            
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true; 
            Projectile.penetrate = -1;
            
            // 🛑 [LOKASI BALANCING DURASI HIDUP API]
            Projectile.timeLeft = 300; 
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 🛑 [LOKASI BALANCING DEBUFF PLAYER - Durasi 5 detik]
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 5 * 60);
        }

        public override void AI() {
            // 1. MEMILIH SUARA SECARA ACAK SAAT SPAWN
            if (Projectile.localAI[0] == 0) {
                int randomSoundSlot = Main.rand.Next(1, 4);
                string soundPath = $"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PinkFlameShot{randomSoundSlot}";
                SoundEngine.PlaySound(new SoundStyle(soundPath), Projectile.Center);
                
                Projectile.localAI[0] = 1f;
            }

            // 2. DYNAMIC HITBOX ADJUSTMENT
            if (Projectile.localAI[1] == 0) {
                Projectile.width = (int)(68 * Projectile.scale);
                Projectile.height = (int)(96 * Projectile.scale);
                Projectile.localAI[1] = 1f;
            }

            // 3. OVERRIDE DAMAGE (TARGET MASTER MODE: 100)
            if (Main.masterMode) {
                Projectile.damage = 34; 
            }
            else if (Main.expertMode) {
                Projectile.damage = 40; 
            }
            else {
                Projectile.damage = 60; 
            }

            // 4. ANIMASI VERTICAL SPRITE SHEET
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6) { 
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type]) {
                    Projectile.frame = 0;
                }
            }

            // =========================================================================
            // PERBAIKAN ORIENTASI ARAH HADAP SPRITE API
            // =========================================================================
            // 🛑 [LOKASI ADJUSTMENT ROTASI SPRITE SHEET]
            // Mengubah '+ MathHelper.PiOver2' menjadi '- MathHelper.PiOver2' agar 
            // spritemu diputar 180 derajat secara internal. Sekarang bagian depan 
            // api dijamin bakal memimpin di garis paling depan velocity terbangnya!
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            int randomSoundSlot = Main.rand.Next(1, 4);
            string soundPath = $"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PinkFlameShot{randomSoundSlot}";
            SoundEngine.PlaySound(new SoundStyle(soundPath), Projectile.Center);

            // 🛑 [DIHAPUS] Dulu di sini nembakin 3-5 PlutoBall pas nabrak tile -- dicabut karena
            // kelewat susah di-dodge (nambahin ancaman "bonus" yang gak kebayang player pas
            // dodge PinkFlame-nya sendiri). Sekarang PinkFlame nabrak tile ya udah beres gitu aja.

            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() * 0.5f;

            spriteBatch.Draw(
                texture, 
                Projectile.Center - Main.screenPosition, 
                sourceRect, 
                Color.White, 
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