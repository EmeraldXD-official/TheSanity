using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [MULTI-MODE PROJECTILE]: ROTATING MAGIC BOOK (6-SECOND REPEATING ATTACK)
    // =========================================================================
    public class AncientMagicBook : ModProjectile
    {
        // Menyediakan jalur tekstur fallback default yang aman
        public override string Texture => "Terraria/Images/Item_" + ItemID.GoldenShower;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 30;
            
            Projectile.friendly = false;
            Projectile.hostile = true;       // Bisa melukai player via contact damage
            Projectile.tileCollide = false;  // Menembus dinding agar bisa melayang bebas di langit
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;       // Tidak mudah hancur oleh serangan player

            // LOKASI TOTAL LIFE BALANCING: Kita set masa hidup buku melayang lebih lama (misal 15 Detik = 900 Frame)
            Projectile.timeLeft = 900; 
        }

        public override void AI() {
            // -------------------------------------------------------------------------
            // 1. MEKANIK JEDA FISIKA: BIARKAN MELESAT JAUH SEBELUM MEMULAI SLOW-MOTION
            // -------------------------------------------------------------------------
            // Kita gunakan variabel internal Projectile.localAI[0] khusus untuk pengereman agar tidak mengganggu timer tembakan
            Projectile.localAI[0]++; 

            // Buku melesat bebas mengikuti kecepatan lemparan boss selama 45 Frame awal (~0.8 detik)
            if (Projectile.localAI[0] > 45f) {
                // Setelah melesat jauh, gaya gesek udara aktif secara bertahap membuat buku melambat estetik di langit
                Projectile.velocity *= 0.88f; 
            }

            // Membuat buku berputar secara konstan di udara sepanjang waktu hidupnya
            Projectile.rotation += 0.22f;

            // -------------------------------------------------------------------------
            // 2. TIMING REPEATING ATTACK CONTROLLER (SETIAP 6 DETIK SEKALI / 360 FRAME)
            // -------------------------------------------------------------------------
            Projectile.ai[0]++; // Timer utama penembakan peluru

            // LOKASI SPEED BALANCING TEMBAKAN: 6 Detik = 360 Frame
            if (Projectile.ai[0] >= 360f) {
                // KUNCI: Reset timer kembali ke 0 agar loop menembak berulang-ulang tanpa menghancurkan buku!
                Projectile.ai[0] = 0f;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int projType = ProjectileID.WoodenArrowHostile; 
                    int damage = Projectile.damage; // Otomatis mewarisi base damage dinamis milik boss (15 atau 35)
                    float shootSpeed = 5f;

                    // Cek mode serangan buku yang dikirim oleh boss (ai[1])
                    int bookMode = (int)Projectile.ai[1];

                    if (bookMode == 0) // ATTACK 1: Golden Shower
                    {
                        projType = ProjectileID.GoldenShowerHostile;
                        shootSpeed = 6f;
                        SoundEngine.PlaySound(SoundID.Item21, Projectile.Center);
                    }
                    else if (bookMode == 1) // ATTACK 2: Cursed Flames
                    {
                        projType = ProjectileID.EyeFire; // Peluru semburan api hijau pekat Hostile
                        shootSpeed = 5f;
                        SoundEngine.PlaySound(SoundID.Item20, Projectile.Center);
                    }
                    else if (bookMode == 2) // ATTACK 3: Demon Scythe
                    {
                        projType = ProjectileID.DemonSickle; // ID internal versi Hostile milik Demon
                        shootSpeed = 4f;
                        SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);
                    }

                    // -------------------------------------------------------------------------
                    // LOGIKA TEMBAKAN MULTI-ARAH MATA ANGIN (SALIB / + SHAPE)
                    // -------------------------------------------------------------------------
                    for (int i = 0; i < 4; i++) {
                        Vector2 velocityDirection = new Vector2(0f, -shootSpeed).RotatedBy(MathHelper.PiOver2 * i);

                        int spawnedProj = Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            Projectile.Center,
                            velocityDirection,
                            projType,
                            damage,
                            2f,
                            Main.myPlayer
                        );

                        // Kunci status peluru hasil tembakan agar mutlak musuh punya
                        if (spawnedProj < Main.maxProjectiles) {
                            Main.projectile[spawnedProj].hostile = true;
                            Main.projectile[spawnedProj].friendly = false;
                        }
                    }
                }
                
                // Note: Perintah Projectile.Kill() dihapus dari sini agar buku tidak mati saat menembak!
            }
        }

        // =========================================================================
        // 3. CONTACT DAMAGE SYSTEM: SUNTIKAN DEBUFF BERDASARKAN JENIS BUKU
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            int bookMode = (int)Projectile.ai[1];

            if (bookMode == 0) {
                target.AddBuff(BuffID.Ichor, 3600); // Debuff Ichor (1 Menit)
            }
            else if (bookMode == 1) {
                target.AddBuff(BuffID.CursedInferno, 1200); // Debuff Cursed Inferno (20 Detik)
            }
            else if (bookMode == 2) {
                target.AddBuff(BuffID.ShadowFlame, 1200); // Debuff Shadowflame (20 Detik)
            }
        }

        // =========================================================================
        // 4. DYNAMIC DRAWING SYSTEM: AMANKAN LOADING SPRITE VANILLA VIA REQUEST ASSETS
        // =========================================================================
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture;
            int bookMode = (int)Projectile.ai[1];

            // Menggunakan Main.Assets.Request secara real-time untuk memaksa texture termuat secara instan dan mencegah bug kegagalan render sprite
            if (bookMode == 1) {
                texture = Main.Assets.Request<Texture2D>("Images/Item_" + ItemID.CursedFlames, AssetRequestMode.ImmediateLoad).Value;
            }
            else if (bookMode == 2) {
                texture = Main.Assets.Request<Texture2D>("Images/Item_" + ItemID.DemonScythe, AssetRequestMode.ImmediateLoad).Value;
            }
            else {
                texture = Main.Assets.Request<Texture2D>("Images/Item_" + ItemID.GoldenShower, AssetRequestMode.ImmediateLoad).Value;
            }

            if (texture == null) return false;

            // Hitung kalkulasi posisi rendering di layar monitor player
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            // Campurkan warna dasar pencahayaan sekitar agar menyatu natural di dunia game
            Color renderColor = lightColor.MultiplyRGB(Color.White);
            
            Main.spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                renderColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );

            return false; // Matikan gambar bawaan agar tidak double sprite
        }
    }
}