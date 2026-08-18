using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // Projectile visual PLEK-KETIPLEK niru ProjectileID.LostSoulFriendly --
    // ID stats + AI style + SPRITE-nya ambil dari asset VANILLA langsung
    // (bukan PNG kita), lewat CloneDefaults() + AIType + Texture override.
    //
    // Dipakai di 2 tempat:
    //   1. SoulCollectorAltarEntity (soul-soul ritual pas revive NPC) --
    //      posisinya DI-PAKSA ngikutin jalur choreographed (smoothstep) yang
    //      udah ada, BUKAN AI wander bawaan LostSoulFriendly. AI vanilla-nya
    //      (dari AIType) tetep jalan tiap tick ngurus hal-hal internal
    //      (animasi frame dkk), tapi Center/velocity-nya ditimpa ulang manual
    //      abis itu lewat ForcePosition() -- lihat
    //      SoulCollectorAltarEntity.TickRitualSouls().
    //   2. Souls.cs (item Souls yang lagi ngambang/ketarik/dilempar di dunia)
    //      -- projectile ini jadi "pendamping" visual yang ngikutin posisi
    //      Item-nya tiap tick (lihat Souls.GetOrCreateVisualProjectile()).
    //      Item.Texture (ikon PNG kita) TETEP dipakai apa adanya, cuma
    //      REPRESENTASI VISUAL DI DUNIA-nya aja yang diambil alih projectile
    //      ini (lihat Souls.PreDrawInWorld yang return false).
    //
    // CATATAN API: kalau nama ID "LostSoulFriendly" beda/pindah di versi
    // tModLoader/vanilla kamu, cek ProjectileID.cs versi kamu buat nama yang
    // sesuai (konsepnya sama: soul/wisp biru non-hostile yang ngambang di
    // Dungeon sebelum Skeletron dikalahin).
    // =========================================================
    public class SoulVisualProjectile : ModProjectile
    {
        // Texture: override langsung ke asset SPRITE VANILLA-nya (bukan bikin
        // PNG baru sendiri) -- ini yang bikin visualnya "plek-ketiplek" sama
        // punya vanilla, bukan cuma AI-nya doang.
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.LostSoulFriendly}";

        public override void SetStaticDefaults()
        {
            // Nyalin Sets statis yang relevan biar behavior/animasi trail-nya
            // (kalau ada) plek-ketiplek sama persis kayak LostSoulFriendly.
            ProjectileID.Sets.TrailCacheLength[Type] = ProjectileID.Sets.TrailCacheLength[ProjectileID.LostSoulFriendly];
            ProjectileID.Sets.TrailingMode[Type] = ProjectileID.Sets.TrailingMode[ProjectileID.LostSoulFriendly];

            // Projectile.hide (di-set di SetDefaults) bikin projectile ini digambar
            // di lapisan "behind NPC" (lihat DrawBehind di bawah) -- efeknya
            // otomatis pake lighting tile normal, BUKAN lighting punya owner
            // player. Tanpa ini soul-nya bisa keliatan lebih terang/redup ga
            // wajar (ngikutin cahaya di sekitar player, bukan di sekitar altar).
            ProjectileID.Sets.DontAttachHideToAlpha[Type] = true;
        }

        public override void SetDefaults()
        {
            // CloneDefaults: nyalin SEMUA field defaults (width/height, frame
            // count, alpha, scale dasar, dll) dari LostSoulFriendly apa adanya.
            Projectile.CloneDefaults(ProjectileID.LostSoulFriendly);

            // AIType: field biasa (BUKAN property virtual -- makanya sebelumnya
            // error CS0506 pas dicoba di-override) yang tModLoader baca abis
            // SetDefaults() buat nentuin AI logic mana yang dipakai projectile
            // ini. Di-set eksplisit di sini (bukan cuma ngandelin CloneDefaults)
            // biar PASTI make AI vanilla LostSoulFriendly, walau CloneDefaults
            // udah ikut nyalin aiType-nya juga.
            AIType = ProjectileID.LostSoulFriendly;

            // Force murni-visual: ga boleh nyakitin/nabrak/collide APAPUN sama
            // sekali, walaupun "base"-nya di-clone dari projectile yang secara
            // definisi udah non-hostile juga -- dipaksa eksplisit di sini biar
            // ga gantung ke default vanilla yang bisa berubah antar versi.
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            // Posisi & lifetime-nya di-drive manual tiap tick sama pemanggil
            // (ForcePosition + RefreshLifetime), jadi ga butuh sinkronisasi
            // ketat -- client cukup nampilin representasi lokalnya sendiri.
            Projectile.netImportant = false;

            // hide = true -> projectile ini TIDAK digambar di jalur normal
            // (yang selalu di atas NPC). Sebagai gantinya kita daftarin dia
            // manual ke salah satu "cache list" lewat DrawBehind() di bawah --
            // pola resmi tModLoader buat ngatur projectile digambar di
            // lapisan mana (lihat ExampleMod/ExampleBehindTilesProjectile.cs).
            Projectile.hide = true;
        }

        // Dipanggil sekali per-frame buat nentuin projectile ini masuk ke
        // cache list yang mana. behindNPCs -> digambar SETELAH tile tapi
        // SEBELUM NPC -- ini yang bikin soul ritual keliatan "di belakang"
        // NPC yang lagi direvive (nyembul dari belakang badannya), bukan
        // nutupin dia kayak sebelumnya (default: semua projectile digambar
        // di atas NPC).
        //
        // CATATAN API: signature hook ini beda-beda antar versi tModLoader --
        // versi kamu ternyata punya 5 parameter List<int> (ada tambahan
        // "overPlayers" di antara behindProjectiles & overWiresUI), BUKAN 4
        // kayak versi lama (compile error CS0115 kalau parameternya cuma 4).
        // Kalau beda lagi di versi kamu, cocokin nama parameter yang bener
        // dari ModProjectile.cs versi kamu -- konsepnya sama, tinggal masukin
        // index ke SATU dari list-list itu.
        public override void DrawBehind(int index, List<int> behindNPCsAndTiles,
            List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers,
            List<int> overWiresUI)
        {
            // Dipindah dari "behindNPCs" ke "behindNPCsAndTiles" -- soul ritualnya
            // kepantau masih nampil DI DEPAN NPC yang lagi direvive. Cache list
            // "behindNPCsAndTiles" ini yang DIJAMIN (dari dokumentasi resmi
            // tModLoader) digambar SEBELUM tile & NPC digambar sama sekali, jadi
            // pasti di belakang NPC-nya. Efek sampingnya soul-nya juga jadi di
            // belakang tile altar itu sendiri, tapi ga masalah karena mayoritas
            // durasi animasinya soul-nya udah naik jauh di atas altar (di luar
            // area sprite altar), cuma sepersekian detik pertama aja yang
            // overlap sama tile-nya.
            behindNPCsAndTiles.Add(index);
        }

        // Dipanggil TIAP TICK sama pemanggil (altar ritual / Souls.cs) buat
        // nyegah projectile ini ke-despawn duluan -- default timeLeft bawaan
        // LostSoulFriendly didesain buat AI wander mandiri, bukan buat "hidup
        // selama masih di-drive manual dari luar". Kalau pemanggilnya berhenti
        // manggil (misal soul-nya udah "mati"/item-nya udah ilang), projectile
        // ini otomatis bersih sendiri begitu timeLeft abis -- ga nyangkut.
        public void RefreshLifetime(int ticks = 5)
        {
            Projectile.timeLeft = ticks;
        }

        // Maksa posisi & velocity projectile ini PERSIS ngikutin target yang
        // dikasih dari luar (jalur choreographed altar, atau posisi Item.Center
        // punya Souls.cs) -- nimpa ulang apapun yang barusan diitung AI vanilla
        // (dari AIType) di tick yang sama, biar geraknya PASTI ngikutin logic
        // pemanggil, bukan jalan-jalan sendiri.
        public void ForcePosition(Vector2 position, Vector2 velocity)
        {
            Projectile.Center = position;
            Projectile.velocity = velocity;
        }
    }
}
