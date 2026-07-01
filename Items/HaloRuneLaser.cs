using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class HaloRuneLaser : ModProjectile
    {
        private const float MaxLaserLength = 1200f; 
        private readonly float[] laserLengths = new float[3]; 
        private const float RotationSpeed = 0.03f; 

        // Mengatur jarak (dalam piksel) dari pusat player sebelum laser mulai muncul agar badan tidak tertutupi
        private const float LaserStartOffset = 55f; 

        public override void SetDefaults() {
            Projectile.width = 26; 
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12; 
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (player.dead || !player.channel) {
                Projectile.Kill();
                return;
            }

            // Update arah proyektil ke arah Mouse secara real-time
            if (Main.myPlayer == Projectile.owner) {
                Vector2 aimDir = Main.MouseWorld - player.MountedCenter;
                aimDir.Normalize();
                Projectile.velocity = aimDir;
                Projectile.direction = Main.MouseWorld.X > player.MountedCenter.X ? 1 : -1;
                Projectile.netUpdate = true;
            }

            Projectile.Center = player.MountedCenter; 
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.heldProj = Projectile.whoAmI;

            // Menggerakkan lengan depan player secara penuh mengarah ke posisi mouse/laser
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.velocity.ToRotation() - MathHelper.PiOver2);

            Projectile.rotation += RotationSpeed;

            // Denyut warna partikel dust agar bervariasi
            float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 0.5f + 0.5f;
            Color dustColor = Color.Lerp(new Color(255, 105, 180), Color.White, pulse);

            for (int i = 0; i < 3; i++) {
                float angle = Projectile.rotation + MathHelper.ToRadians(i * 120);
                Vector2 direction = angle.ToRotationVector2();
                
                // Menghitung panjang laser dimulai dari titik luar setelah Offset
                Vector2 laserStartPos = Projectile.Center + direction * LaserStartOffset;
                laserLengths[i] = CalculateLaserLength(laserStartPos, direction);

                // Partikel dust baru akan muncul di sepanjang laser setelah melewati Offset
                if (Main.rand.NextBool(3)) {
                    Vector2 dustPos = laserStartPos + direction * Main.rand.NextFloat(laserLengths[i]);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.PinkTorch, Vector2.Zero, 100, dustColor, 1.3f);
                    dust.noGravity = true;
                }
            }

            if (Projectile.ai[0]++ % 12 == 0) {
                if (!player.CheckMana(player.inventory[player.selectedItem], -1, true)) {
                    Projectile.Kill();
                }
            }
        }

        private float CalculateLaserLength(Vector2 start, Vector2 direction) {
            float distance = 0f;
            float adjustedMax = MaxLaserLength - LaserStartOffset; 

            while (distance < adjustedMax) {
                Vector2 checkPos = start + direction * distance;
                Point tileCoord = checkPos.ToTileCoordinates();

                if (WorldGen.InWorld(tileCoord.X, tileCoord.Y)) {
                    Tile tile = Main.tile[tileCoord.X, tileCoord.Y];
                    if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                        return distance; 
                    }
                } else {
                    break;
                }
                distance += 8f; 
            }
            return adjustedMax;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            for (int i = 0; i < 3; i++) {
                float angle = Projectile.rotation + MathHelper.ToRadians(i * 120);
                Vector2 direction = angle.ToRotationVector2();
                
                // Hitbox serangan disesuaikan agar tidak mengenai musuh yang berada tepat di dalam tubuh player
                Vector2 startPoint = Projectile.Center + direction * LaserStartOffset;
                Vector2 endPoint = startPoint + direction * laserLengths[i];

                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), startPoint, endPoint, 16f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Player player = Main.player[Projectile.owner];

            // --- DRAW STAFF MANUALLY (Dikecilkan ukurannya & sinkron dengan tangan) ---
            Texture2D staffTexture = ModContent.Request<Texture2D>("TheSanity/Items/HaloRune").Value;
            int staffFrameCount = 10;
            int staffFrameHeight = staffTexture.Height / staffFrameCount;
            
            int currentStaffFrame = Main.itemAnimations[ModContent.ItemType<Items.HaloRune>()]?.Frame ?? 0;
            Rectangle staffSrcRect = new Rectangle(0, currentStaffFrame * staffFrameHeight, staffTexture.Width, staffFrameHeight);
            
            // Mengubah ukuran staff menjadi lebih kecil saat ditembakkan (75% dari ukuran sprite asli)
            float staffScale = 0.75f; 

            // Titik tumpu genggaman tangan pada sprite (X: Tengah, Y: Dekat Bawah gagang kristal)
            Vector2 staffOrigin = new Vector2(staffTexture.Width / 2f, staffFrameHeight - 14f);
            
            // Jarak posisi rendering tepat di pusat genggaman tangan player (offset maju sejauh 4f)
            Vector2 staffDrawPos = player.MountedCenter + Projectile.velocity * 4f - Main.screenPosition;
            float staffRot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Main.spriteBatch.Draw(staffTexture, staffDrawPos, staffSrcRect, lightColor, staffRot, staffOrigin, staffScale, SpriteEffects.None, 0f);

            // --- DRAW LASERS WITH GLOW EFFECT & ADDITIVE BLENDING ---
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            int frame1Width = 18; 
            int frame2Width = 38; 
            int frame3Width = 18; 
            int textureHeight = 26;

            Rectangle frame1Rect = new Rectangle(0, 0, frame1Width, textureHeight);
            Rectangle frame2Rect = new Rectangle(frame1Width, 0, frame2Width, textureHeight);
            Rectangle frame3Rect = new Rectangle(frame1Width + frame2Width, 0, frame3Width, textureHeight);

            Vector2 origin = new Vector2(0f, textureHeight / 2f); 

            // Efek Kelap-kelip: Paduan denyut frekuensi cepat (0.3f) dan denyut warna lambat (0.06f)
            float fastPulse = (float)Math.Sin(Main.GameUpdateCount * 0.3f) * 0.18f + 0.82f;
            float slowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.06f) * 0.5f + 0.5f;
            
            // Jittering: Ketebalan laser berubah-ubah sedikit setiap frame agar terasa dinamis berenergi
            float thicknessGlow = (1.6f + (float)Math.Sin(Main.GameUpdateCount * 0.5f) * 0.2f) * fastPulse;
            float thicknessCore = (1.0f + (float)Math.Cos(Main.GameUpdateCount * 0.5f) * 0.1f) * fastPulse;

            Color coreColor = Color.Lerp(new Color(255, 225, 245), Color.White, slowPulse) * fastPulse; 
            Color glowColor = new Color(255, 20, 147) * 0.5f * fastPulse; 

            // Buka mode Additive Blend untuk membuat efek laser menyala neon benderang
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < 3; i++) {
                float angle = Projectile.rotation + MathHelper.ToRadians(i * 120);
                Vector2 direction = angle.ToRotationVector2();
                
                float totalLength = laserLengths[i];
                
                // Dorong posisi gambar awal laser keluar sejauh 'LaserStartOffset' piksel agar tubuh player bersih dari laser
                Vector2 drawPosition = Projectile.Center + direction * LaserStartOffset - Main.screenPosition;

                // --- LAYER 1: GLOW EFFECT (Lapisan Luar Aura Laser) ---
                Main.spriteBatch.Draw(texture, drawPosition, frame1Rect, glowColor, angle, origin, new Vector2(1f, thicknessGlow), SpriteEffects.None, 0f);
                float bodyLengthGlow = totalLength - frame1Width - frame3Width;
                if (bodyLengthGlow > 0) {
                    Vector2 bodyPosition = drawPosition + direction * frame1Width;
                    Vector2 bodyScale = new Vector2(bodyLengthGlow / frame2Width, thicknessGlow); 
                    Main.spriteBatch.Draw(texture, bodyPosition, frame2Rect, glowColor, angle, origin, bodyScale, SpriteEffects.None, 0f);
                }
                float endDstGlow = Math.Max(frame1Width, totalLength - frame3Width);
                Vector2 endPositionGlow = drawPosition + direction * endDstGlow;
                Main.spriteBatch.Draw(texture, endPositionGlow, frame3Rect, glowColor, angle, origin, new Vector2(1f, thicknessGlow), SpriteEffects.None, 0f);

                // --- LAYER 2: CORE EFFECT (Lapisan Inti Dalam Putih Terang) ---
                Main.spriteBatch.Draw(texture, drawPosition, frame1Rect, coreColor, angle, origin, new Vector2(1f, thicknessCore), SpriteEffects.None, 0f);
                float bodyLength = totalLength - frame1Width - frame3Width;
                if (bodyLength > 0) {
                    Vector2 bodyPosition = drawPosition + direction * frame1Width;
                    Vector2 bodyScale = new Vector2(bodyLength / frame2Width, thicknessCore); 
                    Main.spriteBatch.Draw(texture, bodyPosition, frame2Rect, coreColor, angle, origin, bodyScale, SpriteEffects.None, 0f);
                }
                float endDst = Math.Max(frame1Width, totalLength - frame3Width);
                Vector2 endPosition = drawPosition + direction * endDst;
                Main.spriteBatch.Draw(texture, endPosition, frame3Rect, coreColor, angle, origin, new Vector2(1f, thicknessCore), SpriteEffects.None, 0f);
            }

            // Kembalikan ke mode AlphaBlend standar Terraria agar interface game tidak ikut bug/berantakan
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}