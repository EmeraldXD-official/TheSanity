using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // ITEM: SoulToken
    // Hasil convert 100x Souls -> 1 SoulToken (lewat tombol "Convert" di GUI
    // Soul Collector Altar, lihat SoulCollectorAltarEntity.TryConvertSouls()
    // & SoulConvertPanel/SoulTokenSlotElement di SoulCollectorUIState.cs).
    //
    // Sprite: TheSanity/CostumeTile/SoulToken -- CUMA 1 GAMBAR STATIS
    // (bukan spritesheet kayak Souls.cs yang 4-frame vertikal).
    //
    // BEDA dari Souls.cs:
    //  - Souls     : berdenyut (pulse) via ItemID.Sets.AnimatesAsSoul, otomatis
    //                ketarik & kekonsumsi altar terdekat, ga bisa di-pickup player,
    //                hilang 60 detik.
    //  - SoulToken : BERPUTAR 360 derajat terus-menerus (rotasi manual lewat
    //                field _visualRotation + hook PreDrawInWorld tiap tick,
    //                karena sprite-nya cuma 1 frame), BISA di-pickup & disimpan
    //                normal di inventory player, TIDAK
    //                ketarik ke altar manapun, hilang setelah 5 MENIT kalau
    //                lagi ngambang di dunia (bukan pas di inventory), dan sell
    //                price 0 (Item.value = 0, ga ada harga jual sama sekali).
    //  - Kalau ngambang & "kerendem" cairan Shimmer -> 1 Town NPC ACAK (KECUALI
    //    Old Man) di-teleport ke lokasi situ, terus token-nya kekonsumsi/hilang.
    // =========================================================
    public class SoulToken : ModItem
    {
        // 5 menit * 60 tick/detik = 18.000 tick sebelum item ini hilang KALAU
        // lagi ngambang di dunia. Timer instance-nya reset otomatis tiap kali
        // Item baru di-spawn (ModItem di-clone per-Item di tModLoader, jadi
        // field instance kayak gini aman dipake per-item -- pola yang sama
        // dipake lifeTimer di Souls.cs).
        private const int LifespanTicks = 60 * 60 * 5;

        // Kecepatan rotasi (radian/tick). TwoPi/120 = 1 putaran penuh per 2 detik.
        private const float RotationSpeed = MathHelper.TwoPi / 120f;

        // Berapa tick harus BENERAN kerendem Shimmer sebelum efek TP NPC-nya
        // trigger, biar ga kepicu cuma numpang lewat/kesenggol arus sedetik.
        private const int ShimmerSubmergeTicksRequired = 20;

        private int _lifeTimer;
        private int _shimmerTimer;

        public override void SetStaticDefaults()
        {
            // Ngambang (no gravity) kayak Souls, TAPI TANPA AnimatesAsSoul --
            // set itu bikin item "berdenyut" pake logic pulse + expect
            // spritesheet, sedangkan sprite kita cuma 1 gambar statis dan
            // efek pembedanya adalah ROTASI manual (lihat PostUpdate).
            ItemID.Sets.ItemNoGravity[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.value = 0; // Ga ada harga jual sama sekali
            Item.rare = ItemRarityID.LightPurple;
            Item.consumable = false;

            // TIDAK RegisterItemAnimation -- sprite cuma 1 frame statis, rotasi
            // ditangani manual lewat _visualRotation (di-update di PostUpdate,
            // di-terapkan lewat PreDrawInWorld) -- lihat komentar di bawah.
        }

        // Boleh diambil normal ke inventory player (beda dari Souls yang CanPickup = false).
        public override bool CanPickup(Player player) => true;

        // CATATAN API: versi tModLoader kamu ternyata ga punya hook
        // ModItem.CanShimmer() (dicoba sebelumnya, compile error CS0115 --
        // "no suitable method found to override"). Jadi TIDAK di-override sama
        // sekali di sini. Efeknya: sistem shimmer bawaan tetep boleh "megang"
        // item ini juga, tapi karena ga ada ItemID.Sets.ShimmerTransformToItem
        // yang di-set buat SoulToken, vanilla ga akan transformasiin dia jadi
        // item lain -- paling cuma didorong arus air/shimmer kayak item biasa.
        // Efek KHUSUS (TP NPC) tetep sepenuhnya dari deteksi manual di
        // PostUpdate di bawah, ga kebentrok sama sistem bawaan itu.

        // Rotasi visual (radian). CATATAN API: Item TIDAK punya field
        // "rotation" bawaan (beda dari Projectile) -- sebelumnya sempet nyoba
        // Item.rotation dan compile error CS1061 ("Item does not contain a
        // definition for 'rotation'"). Jadi rotasi disimpen sendiri di field
        // instance ini, terus di-terapkan pas digambar lewat hook
        // PreDrawInWorld() (yang punya parameter "ref float rotation" khusus
        // buat kasus kayak gini) -- lihat override PreDrawInWorld di bawah.
        private float _visualRotation;

        public override void PostUpdate()
        {
            // --- Rotasi 360 derajat terus-menerus (ciri khas SoulToken) ---
            _visualRotation += RotationSpeed;
            if (_visualRotation > MathHelper.TwoPi)
                _visualRotation -= MathHelper.TwoPi;

            // --- Glow lembut di kegelapan (ciri "glow in the dark") ---
            Lighting.AddLight(Item.Center, 0.55f, 0.35f, 0.75f);

            // --- Deteksi manual cairan Shimmer di posisi item.
            //     CATATAN API: kalau nama field LiquidType/LiquidAmount beda di
            //     versi tModLoader kamu, cari "LiquidID.Shimmer" di source
            //     tModLoader versi kamu buat nama field yang sesuai (konsepnya
            //     sama: cek tile liquid di posisi item == Shimmer & jumlahnya > 0). ---
            Point tileCoord = Item.Center.ToTileCoordinates();
            Tile tile = Main.tile[tileCoord.X, tileCoord.Y];
            bool inShimmer = tile != null && tile.LiquidType == LiquidID.Shimmer && tile.LiquidAmount > 0;

            if (inShimmer)
            {
                _shimmerTimer++;

                if (_shimmerTimer >= ShimmerSubmergeTicksRequired)
                {
                    TriggerShimmerTeleport(Item.Center);

                    Item.active = false;
                    Item.TurnToAir();
                    return;
                }
            }
            else
            {
                _shimmerTimer = 0;
            }

            // --- Umur hidup: hilang setelah 5 menit ngambang di dunia ---
            _lifeTimer++;
            if (_lifeTimer >= LifespanTicks)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int d = 0; d < 16; d++)
                    {
                        Vector2 dustPos = Item.position + new Vector2(
                            Main.rand.NextFloat(0, Item.width),
                            Main.rand.NextFloat(0, Item.height));

                        Dust.NewDust(dustPos, 1, 1, DustID.PurpleTorch, 0f, 0f, 100, default, 1.3f);
                    }

                    Item.active = false;
                    Item.TurnToAir();
                }
            }
        }

        // Terapin _visualRotation pas item ini digambar ngambang di dunia.
        // Ini satu-satunya cara "muter"-in sprite item di ground di tModLoader
        // (Item ga punya field rotation sendiri kayak Projectile) -- parameter
        // "rotation" di hook ini di-pass by-ref, jadi tinggal di-timpa di sini.
        // Return true biar drawing default tetep jalan (cuma rotasinya yang
        // kita override, texture/posisi/scale biar vanilla yang urus).
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            rotation = _visualRotation;
            return true;
        }

        // Pilih 1 town NPC hidup secara acak (KECUALI Old Man) dan pindahin dia
        // ke lokasi si SoulToken kerendem Shimmer. Cuma sisi otoritatif
        // (singleplayer/server) yang boleh eksekusi, biar ga geger/dobel di
        // multiplayer -- pola yang sama dipake AddSoul (Souls.cs) & TryRevive
        // (SoulCollectorUIState.cs).
        private static void TriggerShimmerTeleport(Vector2 worldPos)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            var candidates = new List<int>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                // Cuma town NPC yang lagi hidup, KECUALI Old Man.
                if (npc.active && npc.townNPC && npc.type != NPCID.OldMan)
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
                return;

            NPC target = Main.npc[candidates[Main.rand.Next(candidates.Count)]];

            target.position = worldPos - target.Size / 2f;
            target.velocity = Vector2.Zero;
            target.netUpdate = true;

            // Sound + burst partikel biar kepindahannya kerasa, ga kesannya glitch senyap.
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.65f }, worldPos);

            for (int d = 0; d < 20; d++)
            {
                Dust dust = Dust.NewDustPerfect(worldPos, DustID.PurpleTorch, Vector2.Zero, 100, default, 1.4f);
                dust.noGravity = true;

                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(2f, 6f);
                dust.velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                dust.fadeIn = 0.3f;
            }
        }
    }
}
