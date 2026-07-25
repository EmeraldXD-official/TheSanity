using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio; // Diperlukan untuk memanggil SoundEngine
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheSanity.Projectiles
{
    public class HydroSpellBookBeam : ModProjectile
    {
        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1; // Menembus semua musuh
            Projectile.tileCollide = false; // Menembus dinding
            Projectile.ignoreWater = true;
            
            // Skala ukuran proyektil (2x lipat lebih besar)
            Projectile.scale = 2.0f; 

            // Menggunakan sistem 16 frame grid (4x4)
            Main.projFrames[Projectile.type] = 16;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            
            // 1. Validasi: Jika player mati atau tidak aktif, hapus semburan air
            if (player.dead || !player.active) {
                Projectile.Kill();
                return;
            }

            // 2. Cek apakah tombol klik kiri masih ditahan (Channeling/Hold)
            if (!player.channel) {
                Projectile.Kill();
                return;
            }

            // 3. Kunci Animasi Player
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;

            // 4. Hitung Arah ke Mouse secara Real-Time
            Vector2 mouseDirection = Main.MouseWorld - player.MountedCenter;
            mouseDirection.Normalize(); 

            Projectile.velocity = mouseDirection;
            Projectile.rotation = Projectile.velocity.ToRotation();
            
            // Mengunci pangkal semburan air tepat di depan dada/buku player
            Projectile.Center = player.MountedCenter + Projectile.velocity * 16f;

            // Membalik tubuh player otomatis ke kanan/kiri mengikuti kursor mouse
            player.ChangeDir(Projectile.velocity.X > 0 ? 1 : -1);

            // Membuat item/buku di tangan mengikuti arah mouse
            player.itemRotation = (Projectile.velocity * player.direction).ToRotation();

            // =========================================================================
            // BARIS BARU: LOGIKA SUARA SEMBURAN BERKELANJUTAN (LOOPING SOUND)
            // Terraria otomatis mengurangi nilai 'soundDelay' setiap tick (frame game).
            // Jika sudah mencapai 0, suara akan dimainkan dan timer di-reset ke 20 tick.
            // =========================================================================
            if (Projectile.soundDelay <= 0) {
                Projectile.soundDelay = 20; // Reset jeda suara (20 tick = sekitar 0.3 detik sekali)
                
                // Memainkan suara sfx air mengalir (SoundID.Item13) tepat di posisi proyektil
                SoundEngine.PlaySound(SoundID.Item13, Projectile.Center);
            }

            // 5. Konsumsi Mana Berkala (Disedot setiap 10 tick sekali)
            Projectile.ai[0]++;
            if (Projectile.ai[0] >= 10) {
                Projectile.ai[0] = 0;
                if (!player.CheckMana(player.inventory[player.selectedItem], pay: true)) {
                    Projectile.Kill(); 
                    return;
                }
            }

            // 6. Memberikan efek cahaya biru terang di sekitar semburan
            Lighting.AddLight(Projectile.Center, 0.3f, 0.6f, 1.0f);

            // 7. Logika Pembacaan Animasi Grid 4x4
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 3) { 
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 16) {
                    Projectile.frame = 0; 
                }
            }
        }

        // Hitbox serangan memanjang (Line Collision)
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            float beamLength = 263f * Projectile.scale; 

            Vector2 endPoint = Projectile.Center + Projectile.velocity * beamLength;

            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, endPoint, 40f * Projectile.scale, ref point)) {
                return true;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            int frameWidth = 263;
            int frameHeight = 92;
            int totalColumns = 4;

            int currentColumn = Projectile.frame % totalColumns;
            int currentRow = Projectile.frame / totalColumns;

            Rectangle sourceRectangle = new Rectangle(
                currentColumn * frameWidth, 
                currentRow * frameHeight, 
                frameWidth, 
                frameHeight
            );

            // Engsel berada di ekor lancip sebelah kanan gambar
            Vector2 origin = new Vector2(frameWidth, frameHeight / 2f);

            // Kalibrasi rotasi (ditambah 180 derajat karena aset menghadap kiri)
            float finalRotation = Projectile.rotation + MathHelper.Pi;
            SpriteEffects effects = SpriteEffects.None;

            // Efek flip dinamis saat menembak ke kanan agar ombak air tidak terbalik atas-bawah
            if (Projectile.velocity.X >= 0) {
                effects = SpriteEffects.FlipVertically;
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                sourceRectangle,
                lightColor,
                finalRotation,
                origin,
                Projectile.scale, 
                effects,
                0
            );

            return false; 
        }
    }
}