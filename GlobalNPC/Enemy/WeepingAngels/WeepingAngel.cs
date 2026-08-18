using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TheSanity.Content.NPCs.WeepingAngel
{
    // =====================================================================
    // WEEPING ANGEL
    // - Ngedraw diri sebagai Tile 105 (Statues), style/variant 1, full 2x3 area.
    // - Ga kedeteksi Lifeform Analyzer / Hunter Potion (kita sengaja TIDAK
    //   set npc.rarity, jadi default 0 = tidak dianggap "rare creature").
    // - Ga bisa di-hover cursor selagi dia "dormant" (ModifyHoverBoundingBox
    //   dibikin kosong supaya cursor ga berubah jadi cursor serang).
    // - AI state machine: Dormant -> Chasing (Slide/Hover) -> Frozen -> Diving
    //
    // ============================ CHANGELOG (fix) =========================
    // BUG 1 — Sprite transparan:
    //   Source rect lama dihitung manual (style * 36px) dengan asumsi semua
    //   style statue berjejer 1 baris terus. Kalau asumsi itu meleset dikit
    //   aja (row wrap, padding, tinggi antar-baris ga seragam), area yang
    //   di-sample dari tile sheet jadi jatuh di celah kosong/transparan.
    //   FIX: EnsureFrameCache() sekarang narik CoordinateFullWidth/Height,
    //   StyleWrapLimit & StyleHorizontal langsung dari TileObjectData tile
    //   105 (persis data yang dipakai game sendiri buat naro style di
    //   spritesheet), jadi source rect-nya presisi tanpa nebak-nebak lagi.
    //   Ditambah guard null-texture di PreDraw biar ga ada frame yang
    //   nge-draw sebelum texture asset-nya selesai di-load.
    //
    // BUG 2 — AI diem aja / stuck:
    //   PlayerIsLookingAtStatue lama cuma cek "statue ada di dalam layar",
    //   dan karena TriggerRange cuma 20 tile (jauh lebih kecil dari lebar
    //   layar), itu HAMPIR SELALU true begitu player masuk range. Akibatnya
    //   kondisi transisi Dormant->Chasing (yang butuh !playerSeesMe) nyaris
    //   ga pernah kesampean -> statue permanen diem di Dormant.
    //   FIX: "melihat" sekarang = statue di layar DAN masuk cone arah-pandang
    //   (didekati dari arah kursor player, fallback ke arah hadap buat
    //   player lain di MP) dari SETIDAKNYA satu player yang aktif — sesuai
    //   nuansa "siapa aja yang lihat, dia freeze/invincible".
    //
    // FITUR BARU (sesuai request):
    //   - Invincible selagi ada player yang "melihat" dia (NPC.dontTakeDamage,
    //     + CanBeHitByItem/CanBeHitByProjectile/CanBeHitByNPC sebagai lapisan
    //     tambahan buat jaga-jaga ada damage source yang bypass dontTakeDamage).
    //   - NPC.chaseable = false dipasang PERMANEN di SetDefaults, jadi seluruh
    //     homing weapon (Chlorophyte Bullet, Homing Flare, dll) ga akan pernah
    //     nganggep statue ini target valid, walaupun lagi Chasing.
    // =====================================================================
    public class WeepingAngel : ModNPC
    {
        // ---- Konfigurasi statue tile ----
        public const int StatueTileType = TileID.Statues; // 105
        public const int StatueStyle = 1;                 // "Variant 1" sesuai request
        public const int StatueTilesWide = 2;              // lebar statue dalam tile
        public const int StatueTilesTall = 3;               // tinggi statue dalam tile
        // Cuma dipakai sebagai FALLBACK kalau TileObjectData ga ketemu (seharusnya ga kejadian).
        private const int FrameCellSize = 18;

        // ---- AI State ----
        private enum AngelState
        {
            Dormant = 0,   // diam total, keliatan seperti statue biasa, belum "aktif"
            Chasing = 1,   // lagi gerak ngejar (slide atau hover)
            Frozen = 2,    // lagi diliatin player -> berhenti total
            Diving = 3     // lagi di udara & keliatan -> jatuh ke tanah
        }

        private AngelState State
        {
            get => (AngelState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        private float Timer
        {
            get => NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        private bool IsHovering
        {
            get => NPC.ai[2] == 1f;
            set => NPC.ai[2] = value ? 1f : 0f;
        }

        // Dipakai buat efek outline abu-abu pas ngejar
        public bool GlowOutline => State == AngelState.Chasing;

        // target player yang lagi dikejar
        private int targetPlayerIndex = -1;

        public override string Texture => "Terraria/Images/NPC_0"; // placeholder, kita override Draw manual jadi ga terlalu penting

        // ---- Frame cache (fix bug #1) ----
        // Static & shared di semua instance karena style/texture-nya sama buat semua Weeping Angel.
        private static bool _frameReady;
        private static Rectangle _frameSource;

        public override void SetStaticDefaults()
        {
            NPCID.Sets.TrailCacheLength[NPC.type] = 1;
            NPCID.Sets.TrailingMode[NPC.type] = 0;

            // PENTING: JANGAN isi NPC.rarity (biarkan default 0).
            // Lifeform Analyzer & Hunter Potion cuma menandai NPC dengan
            // rarity > 0, jadi dengan rarity 0 statue ini otomatis "invisible"
            // buat kedua item itu tanpa perlu hook tambahan.
        }

        public override void SetDefaults()
        {
            NPC.width = StatueTilesWide * 16 + 4;
            NPC.height = StatueTilesTall * 16 + 4;
            NPC.damage = 2;              // damage dasar (belum kena Stoned)
            NPC.defense = 30;
            NPC.lifeMax = 2000;
            NPC.HitSound = SoundID.Tink;
            NPC.DeathSound = SoundID.Shatter;
            NPC.value = 0f;
            NPC.knockBackResist = 0f;    // statue, berat, ga kedorong
            NPC.noGravity = true;        // gravitasi kita atur manual (buat mode hover)
            NPC.noTileCollide = false;
            NPC.aiStyle = -1;            // full custom AI
            NPC.friendly = false;
            NPC.lavaImmune = false;

            // FITUR BARU: homing weapon selalu ignore statue ini, permanen,
            // ga peduli lagi Dormant/Chasing/Frozen/Diving. `chaseable` ini
            // persis flag yang dicek NPC.CanBeChasedBy() buat validasi target
            // homing projectile di vanilla.
            NPC.chaseable = false;

            SpawnModHooks.RegisterAngel(NPC);
        }

        public override void OnKill()
        {
            SpawnModHooks.UnregisterAngel(NPC);
            RollLoot();
        }

        // =====================================================================
        // HOVER / CURSOR HIDING
        // Selama statue masih Dormant, kita nolin hover box-nya jadi 0 supaya
        // cursor ga berubah jadi cursor "serang" pas diarahin ke dia -> keliatan
        // persis kayak tile statue vanilla biasa.
        // =====================================================================
        public override void ModifyHoverBoundingBox(ref Rectangle boundingBox)
        {
            if (State == AngelState.Dormant)
            {
                boundingBox = Rectangle.Empty;
            }
        }

        // =====================================================================
        // AI
        // =====================================================================
        private const float TriggerRange = 20f * 16f;     // 20 block
        private const float StoppingDistance = 32f;
        private const float LookConeHalfAngleDegrees = 50f; // lebar "kerucut pandang" player

        public override void AI()
        {
            EnsureFrameCache(); // aman dipanggil tiap tick, kerjanya cuma sekali (di-guard di dalam)

            NPC.TargetClosest(false);
            Player closest = FindClosestValidPlayer();

            if (closest == null)
            {
                NPC.dontTakeDamage = false;
                ResetToDormant();
                return;
            }

            targetPlayerIndex = closest.whoAmI;
            float distance = Vector2.Distance(NPC.Center, closest.Center);
            bool losClear = Collision.CanHitLine(NPC.position, NPC.width, NPC.height, closest.position, closest.width, closest.height);

            // FIX bug #2: cek "dilihat" ke SEMUA player aktif (bukan cuma target
            // terdekat), pakai cone arah-pandang, bukan cuma "ada di layar".
            bool playerSeesMe = ComputeSeenByAnyPlayer();

            // FITUR BARU: invincible selagi ada yang lihat.
            NPC.dontTakeDamage = playerSeesMe;

            switch (State)
            {
                case AngelState.Dormant:
                    NPC.velocity = Vector2.Zero;
                    ApplyGravitySnap();
                    if (distance <= TriggerRange && losClear && !playerSeesMe)
                    {
                        State = AngelState.Chasing;
                        Timer = 0;
                    }
                    break;

                case AngelState.Chasing:
                    if (playerSeesMe)
                    {
                        // Ketauan lagi jalan -> langsung diem.
                        if (IsHovering)
                        {
                            // Lagi di udara & ketauan -> jatuhin diri (Diving)
                            State = AngelState.Diving;
                        }
                        else
                        {
                            State = AngelState.Frozen;
                        }
                        NPC.velocity = Vector2.Zero;
                        break;
                    }

                    if (distance > TriggerRange * 1.5f)
                    {
                        // Kejauhan, balik dormant (statue "give up" diam2)
                        State = AngelState.Dormant;
                        NPC.velocity = Vector2.Zero;
                        break;
                    }

                    bool playerStoned = closest.HasBuff(BuffID.Stoned);
                    if (!losClear && !playerStoned)
                    {
                        // Line of sight keblokir. Coba cari jalan lain (pathing simpel).
                        if (!TryStepAroundObstruction(closest))
                        {
                            // Ga ada jalan sama sekali -> nyerah
                            State = AngelState.Dormant;
                            NPC.velocity = Vector2.Zero;
                            break;
                        }
                    }
                    else
                    {
                        MoveTowards(closest, playerStoned);
                    }
                    break;

                case AngelState.Frozen:
                    NPC.velocity = Vector2.Zero;
                    if (!playerSeesMe)
                    {
                        // Udah ga diliatin lagi -> lanjut ngejar (kalau masih dalam jangkauan)
                        Timer += 1f;
                        if (Timer > 10f) // delay dikit biar ga langsung gerak pas mata baru lepas
                        {
                            State = distance <= TriggerRange * 1.5f ? AngelState.Chasing : AngelState.Dormant;
                            Timer = 0;
                        }
                    }
                    else
                    {
                        Timer = 0;
                    }
                    break;

                case AngelState.Diving:
                    NPC.noGravity = false;
                    NPC.velocity.X *= 0.9f;
                    NPC.velocity.Y += 0.4f;
                    if (NPC.velocity.Y > 12f) NPC.velocity.Y = 12f;
                    if (NPC.collideY)
                    {
                        // Udah nyampe tanah, snap rapi & jadi statue diam lagi
                        NPC.velocity = Vector2.Zero;
                        NPC.noGravity = true;
                        IsHovering = false;
                        State = playerSeesMe ? AngelState.Frozen : AngelState.Dormant;
                    }
                    break;
            }
        }

        private void ResetToDormant()
        {
            State = AngelState.Dormant;
            NPC.velocity = Vector2.Zero;
            targetPlayerIndex = -1;
        }

        private Player FindClosestValidPlayer()
        {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player p in Main.player)
            {
                if (!p.active || p.dead || p.ghost) continue;
                float d = Vector2.Distance(NPC.Center, p.Center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// Cek apakah ADA player (siapapun, ga cuma target terdekat) yang lagi
        /// "melihat" statue ini. Dipakai buat invincibility & state machine,
        /// biar berasa kayak Weeping Angel beneran: siapa aja yang liat, dia
        /// freeze & ga bisa diserang.
        /// </summary>
        private bool ComputeSeenByAnyPlayer()
        {
            float checkRadius = TriggerRange * 2f; // optimisasi: skip player yang jelas kejauhan
            float checkRadiusSq = checkRadius * checkRadius;

            foreach (Player p in Main.player)
            {
                if (!p.active || p.dead) continue;
                if (Vector2.DistanceSquared(NPC.Center, p.Center) > checkRadiusSq) continue;
                if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, p.position, p.width, p.height)) continue;
                if (PlayerIsLookingAtStatue(p)) return true;
            }
            return false;
        }

        /// <summary>
        /// "Player melihat statue" = statue ada di layar player itu DAN posisi
        /// statue masuk ke cone arah-pandang player (didekati dari arah kursor
        /// buat local player, karena Terraria ga punya "head direction" beneran).
        /// Untuk player lain di multiplayer, Main.MouseWorld cuma valid buat
        /// local player, jadi kita fallback ke arah hadap (player.direction)
        /// dengan cone yang sama.
        /// </summary>
        private bool PlayerIsLookingAtStatue(Player player)
        {
            Rectangle screenRect;
            if (player.whoAmI == Main.myPlayer)
            {
                Vector2 screenMin = Main.screenPosition;
                Vector2 screenMax = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight);
                screenRect = new Rectangle((int)screenMin.X, (int)screenMin.Y, (int)(screenMax.X - screenMin.X), (int)(screenMax.Y - screenMin.Y));
            }
            else
            {
                // Approksimasi kotak layar di sekitar player lain (kamera Terraria selalu center ke player).
                screenRect = new Rectangle(
                    (int)(player.Center.X - Main.screenWidth / 2f),
                    (int)(player.Center.Y - Main.screenHeight / 2f),
                    Main.screenWidth, Main.screenHeight);
            }

            if (!screenRect.Intersects(NPC.getRect())) return false;

            Vector2 aimDir = player.whoAmI == Main.myPlayer
                ? Main.MouseWorld - player.Center
                : new Vector2(player.direction, 0f);

            if (aimDir.LengthSquared() < 1f) return false;
            aimDir.Normalize();

            Vector2 toStatue = NPC.Center - player.Center;
            if (toStatue.LengthSquared() < 4f) return true; // literally nempel, anggap keliatan
            toStatue.Normalize();

            float dot = Vector2.Dot(aimDir, toStatue);
            float cosHalfAngle = (float)Math.Cos(MathHelper.ToRadians(LookConeHalfAngleDegrees));
            return dot >= cosHalfAngle;
        }

        private void MoveTowards(Player target, bool playerStoned)
        {
            float speed = playerStoned ? 12f : 3.5f;
            bool sameLevel = Math.Abs(target.Center.Y - NPC.Center.Y) < 24f;

            if (sameLevel && !IsHovering)
            {
                // "Geser diri sendiri" -> gerak horizontal instan/kaku, no easing,
                // biar berasa kayak "teleport dikit-dikit" ala Weeping Angel.
                float dir = Math.Sign(target.Center.X - NPC.Center.X);
                NPC.velocity.X = dir * speed;
                NPC.velocity.Y = 0f;
                NPC.noGravity = true;
            }
            else
            {
                // Player lebih tinggi (atau statue udah di udara) -> hover pelan
                IsHovering = true;
                NPC.noGravity = true;
                Vector2 dirVec = target.Center - NPC.Center;
                if (dirVec.LengthSquared() > 4f)
                {
                    dirVec.Normalize();
                    Vector2 desired = dirVec * (speed * 0.6f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, desired, playerStoned ? 0.2f : 0.05f);
                }
            }

            if (playerStoned)
            {
                NPC.noTileCollide = true; // "leluasa gerak tanpa hambatan"
            }
            else
            {
                NPC.noTileCollide = false;
            }
        }

        /// <summary>
        /// Pathing simpel: kalau garis lurus ke player keblokir, coba beberapa
        /// arah alternatif (naik, turun, muter) buat cari celah. Ini BUKAN A*
        /// penuh, tapi cukup buat kesan "smart" tanpa bikin AI berat.
        /// </summary>
        private bool TryStepAroundObstruction(Player target)
        {
            Vector2[] candidateOffsets =
            {
                new Vector2(0, -48),  // coba naik
                new Vector2(0, 48),   // coba turun
                new Vector2(48, 0),
                new Vector2(-48, 0),
                new Vector2(48, -48),
                new Vector2(-48, -48),
            };

            foreach (var offset in candidateOffsets)
            {
                Vector2 probe = NPC.Center + offset;
                bool clearToProbe = Collision.CanHitLine(NPC.position, NPC.width, NPC.height, probe - new Vector2(NPC.width, NPC.height) / 2f, NPC.width, NPC.height);
                bool probeToTarget = Collision.CanHitLine(probe - new Vector2(NPC.width, NPC.height) / 2f, NPC.width, NPC.height, target.position, target.width, target.height);
                if (clearToProbe && probeToTarget)
                {
                    Vector2 dir = offset;
                    dir.Normalize();
                    NPC.velocity = dir * 3f;
                    IsHovering = true;
                    NPC.noGravity = true;
                    return true;
                }
            }
            return false;
        }

        private void ApplyGravitySnap()
        {
            // Pas dormant, pastiin dia nempel rapi di tanah (jaga2 abis dijatohin).
            NPC.noGravity = true;
        }

        // =====================================================================
        // COMBAT: damage & Stoned debuff
        // =====================================================================
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            if (target.HasBuff(BuffID.Stoned))
            {
                modifiers.SourceDamage.Flat += 98f; // total efektif ~100
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            target.AddBuff(BuffID.Stoned, 30 * 60); // 30 detik
        }

        // FITUR BARU: lapisan tambahan buat invincibility selagi diliatin.
        // NPC.dontTakeDamage (di-set tiap tick di AI()) sebenarnya udah cukup
        // buat vanilla damage pipeline, tapi 3 override ini jaga-jaga kalau ada
        // damage source custom yang bypass dontTakeDamage.
        public override bool? CanBeHitByItem(Player player, Item item)
        {
            return NPC.dontTakeDamage ? false : (bool?)null;
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            return NPC.dontTakeDamage ? false : (bool?)null;
        }

        public override bool CanBeHitByNPC(NPC attacker)
        {
            return !NPC.dontTakeDamage;
        }

        // =====================================================================
        // LOOT
        // =====================================================================
        private static readonly int[] LootPool =
        {
            3719, 443, 3720, 3712, 463, 466, 454, 3710, 471, 441,
            3718, 3715, 3717, 452, 449, 459, 3714, 3716, 478, 2672,
            446, 440, 3713, 3709, 3708, 3711, 52
        };

        private const int RareDropItem = 4276;
        private const float RareDropChance = 0.005f; // 0.5%

        private void RollLoot()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int count = Main.rand.Next(1, 4); // 1-3 item
            for (int i = 0; i < count; i++)
            {
                int itemId = LootPool[Main.rand.Next(LootPool.Length)];
                Item.NewItem(NPC.GetSource_Loot(), NPC.getRect(), itemId, 1);
            }

            if (Main.rand.NextFloat() < RareDropChance)
            {
                Item.NewItem(NPC.GetSource_Loot(), NPC.getRect(), RareDropItem, 1);
            }
        }

        // =====================================================================
        // FRAME CACHE — fix bug #1 (sprite transparan)
        // Narik source-rect LANGSUNG dari TileObjectData tile 105 (bukan nebak
        // manual), jadi selaras persis sama gimana game naro tiap style di
        // spritesheet-nya (termasuk kalau ada row-wrap / padding non-seragam).
        // =====================================================================
        private static void EnsureFrameCache()
        {
            if (_frameReady) return;

            // NB: ga perlu manual "LoadTile" di sini — di tModLoader versi sekarang,
            // TextureAssets.Tile[type] itu Asset<Texture2D>, dan cuma dengan akses
            // .Value (dipanggil di PreDraw) itu udah otomatis trigger load kalau
            // belum ke-load. (Main.instance.LoadTile udah ga ada di API 1.4.)

            TileObjectData data = TileObjectData.GetTileData(StatueTileType, StatueStyle, 0);
            if (data != null)
            {
                int wrap = data.StyleWrapLimit <= 0 ? 1 : data.StyleWrapLimit;
                int col = data.StyleHorizontal ? (StatueStyle % wrap) : (StatueStyle / wrap);
                int row = data.StyleHorizontal ? (StatueStyle / wrap) : (StatueStyle % wrap);

                int frameX = col * data.CoordinateFullWidth;
                int frameY = row * data.CoordinateFullHeight;

                // CoordinateFullWidth/Height udah termasuk padding trailing antar-style;
                // buat draw kita mau ukuran "bersih" tanpa padding trailing itu.
                int width = data.CoordinateFullWidth - data.CoordinatePadding;
                int height;
                if (data.CoordinateHeights != null && data.CoordinateHeights.Length > 0)
                {
                    height = 0;
                    foreach (int h in data.CoordinateHeights) height += h;
                    height += data.CoordinatePadding * (data.CoordinateHeights.Length - 1);
                }
                else
                {
                    height = StatueTilesTall * FrameCellSize - data.CoordinatePadding;
                }

                _frameSource = new Rectangle(frameX, frameY, width, height);
            }
            else
            {
                // Fallback (seharusnya ga pernah kepakai) biar tetep ada sesuatu
                // yang ke-draw drpd transparan total kalau TileObjectData somehow null.
                int frameWidth = StatueTilesWide * FrameCellSize;
                int frameHeight = StatueTilesTall * FrameCellSize;
                _frameSource = new Rectangle(StatueStyle * frameWidth, 0, frameWidth, frameHeight);
            }

            _frameReady = true;
        }

        // =====================================================================
        // DRAW: render pakai texture Tile 105, style 1, full 2x3
        // =====================================================================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            EnsureFrameCache();

            Texture2D tileTexture = TextureAssets.Tile[StatueTileType].Value;
            if (tileTexture == null) return false; // belum sempet ke-load, skip drpd nge-crash

            Rectangle sourceRect = _frameSource;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = new Vector2(sourceRect.Width, sourceRect.Height) / 2f;

            if (GlowOutline)
            {
                Color glow = new Color(190, 190, 190, 120);
                Vector2[] offsets =
                {
                    new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2)
                };
                foreach (var off in offsets)
                {
                    spriteBatch.Draw(tileTexture, drawPos + off, sourceRect, glow, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(tileTexture, drawPos, sourceRect, Lighting.GetColor(NPC.Center.ToTileCoordinates()), NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
