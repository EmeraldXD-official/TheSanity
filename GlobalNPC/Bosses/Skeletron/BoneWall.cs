using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // === Pattern BARU: Bone Wall (dipakai phase 1 & 2) ===
    // Tembok tulang RAKSASA yang muncul nempel di salah satu sisi player
    // (arahnya ditentuin state machine Head berdasarkan posisi player
    // relatif ke boss — lihat State.BoneWallCast di
    // SkeletronReworkGlobalNPC), nge-block gerakan player kayak dinding
    // solid (gak bisa ditembus/dilewatin, murni collision push-back
    // manual, BUKAN tile beneran). Numpang sprite ThrowBones.png yang sama
    // kayak ThrownBone (single bone), tapi di-scale GEDE BANGET dan
    // ditumpuk (stack) vertikal beberapa biji biar nutupin ketinggian yang
    // cukup buat jadi "tembok" penuh, bukan cuma satu tulang doang.
    //
    // Tiap instance class ini = SATU segmen tumpukan. SpawnWall() di bawah
    // manggil NewProjectile berkali-kali (WallSegmentCount) buat bikin 1
    // kolom penuh sekali panggil.
    public class BoneWall : ModProjectile
    {
        // === Ukuran & stacking ===
        // Bone asli di dalam kanvas ThrowBones.png ~60x136 (sama patokan
        // kayak catatan di ThrownBone.SetDefaults). BigScale gede banget
        // biar satu segmen aja udah keliatan kayak potongan tembok, bukan
        // tulang lempar biasa.
        const float BigScale = 4.2f;
        const int WallSegmentCount = 5;       // jumlah tulang ditumpuk buat 1 kolom
        // FIX: overlap dikit antar segmen (0.82 bukan 1.0) biar gak ada
        // celah transparan kelihatan nyambung antar sprite pas ditumpuk.
        const float SegmentOverlap = 0.82f;

        const int GrowTime = 18;  // munculnya (scale 0 -> full) sebelum jadi solid — selama ini player MASIH bisa lewat (belum block/damage)
        const int FadeOutTime = 14; // fade pas wall ilang di akhir pattern

        // damage kontak kecil (murni "gak enak disentuh", BUKAN sumber
        // damage utama pattern ini — sumber damage utamanya ThrownBone
        // zigzag yang dilempar bareng, lihat ThrownBone.SpawnZigzagVolley)
        public const int ContactDamage = 8;

        float growProgress = 0f;   // 0..1, dipakai buat scale-in/out & alpha
        bool isAnchorSegment = false; // segmen paling bawah (index 0) -> gambar juga crack decal di dasarnya
        int totalLifetime = 60;    // berapa tick SOLID sebelum mulai fade-out, di-set dari luar lewat Setup()

        static Asset<Texture2D> crackTex; // di-cache statis, cuma dipake segmen anchor buat telegraph tanah retak

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/ThrowBones";

        public override void SetDefaults()
        {
            // Hitbox dihitung dari ukuran tulang ASLI * BigScale (bukan
            // kanvas 162x162-nya) — sama pola kayak ThrownBone.
            Projectile.width = (int)(60 * BigScale);
            Projectile.height = (int)(136 * BigScale);
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.damage = 0; // damage nyala begitu udah solid penuh, lihat AI()
        }

        // Dipanggil manual sekali dari SpawnWall() sesudah NewProjectile(),
        // sebelum sync pertama — analog SetupEmerge() punya BigBoneSpike.
        public void Setup(bool anchor, int lifetimeTicks)
        {
            isAnchorSegment = anchor;
            totalLifetime = lifetimeTicks;
            Projectile.timeLeft = lifetimeTicks + FadeOutTime + 5; // buffer kecil
            Projectile.netUpdate = true;
        }

        public override void AI()
        {
            Projectile.ai[0]++;
            float timer = Projectile.ai[0];

            if (timer <= GrowTime)
            {
                growProgress = EaseOutBack(timer / GrowTime);
                Projectile.damage = 0; // masih kebentuk, belum solid/berbahaya — player masih bisa lewat sebentar

                if (isAnchorSegment && Main.rand.NextBool(2))
                    SpawnFormDust();
            }
            else if (timer <= totalLifetime)
            {
                growProgress = 1f;
                Projectile.damage = ContactDamage;

                // === Blocking: dorong player keluar biar gak bisa nembus ===
                // Cuma aktif begitu udah FULL solid (lewat GrowTime).
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (!p.active || p.dead) continue;

                    Rectangle wallHitbox = Projectile.Hitbox;
                    Rectangle playerHitbox = p.Hitbox;
                    if (!wallHitbox.Intersects(playerHitbox)) continue;

                    // dorong ke sisi tempat player DATENG (biar gak nembus
                    // tengah tembok pas overlap), bukan selalu 1 arah tetap.
                    float pushDir = p.Center.X < Projectile.Center.X ? -1f : 1f;
                    float overlapX = pushDir < 0f
                        ? playerHitbox.Right - wallHitbox.Left
                        : wallHitbox.Right - playerHitbox.Left;

                    if (overlapX > 0f)
                    {
                        p.position.X += pushDir * overlapX;
                        if ((pushDir < 0f && p.velocity.X > 0f) || (pushDir > 0f && p.velocity.X < 0f))
                            p.velocity.X = 0f;
                    }
                }
            }
            else
            {
                // fase fade-out di akhir lifetime — udah gak nge-block/damage lagi
                float fadeTick = timer - totalLifetime;
                growProgress = 1f - MathHelper.Clamp(fadeTick / FadeOutTime, 0f, 1f);
                Projectile.damage = 0;
            }
        }

        void SpawnFormDust()
        {
            Vector2 dustPos = Projectile.Bottom + Main.rand.NextVector2Circular(50f, 15f);
            Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<BoneChipDust>(), new Vector2(0f, -2f));
            d.noGravity = false;
            d.scale = 1.3f;
        }

        static float EaseOutBack(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            float scale = BigScale * growProgress;
            float alpha = MathHelper.Clamp(growProgress, 0f, 1f);

            Color drawColor = Color.Lerp(lightColor, Color.White, 0.2f) * alpha;

            // crack decal di dasar kolom (cuma segmen anchor) — reuse asset
            // BonePortalGroundCrack.png sebagai telegraph tanah retak. Di-
            // request manual lewat ModContent.Request (bukan lewat property
            // Texture di atas), karena PreDraw ModProjectile cuma otomatis
            // nge-load SATU texture per class.
            if (isAnchorSegment)
            {
                crackTex ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Skeletron/BonePortalGroundCrack");
                if (crackTex.IsLoaded)
                {
                    Texture2D crack = crackTex.Value;
                    Vector2 crackOrigin = new Vector2(crack.Width / 2f, crack.Height / 2f);
                    Vector2 crackPos = Projectile.Bottom - Main.screenPosition;
                    float crackScale = (Projectile.width / (float)crack.Width) * 1.3f;
                    Color crackColor = Color.White * (alpha * 0.85f);

                    Main.spriteBatch.Draw(crack, crackPos, null, crackColor, 0f, crackOrigin,
                        crackScale, SpriteEffects.None, 0f);
                }
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, drawColor,
                Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

            return false;
        }

        // === Dipanggil dari state machine Head (SkeletronReworkGlobalNPC),
        // State.BoneWallCast (phase 1 & 2) ===
        // sideSign: +1 = tembok muncul di KANAN player, -1 = di KIRI player
        // (arahnya udah ditentuin pemanggil berdasarkan posisi player
        // relatif ke boss).
        // durationTicks: berapa lama wall ini SOLID (block+damage, dihitung
        // dari tick spawn-nya sendiri) sebelum mulai fade-out — disamain
        // pemanggil ke sisa durasi Cast+Active pattern ini di state
        // machine, biar ilangnya wall pas barengan sama Recovery dimulai.
        public static void SpawnWall(NPC headNpc, Player target, int sideSign, int durationTicks, float distanceFromPlayer = 260f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float columnX = target.Center.X + sideSign * distanceFromPlayer;
            float segmentHeight = 136f * BigScale * SegmentOverlap;
            float startY = target.Center.Y - segmentHeight * (WallSegmentCount - 1) / 2f;

            for (int i = 0; i < WallSegmentCount; i++)
            {
                Vector2 pos = new Vector2(columnX, startY + segmentHeight * i);

                int index = Projectile.NewProjectile(headNpc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<BoneWall>(), 0, 4f, Main.myPlayer);

                if (index >= 0 && index < Main.maxProjectiles
                    && Main.projectile[index].active
                    && Main.projectile[index].ModProjectile is BoneWall segment)
                {
                    segment.Setup(anchor: i == 0, durationTicks);
                }
            }
        }

        // === PATTERN BARU (phase 2 doang): Bone Wall Slam ===
        // Dua utility static di bawah ini MURNI buat dipanggil dari state
        // machine Head (SkeletronReworkGlobalNPC) — BoneWall sendiri gak
        // ngatur gerakan Head atau nge-track "attack ini punya siapa",
        // sama pola simpelnya kayak ThrownBone.CheckBoneWallCollision yang
        // scan semua BoneWall type projectile aktif tanpa mbedain
        // attack/pemilik (di fight ini emang cuma ada 1 kolom wall aktif
        // dalam satu waktu, jadi aman).

        // Dipanggil TIAP TICK dari State.BoneWallActive selama phase 2 &
        // belum pernah ke-trigger (wallSlamTriggered) — true kalau ADA
        // segmen wall yang lagi SOLID (damage > 0, bukan pas growing/
        // fading) yang hitbox-nya overlap sama player, dan touchCenter
        // dikeluarin sebagai titik yang jadi tujuan hentakan Head.
        public static bool IsTouchedByPlayer(Player player, out Vector2 touchCenter)
        {
            int wallType = ModContent.ProjectileType<BoneWall>();
            touchCenter = Vector2.Zero;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != wallType || p.damage <= 0) continue;

                if (p.Hitbox.Intersects(player.Hitbox))
                {
                    touchCenter = p.Center;
                    return true;
                }
            }
            return false;
        }

        // Dipanggil sekali dari State.BoneWallSlamCast begitu Head nyampe
        // menghentak ke titik sentuhan (shatterOrigin). Ngancurin SEMUA
        // segmen wall yang lagi aktif (bukan cuma segmen yang disentuh —
        // kolomnya emang cuma 1 kesatuan tembok) lalu munculin 3 ThrownBone
        // versi BESAR (ThrownBone.SpawnWallShatterBurst) dari titik
        // sentuhan itu, menyebar fan ke arah towardTarget (biasanya arah
        // ke player).
        public static void ShatterAll(IEntitySource source, Vector2 shatterOrigin, Vector2 towardTarget)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int wallType = ModContent.ProjectileType<BoneWall>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != wallType) continue;

                p.Kill();
            }

            ThrownBone.SpawnWallShatterBurst(source, shatterOrigin, towardTarget);
        }

        // === Multiplayer sync ===
        // isAnchorSegment & totalLifetime cuma di-set sekali server-side
        // lewat Setup() (dipanggil dari SpawnWall), jadi perlu dikirim
        // manual ke client — sama pola kayak Direction/sizeMul di
        // BigBoneSpike (Projectile.ai cuma 2 slot dan udah kepake ai[0]
        // buat timer di sini).
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(isAnchorSegment);
            writer.Write((short)totalLifetime);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            isAnchorSegment = reader.ReadBoolean();
            totalLifetime = reader.ReadInt16();
        }
    }
}