using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // ITEM: Souls
    // Sprite (ICON doang -- lihat catatan visual-di-dunia di bawah): 14x88,
    // 4 frame vertikal (tiap frame 14x22)
    //
    // VISUAL DI DUNIA: item ini CanPickup = false, jadi PNG-nya cuma pernah
    // keliatan pas lagi ngambang/ketarik/dilempar di dunia -- ga pernah
    // nangkring di inventory. Representasi visual DI DUNIA itu sekarang
    // diambil alih SoulVisualProjectile "pendamping" (clone plek-ketiplek
    // ProjectileID.LostSoulFriendly, lihat SoulVisualProjectile.cs) yang
    // ngikutin Item.Center tiap tick -- PreDrawInWorld() di bawah return
    // false biar PNG-nya sendiri ga ikut kegambar dobel. Item.Texture (ikon
    // PNG) TETAP ada & TETAP dipakai apa adanya di tempat lain manapun yang
    // butuh (tooltip/bestiary/dll) -- yang di-skip cuma gambar default-nya
    // pas lagi ngambang di dunia.
    // =========================================================
    public class Souls : ModItem
    {
        // 60 detik * 60 tick/detik = 3600 tick sebelum item ini hilang
        private const int LifespanTicks = 3600;

        // Jarak (pixel) mulai "nyari" altar terdekat buat didatengin.
        // Di luar radius ini soul cuma melayang santai kayak wisp biasa.
        // 100 block * 16px/block = 1600px
        private const float SeekRadius = 1600f;

        // Jarak (pixel) dianggap "nyampe" di eye altar -> soul kekonsumsi.
        private const float ConsumeDistance = 14f;

        // Timer umur item, disimpan per-instance (bukan pakai Item.ai[] karena Item tidak punya itu)
        private int lifeTimer = 0;

        // Timer buat throttle spawn dust ekor/after-image (lihat SpawnTrailDust) --
        // ga tiap tick, biar ga spam dust kalau lagi banyak Souls sekaligus di
        // layar (misal abis /testsoul dengan count gede).
        private int trailTimer = 0;

        // whoAmI dari SoulVisualProjectile "pendamping" item ini di dunia --
        // (-1) berarti belum/ga ada. Di-maintain tiap tick lewat
        // GetOrCreateVisualProjectile() (spawn ulang otomatis kalau somehow
        // ke-kill/invalid), dan dibunuh eksplisit di semua titik item ini
        // beneran ilang (konsumsi altar / umur abis) lewat KillVisualProjectile().
        //
        // SENGAJA ga di-gate netMode (beda dari logic gameplay kayak AddSoul)
        // -- ini murni kosmetik lokal, tiap client (termasuk semua yang lihat
        // Item ini, yang posisinya udah disinkronkan vanilla) independen
        // spawn & maintain projectile pendampingnya sendiri-sendiri.
        private int _visualProjectileWhoAmI = -1;

        public override void SetStaticDefaults()
        {
            // Fitur bawaan vanilla khusus item "soul": animasi frame otomatis,
            // gerakan melayang, dan GLOW di kegelapan (persis kayak Soul of Night dkk)
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            ItemID.Sets.ItemIconPulse[Type] = true;
            ItemID.Sets.ItemNoGravity[Type] = true;

            // WAJIB: daftarkan animasinya, kalau tidak Main.itemAnimations[Type] bakal null
            // dan bikin game crash pas coba menggambar item ini.
            // 4 frame vertikal, ganti frame tiap 6 tick.
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(6, 4));
        }

        public override void SetDefaults()
        {
            Item.width = 14;
            Item.height = 22; // tinggi PER FRAME (88 / 4 frame = 22)
            Item.maxStack = 9999;
            Item.value = 0;
            Item.rare = ItemRarityID.Blue;
            Item.consumable = false;
        }

        // Item ini TIDAK BISA diambil player sama sekali lewat cara normal
        public override bool CanPickup(Player player)
        {
            return false;
        }

        // CATATAN API: SoundStyle.PitchVariance ada di tModLoader versi yang cukup baru
        // (dipakai lewat 'with' expression). Kalau versi kamu compile error di baris ini,
        // ganti jadi set Pitch manual pake Main.rand.NextFloat(-0.15f, 0.15f) langsung.
        public override void PostUpdate()
        {
            // Maintain projectile visual "pendamping" -- spawn kalau belum ada
            // (atau ke-invalid), lalu paksa posisinya ngikutin Item ini persis
            // tiap tick. Diselipin di awal biar konsisten kegambar sesuai
            // posisi TERBARU item di tick yang sama.
            SoulVisualProjectile visual = GetOrCreateVisualProjectile();
            visual.ForcePosition(Item.Center, Item.velocity);
            visual.RefreshLifetime();

            // FindNearestAltar() cuma milih altar yang !IsFull. Karena tiap item
            // di-update satu-satu (bukan paralel) dalam 1 tick, altar yang BARU
            // penuh dari soul lain di tick yang sama otomatis udah ke-exclude
            // buat item ini juga -> altar penuh beneran ga bakal narik/nerima
            // soul lagi, ga ada celah race condition.
            SoulCollectorAltarEntity targetAltar = FindNearestAltar();

            if (targetAltar != null)
            {
                Vector2 eyeCenter = targetAltar.GetEyeWorldCenter();
                Vector2 toEye = eyeCenter - Item.Center;
                float dist = toEye.Length();

                if (dist <= ConsumeDistance)
                {
                    // Nyampe di eye -> soul kekonsumsi altar.
                    // AddSoul cuma dipanggil di sisi yang otoritatif (singleplayer / server),
                    // biar ga dobel nambah di tiap client. Buat sinkronisasi jumlah soul
                    // ke semua client di multiplayer, kirim packet custom pas SoulCount berubah
                    // (belum diimplementasikan di sini, cuma logic lokalnya).
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        targetAltar.AddSoul(1);

                    // Sound "terhisap" -- Mana Crystal shimmer, dikecilin dikit +
                    // pitch di-random tipis biar ga monoton kalau banyak soul
                    // kekonsumsi barengan.
                    SoundEngine.PlaySound(SoundID.Item29 with
                    {
                        Volume = 0.55f,
                        PitchVariance = 0.25f
                    }, Item.Center);

                    // Particle konsumsi: WhiteTorch (noGravity), nyebar ke segala arah
                    // kayak burst/ledakan kecil pas soul-nya masuk ke eye.
                    for (int d = 0; d < 12; d++)
                    {
                        Dust dust = Dust.NewDustPerfect(Item.Center, DustID.WhiteTorch, Vector2.Zero, 100, default, 1.2f);
                        dust.noGravity = true;

                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(2f, 5f);
                        dust.velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                        dust.fadeIn = 0.3f;
                    }

                    KillVisualProjectile();
                    Item.active = false;
                    Item.TurnToAir();
                    return;
                }

                Vector2 dir = toEye / dist;
                float closeness = 1f - MathHelper.Clamp(dist / SeekRadius, 0f, 1f);

                // Makin deket altar, makin kenceng ketariknya.
                float pull = MathHelper.Lerp(0.05f, 0.5f, closeness);
                Item.velocity += dir * pull;

                float maxSpeed = MathHelper.Lerp(3f, 13f, closeness);
                if (Item.velocity.Length() > maxSpeed)
                    Item.velocity = Vector2.Normalize(Item.velocity) * maxSpeed;

                Item.velocity *= 0.98f;
            }
            else
            {
                // Ga ada altar dalam jangkauan -> tetep melayang lembut kayak wisp biasa
                float t = Main.GameUpdateCount * 0.05f + Item.whoAmI;
                Item.velocity.X += (float)Math.Cos(t * 0.7f) * 0.015f;
                Item.velocity.Y += (float)Math.Sin(t) * 0.02f;
                Item.velocity *= 0.98f;
            }

            // --- Ekor/after-image di belakang item, cuma pas dia beneran lagi
            //     kenceng bergerak (misal lagi ketarik altar) -- SENGAJA di-skip
            //     kalau cuma wisping pelan biar ga spam dust waktu banyak Souls
            //     sekaligus di layar. Throttle tiap 3 tick, dust-nya sendiri yang
            //     ngurus mengecil & pudar-nya (lihat SpawnTrailDust). ---
            trailTimer++;
            if (trailTimer % 3 == 0 && Item.velocity.LengthSquared() > 4f)
                SpawnTrailDust(Item.Center);

            // --- Cahaya biru lembut di sekitar item (opsional, sesuai tema Soul) ---
            Lighting.AddLight(Item.Center, 0.25f, 0.45f, 0.85f);

            // --- Hitung umur item, hilang setelah 1 menit ---
            lifeTimer += 1;

            if (lifeTimer >= LifespanTicks)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Burst partikel putih di sekeliling item sebelum hilang
                    for (int d = 0; d < 20; d++)
                    {
                        Vector2 dustPos = Item.position + new Vector2(
                            Main.rand.NextFloat(0, Item.width),
                            Main.rand.NextFloat(0, Item.height));

                        Dust.NewDust(dustPos, 1, 1, DustID.WhiteTorch, 0f, 0f, 100, default, 1.4f);
                    }

                    KillVisualProjectile();
                    Item.active = false;
                    Item.TurnToAir();
                }
            }
        }

        // Ambil projectile pendamping yang masih valid, atau spawn baru kalau
        // belum ada / somehow udah ke-kill/invalid (misal projectile slot-nya
        // kepake ulang sama projectile lain di antara tick). Dipanggil TIAP
        // TICK dari PostUpdate() -- makanya cek validitasnya lengkap tiap kali.
        private SoulVisualProjectile GetOrCreateVisualProjectile()
        {
            if (_visualProjectileWhoAmI != -1 && _visualProjectileWhoAmI < Main.maxProjectiles)
            {
                Projectile existing = Main.projectile[_visualProjectileWhoAmI];
                if (existing.active
                    && existing.type == ModContent.ProjectileType<SoulVisualProjectile>()
                    && existing.ModProjectile is SoulVisualProjectile existingVisual)
                {
                    return existingVisual;
                }
            }

            int index = Projectile.NewProjectile(Item.GetSource_FromThis(), Item.Center, Vector2.Zero,
                ModContent.ProjectileType<SoulVisualProjectile>(), 0, 0f, Main.myPlayer);

            _visualProjectileWhoAmI = index;
            return Main.projectile[index].ModProjectile as SoulVisualProjectile;
        }

        // Bunuh projectile pendamping ini secara eksplisit -- dipanggil di
        // SEMUA titik item Souls ini beneran ilang (konsumsi altar / umur
        // abis), biar ga ninggalin projectile "hantu" yang masih nampang
        // sesaat sebelum RefreshLifetime-nya sendiri abis.
        private void KillVisualProjectile()
        {
            if (_visualProjectileWhoAmI != -1 && _visualProjectileWhoAmI < Main.maxProjectiles)
            {
                Projectile proj = Main.projectile[_visualProjectileWhoAmI];
                if (proj.active && proj.type == ModContent.ProjectileType<SoulVisualProjectile>())
                    proj.Kill();
            }

            _visualProjectileWhoAmI = -1;
        }

        // Skip gambar default (PNG icon Souls) pas item ini ngambang di dunia
        // -- representasi visualnya sekarang diambil alih SoulVisualProjectile
        // pendamping (lihat GetOrCreateVisualProjectile), yang gambarnya
        // otomatis lewat jalur draw Projectile bawaan game. Item.Texture (PNG)
        // sendiri TETAP utuh & ga disentuh -- cuma DRAW-nya di dunia yang
        // di-skip di sini.
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            return false;
        }

        // Warna tint buat ekor/after-image -- disamain sama tint biru yang
        // dipake di SoulCollectorAltarEntity.DrawRitualSouls (biar item Souls
        // yang ngambang/lagi ketarik altar dan soul visual pas ritual revive
        // punya "palet" ekor yang sama, senada).
        private static readonly Color TrailTint = new Color(150, 195, 255);

        // Ekor/after-image di belakang item ini pas lagi bergerak kenceng.
        // SENGAJA pake dust WhiteTorch yang di-tint biru (bukan gambar ulang
        // sprite item Souls kecil-kecil) -- dust "torch" ini otomatis mengecil
        // & pudar sendiri seiring umurnya, jadi hasilnya kayak ekor komet yang
        // smooth & makin mengecil ke ujung, tanpa perlu texture/asset baru.
        private static void SpawnTrailDust(Vector2 position)
        {
            Dust dust = Dust.NewDustPerfect(position, DustID.WhiteTorch, Vector2.Zero, 150, TrailTint, Main.rand.NextFloat(0.5f, 0.7f));
            dust.noGravity = true;
            dust.fadeIn = 0f;
        }

        // Cari SoulCollectorAltarEntity terdekat yang masih punya slot (belum penuh 100k)
        // dalam radius SeekRadius. TileEntity.ByPosition otomatis kepisi tiap kali
        // altar di-place (lewat Hook_AfterPlacement) dan kehapus pas altar dihancurin.
        private SoulCollectorAltarEntity FindNearestAltar()
        {
            SoulCollectorAltarEntity closest = null;
            float closestDistSq = SeekRadius * SeekRadius;

            foreach (TileEntity te in TileEntity.ByPosition.Values)
            {
                if (te is SoulCollectorAltarEntity altar && !altar.IsFull)
                {
                    float distSq = Vector2.DistanceSquared(Item.Center, altar.GetEyeWorldCenter());
                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closest = altar;
                    }
                }
            }

            return closest;
        }
    }

    // =========================================================
    // GLOBAL DROP: berlaku untuk SEMUA musuh (termasuk NPC dari mod lain)
    // - Non-boss: 43% chance, drop 1
    // - Boss    : 100% chance, drop 5
    // =========================================================
    public class SoulsGlobalDrop : global::Terraria.ModLoader.GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            // Skip NPC kota / NPC ramah (kritter, pedagang, dll)
            if (npc.friendly || npc.townNPC)
                return;

            int soulType = ModContent.ItemType<Souls>();

            if (npc.boss)
            {
                // Boss: selalu drop, jumlahnya 5
                Item.NewItem(npc.GetSource_Loot(), npc.getRect(), soulType, 5);
            }
            else
            {
                // Non-boss: 43% kemungkinan drop 1
                if (Main.rand.NextFloat() < 0.43f)
                {
                    Item.NewItem(npc.GetSource_Loot(), npc.getRect(), soulType, 1);
                }
            }
        }
    }

    // =========================================================
    // ANTI-CHEAT: kalau item ini somehow masuk inventory player
    // (misal lewat /additem atau cheat menu), otomatis dilempar
    // keluar ke dunia tiap tick, berapa pun jumlahnya.
    // =========================================================
    public class SoulsAntiCheatPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            int soulType = ModContent.ItemType<Souls>();

            for (int i = 0; i < Player.inventory.Length; i++)
            {
                Item item = Player.inventory[i];

                if (item.type == soulType && item.stack > 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Item.NewItem(
                            new EntitySource_Misc("SoulsAntiCheat"),
                            Player.getRect(),
                            soulType,
                            item.stack);
                    }

                    item.TurnToAir();
                }
            }
        }
    }
}
