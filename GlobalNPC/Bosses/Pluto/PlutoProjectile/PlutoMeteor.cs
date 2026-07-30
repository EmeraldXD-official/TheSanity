using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.DataStructures;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // =========================================================================
    // METEOR PATTERN 8 (METEOR STORM DASH) -- lihat MeteorShowerDash.cs buat logic
    // pemanggilan di PlutoHead. Class ini nanganin SATU meteor, baik versi BESAR
    // (disebar dari langit tiap siklus dash) MAUPUN versi KECIL/serpihan (hasil
    // ledakan pas kena PlutoMeteorNuke -- lihat Kill() di bawah). Dibedain lewat
    // ai[0] == 1f (fragment), ai[0] == 0f/default (meteor utuh).
    // =========================================================================
    public class PlutoMeteor : ModProjectile
    {
        // Spritesheet: 50x308 -> 4 frame vertikal @ 50x77.
        // 🛑 [PENTING] Depan sprite ini SUDAH menghadap ke BAWAH secara default (rotation 0),
        // beda sama kebanyakan proyektil lain yang defaultnya nunjuk ke ATAS -- makanya rotasi
        // di AI() pakai offset MINUS PiOver2 (bukan PLUS). Kalau nanti keliatan mlintir salah
        // arah pas di-test in-game, ini duluan yang dicek/dibalik tandanya.
        private const int FrameCount = 4;

        private bool IsFragment => Projectile.ai[0] == 1f;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PlutoMeteorite";

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = FrameCount;
        }

        public override void SetDefaults() {
            // 🛑 [DIBIKIN LEBIH BESAR LAGI] scale default dinaikin dari 1.6f -> 2.2f, hitbox
            // (width/height) ikut disesuain proporsional (basis asli 40x220 * 2.2). Sekarang
            // meteornya jauh lebih dominan secara visual & lebih gampang keliatan dari jauh.
            Projectile.width = 88;
            Projectile.height = 484;
            Projectile.hostile = true;
            Projectile.friendly = false;
            // Meteor beneran nabrak lantai/tanah arena -> otomatis kepanggil Kill() -> meledak
            // jadi serpihan di titik itu (bukan tembus lantai kayak roket RedMiniNuke).
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.scale = 2.2f;
        }

        public override void OnSpawn(IEntitySource source) {
            if (IsFragment) {
                // Serpihan tetap jauh lebih kecil dari meteor induknya walau meteor utuhnya
                // sekarang lebih besar -- basis ukuran fragment TETAP dari ukuran ASLI (40x220),
                // BUKAN dari hitbox meteor induk yang udah di-scale up, biar serpihannya ttp
                // kerasa kayak "pecahan kecil", bukan ikut membesar proporsional sama induknya.
                Projectile.scale = Main.rand.NextFloat(0.32f, 0.5f);
                Projectile.width = (int)(40 * Projectile.scale);
                Projectile.height = (int)(220 * Projectile.scale);
                Projectile.timeLeft = 160;
            }
        }

        public override void AI() {
            if (IsFragment) {
                // 🛑 [GRAVITASI] Serpihan KENA gravitasi manual (meteor utuhnya sendiri TIDAK,
                // dia jatuh lurus konstan dari kecepatan awal yang dikasih pattern-nya) -- sesuai
                // request: hasil ledakan mencar dulu ke segala arah, baru abis itu jatuh natural
                // ketarik ke bawah.
                Projectile.velocity.Y += 0.25f;
                if (Projectile.velocity.Y > 14f) Projectile.velocity.Y = 14f;
            }
            else {
                // 🛑 [LOOP SUARA ACAK] Meteor utuh terus "nyalak" salah satu dari 3 suara suar api
                // secara acak selama masih jatuh (localAI[1] dipakai sebagai countdown internal,
                // bukan buat animasi/hit-detection apapun jadi aman dipakai bebas di sini).
                Projectile.localAI[1]--;
                if (Projectile.localAI[1] <= 0) {
                    int soundNum = Main.rand.Next(1, 4); // 1,2,3
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PinkFlameShot{soundNum}"), Projectile.Center);
                    Projectile.localAI[1] = Main.rand.Next(35, 55);
                }
            }

            // Sprite depan udah nunjuk ke BAWAH secara default -> rotasi = arah gerak MINUS 90
            // derajat (bukan plus), biar pas velocity lurus ke bawah, rotation jadi 0 (sprite
            // ke-gambar apa adanya, ga kebalik/miring salah arah).
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % FrameCount;
            }

            // 🛑 [DIBIKIN LEBIH MERAH] Ekor api di belakang meteor -- dust Torch di-tint manual
            // ke merah pekat (bukan warna oranye aslinya) biar sesuai request "lebih merah".
            Vector2 flameSpawnPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * (Projectile.height * 0.35f);
            int fireDust = Dust.NewDust(flameSpawnPos, 6, 6, DustID.Torch, 0f, 0f, 80, default, 1.5f * Projectile.scale);
            Main.dust[fireDust].color = new Color(255, 40, 20);
            Main.dust[fireDust].velocity = -Projectile.velocity * 0.15f;
            Main.dust[fireDust].noGravity = true;

            if (Main.rand.NextBool(3)) {
                int smoke = Dust.NewDust(flameSpawnPos, 6, 6, DustID.Smoke, 0f, 0f, 120, default, 1.1f * Projectile.scale);
                Main.dust[smoke].velocity = -Projectile.velocity * 0.08f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // Nabrak player -> langsung Kill() (bukan nembus), biar meteor ttp konsisten "pecah
            // jadi serpihan" nggak peduli dia matinya karena kena lantai, kena player, atau kena
            // PlutoMeteorNuke (semua jalur mati ketemu di Kill() yang sama di bawah).
            Projectile.Kill();
        }

        public override void Kill(int timeLeft) {
            int randomExplosionSlot = Main.rand.Next(1, 4);
            string explodePath = $"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/MineExplode{randomExplosionSlot}";
            SoundEngine.PlaySound(new SoundStyle(explodePath), Projectile.Center);

            for (int i = 0; i < 18; i++) {
                int fire = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 60, default, 2.2f);
                Main.dust[fire].color = new Color(255, 35, 20);
                Main.dust[fire].velocity *= 2.5f;
                Main.dust[fire].noGravity = true;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 🛑 [SERPIHAN] Cuma meteor UTUH yang mecah jadi serpihan pas mati. Serpihan sendiri
            // SENGAJA tidak mecah lagi kalau dia mati (dicek lewat IsFragment) biar ga ada
            // kemungkinan infinite-split kecil-kecil terus-terusan.
            if (!IsFragment) {
                int fragmentCount = Main.rand.Next(5, 9); // 5-8 serpihan mencar ke segala arah
                for (int i = 0; i < fragmentCount; i++) {
                    float angle = (MathHelper.TwoPi / fragmentCount * i) + Main.rand.NextFloat(-0.3f, 0.3f);
                    float speed = Main.rand.NextFloat(4.5f, 8f);
                    // Sedikit dorongan ke atas dulu sebelum ketarik gravitasi di AI(), biar
                    // "mencarnya" kerasa kayak ledakan asli, bukan langsung jatuh doang.
                    Vector2 burstVel = angle.ToRotationVector2() * speed + new Vector2(0f, -2f);

                    int frag = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        burstVel,
                        Projectile.type, // reuse class yang sama, dibedain lewat ai[0]
                        Projectile.damage / 2,
                        1f,
                        Main.myPlayer
                    );

                    if (frag != Main.maxProjectiles) {
                        Main.projectile[frag].ai[0] = 1f; // tandain sebagai FRAGMENT
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / FrameCount;

            Rectangle sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 🛑 [DIBIKIN LEBIH MERAH] Body utama di-lerp ke warna merah pekat (bukan lightColor
            // polos apa adanya) sebelum digambar, biar meteornya keliatan "membara" walau lagi
            // di tempat yang cukup terang sekalipun.
            Color bodyColor = Color.Lerp(lightColor, new Color(255, 90, 70), 0.55f);
            spriteBatch.Draw(texture, drawPos, sourceRect, bodyColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            // ============================================================
            // 🛑 [GLOW SELURUH BODY] Ngga ada file "...Glow" terpisah buat sprite ini, jadi
            // glow-nya dibikin dari TEXTURE UTAMA itu sendiri: digambar ULANG di atas versi
            // normalnya pakai BlendState.Additive + warna merah nyala, biar seluruh siluet
            // meteornya keliatan "menyala dari dalam" (full-bright, ga kena lighting map),
            // bukan cuma nempel efek di tepi doang.
            // ============================================================
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            float pulsate = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.identity);

            // Layer glow inti: nempel pas di siluet asli, cukup terang.
            Color glowColor = new Color(255, 40, 20) * (0.85f * pulsate);
            spriteBatch.Draw(texture, drawPos, sourceRect, glowColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            // Layer aura: sedikit lebih gede & lebih transparan -> kesan panas "membungkus"
            // seluruh body, bukan cuma nempel di permukaan tekstur doang.
            Color auraColor = new Color(255, 60, 30) * (0.35f * pulsate);
            spriteBatch.Draw(texture, drawPos, sourceRect, auraColor, Projectile.rotation, origin, Projectile.scale * 1.08f, SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
