using CollisionLib;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Systems
{
    /// <summary>
    /// NPC tak kasat mata yang nampung sekumpulan CollisionSurface dari
    /// Impact Library ("CollisionLib") buat bikin border arena LINGKARAN yang
    /// bener-bener solid: player bisa nempel/gak bisa tembus, dan grapple hook
    /// vanilla (aiStyle 7) bisa nempel di border ini juga.
    ///
    /// CollisionSurface cuma nerima 2 titik (garis lurus), jadi lingkaran di
    /// sini didekati pakai banyak garis pendek yang disusun keliling
    /// (polygon N sisi). Makin besar Segments, makin bulat bentuknya, tapi
    /// makin berat juga (lebih banyak CollisionSurface yang di-Update tiap tick).
    ///
    /// BEDA dari versi kotak lama: border ini sekarang BUKAN statis. Karena
    /// arena harus ngikutin boss Twins, UpdatePosition(...) dipanggil TIAP TICK
    /// dari luar (TwinsArenaGlobalNPC) buat geser titik pusat & re-build semua
    /// CollisionSurface-nya ke posisi baru.
    ///
    /// TIDAK menangani proyektil biasa (bounce/kill) - itu tetap ditangani
    /// terpisah oleh ArenaBorderProjectile.cs (GlobalProjectile), karena
    /// Impact Library cuma cover player + grapple hook.
    ///
    /// BISA JUGA jadi border KOTAK (dipakai TorchGod): tinggal panggil
    /// UpdatePosition(..., ColliderShape.Square) - shape lama (Circle) tetap
    /// jadi default kalau parameter shape-nya gak diisi, jadi caller lama
    /// (TwinsArenaGlobalNPC) gak perlu diubah sama sekali.
    /// </summary>
    public class ArenaBorderColliderNPC : ModNPC
    {
        public enum ColliderShape
        {
            Circle,
            Square
        }

        // Di-set dari luar (TwinsArenaGlobalNPC / TorchGodArenaGlobalNPC) tepat
        // setelah NPC ini di-spawn.
        public Func<bool> RemovalCondition = null;

        // Jumlah sisi polygon buat ngedeketin bentuk lingkaran. 32 udah cukup
        // halus buat radius ratusan pixel; naikin kalau border-nya kepengen
        // lebih presisi (dengan konsekuensi performa). Cuma dipakai kalau
        // shape == Circle.
        private const int Segments = 32;

        private CollisionSurface[] colliders;
        private Vector2 center;
        private float radius; // buat Circle = radius asli, buat Square = setengah panjang sisi
        private ColliderShape shape = ColliderShape.Circle;
        private bool initialized = false;

        public override void SetStaticDefaults()
        {
            var drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers(0) { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(NPC.type, drawModifiers);
        }

        public override void SetDefaults()
        {
            NPC.width = 16;
            NPC.height = 16;
            NPC.lifeMax = 1;
            NPC.immortal = true;
            NPC.dontTakeDamage = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noTileCollide = true;
        }

        public override bool CheckActive() => true;

        // Dipanggil sekali waktu spawn, DAN tiap tick berikutnya kalau border
        // harus ngikutin boss. Re-build semua CollisionSurface ke posisi baru.
        // newRadius = radius (buat Circle) ATAU setengah panjang sisi (buat Square).
        public void UpdatePosition(Vector2 newCenter, float newRadius, ColliderShape newShape = ColliderShape.Circle)
        {
            center = newCenter;
            radius = newRadius;
            shape = newShape;
            initialized = true;

            NPC.width = (int)(radius * 2f);
            NPC.height = (int)(radius * 2f);
            NPC.position = center - new Vector2(radius, radius);

            RebuildColliders();
        }

        private void RebuildColliders()
        {
            // styles per sisi: [bawah, atas, kiri, kanan] -> 1 = solid (gak bisa
            // ditembus/drop-through), true di akhir CollisionSurface = bisa di-grapple.
            int[] solidAllSides = { 1, 1, 1, 1 };

            if (shape == ColliderShape.Square)
            {
                // 4 garis lurus keliling kotak (searah jarum jam).
                Vector2 topLeft = center + new Vector2(-radius, -radius);
                Vector2 topRight = center + new Vector2(radius, -radius);
                Vector2 bottomRight = center + new Vector2(radius, radius);
                Vector2 bottomLeft = center + new Vector2(-radius, radius);

                colliders = new CollisionSurface[4];
                colliders[0] = new CollisionSurface(topLeft, topRight, solidAllSides, true);
                colliders[1] = new CollisionSurface(topRight, bottomRight, solidAllSides, true);
                colliders[2] = new CollisionSurface(bottomRight, bottomLeft, solidAllSides, true);
                colliders[3] = new CollisionSurface(bottomLeft, topLeft, solidAllSides, true);
                return;
            }

            // shape == Circle -> polygon N sisi kayak sebelumnya.
            colliders = new CollisionSurface[Segments];

            for (int i = 0; i < Segments; i++)
            {
                float angleA = MathHelper.TwoPi * i / Segments;
                float angleB = MathHelper.TwoPi * (i + 1) / Segments;

                Vector2 pointA = center + angleA.ToRotationVector2() * radius;
                Vector2 pointB = center + angleB.ToRotationVector2() * radius;

                colliders[i] = new CollisionSurface(pointA, pointB, solidAllSides, true);
            }
        }

        public override bool PreAI()
        {
            if (RemovalCondition != null && RemovalCondition())
            {
                NPC.active = false;
                return false;
            }

            return true;
        }

        public override void AI()
        {
            if (!initialized || colliders == null)
                return;

            foreach (var surface in colliders)
                surface.Update();
        }

        public override void PostAI()
        {
            if (!initialized || colliders == null)
                return;

            foreach (var surface in colliders)
                surface.PostUpdate();
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }
    }
}
